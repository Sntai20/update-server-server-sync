# Downstream Sync Implementation - README

## Overview

The downstream sync feature enables WorkerService to act as a downstream client, pulling metadata and content from Azure Functions instead of directly from Microsoft Update. This reduces load on Microsoft Update servers and provides a centralized management point.

## Architecture

```
Microsoft Update
      ?
Azure Functions (UpdateEngine)
      ?
   Azurite (Azure Storage Emulator)
      ?
HTTP APIs (/api/metadata/export, /api/content/{hash})
      ?
WorkerService (Downstream Client)
      ?
Local Filesystem (./data/metadata/, ./data/content/)
```

## Quick Start

**See**: `docs/guides/QUICK_START_DOWNSTREAM_SYNC.md` for step-by-step instructions.

### TL;DR

```bash
# 1. Start AppHost
cd UpdateEngine.AppHost/src
dotnet run

# 2. Run automated test (in new terminal)
./scripts/test/Test-DownstreamSync.ps1

# 3. Verify console output shows "Downstream sync ENABLED"
```

## Implementation Files

### Core Services
- **`UpdateEngine.Core/src/Services/DownstreamSyncService.cs`**: HTTP client for pulling from Functions
- **`UpdateEngine.Core/src/Services/NoOpDownstreamSyncService.cs`**: No-op implementation when disabled

### Configuration
- **`UpdateEngine.Configuration/src/DownstreamConfiguration.cs`**: Configuration model with validation
- **`UpdateEngine.Configuration/src/AppConfig.cs`**: Includes DownstreamConfiguration property
- **`UpdateEngine.Configuration/src/shared/appsettings.Development.json`**: Shared dev settings (enabled by default)
- **`UpdateEngine.WorkerService/src/appsettings.Development.json`**: WorkerService overrides (filesystem + downstream)

### Orchestration
- **`UpdateEngine.WorkerService/src/Workers/SyncWorker.cs`**: Dual-mode sync worker (upstream or downstream)
- **`UpdateEngine.UpdateEngine.AppHost/src/Program.cs`**: Conditional storage and service discovery configuration
- **`UpdateEngine.Core/src/ServiceCollectionExtensions.cs`**: DI registration with Aspire service discovery

### Testing & Documentation
- **`scripts/test/Test-DownstreamSync.ps1`**: Automated end-to-end test script
- **`docs/guides/QUICK_START_DOWNSTREAM_SYNC.md`**: Quick start guide for testing
- **`docs/guides/DOWNSTREAM_SYNC_GUIDE.md`**: Comprehensive architecture and configuration guide

## Configuration

### Enable Downstream Sync (Development)

Already configured in shared development settings. WorkerService pulls from Functions by default.

**`UpdateEngine.Configuration/src/shared/appsettings.Development.json`**:
```json
{
  "UpdateEngine": {
    "DownstreamConfiguration": {
      "SyncFromUpstream": true,
      "UpstreamFunctionsUrl": "http://UpdateEngine",  // Aspire service name
      "SyncIntervalMinutes": 15,
      "EnableContentSync": true
    }
  }
}
```

**`UpdateEngine.WorkerService/src/appsettings.Development.json`**:
```json
{
  "UpdateEngine": {
    "StorageConfiguration": {
      "UseAzureStorageForMetadata": false,  // Use filesystem
      "UseAzureStorageForContent": false,
      "MetadataPath": "./data/metadata",
      "ContentPath": "./data/content"
    },
    "DownstreamConfiguration": {
      "SyncFromUpstream": true,  // Pull from Functions
      "UpstreamFunctionsUrl": "http://UpdateEngine"
    }
  }
}
```

### Disable Downstream Sync

To make WorkerService sync directly from Microsoft Update instead:

**`UpdateEngine.WorkerService/src/appsettings.Development.json`**:
```json
{
  "UpdateEngine": {
    "DownstreamConfiguration": {
      "SyncFromUpstream": false  // Change to false
    }
  }
}
```

### Production Configuration

**`UpdateEngine.WorkerService/src/appsettings.json`**:
```json
{
  "UpdateEngine": {
    "DownstreamConfiguration": {
      "SyncFromUpstream": true,
      "UpstreamFunctionsUrl": "https://your-functions.azurewebsites.net",
      "SyncIntervalMinutes": 60,
      "EnableContentSync": true,
      "HttpTimeout": "00:10:00",
      "MaxRetries": 3
    },
    "StorageConfiguration": {
      "UseAzureStorageForMetadata": false,
      "MetadataPath": "/var/update-engine/metadata",
      "ContentPath": "/var/update-engine/content"
    }
  }
}
```

## How It Works

### 1. AppHost Detects Configuration

`UpdateEngine.UpdateEngine.AppHost/src/Program.cs` (lines 107-150):
```csharp
var workerServiceConfig = new AppConfig();
builder.Configuration.GetSection(AppConfig.SectionName).Bind(workerServiceConfig);

// Check if WorkerService wants filesystem storage
var workerServiceWantsAzure = 
    workerServiceConfig.StorageConfiguration.UseAzureStorageForMetadata 
    || workerServiceConfig.StorageConfiguration.UseAzureStorageForContent;

if (!workerServiceWantsAzure) {
    // Use filesystem
    if (workerServiceConfig.DownstreamConfiguration.SyncFromUpstream) {
        workerService.WithReference(updateFunctions);  // Enable service discovery
        Console.WriteLine("WorkerService: Downstream sync ENABLED");
    }
}
```

### 2. Aspire Service Discovery

- WorkerService configuration uses `"UpstreamFunctionsUrl": "http://UpdateEngine"`
- Aspire automatically resolves `http://UpdateEngine` to the actual dynamic port (e.g., `http://localhost:57392`)
- No hardcoded ports needed
- ServiceCollectionExtensions registers HttpClient with Aspire resilience patterns

### 3. SyncWorker Dual Mode

`UpdateEngine.WorkerService/src/Workers/SyncWorker.cs`:
```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    if (currentConfig.DownstreamConfiguration.SyncFromUpstream) {
        // DOWNSTREAM: Pull from Functions
        await this.ExecuteDownstreamSyncAsync(stoppingToken);
    } else {
        // UPSTREAM: Pull from Microsoft Update
        await this.ExecuteUpstreamSyncAsync(stoppingToken);
    }
}
```

### 4. DownstreamSyncService

`UpdateEngine.Core/src/Services/DownstreamSyncService.cs`:
```csharp
public async Task SyncMetadataFromUpstreamAsync(
    ServiceMetadataFilter filter, 
    CancellationToken cancellationToken)
{
    // Call Functions API: GET /api/metadata/export
    var packages = await httpClient.GetFromJsonAsync<List<PackageInfo>>(...);
    
    // Import into local metadata store
    // TODO: Implement IMetadataStore.ImportMetadata()
}

public async Task SyncContentFromUpstreamAsync(
    ServiceMetadataFilter filter, 
    CancellationToken cancellationToken)
{
    // Query Functions for file list: POST /api/metadata/query
    var updates = await httpClient.PostAsJsonAsync(...);
    
    // Download each file: GET /api/content/{hash}
    foreach (var file in updates.Files) {
        var stream = await httpClient.GetStreamAsync($"/api/content/{file.Hash}");
        // TODO: Implement IContentStore.AddContentStream()
    }
}
```

## Testing

### Automated Test

```powershell
# Run complete end-to-end test
./scripts/test/Test-DownstreamSync.ps1

# Skip Functions sync (if Functions already have data)
./scripts/test/Test-DownstreamSync.ps1 -SkipFunctionsSync

# Verbose output
./scripts/test/Test-DownstreamSync.ps1 -Verbose
```

### Manual Test

```bash
# 1. Start AppHost
cd UpdateEngine.AppHost/src
dotnet run

# 2. Check Aspire Dashboard
# Open: http://localhost:15275

# 3. Trigger Functions sync
curl -X POST http://localhost:7071/api/sync \
  -H "Content-Type: application/json" \
  -d '{"syncType":"Critical"}'

# 4. Verify Functions has data
curl http://localhost:7071/api/GetStoreStatus

# 5. Trigger WorkerService downstream sync
curl -X POST http://localhost:8080/api/sync \
  -H "Content-Type: application/json" \
  -d '{"syncType":"Comprehensive"}'

# 6. Verify WorkerService has data
curl http://localhost:8080/api/metadata/status

# 7. Check filesystem
ls -la ./data/metadata/
ls -la ./data/content/
```

## Verification Checklist

? **Configuration**:
- [ ] `appsettings.Development.json` has `UseAzureStorageForMetadata: false`
- [ ] `appsettings.Development.json` has `SyncFromUpstream: true`
- [ ] `appsettings.Development.json` has `UpstreamFunctionsUrl: "http://UpdateEngine"`

? **AppHost Startup**:
- [ ] Console shows "WorkerService: Using local filesystem storage"
- [ ] Console shows "WorkerService: Downstream sync ENABLED"
- [ ] No errors about missing configuration

? **Aspire Dashboard**:
- [ ] All services show "Running" status
- [ ] UpdateEngine (Functions) has dynamic port assigned
- [ ] WorkerService shows port 8080

? **Functions**:
- [ ] `/api/GetStoreStatus` returns metadata count > 0
- [ ] Logs show "Updates synchronization completed"
- [ ] Azurite has data in "data" container

? **WorkerService**:
- [ ] Logs show "Mode: DOWNSTREAM (from Functions)"
- [ ] Logs show "Received X metadata packages from upstream"
- [ ] Logs show "Content synchronization completed"
- [ ] `/api/metadata/status` returns totalPackages > 0

? **Filesystem**:
- [ ] `./data/metadata/` directory exists
- [ ] `./data/metadata/` contains files
- [ ] `./data/content/` directory exists
- [ ] `./data/content/` contains files

## Troubleshooting

See **`docs/guides/QUICK_START_DOWNSTREAM_SYNC.md`** for detailed troubleshooting.

### Common Issues

| Issue | Solution |
|-------|----------|
| "Downstream sync ENABLED" not shown | Check `UseAzureStorageForMetadata: false` in WorkerService config |
| WorkerService can't find Functions | Verify AppHost added `.WithReference(updateFunctions)` |
| No files in ./data/ | Check WorkerService logs for sync completion |
| HTTP 403 from Microsoft Update | Known issue, doesn't affect downstream sync testing |

## Status & Roadmap

### ? Completed (Phase 1)
- [x] DownstreamSyncService skeleton implementation
- [x] DownstreamConfiguration model with validation
- [x] AppConfig integration
- [x] ServiceCollectionExtensions registration
- [x] SyncWorker dual-mode support
- [x] AppHost conditional storage with service discovery
- [x] Aspire service discovery integration
- [x] Comprehensive documentation
- [x] Automated testing script

### ?? In Progress (Phase 2)
- [ ] Implement `IMetadataStore.ImportMetadata()` API
- [ ] Implement `IContentStore.AddContentStream()` API
- [ ] End-to-end testing with real data
- [ ] Performance benchmarking

### ?? Planned (Phase 3+)
- [ ] Delta/incremental sync
- [ ] Content compression
- [ ] Bandwidth throttling
- [ ] Progress reporting
- [ ] Health check enhancements
- [ ] Metrics and monitoring

## Benefits

### Development
- ? **Works out of the box**: Enabled by default in development
- ? **No hardcoded ports**: Aspire service discovery handles dynamic ports
- ? **Easy testing**: Automated test script + clear documentation
- ? **Filesystem inspection**: Easy to verify data with `ls` commands

### Production
- ? **Reduced upstream load**: Single Microsoft Update connection point
- ? **Centralized management**: Functions control what gets synced
- ? **Local caching**: WorkerService provides fast local access
- ? **Hybrid deployment**: Cloud Functions + on-premises WorkerService
- ? **Bandwidth optimization**: Download from Functions instead of internet
- ? **Air-gapped support**: WorkerService can run without internet access

## Related Documentation

- **Quick Start**: `docs/guides/QUICK_START_DOWNSTREAM_SYNC.md`
- **Architecture**: `docs/guides/DOWNSTREAM_SYNC_GUIDE.md`
- **Development Schedules**: `docs/guides/DEVELOPMENT_SCHEDULES.md`
- **Configuration Guide**: `docs/guides/STORAGE_GUIDE.md`

## Contributing

When implementing missing APIs:

1. **IMetadataStore.ImportMetadata()**: 
   - Import JSON packages from downstream source
   - Validate package structure
   - Add to store with proper indexing
   - Handle duplicates gracefully

2. **IContentStore.AddContentStream()**:
   - Stream download from HTTP response
   - Verify hash during download
   - Write to filesystem/blob storage
   - Update content index

See existing implementations in:
- `UpdateEngine.Metadata/src/Storage/Local/PackageStore.cs`
- `UpdateEngine.Metadata/src/Storage/Azure/PackageStore.cs`

---

**Last Updated**: 2025-01-16  
**Status**: Phase 1 Complete (Skeleton Implementation)  
**Target Framework**: .NET 9.0  
**Aspire**: Latest
