# Microsoft Update Server Azure Functions

This project converts the Microsoft Update server code into Azure isolated C# functions, allowing you to run a Windows Update server in Azure Functions.

## Overview

The Azure Functions implementation provides the same functionality as the original ASP.NET Core server but in a serverless environment:

- **Client Sync Functions**: Handle Windows Update client requests (MUv6 protocol)
- **Server Sync Functions**: Handle WSUS server-to-server synchronization
- **Content Functions**: Serve update content files
- **Authentication Functions**: Handle client and server authentication
- **Caching Layer**: Redis-backed distributed caching for improved performance ✨ **NEW**

## Key Features

### 🚀 Performance Optimization
- **Redis Distributed Caching**: Cache metadata statistics, update details, and content availability
- **50-95% Performance Improvement**: Reduce load on storage and upstream servers
- **Automatic Cache Invalidation**: Intelligent cache clearing after sync operations
- **Graceful Degradation**: Falls back to direct queries if Redis unavailable

### 📊 Monitoring & Health
- **Health Check Endpoints**: Built-in health monitoring for all critical components
- **Redis Health Monitoring**: Real-time cache connectivity and performance checks
- **Application Insights Integration**: Comprehensive telemetry and diagnostics

### ⚙️ Configuration Hot-Reload
- **Dynamic Configuration**: Update settings without restarting functions
- **Feature Flags**: Toggle features at runtime
- **Configurable TTLs**: Fine-tune cache expiration based on your needs

## Architecture

```text
┌─────────────────┐    ┌──────────────────────┐    ┌─────────────────┐
│  Windows Update │───▶│  Azure Functions     │───▶│  Redis Cache    │
│  Clients        │    │  (HTTP Triggered)    │    │  (Distributed)  │
└─────────────────┘    └──────────────────────┘    └─────────────────┘
                                │                           │
                                │                           │
                                ▼                           ▼
                       ┌─────────────────┐       ┌─────────────────┐
                       │  Storage        │       │  Cache-Aside    │
                       │  (Metadata &    │       │  Pattern        │
                       │  Content)       │       │  - 5-60 min TTL │
                       └─────────────────┘       └─────────────────┘
```

### Caching Architecture

The Update Engine implements a **cache-aside pattern** with Redis:

| Data Type | Cache Key | TTL | Performance Gain |
|-----------|-----------|-----|------------------|
| Metadata Statistics | `msupdate:metadata:stats` | 5 min | 70-90% |
| Update Details | `msupdate:metadata:update:{id}` | 60 min | 80-95% |
| Content Availability | `msupdate:content:availability:{id}` | 15 min | 50-80% |

**See [CACHING_GUIDE.md](../../docs/guides/CACHING_GUIDE.md) for complete caching documentation.**

## Functions

1. **ClientWebService** (`/api/ClientWebService/client.asmx`) - SOAP endpoint for Windows Update clients
2. **SimpleAuthWebService** (`/api/SimpleAuthWebService/SimpleAuth.asmx`) - Client authentication
3. **ServerSyncWebService** (`/api/ServerSyncWebService/ServerSyncWebService.asmx`) - WSUS server sync
4. **DssAuthWebService** (`/api/DssAuthWebService/DssAuthWebService.asmx`) - Server authentication
5. **GetMicrosoftUpdateContent** (`/api/content/{contentHash}`) - Content delivery
6. **FetchConfiguration** (`/api/FetchConfiguration`) - Fetch server configuration from upstream
7. **FetchCategories** (`/api/FetchCategories`) - Sync product categories and classifications
8. **FetchUpdates** (`/api/FetchUpdates`) - Download update metadata from Microsoft Update
9. **ReindexStore** (`/api/ReindexStore`) - Rebuild metadata search indices
10. **QueryMetadataStoreStatus** (`/api/QueryMetadataStoreStatus`) - Get metadata store status and statistics

## Prerequisites

- .NET 9.0 SDK
- Azure Functions Core Tools v4
- Azure subscription
- Update metadata and content (sync from Microsoft Update first)

## Configuration

### Local Development

Configure `local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "MetadataStorePath": "./store",
    "ContentStorePath": "./content",
    "ServiceConfigurationJson": "{\"ServiceUrl\":\"http://localhost:7071\",\"ContentUrl\":\"http://localhost:7071/api/content\"}",
    "ContentHttpRoot": "http://localhost:7071/api/content",
    "UpdateEngine__CacheConfiguration__EnableDistributedCache": "true",
    "ConnectionStrings__RedisConnection": "localhost:6379"
  }
}
```

### Caching Configuration

Add caching configuration to enable Redis-backed caching:

```json
{
  "UpdateEngine": {
    "CacheConfiguration": {
      "EnableDistributedCache": true,
      "KeyPrefix": "msupdate:",
      "DefaultExpirationMinutes": 60,
      "StatisticsCacheMinutes": 5,
      "UpdateDetailsCacheMinutes": 60,
      "ContentAvailabilityCacheMinutes": 15,
      "InvalidateOnSync": true
    }
  },
  "ConnectionStrings": {
    "RedisConnection": "localhost:6379"
  }
}
```

**Configuration Options:**
- `EnableDistributedCache`: Master switch for caching (default: `false`)
- `KeyPrefix`: Cache key prefix to avoid collisions (default: `"msupdate:"`)
- TTL settings: Configure cache expiration based on data volatility
- `InvalidateOnSync`: Auto-invalidate caches after sync operations

**See [Configuration/appsettings.example.json](../../Configuration/appsettings.example.json) for complete example.**

### Azure Production

Use the ARM template in `/deploy` folder or configure these application settings:

- `MetadataStorageConnection`: Connection string to metadata storage
- `ContentStorageConnection`: Connection string to content storage  
- `MetadataStorePath`: Path/container for metadata
- `ContentStorePath`: Path/container for content
- `ContentHttpRoot`: Base URL for serving content
- `ServiceConfigurationJson`: Service configuration JSON

**Caching in Production:**
- `ConnectionStrings__RedisConnection`: Azure Cache for Redis connection string
- `UpdateEngine__CacheConfiguration__EnableDistributedCache`: Set to `true`
- `UpdateEngine__CacheConfiguration__KeyPrefix`: Set unique prefix per environment

## Building and Running

### Local Development with Caching

**Option 1: Aspire (Recommended) - Includes Redis**
```bash
# Start AppHost (starts Azure Functions + Redis automatically)
cd ../../AppHost/src
dotnet run

# Result:
# ✅ Azure Functions: http://localhost:7071
# ✅ Redis: localhost:6379
# ✅ Aspire Dashboard: http://localhost:15888
```

**Option 2: Azure Functions Only**
```bash
# Start Redis separately (Docker)
docker run -d -p 6379:6379 redis:latest

# Start Azure Functions
cd UpdateEngine/src
func start
```

### Deploy to Azure

```bash
# Using ARM template
az deployment group create \
  --resource-group your-rg \
  --template-file deploy/azuredeploy.json \
  --parameters deploy/azuredeploy.parameters.json

# Or using Function Apps CLI
func azure functionapp publish your-function-app-name
```

## Usage

### Configure Windows Update Clients

Set Group Policy to point to your Azure Function:

1. **Specify intranet Microsoft update service location**
   - Set intranet update service: `https://your-function-app.azurewebsites.net/api/ClientWebService`
   - Set intranet statistics server: `https://your-function-app.azurewebsites.net/api/ClientWebService`

### Configure WSUS Servers

Point WSUS to sync from your Azure Function:

- Upstream server URL: `https://your-function-app.azurewebsites.net/api/ServerSyncWebService`

## Content Delivery

The content function supports:

- HTTP GET and HEAD requests
- Range requests for partial downloads
- SHA1 and SHA256 content addressing
- Proper MIME types and caching headers

Content URLs follow the pattern:

```
https://your-function-app.azurewebsites.net/api/content/{hash}
```

Where `{hash}` is the hex-encoded SHA1 or SHA256 hash of the content.

## Metadata Synchronization

The metadata sync functions provide the same capabilities as the upsync command-line tool but in a serverless environment:

### FetchConfiguration

```bash
POST /api/FetchConfiguration
Content-Type: application/json

{
  "UpstreamEndpoint": "https://your-upstream-server.com"  // Optional, defaults to Microsoft Update
}
```

### FetchCategories  

```bash
POST /api/FetchCategories
Content-Type: application/json

{
  "UpstreamEndpoint": "https://your-upstream-server.com"  // Optional
}
```

### FetchUpdates

```bash
POST /api/FetchUpdates  
Content-Type: application/json

{
  "UpstreamEndpoint": "https://your-upstream-server.com",  // Optional
  "UpdateIds": ["12345678-1234-1234-1234-123456789abc"],   // Optional, specific updates
  "ProductFilters": ["guid1", "guid2"],                    // Optional, product categories
  "ClassificationFilters": ["guid3", "guid4"]             // Optional, classifications
}
```

### ReindexStore

```bash
POST /api/ReindexStore
Content-Type: application/json

{
  "ForceReindex": false  // Optional, force reindex even if not required
}
```

### QueryMetadataStoreStatus

```bash
GET /api/QueryMetadataStoreStatus
```

Returns metadata store statistics and health information.

## Monitoring

The functions include comprehensive logging and can be monitored through:

- Application Insights (configured automatically)
- Azure Functions runtime logs
- Custom metrics and telemetry

## Monitoring & Health Checks

### Health Endpoints

```bash
# Check overall health (includes Redis)
GET /api/health

# Response:
{
  "status": "Healthy",
  "totalDuration": "00:00:00.1234567",
  "entries": {
    "redis-cache": {
      "status": "Healthy",
      "description": "Redis cache is healthy",
      "data": {
        "ResponseTimeMs": "12",
        "CacheType": "Redis",
        "LastCheckTime": "2025-01-16T10:30:00Z"
      }
    },
    "metadata-store": { ... },
    "content-store": { ... }
  }
}
```

### Cache Monitoring

**View cache operations:**
```bash
# Connect to Redis CLI
redis-cli

# View all cached keys
KEYS msupdate:*

# Check specific cache entry
GET msupdate:metadata:stats

# Check TTL
TTL msupdate:metadata:stats

# Clear all caches (testing only)
KEYS msupdate:* | xargs redis-cli DEL
```

**Application Insights Queries:**
```kusto
// Cache hit rate
traces
| where message contains "Cache HIT" or message contains "Cache MISS"
| summarize hits = countif(message contains "HIT"), 
           misses = countif(message contains "MISS")
| extend hit_rate = hits * 100.0 / (hits + misses)
```

## Security Considerations

- Use Azure AD authentication for production
- Configure network restrictions as needed
- Enable HTTPS only (automatic in Azure Functions)
- Use managed identities for storage access
- Review and configure CORS settings

## Limitations

- SOAP message handling is simplified - you may need to implement full SOAP parsing for complex scenarios
- Content storage is configured for Azure Blob Storage or local file system
- Function timeout limits may affect large sync operations

## Troubleshooting

### Common Issues

1. **Missing metadata store**: Ensure you've synced updates first using the upsync tool
2. **Content not found**: Verify content storage configuration and that content files exist
3. **SOAP parsing errors**: Check request format and implement proper SOAP envelope parsing
4. **Authentication failures**: Verify service configuration JSON is properly formatted

### Caching Issues

**Redis Connection Fails:**
```bash
# Check Redis is running
docker ps | grep redis

# Test connection
redis-cli -h localhost -p 6379 ping
# Should return "PONG"

# Check health endpoint
curl http://localhost:7071/api/health
```

**Low Cache Hit Rate:**
- Increase TTLs in configuration
- Check if invalidation is too aggressive
- Monitor request patterns in Application Insights

**Stale Data:**
- Ensure `InvalidateOnSync: true`
- Reduce TTLs for frequently changing data
- Manually clear caches: `redis-cli FLUSHDB`

**See [CACHING_GUIDE.md](../../docs/guides/CACHING_GUIDE.md) for detailed troubleshooting.**

## Documentation

- **[CACHING_GUIDE.md](../../docs/guides/CACHING_GUIDE.md)** - Complete caching guide
- **[WEEK3_COMPLETION_SUMMARY.md](../../docs/guides/WEEK3_COMPLETION_SUMMARY.md)** - Week 3 caching implementation summary
- **[ARCHITECTURE_DECISIONS.md](../../docs/guides/ARCHITECTURE_DECISIONS.md)** - Configuration and health check patterns
- **[TESTING_STRATEGY.md](../../docs/guides/TESTING_STRATEGY.md)** - Testing strategies including cache tests

## Contributing

This is a conversion of the existing Microsoft Update server code. For improvements to the core functionality, see the main repository.

For Azure Functions specific issues:

1. Fork the repository
2. Create a feature branch
3. Submit a pull request

## License

MIT License - see the main repository for details.
