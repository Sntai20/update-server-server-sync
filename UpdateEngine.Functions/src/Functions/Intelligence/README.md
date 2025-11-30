# Anomaly Detection System

## Overview

The Anomaly Detection system uses ML.NET's RandomizedPCA algorithm to detect anomalous Windows Update metadata patterns. It analyzes 13 rich features extracted from the Microsoft Update library to identify suspicious updates.

## Architecture

### Components

1. **AnomalyDetectionFunctions** - Azure Functions endpoints
   - `IngestAnomaly` (HTTP POST) - Ingests anomaly events from external systems
   - `RunScheduledAnomalyDetection` (Timer) - Periodic analysis of metadata store

2. **AnomalyDetectionService** - ML.NET service implementation
   - Feature extraction from `SoftwareUpdate` and `UpdateMetadata`
   - Model training and persistence
   - Scoring with configurable thresholds

3. **QueueService** - Async event processing
   - Azure Storage Queue integration
   - Event serialization and enqueueing

4. **Models**
   - `UpdateMetadata` - 17 properties for anomaly analysis
   - `AnomalyEvent` - Event payload for HTTP ingestion
   - `UpdateFeatures` - ML.NET feature vector (13 features)

## Configuration

### appsettings.Development.json

```json
{
  "AnomalyDetection": {
    "ModelPath": "./anomaly-model.zip",
    "AnomalyScoreThreshold": 0.85,
    "QueueName": "anomaly-events"
  },
  "Features": {
    "EnableAnomalyDetection": true
  },
  "UpdateEngine": {
    "SyncConfiguration": {
      "AnomalyDetectionSchedule": "0 * * * * *"  // Every minute (dev)
    }
  }
}
```

### appsettings.Production.json

```json
{
  "AnomalyDetection": {
    "ModelPath": "./anomaly-model.zip",
    "AnomalyScoreThreshold": 0.90,
    "QueueName": "anomaly-events-prod"
  },
  "Features": {
    "EnableAnomalyDetection": true
  },
  "UpdateEngine": {
    "SyncConfiguration": {
      "AnomalyDetectionSchedule": "0 0 */2 * * *"  // Every 2 hours (prod)
    }
  }
}
```

### local.settings.json

```json
{
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "AnomalyDetectionSchedule": "0 * * * * *"
  }
}
```

## Configuration Settings

| Setting | Description | Default | Dev | Prod |
|---------|-------------|---------|-----|------|
| `AnomalyDetection:ModelPath` | Path to trained ML.NET model | `./anomaly-model.zip` | Same | Same |
| `AnomalyDetection:AnomalyScoreThreshold` | Score threshold for anomaly detection (0.0-1.0) | 0.85 | 0.85 | 0.90 |
| `AnomalyDetection:QueueName` | Azure Storage Queue name | `anomaly-events` | `anomaly-events` | `anomaly-events-prod` |
| `Features:EnableAnomalyDetection` | Master enable/disable flag | `false` | `true` | `true` |
| `AnomalyDetectionSchedule` | Timer trigger CRON expression | `0 0 */1 * * *` | `0 * * * * *` | `0 0 */2 * * *` |
| `AzureWebJobsStorage` | Connection string for queue storage | (Required) | `UseDevelopmentStorage=true` | Azure connection string |

## Feature Engineering

The system extracts 13 features from Windows Update metadata:

### Basic Features (5)
1. **FileSize** - Total size of update files
2. **IsSigned** - Cryptographic signature present
3. **DomainReputation** - Publisher reputation score (0.0-1.0)
4. **HashMatch** - Hash verification status
5. **UpdateFrequency** - Update release cadence (0.0-1.0)

### Enhanced Features from Microsoft Update Library (8)
6. **SupersededCount** - Number of updates this supersedes
7. **SupersededByCount** - Number of updates superseding this
8. **BundledUpdatesCount** - Number of bundled updates
9. **IsSecurityUpdate** - Security classification flag
10. **IsCriticalUpdate** - Critical classification flag
11. **IsCumulativeUpdate** - Cumulative update flag
12. **ApplicabilityRulesCount** - Number of applicability rules
13. **HasComplexApplicability** - Complex applicability indicator

## Usage

### 1. Train the Model (One-time Setup)

The model must be trained with historical data before use. The service will not function without a trained model.

```csharp
var trainingData = await GetHistoricalUpdateMetadata();
await anomalyDetectionService.TrainModelAsync(trainingData);
```

The model is saved to `./anomaly-model.zip` and auto-loaded on subsequent startups.

### 2. Enable Feature Flag

Set `Features:EnableAnomalyDetection` to `true` in configuration.

### 3. Configure Azure Storage

Ensure `AzureWebJobsStorage` connection string is configured:
- **Development**: Use Azurite emulator (`UseDevelopmentStorage=true`)
- **Production**: Azure Storage account connection string

### 4. Deploy and Monitor

The system will:
- Run scheduled analysis based on `AnomalyDetectionSchedule`
- Accept HTTP POST requests to `/api/ingest-anomaly`
- Log warnings for anomalies with score > threshold
- Enqueue events to Azure Storage Queue for async processing

## API Endpoints

### POST /api/ingest-anomaly

Ingest anomaly events from external systems.

**Request Body:**
```json
{
  "kb_ID": "KB5001234",
  "hashMatch": false,
  "score": 0.92,
  "isSigned": true,
  "domainReputation": 0.5
}
```

**Response (202 Accepted):**
```json
{
  "status": "accepted",
  "kbId": "KB5001234",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

**Error Responses:**
- `503 Service Unavailable` - Anomaly detection disabled
- `400 Bad Request` - Invalid payload or missing KB_ID
- `500 Internal Server Error` - Processing error

### Timer Trigger: ScheduledAnomalyDetection

Runs periodically based on `AnomalyDetectionSchedule` CRON expression.

**Actions:**
1. Queries metadata store for recent updates
2. Scores each update using ML.NET model
3. Logs warnings for anomalies (score > threshold)
4. Processes both demo and real update data

## Monitoring

### Logs

**Development:**
- `UpdateEngine.Core.Services.AnomalyDetectionService` = `Debug`
- Detailed scoring information for each update

**Production:**
- `UpdateEngine.Core.Services.AnomalyDetectionService` = `Information`
- Warnings for detected anomalies only

### Key Log Messages

```
[Information] Loaded anomaly detection model from ./anomaly-model.zip
[Warning] ALERT: Anomaly detected for KB_ID=KB5001234 (score=0.92)
[Warning] Quarantined KB5001234 due to hash mismatch
[Information] Anomaly detection completed for 100 updates
[Debug] Returning neutral anomaly score due to unsupported expression type
```

### Metrics

The system integrates with OpenTelemetry metrics:
- Anomaly detection runs
- Updates scored
- Anomalies detected
- Queue message counts

## Troubleshooting

### Model Not Found Error

```
[Warning] Anomaly detection model not found at ./anomaly-model.zip
```

**Solution:** Train the model using `TrainModelAsync()` or copy pre-trained model to deployment directory.

### Queue Service Initialization Failed

```
[Error] Failed to initialize anomaly queue service
```

**Solution:** Check `AzureWebJobsStorage` connection string. For development, ensure Azurite is running.

### Invalid GUID Byte Array Error

```
[Error] Byte array for Guid must be exactly 16 bytes long
Exception: System.ArgumentException: Byte array for Guid must be exactly 16 bytes long. (Parameter 'b')
   at UpdateEngine.Core.Services.QueryService.QueryMetadataAsync(MetadataQueryRequest request)
```

**Cause:** Some packages in the metadata store have malformed IDs that aren't exactly 16 bytes (required for GUID conversion).

**Solution:** This is now handled automatically. The `QueryService` validates ID lengths and skips packages with invalid IDs, logging them at Debug level:
```
[Debug] Skipping package 'PackageName' with invalid ID length: 12 bytes
```

**Impact:** Packages with invalid IDs are excluded from query results and anomaly detection but don't cause the entire operation to fail.

### Unsupported Expression Type

```
[Debug] Skipping applicability analysis due to unsupported expression type
```

**Behavior:** Returns neutral score (0.0) for updates with newer expression types. Not an error.

### Storage Access Issues

```
[Debug] Returning neutral anomaly score due to storage access issue
```

**Behavior:** Returns neutral score when metadata is missing or CompressedMetadataStore is write-only. Expected during partial sync.

### Empty Categories Lookup

```
[Information] Categories lookup built with 0 categories
```

**Cause:** The metadata store doesn't contain any classification or product categories yet, or they all have invalid IDs.

**Solution:** This is normal during initial startup before the first sync completes. After the first successful sync, categories should be populated. If categories remain at 0 after sync:
1. Check that sync completed successfully
2. Verify metadata store is readable
3. Check logs for package ID validation warnings

## Development Workflow

### Running Locally

1. Start Azurite emulator:
   ```bash
   azurite --silent --location c:\azurite --debug c:\azurite\debug.log
   ```

2. Configure settings in `local.settings.json`

3. Run Azure Functions:
   ```bash
   cd UpdateEngine.Functions/src
   func start
   ```

4. Trigger manual analysis:
   ```bash
   curl -X POST http://localhost:7071/admin/functions/ScheduledAnomalyDetection
   ```

### Testing Ingestion Endpoint

```bash
curl -X POST http://localhost:7071/api/ingest-anomaly \
  -H "Content-Type: application/json" \
  -d '{
    "kb_ID": "KB5001234",
    "hashMatch": false,
    "score": 0.92,
    "isSigned": true,
    "domainReputation": 0.5
  }'
```

## Production Deployment

### Azure Function App Settings

Configure the following app settings in Azure Portal:

```
Features__EnableAnomalyDetection=true
AnomalyDetection__ModelPath=./anomaly-model.zip
AnomalyDetection__AnomalyScoreThreshold=0.90
AnomalyDetection__QueueName=anomaly-events-prod
AnomalyDetectionSchedule=0 0 */2 * * *
AzureWebJobsStorage=<connection-string>
```

### Model Deployment

1. Train model with production data
2. Upload `anomaly-model.zip` to Function App:
   - Kudu Console (`https://<app-name>.scm.azurewebsites.net/DebugConsole`)
   - Or include in deployment package

### Monitoring & Alerts

Set up Azure Monitor alerts for:
- High anomaly detection rate (> 5% of updates)
- Queue message backlog (> 100 messages)
- Function execution failures
- Model scoring errors

## Performance Considerations

### Model Training
- Minimum 100 samples recommended
- Training time: ~5-30 seconds for 1000 samples
- Model size: ~50-100 KB

### Scoring Performance
- ~1-2ms per update (ML.NET prediction)
- Category lookup cached (one-time cost per service lifetime)
- Batch scoring recommended for large datasets

### Queue Processing
- Base64 encoding adds ~33% overhead
- Consider batch dequeue for high-volume scenarios
- Queue visibility timeout: 30 seconds default

## Future Enhancements

- [ ] Time-series anomaly detection (SsaSpikeDetection)
- [ ] Change point detection for trend analysis
- [ ] Automatic model retraining pipeline
- [ ] Integration with Azure Log Analytics
- [ ] Real-time alerting via SignalR/Event Grid
- [ ] Dashboard visualization with Grafana
- [ ] A/B testing for threshold tuning
