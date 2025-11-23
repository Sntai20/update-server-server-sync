# WorkerService - ASP.NET Core Worker Service for UpdateEngine

## Overview

This is an ASP.NET Core Worker Service implementation that provides an alternative hosting model to Azure Functions. It demonstrates the **dual hosting pattern** where ~95% of the code is shared between hosting models through orchestrators and domain services.

## Architecture

```
WorkerService/
??? Program.cs                      # ASP.NET Core host setup
??? appsettings.json                # Configuration
??? appsettings.Development.json    # Dev overrides
?
??? Controllers/                    # ASP.NET Core REST APIs (5% of code)
?   ??? SyncController.cs           # Sync operations
?   ??? MetadataController.cs       # Metadata queries
?   ??? HealthController.cs         # Health check access
?
??? Workers/                        # Background services
    ??? SyncWorker.cs               # Scheduled sync operations
    ??? HealthCheckWorker.cs        # Periodic health monitoring
```

## Features

### ? HTTP API Endpoints

#### Sync Operations
- `POST /api/sync` - Start sync operation
- `GET /api/sync/status` - Get sync status

#### Metadata Operations
- `GET /api/metadata/statistics` - Get metadata store statistics
- `POST /api/metadata/query` - Query updates by filter
- `GET /api/metadata/index/status` - Get index status
- `POST /api/metadata/index/reindex` - Reindex metadata store

#### Health Checks (Kubernetes-compatible)
- `GET /health` - Comprehensive health status
- `GET /health/live` - Liveness probe (critical checks only)
- `GET /health/ready` - Readiness probe (all checks)
- `GET /health/tags/{tag}` - Health by specific tag

#### API Documentation
- Swagger UI available at `/swagger` (Development only)

### ? Background Workers

#### SyncWorker
- **Purpose**: Executes scheduled sync operations
- **Interval**: Configurable via `SyncConfiguration.SyncIntervalMinutes` (default: 60 minutes)
- **Configuration**: Hot-reload support via `IOptionsMonitor<AppConfig>`
- **Features**:
  - Respects `EnableScheduledSync` feature flag
  - Automatic retry on errors
  - Comprehensive logging

#### HealthCheckWorker
- **Purpose**: Performs periodic health checks
- **Interval**: Configurable via `SyncConfiguration.HealthCheckIntervalMinutes` (default: 5 minutes)
- **Configuration**: Hot-reload support via `IOptionsMonitor<AppConfig>`
- **Features**:
  - Detailed logging of health check results
  - Alerts on unhealthy/degraded status
  - Exception and diagnostic data logging

## Configuration

### appsettings.json

```json
{
  "UpdateEngine": {
    "ServiceConfiguration": {
      "ServiceUrl": "http://localhost:8080"
    },
    "SyncConfiguration": {
      "SyncIntervalMinutes": 60,
      "HealthCheckIntervalMinutes": 5,
      "EnableScheduledSync": false
    },
    "StorageConfiguration": {
      "MetadataPath": "./data/metadata",
      "ContentPath": "./data/content"
    },
    "CacheConfiguration": {
      "EnableDistributedCache": true,
      "RedisConnectionString": "localhost:6379"
    },
    "FeatureFlags": {
      "EnableComprehensiveSync": true,
      "EnableCaching": true
    }
  }
}
```

## Running Locally

### Option 1: Direct Run

```bash
cd WorkerService
dotnet run
```

Service will be available at `http://localhost:8080`

### Option 2: Via AppHost (Recommended)

```bash
cd AppHost/src
dotnet run
```

This starts:
- **Azure Functions**: `http://localhost:7071`
- **Worker Service**: `http://localhost:8080`
- **Redis**: `localhost:6379`
- **Azurite**: `localhost:10000`
- **Aspire Dashboard**: `http://localhost:15888`

## Testing

### Manual Testing with curl

```bash
# Health check
curl http://localhost:8080/health

# Liveness probe (critical checks only)
curl http://localhost:8080/health/live

# Readiness probe (all checks)
curl http://localhost:8080/health/ready

# Sync status
curl http://localhost:8080/api/sync/status

# Metadata statistics
curl http://localhost:8080/api/metadata/statistics

# Start sync
curl -X POST http://localhost:8080/api/sync \
  -H "Content-Type: application/json" \
  -d '{"syncType":"Categories","action":"Start"}'
```

### Swagger UI

Open `http://localhost:8080/swagger` in Development mode

## Code Reuse

### Shared with Azure Functions (~95%)

- ? **Orchestrators**: `ISyncOrchestrator`, `IMetadataOrchestrator`, `IContentOrchestrator`
- ? **Models**: `SyncModels`, `MetadataModels`, `HealthModels`
- ? **Configuration**: `AppConfig`, `SyncConfiguration`, `FeatureFlags`
- ? **Domain Services**: `SyncService`, `MetadataQueryService`
- ? **Health Checks**: `MetadataStoreHealthCheck`, `RedisHealthCheck`
- ? **Caching**: `CacheService`
- ? **DI Registration**: `ServiceCollectionExtensions.AddUpdateEngineCore()`

### Worker Service Specific (~5%)

- ? **Controllers**: ASP.NET Core controllers (thin adapters)
- ? **Workers**: `BackgroundService` implementations
- ? **Program.cs**: ASP.NET Core host setup

## Deployment

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["WorkerService/WorkerService.csproj", "WorkerService/"]
RUN dotnet restore "WorkerService/WorkerService.csproj"
COPY . .
WORKDIR "/src/WorkerService"
RUN dotnet build "WorkerService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WorkerService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Microsoft.UpdateServices.WorkerService.dll"]
```

### Azure Container Apps

```bash
az containerapp create \
  --name update-engine-worker \
  --resource-group myResourceGroup \
  --image myregistry.azurecr.io/update-engine-worker:latest \
  --environment myEnvironment \
  --target-port 8080 \
  --ingress external \
  --min-replicas 1 \
  --max-replicas 10
```

### Kubernetes

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: update-engine-worker
spec:
  replicas: 2
  selector:
    matchLabels:
      app: update-engine-worker
  template:
    metadata:
      labels:
        app: update-engine-worker
    spec:
      containers:
      - name: worker
        image: myregistry.azurecr.io/update-engine-worker:latest
        ports:
        - containerPort: 8080
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 5
```

## Configuration Patterns

### Hot-Reload Support

Controllers and Workers support different configuration patterns:

```csharp
// Controllers: Per-request snapshot
public SyncController(IOptionsSnapshot<AppConfig> config)
{
    var current = config.Value; // Snapshot for this request
}

// Workers: Hot-reload support
public SyncWorker(IOptionsMonitor<AppConfig> config)
{
    var current = config.CurrentValue; // Always up-to-date
    config.OnChange(newConfig => { /* react to changes */ });
}
```

## Monitoring

### Health Check Tags

Health checks are tagged for filtering:

- **critical**: Must pass for system to be considered healthy
- **storage**: Storage-related checks (metadata, content, Azure)
- **network**: Network connectivity checks (upstream)
- **cache**: Redis cache checks
- **azure**: Azure-specific checks

### Logging

Structured logging with different levels:

- **Information**: Normal operations (health checks passing, sync completed)
- **Warning**: Degraded state (health checks degraded, sync warnings)
- **Error**: Failures (health checks failing, sync errors)

### Metrics (via OpenTelemetry)

Automatically exported through Aspire ServiceDefaults:

- HTTP request metrics
- Health check durations
- Background worker execution times
- Cache hit/miss rates

## Troubleshooting

### Common Issues

#### Workers not starting

```bash
# Check logs
dotnet run --verbosity detailed

# Verify configuration
cat appsettings.json | grep -A 5 "SyncConfiguration"
```

#### Health checks failing

```bash
# Check specific health check
curl http://localhost:8080/api/health/tags/storage

# View detailed health report
curl http://localhost:8080/api/health | jq
```

#### Redis connection issues

```bash
# Verify Redis is running
redis-cli ping

# Check connection string in configuration
# ConnectionString format: "localhost:6379" or "redis:6379"
```

## Architecture Benefits

### vs. Azure Functions

| Feature | Azure Functions | Worker Service | Winner |
|---------|----------------|----------------|--------|
| **Cold Start** | 10-15 seconds | None (always running) | Worker Service |
| **Cost** | Consumption-based | Fixed VMs/Containers | Depends on usage |
| **Scaling** | Automatic | Manual/HPA | Azure Functions |
| **Deployment** | Simple (func publish) | Docker/K8s | Azure Functions |
| **Long-Running** | 5-10 min limit | Unlimited | Worker Service |
| **Background Workers** | TimerTrigger | BackgroundService | Worker Service |

### When to Use Worker Service

? **Use Worker Service when**:
- Need long-running background operations
- Want to avoid cold starts
- Deploying to Kubernetes or Container Apps
- Need fine-grained control over workers
- Have predictable, steady workload

? **Use Azure Functions when**:
- Have sporadic, event-driven workload
- Want automatic scaling
- Need simple deployment
- Want consumption-based pricing
- Operations complete quickly (<5 min)

## References

- [IMPLEMENTATION_SUMMARY.md](../IMPLEMENTATION_SUMMARY.md) - Complete architecture overview
- [WEEK4_DAY1_SUMMARY.md](./WEEK4_DAY1_SUMMARY.md) - Day 1 progress
- [ARCHITECTURE_DECISIONS.md](./ARCHITECTURE_DECISIONS.md) - Design decisions
- [TESTING_STRATEGY.md](./TESTING_STRATEGY.md) - Testing approach

---

**Project**: Microsoft Update Server-Server Sync  
**Component**: Worker Service (ASP.NET Core hosting model)  
**Week 4 Status**: Day 1 Complete (~40%)  
**Build Status**: ? Building successfully  
**Code Reuse**: ~95% shared with Azure Functions
