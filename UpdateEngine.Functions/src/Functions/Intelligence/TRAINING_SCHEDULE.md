# Model Training Schedule Summary

## Environment-Specific Training Schedules

| Environment | Schedule Interval | CRON Expression | First Training After Startup |
|-------------|------------------|-----------------|------------------------------|
| **Local Development** | **Every 5 minutes** | `0 */5 * * * *` | **~5 minutes** |
| Defaults/Staging | Every hour | `0 0 */1 * * *` | ~1 hour |
| Production | Every 30 days | `0 0 0 */30 * *` | ~30 days |

## Local Development Behavior

### Automatic Training Timeline

When you start your Azure Functions locally:

```
[00:00:00] Azure Functions startup complete
[00:01:00] First anomaly detection runs (if enabled)
[00:03:00] First critical sync runs
[00:05:00] ?? FIRST MODEL TRAINING RUNS
[00:10:00] Second model training
[00:15:00] Third model training
...and so on every 5 minutes
```

### What Happens During Each Training Cycle

1. **Check metadata store** for available updates
2. **Extract up to 2000 samples** for training
3. **Validate minimum 100 updates** are available
4. **Train RandomizedPCA model** with 13 features
5. **Save model** to `./anomaly-model.zip`
6. **Model auto-loads** for anomaly detection scoring

### Logs to Expect

```
[00:05:00] [Information] Scheduled model training triggered at 2024-01-15T00:05:00
[00:05:01] [Information] Extracting features from 2000 updates
[00:05:02] [Information] Training model with 1987 samples
[00:05:04] [Information] Scheduled model training completed successfully
```

## Configuration Files

### local.settings.json
```json
{
  "Values": {
    "ModelTrainingSchedule": "0 */5 * * * *"
  }
}
```

### appsettings.Development.json
```json
{
  "UpdateEngine": {
    "SyncConfiguration": {
      "ModelTrainingSchedule": "0 */5 * * * *"
    }
  }
}
```

## Why Every 5 Minutes?

**Optimized for Developer Experience:**

? **Immediate Feedback**: Start testing anomaly detection within 5 minutes
? **Rapid Iteration**: Re-train quickly after adding more updates
? **No Manual Steps**: Fully automatic - just start and wait
? **Testing Friendly**: Frequent training helps validate your changes

**For production**, the schedule is much more conservative (30 days) to avoid unnecessary compute costs.

## Manual Training (Still Available)

If you don't want to wait 5 minutes, you can still train manually:

```powershell
# Immediate training via HTTP endpoint
Invoke-RestMethod -Uri "http://localhost:7071/api/train-model?sampleSize=1000" -Method POST
```

This is useful when:
- You want to train immediately on first startup
- You've just added a large batch of updates
- You're testing specific model configurations
- You need a specific sample size

## Customizing the Schedule

Want a different interval? Edit `local.settings.json`:

```json
{
  "Values": {
    // Every 10 minutes
    "ModelTrainingSchedule": "0 */10 * * * *",
    
    // Every 15 minutes  
    "ModelTrainingSchedule": "0 */15 * * * *",
    
    // Every hour (like defaults)
    "ModelTrainingSchedule": "0 0 */1 * * *",
    
    // Every 30 minutes
    "ModelTrainingSchedule": "0 */30 * * * *"
  }
}
```

## Performance Impact

**Training Performance** (2000 samples):
- Duration: ~5-10 seconds
- Memory: ~150 MB
- CPU: Moderate spike during training
- Storage: ~500 KB model file

**Development Machine Impact**:
- Minimal - training happens in background
- Won't interfere with debugging
- Can be paused by stopping Functions app

## Next Steps

1. ? Start Azurite: `azurite --silent --location c:\azurite`
2. ? Start Functions: `cd UpdateEngine.Functions\src && func start`
3. ? Wait 5 minutes for first training
4. ? Check for model file: `Test-Path "./anomaly-model.zip"`
5. ? Test anomaly detection endpoints

## See Also

- **Complete Training Guide**: `MODEL_TRAINING_GUIDE.md`
- **Quick Reference**: `TRAINING_QUICK_REF.md`
- **Anomaly Detection System**: `README.md`
- **Metrics Documentation**: `../../Core/Metrics/ANOMALY_METRICS.md`
