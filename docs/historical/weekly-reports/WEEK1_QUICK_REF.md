# Week 1 Quick Reference Card

## ?? Build Commands

```powershell
# Build all projects
dotnet build Configuration/Configuration.csproj
dotnet build UpdateEngine/src/UpdateEngine.csproj  
dotnet build AppHost/src/AppHost.csproj

# Clean build
dotnet clean && dotnet build --no-incremental
```

---

## ?? Run Commands

```powershell
# Run with Aspire (recommended)
cd AppHost/src && dotnet run

# Run Functions standalone
cd UpdateEngine/src && func start

# Run specific configuration
func start --environment Development
```

---

## ?? Health Check Endpoints

```bash
# All health checks
GET http://localhost:7071/health

# Critical checks only (liveness)
GET http://localhost:7071/health?tags=critical

# Storage checks only
GET http://localhost:7071/health?tags=storage

# Network checks only  
GET http://localhost:7071/health?tags=network
```

---

## ?? Sync API Endpoints

```bash
# Get sync status
GET http://localhost:7071/api/sync/status

# Start category sync
POST http://localhost:7071/api/sync
Content-Type: application/json
{
  "syncType": "categories",
  "action": "start"
}

# Start update sync with filter
POST http://localhost:7071/api/sync
Content-Type: application/json
{
  "syncType": "updates",
  "action": "start",
  "filter": {
    "productTitles": ["Windows 10"],
    "fromDate": "2024-01-01"
  }
}

# Pause sync
POST http://localhost:7071/api/sync
Content-Type: application/json
{
  "action": "pause"
}

# Resume sync
POST http://localhost:7071/api/sync
Content-Type: application/json
{
  "action": "resume"
}

# Cancel sync
POST http://localhost:7071/api/sync
Content-Type: application/json
{
  "action": "cancel"
}
```

---

## ?? Configuration Structure

```json
{
  "UpdateEngine": {
    "ServiceConfiguration": {
      "ServiceUrl": "http://localhost:7071",
      "ContentUrl": "http://localhost:7071/content",
      "MaxUpdateCount": 10000
    },
    "SyncConfiguration": {
      "SyncCriticalSchedule": "0 */2 * * * *",
      "EnableScheduledSync": true
    },
    "StorageConfiguration": {
      "UseAzureStorageForMetadata": false,
      "MetadataPath": "./data/metadata",
      "UseAzureStorageForContent": false,
      "ContentPath": "./data/content"
    },
    "FeatureFlags": {
      "EnableDetailedLogging": true,
      "EnableMetrics": false
    }
  }
}
```

---

## ?? Dependency Injection

```csharp
// Register all services
services.AddUpdateEngineCore(configuration);

// What gets registered:
// - IOptionsMonitor<AppConfig>
// - IMetadataStore  
// - IContentStore?
// - ISyncOrchestrator
// - ISyncService
// - IHealthCheck (4 checks)
// - IHttpClientFactory
```

---

## ?? Testing Quick Start

```csharp
// Mock configuration
var mockConfig = new Mock<IOptionsMonitor<AppConfig>>();
mockConfig.Setup(m => m.CurrentValue).Returns(appConfig);

// Mock service
var mockService = new Mock<ISyncService>();
mockService.Setup(s => s.GetSyncStatusAsync(It.IsAny<CancellationToken>()))
    .ReturnsAsync(new SyncStatus { IsRunning = false });

// Create orchestrator
var orchestrator = new SyncOrchestrator(
    mockService.Object,
    logger.Object,
    mockConfig.Object
);
```

---

## ?? Health Check Response Format

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0520000",
  "entries": {
    "metadata-store": {
      "status": "Healthy",
      "description": "Metadata store is accessible",
      "data": {
        "reindexRequired": false
      }
    },
    "content-store": {
      "status": "Degraded",
      "description": "Content store not configured"
    },
    "upstream-connection": {
      "status": "Healthy",
      "description": "Upstream server is reachable",
      "data": {
        "endpoint": "https://fe3.update.microsoft.com"
      }
    }
  }
}
```

---

## ?? Common Issues & Fixes

### Build Error: "Type 'AppConfig' does not exist"
```powershell
# Fix: Add project reference
dotnet add UpdateEngine/src/UpdateEngine.csproj reference Configuration/Configuration.csproj
```

### Error: "IMetadataStore not registered"
```csharp
// Fix: Call AddUpdateEngineCore in Program.cs
services.AddUpdateEngineCore(context.Configuration);
```

### Error: "Configuration section 'UpdateEngine' not found"
```json
// Fix: Add to appsettings.json
{
  "UpdateEngine": {
    "ServiceConfiguration": { },
    "SyncConfiguration": { },
    "StorageConfiguration": { },
    "FeatureFlags": { }
  }
}
```

### Azure Storage: "CloudBlobClient not found"
```csharp
// Note: Library uses OLD Azure SDK
// Workaround: Use local file system storage for now
"UseAzureStorageForMetadata": false,
"MetadataPath": "./data/metadata"
```

---

## ?? Key File Locations

| Component | Location |
|-----------|----------|
| Configuration POCOs | `Configuration/*.cs` |
| Health Checks | `UpdateEngine/src/Core/HealthChecks/*.cs` |
| Orchestrators | `UpdateEngine/src/Core/Orchestrators/*.cs` |
| Services | `UpdateEngine/src/Services/*.cs` |
| DI Registration | `UpdateEngine/src/Core/ServiceCollectionExtensions.cs` |
| Azure Functions | `UpdateEngine/src/Functions/Core/*.cs` |
| Program.cs | `UpdateEngine/src/Program.cs` |
| AppHost | `AppHost/src/Program.cs` |
| Documentation | `docs/guides/*.md` |

---

## ?? Environment Variables (AppHost)

```bash
# Storage
UseAzureStorageForMetadata=false
MetadataPath=./data/metadata
UseAzureStorageForContent=false
ContentPath=./data/content

# Service
ServiceUrl=http://localhost:7071
MaxUpdateCount=10000

# Features
EnableScheduledSync=true
EnableDetailedLogging=true
EnableMetrics=false

# Schedules (CRON)
SyncCriticalSchedule="0 */2 * * * *"
SyncComprehensiveSchedule="0 0 */1 * * *"
```

---

## ?? Architecture Layers

```
???????????????????????????????????????
?   Hosting Layer (Azure Functions)   ?  ? UnifiedSyncFunction.cs
???????????????????????????????????????
?   Orchestrators (Host-Agnostic)     ?  ? SyncOrchestrator.cs
???????????????????????????????????????
?   Services (Business Logic)         ?  ? ISyncService, SyncService
???????????????????????????????????????
?   Storage (Metadata & Content)      ?  ? IMetadataStore, IContentStore
???????????????????????????????????????
?   Health Checks                      ?  ? 4 health check classes
???????????????????????????????????????
?   Configuration (Hot-Reload)        ?  ? AppConfig, IOptionsMonitor
???????????????????????????????????????
```

---

## ?? Documentation Quick Links

- [Week 1 Completion Summary](./WEEK1_COMPLETION_SUMMARY.md)
- [Week 1 Progress Report](./WEEK1_PROGRESS_REPORT.md)
- [Implementation Summary](./IMPLEMENTATION_SUMMARY.md)
- [Architecture Decisions](./ARCHITECTURE_DECISIONS.md)
- [Testing Strategy](./TESTING_STRATEGY.md)
- [Config & Health Check Quick Ref](./CONFIG_HEALTHCHECK_QUICKREF.md)

---

## ? Week 1 Checklist

- [x] Configuration builds (0 errors)
- [x] UpdateEngine builds (0 errors)
- [x] AppHost builds (0 errors)
- [x] Health checks implemented (4)
- [x] ISyncService created & implemented
- [x] SyncOrchestrator integrated
- [x] UnifiedSyncFunction working
- [x] DI configured centrally
- [x] Aspire orchestration working
- [ ] Unit tests (Week 2)
- [ ] Integration tests (Week 2)

---

**Quick Start**: `cd AppHost/src && dotnet run` ??

**Status**: ? Week 1 Complete - All Builds Green

**Last Updated**: 2025-11-22
