# Testing Downstream Sync: Functions ? WorkerService

## Architecture

```
Microsoft Update (Internet)
    ? SOAP
Azure Functions (Azurite - Cloud Emulation)
  - Metadata: azurite://data container
  - Content: azurite://data container
    ? HTTP REST (Aspire Service Discovery)
WorkerService (Local Filesystem - Downstream Cache)
  - Metadata: ./data/metadata/
  - Content: ./data/content/
```

## Configuration Summary

### Azure Functions (Upstream Source)
- **Storage**: Azurite (injected by AppHost via `WithReference(data)`)
- **Mode**: Syncs from Microsoft Update
- **Schedule**: Every 3 minutes (SyncCritical)

### WorkerService (Downstream Cache)
- **Storage**: Local Filesystem (`./data/metadata`, `./data/content`)
- **Mode**: Downstream sync from Functions (`SyncFromUpstream: true`)
- **Source**: Functions via Aspire service discovery (`http://UpdateEngine`)
- **Schedule**: Every 15 minutes

## Step-by-Step Testing

### 1. Start AppHost

```bash
cd UpdateEngine.AppHost/src
dotnet run
```

**Expected Output**:
```
WorkerService: Using local filesystem storage for downstream cache (paths from appsettings.Development.json)
WorkerService: Downstream sync ENABLED - Will pull from UpdateEngine Functions
```

### 2. Verify Aspire Dashboard

Open: http://localhost:15888

**Check**:
- ? UpdateEngine (Functions) - Running on dynamic port (e.g., 57392)
- ? WorkerService - Running on port 8080
- ? Storage (Azurite) - Running
- ? Redis - Running

### 3. Trigger Functions Sync (Populate Azurite)

```bash
# Trigger Functions to sync from Microsoft Update
curl -X POST http://localhost:7071/api/sync \
  -H "Content-Type: application/json" \
  -d '{"syncType":"Critical","action":"Start"}'
```

**Expected Response**:
```json
{
  "message": "Sync operation started",
  "syncType": "Critical"
}
```

**Check Functions Logs** (in Aspire Dashboard):
```
[Info] Starting scheduled critical updates sync
[Info] Starting updates synchronization with filter
[Info] Updates synchronization completed
[Info] Critical sync: automatically downloading content for critical updates
[Info] Content sync: 150 files to download
[Info] Content synchronization completed: 150 files
```

### 4. Verify Functions Storage (Azurite)

```bash
# Check metadata status
curl http://localhost:7071/api/metadata/status

# Expected:
{
  "packageCount": 654,
  "isHealthy": true,
  "storageType": "AzureBlob",
  "containerName": "data"
}

# Check content status
curl http://localhost:7071/api/content/status

# Expected:
{
  "fileCount": 150,
  "totalSize": 524288000,
  "isHealthy": true
}
```

### 5. Trigger WorkerService Downstream Sync

```bash
# Manually trigger WorkerService to pull from Functions
curl -X POST http://localhost:8080/api/sync \
  -H "Content-Type: application/json" \
  -d '{"syncType":"Comprehensive","action":"Start"}'
```

**Expected WorkerService Logs** (in Aspire Dashboard or terminal):
```
[Info] SyncWorker initialized - Mode: DOWNSTREAM (from Functions), Interval: 15 minutes
[Info] Executing scheduled DOWNSTREAM sync from Functions
[Info] Starting downstream metadata sync from Functions
[Info] Received 654 metadata packages from upstream
[Info] Downstream metadata sync completed
[Info] Starting downstream content sync from Functions
[Info] Found 654 updates with content to sync
[Info] Downloading content file: windows11-kb5012345.cab (50 MB)
[Info] Content synchronization completed: 150/150 files downloaded
[Info] Downstream content sync completed
```

### 6. Verify WorkerService Filesystem Storage

```bash
# Check that metadata was written to filesystem
ls -la ./UpdateEngine.WorkerService/src/data/metadata/

# Expected:
# index.db
# packages/
# categories/

# Check that content was written to filesystem
ls -la ./UpdateEngine.WorkerService/src/data/content/

# Expected:
# (SHA256 hash named files)
# abc123def456...cab
# 789012ghi345...msu
```

**Or use WorkerService API**:
```bash
# Check WorkerService metadata status
curl http://localhost:8080/api/metadata/statistics

# Expected:
{
  "packageCount": 654,
  "storageType": "LocalFilesystem",
  "metadataPath": "./data/metadata"
}
```

### 7. Test Scheduled Sync

Wait 15 minutes and check WorkerService logs for automatic sync:

```
[Info] Executing scheduled DOWNSTREAM sync from Functions
[Info] Starting downstream metadata sync from Functions
[Info] Downstream metadata sync completed
```

## Troubleshooting

### Issue: WorkerService Not Pulling from Functions

**Check 1**: Verify service discovery
```bash
# In Aspire Dashboard, check WorkerService environment variables
# Should see: services__UpdateEngine__http__0 = http://localhost:57392
```

**Check 2**: Verify downstream configuration
```bash
curl http://localhost:8080/api/StorageDiagnostics

# Expected:
{
  "downstreamConfiguration": {
    "syncFromUpstream": true,
    "upstreamFunctionsUrl": "http://UpdateEngine"
  }
}
```

**Check 3**: Check logs for connection errors
```
[Error] HTTP error syncing metadata from upstream: No connection could be made
```

**Solution**: Ensure Functions are running and accessible via Aspire service name.

### Issue: WorkerService Using Azurite Instead of Filesystem

**Symptom**: Logs show "Using Azurite storage emulator"

**Solution**: Verify `appsettings.Development.json` in WorkerService:
```json
{
  "StorageConfiguration": {
    "UseAzureStorageForMetadata": false,
    "UseAzureStorageForContent": false
  }
}
```

### Issue: Functions Not Syncing from Microsoft Update

**Check**: Functions logs for sync errors
```
[Error] System.ServiceModel.FaultException: Access denied
```

**Cause**: HTTP 403 from Microsoft Update (known issue)

**Workaround**: Test with existing metadata:
```bash
# Export metadata from Functions to JSON
curl http://localhost:7071/api/metadata/export > metadata.json

# Verify it has data
cat metadata.json | jq '.length'
# Expected: 654
```

### Issue: Content Not Downloading

**Check 1**: Verify Functions have content
```bash
curl http://localhost:7071/api/content/status
```

**Check 2**: Verify WorkerService content sync enabled
```json
{
  "DownstreamConfiguration": {
    "EnableContentSync": true
  }
}
```

**Check 3**: Check disk space
```bash
df -h ./data/content
```

## Success Criteria

? **Functions**:
- Syncs from Microsoft Update every 3 minutes
- Stores metadata in Azurite (data container)
- Stores content in Azurite (data container)
- Exposes HTTP APIs on dynamic port

? **WorkerService**:
- Pulls from Functions every 15 minutes (or on-demand)
- Writes metadata to `./data/metadata/`
- Writes content to `./data/content/`
- Uses Aspire service discovery (no hardcoded ports)

? **Data Flow**:
- Microsoft Update ? Functions (SOAP sync)
- Functions ? Azurite (Azure SDK writes)
- WorkerService ? Functions (HTTP GET requests)
- Functions ? WorkerService (HTTP responses with data)
- WorkerService ? Filesystem (local writes)

## Performance Expectations

| Metric | Expected Value |
|--------|----------------|
| Functions Sync Time | 3-5 minutes (from Microsoft Update) |
| WorkerService Metadata Sync | 30-60 seconds (from Functions) |
| WorkerService Content Sync | 2-5 minutes (150 files ~500MB) |
| Metadata Size | ~10MB (654 packages) |
| Content Size | ~500MB (150 critical updates) |

## Next Steps

1. **Automation**: Scheduled sync works automatically (every 15 minutes)
2. **Monitoring**: Use Aspire Dashboard to watch sync progress
3. **Validation**: Compare package counts between Functions and WorkerService
4. **Cleanup**: Clear `./data/metadata` and `./data/content` to test fresh sync

## Related Documentation

- [DOWNSTREAM_SYNC_GUIDE.md](./DOWNSTREAM_SYNC_GUIDE.md) - Architecture and configuration
- [STORAGE_GUIDE.md](./STORAGE_GUIDE.md) - Storage backend options
- [DEVELOPMENT_SCHEDULES.md](./DEVELOPMENT_SCHEDULES.md) - Timer schedule configuration

---

**Status**: Ready for Testing  
**Last Updated**: 2025-01-24  
**Target**: .NET 9, Azure Functions v4, Aspire
