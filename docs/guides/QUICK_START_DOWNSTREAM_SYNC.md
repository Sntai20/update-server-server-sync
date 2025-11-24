# Quick Start: Downstream Sync Testing

This guide walks you through testing the complete downstream sync implementation where Azure Functions download metadata and content from Microsoft Update to Azurite, then WorkerService acts as a downstream client pulling from Functions and writing to the local filesystem.

## Architecture Overview

```
Microsoft Update ? Azure Functions ? Azurite (cloud emulation)
                           ?
                   WorkerService ? Local Filesystem (./data/)
```

**Azure Functions (UpdateEngine)**:
- Syncs from Microsoft Update
- Stores in Azurite (Azure Storage Emulator)
- Exposes HTTP APIs for downstream clients
- Port: Dynamic (assigned by Aspire)

**WorkerService**:
- Acts as downstream client
- Pulls from Functions via Aspire service discovery (`http://UpdateEngine`)
- Writes to local filesystem (`./data/metadata/`, `./data/content/`)
- Port: 8080 (default)

## Prerequisites

1. **.NET 9 SDK** installed
2. **Docker Desktop** running (for Azurite and Redis)
3. **AppHost** not currently running (we'll start it fresh)

## Step 1: Start AppHost

Open a terminal in the repository root:

```bash
cd UpdateEngine.AppHost/src
dotnet run
```

**Expected Console Output**:

```
WorkerService: Using local filesystem storage for downstream cache (paths from appsettings.Development.json)
WorkerService: Downstream sync ENABLED - Will pull from UpdateEngine Functions

Now listening on: http://localhost:15275
Application started. Press Ctrl+C to shut down.
```

**? Success Indicators**:
- Console shows "Downstream sync ENABLED"
- No errors about missing configuration
- Aspire Dashboard URL displayed (usually http://localhost:15275)

## Step 2: Verify Aspire Dashboard

1. Open browser to **http://localhost:15275** (Aspire Dashboard)
2. Click on **Resources** tab
3. Verify all services are running:
   - ? **UpdateEngine** (Azure Functions) - Status: Running
   - ? **WorkerService** - Status: Running
   - ? **Storage** (Azurite) - Status: Running
   - ? **Redis** - Status: Running

4. Note the **UpdateEngine** port (usually 7071, but may vary)

## Step 3: Run Automated Test

Open a **new terminal** (keep AppHost running):

```powershell
# From repository root
./scripts/test/Test-DownstreamSync.ps1
```

The script will:
1. Auto-detect Functions port from Aspire
2. Check initial state of both services
3. Trigger Functions sync from Microsoft Update (5-15 minutes)
4. Trigger WorkerService downstream sync from Functions
5. Verify filesystem storage created
6. Display summary report

**Expected Output**:

```
??????????????????????????????????????????????????????????????????
?  Downstream Sync End-to-End Test                              ?
?  Microsoft Update ? Functions ? WorkerService ? Filesystem    ?
??????????????????????????????????????????????????????????????????

=== Step 0: Pre-flight Checks ===
? Azure Functions is running
? WorkerService is running

=== Step 1: Check Azure Functions Initial State ===
? Functions Metadata Store:
  Total Updates: 0
  Total Products: 0
  Total Categories: 0

=== Step 2: Trigger Functions Sync from Microsoft Update ===
? This may take 5-15 minutes depending on network speed...
? Functions sync initiated successfully

=== Step 4: Trigger WorkerService Downstream Sync from Functions ===
? WorkerService downstream sync initiated

=== Step 5: Verify WorkerService Filesystem Storage ===
? Metadata directory exists: ./data/metadata
?   Files in metadata directory: 15
? Content directory exists: ./data/content
?   Files in content directory: 150

??????????????????????????????????????????????????????????????????
?  ? SUCCESS: Complete downstream sync flow verified!           ?
?  Microsoft Update ? Functions ? WorkerService ? Filesystem    ?
??????????????????????????????????????????????????????????????????
```

## Step 4: Manual Verification (Optional)

### Check Functions Status

```bash
# Replace 7071 with your actual Functions port from Aspire Dashboard
curl http://localhost:7071/api/GetStoreStatus
```

**Expected Response**:
```json
{
  "metadataStore": {
    "totalUpdates": 654,
    "totalProducts": 120,
    "totalCategories": 45
  }
}
```

### Check WorkerService Status

```bash
curl http://localhost:8080/api/metadata/status
```

**Expected Response**:
```json
{
  "totalPackages": 654,
  "indexedUpdates": 654,
  "storageType": "FileSystem"
}
```

### Check Filesystem

```bash
# From repository root
ls -la ./data/metadata/
ls -la ./data/content/ | head -20
```

**Expected Output**:
```
./data/metadata/:
  index.db            (Metadata index)
  updates/            (Update metadata)
  categories/         (Category data)

./data/content/:
  abc123...cab        (Update file 1)
  def456...cab        (Update file 2)
  ...
```

## Step 5: Monitor Logs in Aspire Dashboard

1. Go to Aspire Dashboard: http://localhost:15275
2. Click **Logs** tab
3. Filter by **UpdateEngine** (Azure Functions):
   ```
   Starting scheduled critical updates sync
   Updates synchronization completed: 654 updates
   Content sync: 150 files to download
   Content synchronization completed: 150 files
   ```

4. Filter by **WorkerService**:
   ```
   SyncWorker initialized - Mode: DOWNSTREAM (from Functions), Interval: 15 minutes
   Executing scheduled DOWNSTREAM sync from Functions
   Starting downstream metadata sync from Functions
   Received 654 metadata packages from upstream
   Downstream metadata sync completed
   Starting downstream content sync from Functions
   Found 654 updates with content to sync
   Content synchronization completed: 150/150 files downloaded
   ```

## Troubleshooting

### Issue: "Downstream sync ENABLED" not shown in console

**Solution**: Check `UpdateEngine.WorkerService/src/appsettings.Development.json`:
```json
{
  "UpdateEngine": {
    "StorageConfiguration": {
      "UseAzureStorageForMetadata": false,  // Must be false
      "UseAzureStorageForContent": false    // Must be false
    },
    "DownstreamConfiguration": {
      "SyncFromUpstream": true,             // Must be true
      "UpstreamFunctionsUrl": "http://UpdateEngine"
    }
  }
}
```

### Issue: WorkerService not finding Functions

**Symptom**: Logs show "Could not resolve service name 'UpdateEngine'"

**Solution**: 
1. Verify AppHost added service reference (check console output)
2. Restart AppHost completely
3. Check WorkerService logs for Aspire service discovery messages

### Issue: No files in ./data/ directories

**Possible Causes**:
1. **Functions haven't synced yet**: Wait for Functions sync to complete (check Aspire logs)
2. **WorkerService using Azurite instead**: Check console output - should say "Using local filesystem storage"
3. **Downstream sync disabled**: Check WorkerService logs for "Mode: DOWNSTREAM"

### Issue: HTTP 403 Forbidden from Microsoft Update

**Symptom**: Functions logs show "Upstream sync failed: 403 Forbidden"

**Explanation**: This is a known issue with Microsoft Update authentication. It doesn't affect downstream sync testing.

**Workaround**: If Functions already have data in Azurite, skip Functions sync:
```powershell
./scripts/test/Test-DownstreamSync.ps1 -SkipFunctionsSync
```

## Success Criteria

? **Complete Success** - All of the following are true:
1. AppHost console shows "Downstream sync ENABLED"
2. Functions API responds with metadata count > 0
3. WorkerService API responds with totalPackages > 0
4. `./data/metadata/` directory exists with files
5. `./data/content/` directory exists with files
6. WorkerService logs show "DOWNSTREAM (from Functions)"
7. Aspire service discovery resolves "http://UpdateEngine"

## Next Steps

### Test Scheduled Sync

The WorkerService will automatically pull from Functions every **15 minutes** (configurable in `appsettings.Development.json`).

**Monitor scheduled sync**:
1. Wait 15 minutes after initial sync
2. Check WorkerService logs in Aspire Dashboard
3. Look for: "Executing scheduled DOWNSTREAM sync from Functions"
4. Verify filesystem files are updated

### Test Manual Sync

Trigger on-demand sync:
```bash
# Trigger Functions sync from Microsoft Update
curl -X POST http://localhost:7071/api/sync -H "Content-Type: application/json" -d '{"syncType":"Critical"}'

# Trigger WorkerService downstream sync from Functions
curl -X POST http://localhost:8080/api/sync -H "Content-Type: application/json" -d '{"syncType":"Comprehensive"}'
```

### Customize Configuration

Edit `UpdateEngine.WorkerService/src/appsettings.Development.json`:

```json
{
  "UpdateEngine": {
    "DownstreamConfiguration": {
      "SyncIntervalMinutes": 5,      // Change sync frequency (default: 15)
      "EnableContentSync": false,    // Metadata only (default: true)
      "HttpTimeout": "00:05:00"      // Shorter timeout (default: 10 min)
    }
  }
}
```

### Production Deployment

For production, update `UpdateEngine.WorkerService/src/appsettings.json`:

```json
{
  "UpdateEngine": {
    "DownstreamConfiguration": {
      "SyncFromUpstream": true,
      "UpstreamFunctionsUrl": "https://your-functions.azurewebsites.net",
      "SyncIntervalMinutes": 60,
      "EnableContentSync": true
    },
    "StorageConfiguration": {
      "UseAzureStorageForMetadata": false,
      "MetadataPath": "/var/update-engine/metadata",
      "ContentPath": "/var/update-engine/content"
    }
  }
}
```

## Performance Expectations

| Operation | Expected Time | Data Size |
|-----------|--------------|-----------|
| Functions initial sync | 5-15 minutes | ~50-200 MB metadata |
| WorkerService downstream sync (metadata) | 10-30 seconds | Same as Functions |
| WorkerService downstream sync (content) | 2-10 minutes | ~500 MB - 2 GB |
| Scheduled background sync | 15 minutes (configurable) | Incremental |

## Additional Resources

- **Detailed Testing Guide**: `docs/guides/TESTING_DOWNSTREAM_SYNC.md`
- **Architecture Guide**: `docs/guides/DOWNSTREAM_SYNC_GUIDE.md`
- **Development Schedules**: `docs/guides/DEVELOPMENT_SCHEDULES.md`
- **Aspire Service Discovery**: https://learn.microsoft.com/dotnet/aspire/service-discovery/overview

## Questions?

If something doesn't work as expected:
1. Check Aspire Dashboard logs (http://localhost:15275)
2. Review configuration files for typos
3. Restart AppHost completely
4. Run test script with `-Verbose` flag for detailed output
5. See `docs/guides/TESTING_DOWNSTREAM_SYNC.md` for comprehensive troubleshooting

---

**Last Updated**: 2025-01-16  
**Target Framework**: .NET 9.0  
**Aspire Version**: Latest
