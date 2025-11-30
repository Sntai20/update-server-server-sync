# Model Training Quick Reference

## Quick Start

### 1. Start Azurite (Local Development)
```powershell
azurite --silent --location c:\azurite --debug c:\azurite\debug.log
```

### 2. Start Azure Functions
```powershell
cd UpdateEngine.Functions\src
func start
```

### 3. Train the Model
```powershell
# PowerShell
Invoke-RestMethod -Uri "http://localhost:7071/api/train-model?sampleSize=1000" -Method POST

# Or curl
curl -X POST "http://localhost:7071/api/train-model?sampleSize=1000"
```

### 4. Verify Model Created
```powershell
Test-Path "./anomaly-model.zip"  # Should return True
```

## Training Schedules

| Environment | Schedule | CRON Expression | Frequency |
|------------|----------|-----------------|-----------|
| **Local Development** | `ModelTrainingSchedule` | `0 */5 * * * *` | **Every 5 minutes** |
| Defaults | `ModelTrainingSchedule` | `0 0 */1 * * *` | Every hour |
| Production | `ModelTrainingSchedule` | `0 0 0 */30 * *` | Every 30 days |

> **Note**: Local development uses an aggressive 5-minute schedule so you can test the anomaly detection system immediately after starting the Functions app. First training will occur 5 minutes after startup.

## Recommended Sample Sizes

| Use Case | Sample Size | Expected Duration |
|----------|-------------|-------------------|
| Testing | 100-500 | ~1-2 seconds |
| Development | 1,000-2,000 | ~2-5 seconds |
| Staging | 5,000-10,000 | ~10-30 seconds |
| Production | 10,000+ | ~30-60+ seconds |

## Common Issues

| Issue | Solution |
|-------|----------|
| "Insufficient training data" | Run sync first: `curl -X POST http://localhost:7071/api/sync/emergency` |
| "Model not found" | Train the model first using HTTP endpoint |
| GUID validation errors | Already fixed - check QueryService.cs has validation |
| Azurite not running | Start Azurite before running functions |

## Configuration Files

### local.settings.json
```json
{
  "Values": {
    "ModelTrainingSchedule": "0 0 0 */7 * *"
  }
}
```

### appsettings.Development.json
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

## Training Endpoints

| Endpoint | Method | Auth Level | Purpose |
|----------|--------|------------|---------|
| `/api/train-model` | POST | Admin | Manual training |
| (Timer Trigger) | N/A | N/A | Scheduled training |

## Monitoring

### Key Logs
```
[Information] Training model with {Count} samples
[Information] Model training completed in {Duration:F2}s
```

### Key Metrics
- `updateengine.anomaly.model_training_started`
- `updateengine.anomaly.model_training_completed`
- `updateengine.anomaly.model_training_duration`

## Feature Extraction (13 Features)

1. FileSize
2. IsSigned
3. DomainReputation
4. HashMatchScore
5. UpdateFrequency
6. SupersededCount
7. SupersededByCount
8. BundledUpdatesCount
9. IsSecurityUpdate
10. IsCriticalUpdate
11. IsCumulativeUpdate
12. ApplicabilityRulesCount
13. HasComplexApplicability

## ML.NET Configuration

| Parameter | Value | Purpose |
|-----------|-------|---------|
| Algorithm | RandomizedPCA | Unsupervised anomaly detection |
| Rank | 6 | Dimensionality reduction (half of 13 features) |
| Normalization | MinMax | Scale all features to [0,1] range |
| Oversampling | 20 | Improve anomaly sensitivity |

## Next Steps After Training

1. ? Verify model file exists: `Test-Path "./anomaly-model.zip"`
2. ? Test scoring: Call anomaly detection endpoints
3. ? Check metrics: Review OpenTelemetry dashboard
4. ? Tune threshold: Adjust based on false positive rate
5. ? Set up alerts: Configure Prometheus/Azure Monitor

## Support

- Full Guide: `MODEL_TRAINING_GUIDE.md`
- Anomaly Detection: `README.md`
- Metrics: `../../Core/Metrics/ANOMALY_METRICS.md`
