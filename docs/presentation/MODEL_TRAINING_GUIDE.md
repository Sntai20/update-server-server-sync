# Model Training Guide

This guide explains how to train the ML.NET anomaly detection model for Windows Update analysis.

## ?? Local Development Quick Start

For **local development**, the model training is configured to happen automatically **every 5 minutes**. This means:

1. **Start Azurite** and your **Azure Functions**
2. **Wait ~5 minutes** for the first scheduled training to run automatically
3. **Model is ready** - anomaly detection will start working immediately

No manual training needed! The system will:
- Check your metadata store every 5 minutes
- Extract up to 2000 samples
- Train the model if at least 100 updates are available
- Save the model to `./anomaly-model.zip`

**Timeline**:
```
[00:00] Functions start
[00:05] First scheduled training runs
[00:10] Second training (if needed)
[00:15] Third training (if needed)
...
```

**Want to train immediately?** Use the HTTP endpoint (see Method 1 below) instead of waiting.

---

## Prerequisites

1. **Metadata Store Populated**: Ensure your metadata store contains Windows Updates (minimum 100 updates recommended)
2. **Azurite Running** (for local development):
   ```powershell
   azurite --silent --location c:\azurite --debug c:\azurite\debug.log
   ```
3. **Configuration Verified**: Check that `appsettings.Development.json` has:
   ```json
   {
     "Features": {
       "EnableAnomalyDetection": true
     },
     "AnomalyDetection": {
       "ModelPath": "./anomaly-model.zip",
       "AnomalyScoreThreshold": 0.85
     }
   }
   ```

## Training Methods

### Method 1: HTTP Endpoint (Manual Training)

**Best for**: Initial training, testing, or on-demand retraining

1. **Start the Azure Functions**:
   ```powershell
   cd UpdateEngine.Functions\src
   func start
   ```

2. **Call the training endpoint**:
   ```powershell
   # PowerShell
   Invoke-RestMethod -Uri "http://localhost:7071/api/train-model?sampleSize=1000" -Method POST
   
   # Or using curl
   curl -X POST "http://localhost:7071/api/train-model?sampleSize=1000"
   ```

3. **Parameters**:
   - `sampleSize` (optional): Number of updates to train on
     - Default: 1000
     - Minimum: 100
     - Recommended: 1000-5000 for development, 10000+ for production

4. **Expected Response**:
   ```json
   {
     "status": "success",
     "message": "Model trained successfully",
     "samplesUsed": 1000,
     "durationSeconds": 2.34,
     "timestamp": "2024-01-15T10:30:00Z"
   }
   ```

5. **Verify Model Created**:
   ```powershell
   Test-Path "./anomaly-model.zip"  # Should return True
   ```

### Method 2: Scheduled Training (Automatic)

**Best for**: Production environments, keeping model up-to-date

The system automatically retrains the model on a schedule:

**Development**: Every 5 minutes (for rapid testing)
```json
"ModelTrainingSchedule": "0 */5 * * * *"
```

**Defaults**: Every hour
```json
"ModelTrainingSchedule": "0 0 */1 * * *"
```

**Production**: Every 30 days at midnight
```json
"ModelTrainingSchedule": "0 0 0 */30 * *"
```

> **Local Development Optimization**: The development schedule is set to every 5 minutes so you can start testing the anomaly detection system immediately. The first training will occur within 5 minutes of starting your Functions app, assuming you have at least 100 updates in your metadata store.

**To customize the schedule**, edit `local.settings.json`:
```json
{
  "Values": {
    "ModelTrainingSchedule": "0 */5 * * * *"
  }
}
```

**CRON Expression Format**:
```
* * * * * *
? ? ? ? ? ?
? ? ? ? ? ?? Day of week (0-6)
? ? ? ? ???? Month (1-12)
? ? ? ?????? Day of month (1-31)
? ? ???????? Hour (0-23)
? ?????????? Minute (0-59)
???????????? Second (0-59)
```

**Common Schedules**:
- Every 7 days: `0 0 0 */7 * *`
- Every 14 days: `0 0 0 */14 * *`
- Every 30 days: `0 0 0 */30 * *`
- Weekly on Sunday: `0 0 0 * * 0`
- Monthly on 1st: `0 0 0 1 * *`

## Training Process Details

### What Happens During Training

1. **Feature Extraction** from metadata store:
   - FileSize, IsSigned, DomainReputation, HashMatch, UpdateFrequency
   - SupersededCount, SupersededByCount, BundledUpdatesCount
   - IsSecurityUpdate, IsCriticalUpdate, IsCumulativeUpdate
   - ApplicabilityRulesCount, HasComplexApplicability

2. **Category Resolution**:
   - Builds lookup of Classification and Product categories
   - Resolves each update's classification (e.g., "Security Updates")
   - Resolves each update's product (e.g., "Windows 10")

3. **ML.NET Training**:
   - Algorithm: RandomizedPCA (unsupervised anomaly detection)
   - Rank: 6 (half of 13 features for dimensionality reduction)
   - Normalization: MinMax scaling applied to all features
   - Oversampling: 20x to improve anomaly detection sensitivity

4. **Model Persistence**:
   - Saves trained model to `./anomaly-model.zip`
   - Model automatically loaded on service startup
   - Used for scoring updates via `Score()` methods

## Troubleshooting

### Issue: "Insufficient training data"

**Error**: `Found X samples, need at least 100`

**Solutions**:
1. Ensure metadata store is populated:
   ```powershell
   # Check if sync has run
   Get-ChildItem ./data/*.json | Measure-Object
   ```
2. Run a sync first:
   ```powershell
   curl -X POST "http://localhost:7071/api/sync/emergency"
   ```
3. Use a smaller `sampleSize` if you have fewer updates available

### Issue: "Skipping update due to conversion error"

**Cause**: Some updates have unsupported expression types or missing metadata

**Impact**: Non-critical - these updates are skipped, training continues with others

**Log Example**:
```
[Debug] Skipping update {GUID} due to conversion error: Unknown expression type
```

### Issue: Model not loading on startup

**Error**: `Anomaly detection model not found at ./anomaly-model.zip`

**Solutions**:
1. Train the model first (see Method 1 above)
2. Verify `ModelPath` in configuration matches actual file location
3. Check file permissions

### Issue: GUID validation errors

**Error**: `Byte array for Guid must be exactly 16 bytes long`

**Status**: Already fixed in `QueryService.cs` - invalid GUIDs are skipped with debug logging

## Monitoring Training

### Logs to Watch

**Successful Training**:
```
[Information] Model training initiated via HTTP trigger
[Information] Extracting 1000 samples from metadata store
[Information] Found 1234 software updates in metadata store
[Information] Training model with 1000 samples
[Information] Model training completed in 2.34s
```

**Scheduled Training**:
```
[Information] Scheduled model training triggered at 2024-01-15T00:00:00
[Information] Extracting features from 2000 updates
[Information] Training model with 1987 samples
[Information] Scheduled model training completed successfully
```

### Metrics

The training process records OpenTelemetry metrics:

- `updateengine.anomaly.model_training_started` - Training initiated
- `updateengine.anomaly.model_training_completed` - Training finished
- `updateengine.anomaly.model_training_duration` - Training duration in seconds

**Tags**:
- `sample_count`: Number of samples used
- `training_size`: "small" (<100), "medium" (100-1000), "large" (>1000)

## Best Practices

### Initial Training

1. **Start small**: Use 1000-2000 samples for initial testing
2. **Verify quality**: Check logs for skipped updates
3. **Test scoring**: Score a few known updates to verify model works
4. **Monitor threshold**: Start with 0.85, adjust based on false positive rate

### Production Training

1. **Larger samples**: Use 10,000+ samples for production models
2. **Regular retraining**: Schedule monthly (30 days) to catch new patterns
3. **Monitor metrics**: Track `model_training_duration` for performance
4. **Version control**: Consider backing up model files before retraining

### Performance Tuning

**Training Duration** (approximate):
- 1,000 samples: ~2-5 seconds
- 5,000 samples: ~10-20 seconds
- 10,000 samples: ~30-60 seconds
- 50,000+ samples: Consider sampling strategies

**Memory Usage**:
- ~100 MB for 10,000 samples
- Scales linearly with sample count

## Next Steps

After training the model:

1. **Verify Anomaly Detection Works**:
   ```powershell
   # Trigger scheduled detection
   curl -X POST "http://localhost:7071/admin/functions/RunScheduledAnomalyDetection"
   ```

2. **Check Metrics Dashboard**: See `ANOMALY_METRICS.md` for Grafana/Prometheus setup

3. **Tune Threshold**: Adjust `AnomalyScoreThreshold` based on false positives:
   - 0.85: Sensitive (more alerts)
   - 0.90: Balanced (default for production)
   - 0.95: Conservative (fewer alerts)

4. **Review Anomalies**: Check logs for detected anomalies and their scores

## Additional Resources

- **Anomaly Detection System**: See `README.md` in this directory
- **Metrics Documentation**: See `ANOMALY_METRICS.md` in Core/Metrics
- **ML.NET Documentation**: https://docs.microsoft.com/dotnet/machine-learning/
- **RandomizedPCA**: https://docs.microsoft.com/dotnet/api/microsoft.ml.trainers.randomizedpcatrainer
