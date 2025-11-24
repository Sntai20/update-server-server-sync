# Downstream Sync Implementation Guide

## Overview

This implementation enables the **WorkerService** to act as a **downstream client** that pulls metadata and content from an upstream **Azure Functions** instance, rather than syncing directly from Microsoft Update.

```
Microsoft Update (upstream)
    ? SOAP/HTTP (Internet)
Azure Functions (Cloud - Authoritative Source)
  - Azure Blob Storage for metadata/content
  - Timer triggers sync from Microsoft Update
  - HTTP REST APIs expose data
    ? HTTP REST (Local Network)
WorkerService (On-Premises - Local Cache)
  - Local filesystem storage
  - Pulls from Functions HTTP APIs
  - Serves local Windows Update clients
```

## Architecture Benefits

### ? Advantages

1. **Single Source of Truth**: Only Functions syncs from Microsoft Update (reduces upstream load)
2. **Centralized Management**: Configure filters/schedules once in Functions
3. **Local Caching**: WorkerService provides fast local access for on-premises clients
4. **Network Efficiency**: WorkerService uses local filesystem, Functions uses cloud storage
5. **Hybrid Deployment**: Cloud authoritative source + on-premises distribution
6. **Reduced Complexity**: WorkerService doesn't need Microsoft Update SOAP client logic

### ?? Architecture Comparison

| Aspect | Current (Both Sync from MS Update) | New (Downstream from Functions) |
|--------|-----------------------------------|--------------------------------|
| **Microsoft Update Load** | 2 sync connections | 1 sync connection (Functions only) |
| **Data Consistency** | Independent (may drift) | Always consistent (single source) |
| **Configuration** | Duplicate (2 places) | Centralized (Functions only) |
| **Network Dependency** | Both need Internet | Only Functions needs Internet |
| **Local Performance** | Direct filesystem | Direct filesystem (same) |
| **Deployment Complexity** | High (2 separate configs) | Low (WorkerService just points to Functions) |

## Implementation Components

### 1. DownstreamSyncService

**Location**: `UpdateEngine.Core/src/Services/DownstreamSyncService.cs`

**Purpose**: HTTP client service for syncing from upstream Functions APIs

**Key Methods**:
```csharp
Task SyncMetadataFromUpstreamAsync(ServiceMetadataFilter? filter, CancellationToken)
Task SyncContentFromUpstreamAsync(ServiceMetadataFilter? filter, CancellationToken)
Task<UpstreamSyncStatus> GetUpstreamSyncStatusAsync(CancellationToken)
```

**Dependencies**:
- `HttpClient` (configured with upstream Functions URL)
- `IMetadataStore` (local filesystem store)
- `IContentStore` (local filesystem store)

### 2. DownstreamConfiguration

**Location**: `UpdateEngine.Configuration/src/DownstreamConfiguration.cs`

**Purpose**: Configuration model for downstream sync behavior

**Key Properties**:
```csharp
string UpstreamFunctionsUrl           // "http://localhost:7071" or cloud URL
bool SyncFromUpstream                 // Enable/disable downstream mode
int SyncIntervalMinutes              // How often to pull from upstream
bool EnableContentSync               // Pull content files (not just metadata)
TimeSpan HttpTimeout                 // Request timeout (default: 10 min)
int MaxRetries                       // Retry failed requests
```

### 3. WorkerService Configuration

**Location**: `UpdateEngine.WorkerService/src/appsettings.json`

**New Section**:
```json
{
  "UpdateEngine": {
    "DownstreamConfiguration": {
      "UpstreamFunctionsUrl": "http://localhost:7071",
      "SyncFromUpstream": false,
      "SyncIntervalMinutes": 60,
      "EnableContentSync": true,
      "HttpTimeout": "00:10:00"
    }
  }
}
```

## Configuration Examples

### Development (Local AppHost) - DEFAULT BEHAVIOR ?

**Location**: `UpdateEngine.Configuration/src/shared/appsettings.Development.json`

```json
{
  "DownstreamConfiguration": {
    "UpstreamFunctionsUrl": "http://UpdateEngine",  // ? Aspire service name (auto-resolves to dynamic port!)
    "SyncFromUpstream": true,
    "SyncIntervalMinutes": 15,
    "EnableContentSync": true
  }
}
```

**This is the default development experience**:
- WorkerService automatically pulls from Functions (no configuration needed!)
- **Aspire Service Discovery**: `"http://UpdateEngine"` is automatically resolved to the correct dynamic port
  - ? No hardcoded `localhost:7071` (Aspire assigns random ports)
  - ? Works even when Aspire changes the Functions port between runs
  - ? Automatic load balancing if multiple Functions instances exist
- AppHost starts both Functions (upstream) and WorkerService (downstream)
- Developers can immediately test downstream sync pattern
- Consistent across all development machines

**How Aspire Service Discovery Works**:
```
WorkerService HttpClient Request
  ?
"http://UpdateEngine" (service name)
  ?
Aspire Service Discovery Resolver
  ?
"http://localhost:57392" (actual dynamic port)
  ?
Azure Functions Instance
```

**Alternative: Hardcoded Localhost (Not Recommended)**

If you need to test without Aspire, you can hardcode the port:

```json
{
  "DownstreamConfiguration": {
    "UpstreamFunctionsUrl": "http://localhost:7071",  // ?? Only works if Functions use fixed port
    "SyncFromUpstream": true
  }
}
```

**?? Problem**: Aspire assigns **random ports** by default, so `localhost:7071` will break when Aspire assigns a different port.

**? Solution**: Use service name `"http://UpdateEngine"` instead (recommended).

### Production (Standalone WorkerService)

**Location**: `UpdateEngine.WorkerService/src/appsettings.json` (base config)

```json
{
  "DownstreamConfiguration": {
    "UpstreamFunctionsUrl": "",
    "SyncFromUpstream": false,  // ? Disabled in production by default
    "SyncIntervalMinutes": 60,
    "EnableContentSync": true
  }
}
```

**Production override** (if using downstream pattern):
**Location**: `UpdateEngine.WorkerService/src/appsettings.Production.json`

```json
{
  "DownstreamConfiguration": {
    "UpstreamFunctionsUrl": "https://myupdateengine.azurewebsites.net",
    "SyncFromUpstream": true,
    "SyncIntervalMinutes": 60,
    "EnableContentSync": true,
    "HttpTimeout": "00:15:00",
    "MaxRetries": 5
  },
  "StorageConfiguration": {
    "UseAzureStorageForMetadata": false,
    "UseAzureStorageForContent": false,
    "MetadataPath": "C:\\UpdateCache\\metadata",
    "ContentPath": "C:\\UpdateCache\\content"
  }
}
```

### Machine-Specific Overrides (Optional)

**Location**: `UpdateEngine.WorkerService/src/appsettings.Development.json`

```json
{
  "_comment": "Only needed for machine-specific overrides",
  "UpdateEngine": {
    // Example: Test standalone mode on dev machine
    "DownstreamConfiguration": {
      "SyncFromUpstream": false  // Override shared default
    }
  }
}
```

## Usage Patterns

### Pattern 1: Hybrid Cloud + On-Premises

**Scenario**: Organization wants central cloud management with local distribution points

```
Internet
  ?
Azure Functions (Central Management)
  - Syncs from Microsoft Update
  - Filters: Windows 11, Security Updates
  - Schedule: Every 3 hours
  ?
Regional WorkerService Instances
  - WorkerService-US-East (pulls from Functions)
  - WorkerService-EU-West (pulls from Functions)
  - WorkerService-APAC (pulls from Functions)
  ?
Local Windows Clients
```

**Benefits**:
- Single filter configuration in Functions
- Regional caching reduces latency
- Consistent update policies across regions

### Pattern 2: Air-Gapped Networks

**Scenario**: Secure network with no direct Internet access

```
DMZ (Internet-connected)
  ?
Azure Functions
  - Syncs from Microsoft Update
  - Exposes HTTP API
  ? (One-way firewall hole)
Secure Network (Air-gapped)
  ?
WorkerService
  - Pulls from Functions via firewall
  - Serves internal clients
  ?
Internal Windows Clients
```

**Benefits**:
- Minimal firewall rules (HTTP from WorkerService ? Functions)
- No direct Microsoft Update access from secure network
- Functions acts as security boundary

### Pattern 3: Branch Office Caching

**Scenario**: Multiple small branch offices with limited bandwidth

```
Azure Functions (HQ)
  ? WAN Link
WorkerService (Branch Office)
  - Caches updates locally
  - Serves 50-100 local clients
  ? LAN
Branch Office Clients
```

**Benefits**:
- Reduces WAN bandwidth usage (only sync once)
- Fast local updates over LAN
- Centralized update approvals at HQ

## Next Steps (Implementation Roadmap)

### Phase 1: Basic Metadata Sync (This PR) ?

- [x] `DownstreamSyncService` skeleton
- [x] `DownstreamConfiguration` model
- [x] WorkerService configuration structure
- [ ] DI registration for `IDownstreamSyncService`
- [ ] Update `SyncWorker` to use `IDownstreamSyncService` when `SyncFromUpstream=true`

### Phase 2: Metadata Import API (Next PR)

- [ ] Add `IMetadataStore.ImportMetadata(IEnumerable<IPackage>)` method
- [ ] JSON serialization for metadata packages
- [ ] Handle incremental updates (merge vs replace)
- [ ] Metadata deduplication logic

### Phase 3: Content Sync (Third PR)

- [ ] Add `IContentStore.AddContentStream(Stream, IContentFileDigest)` method
- [ ] HTTP range request support for large files
- [ ] Resume interrupted downloads
- [ ] Content verification (SHA256 validation)

### Phase 4: Advanced Features (Future)

- [ ] Delta/incremental sync (only changed updates)
- [ ] Compression for metadata transfer
- [ ] Multi-upstream failover
- [ ] Bandwidth throttling
- [ ] Progress reporting/UI

## Testing Strategy

### Unit Tests

```csharp
// Test DownstreamSyncService with mocked HttpClient
public class DownstreamSyncServiceTests
{
    [Fact]
    public async Task SyncMetadataFromUpstream_Success()
    {
        // Arrange: Mock HttpClient returning metadata JSON
        // Act: Call SyncMetadataFromUpstreamAsync()
        // Assert: Verify metadata store updated
    }

    [Fact]
    public async Task SyncContentFromUpstream_DownloadsFiles()
    {
        // Arrange: Mock HttpClient returning content streams
        // Act: Call SyncContentFromUpstreamAsync()
        // Assert: Verify content files written to disk
    }
}
```

### Integration Tests

```csharp
// Test full downstream sync flow with real Functions instance
public class DownstreamIntegrationTests : IClassFixture<AspireTestFixture>
{
    [Fact]
    public async Task WorkerService_SyncsFromFunctions()
    {
        // Arrange: Start Functions (upstream) via Aspire
        // Arrange: Start WorkerService (downstream) configured to pull from Functions
        // Arrange: Trigger Functions metadata sync
        // Act: Trigger WorkerService downstream sync
        // Assert: Verify WorkerService has same metadata as Functions
    }
}
```

### Manual Testing

```bash
# 1. Start Functions (upstream)
cd UpdateEngine.Functions
func start

# 2. Trigger Functions sync (populate Azure Storage)
curl -X POST http://localhost:7071/api/sync \
  -H "Content-Type: application/json" \
  -d '{"syncType":"Critical","action":"Start"}'

# 3. Start WorkerService (downstream) with SyncFromUpstream=true
cd UpdateEngine.WorkerService
dotnet run

# 4. Trigger WorkerService downstream sync
curl -X POST http://localhost:8080/api/sync \
  -H "Content-Type: application/json" \
  -d '{"syncType":"Comprehensive","action":"Start"}'

# 5. Verify WorkerService has metadata
curl http://localhost:8080/api/metadata/statistics
```

## Performance Considerations

### Metadata Sync

**Current Design** (Phase 1 - Prototype):
- Export all metadata as JSON from Functions
- Transfer full JSON payload
- Import all packages into WorkerService store

**Future Optimization** (Phase 4):
- Incremental sync (only changed updates since last sync)
- Compression (gzip JSON responses)
- Pagination (chunk large metadata sets)
- ETag/If-Modified-Since headers

### Content Sync

**Bandwidth Usage**:
- Windows 11 monthly security updates: ~500MB - 1GB
- Full catalog sync: 10GB+ (not recommended for downstream)
- Recommended: Filter to specific products/classifications

**Optimization Strategies**:
1. **Selective Sync**: Only download files for approved updates
2. **Deduplication**: Check local existence before download
3. **Resume Support**: Handle interrupted downloads (HTTP range requests)
4. **Parallel Downloads**: Download multiple files concurrently
5. **Bandwidth Throttling**: Respect network policies

### Example Bandwidth Calculation

```
Scenario: Branch office with 100 Windows 11 clients
- Functions: Downloads 1GB/month from Microsoft Update
- WorkerService: Downloads same 1GB/month from Functions once
- 100 Clients: Download from WorkerService over LAN (no WAN impact)

Without WorkerService: 100 clients × 1GB = 100GB WAN bandwidth/month
With WorkerService: 1 × 1GB = 1GB WAN bandwidth/month
Savings: 99GB WAN bandwidth/month (99% reduction!)
```

## Troubleshooting

### Issue: WorkerService not pulling from Functions

**Check**:
1. `DownstreamConfiguration.SyncFromUpstream` is `true`
2. `DownstreamConfiguration.UpstreamFunctionsUrl` is correct
3. Functions is running and accessible
4. Network connectivity (firewall rules)

**Logs to check**:
```
WorkerService: "Starting downstream metadata sync from upstream Functions API"
WorkerService: "Received {Count} metadata packages from upstream"
```

### Issue: HTTP timeout errors

**Solution**: Increase `DownstreamConfiguration.HttpTimeout`

```json
{
  "DownstreamConfiguration": {
    "HttpTimeout": "00:30:00"  // 30 minutes for very large metadata sets
  }
}
```

### Issue: Content files not downloading

**Check**:
1. `DownstreamConfiguration.EnableContentSync` is `true`
2. Functions has content files available (`/api/content/{hash}` returns 200)
3. WorkerService has write permissions to `StorageConfiguration.ContentPath`
4. Sufficient disk space

## Security Considerations

### Authentication

**Current**: No authentication (development/internal network only)

**Production Recommendations**:
1. **API Keys**: Add authentication header to HTTP requests
2. **Client Certificates**: Mutual TLS for downstream WorkerService
3. **Azure AD**: OAuth2/OpenID Connect for cloud deployments
4. **VPN**: Secure tunnel for on-premises ? cloud communication

**Example with API Key**:
```csharp
services.AddHttpClient<IDownstreamSyncService, DownstreamSyncService>()
    .ConfigureHttpClient((sp, client) =>
    {
        var config = sp.GetRequiredService<IOptions<AppConfig>>().Value;
        client.BaseAddress = new Uri(config.DownstreamConfiguration.UpstreamFunctionsUrl);
        client.DefaultRequestHeaders.Add("X-API-Key", "your-api-key-here");
    });
```

### Data Integrity

1. **SHA256 Validation**: Verify content files after download
2. **Metadata Signatures**: Validate metadata authenticity (future)
3. **TLS/HTTPS**: Encrypt data in transit
4. **Content Hash Verification**: Compare downloaded file hash with expected value

## Related Documentation

- [WorkerService README](../UpdateEngine.WorkerService/src/README.md)
- [Azure Functions README](../UpdateEngine.Functions/src/README.md)
- [Storage Configuration Guide](../docs/guides/STORAGE_GUIDE.md)
- [Testing Guide](../docs/guides/TESTING_GUIDE.md)

---

**Status**: Phase 1 (Skeleton) - Ready for Review  
**Next Phase**: Phase 2 (Metadata Import API)  
**Target**: .NET 9, Azure Functions v4, ASP.NET Core 9

