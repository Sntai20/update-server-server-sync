# Implementation Summary: Dual Hosting + Solution Integration

## ?? Complete Architecture Overview

This document provides a complete overview of how the dual hosting model integrates with your **entire solution**.

## ?? Complete Solution Structure

```
update-server-server-sync/
?
??? ?? AppHost/                                  # .NET Aspire Orchestration
?   ??? src/
?       ??? AppHost.csproj
?       ??? Program.cs                           # Orchestrates all projects
?       ??? ConfigurationHelper.cs               # Shared configuration helpers
?
??? ?? Configuration/                            # Shared Configuration Models
?   ??? Configuration.csproj
?   ??? AppConfig.cs                             # Configuration POCO (IOptionsMonitor pattern)
?   ??? ServiceConfiguration.cs                  # Service-level settings
?   ??? SyncConfiguration.cs                     # Sync operation settings
?   ??? StorageConfiguration.cs                  # Storage settings
?   ??? FeatureFlags.cs                          # Runtime feature toggles
?
??? ?? ServiceDefaults/                          # Aspire Service Defaults
?   ??? ServiceDefaults/
?       ??? ServiceDefaults.csproj
?       ??? Extensions.cs                        # Shared Aspire extensions
?           ??? AddServiceDefaults()             # Telemetry, health checks, resilience
?           ??? ConfigureOpenTelemetry()         # Distributed tracing
?
??? ?? UpdateEngine/                             # Main Application
?   ??? src/
?   ?   ??? UpdateEngine.csproj                  # Azure Functions
?   ?   ??? Program.cs                           # Azure Functions host
?   ?   ?
?   ?   ??? ?? Core/                            # HOST-AGNOSTIC (90% of code)
?   ?   ?   ??? Orchestrators/                   # Business logic layer
?   ?   ?   ?   ??? ISyncOrchestrator.cs
?   ?   ?   ?   ??? SyncOrchestrator.cs         ? SHARED: Functions + Worker + CLI
?   ?   ?   ?   ??? IMetadataOrchestrator.cs
?   ?   ?   ?   ??? MetadataOrchestrator.cs     ? SHARED: Functions + Worker + CLI
?   ?   ?   ?   ??? IHealthOrchestrator.cs
?   ?   ?   ?   ??? HealthOrchestrator.cs       ? SHARED: Functions + Worker + CLI
?   ?   ?   ?   ??? IContentOrchestrator.cs
?   ?   ?   ?
?   ?   ?   ??? Models/                          # Shared request/response models
?   ?   ?   ?   ??? SyncModels.cs               ? SHARED: All hosts
?   ?   ?   ?   ??? MetadataModels.cs           ? SHARED: All hosts
?   ?   ?   ?   ??? HealthModels.cs             ? SHARED: All hosts
?   ?   ?   ?   ??? ContentModels.cs            ? SHARED: All hosts
?   ?   ?   ?
?   ?   ?   ??? ServiceCollectionExtensions.cs  ? SHARED: DI registration
?   ?   ?
?   ?   ??? ?? Functions/                       # Azure Functions Adapters (5% of code)
?   ?   ?   ??? Core/
?   ?   ?   ?   ??? UnifiedSyncFunction.cs      # Uses ISyncOrchestrator
?   ?   ?   ?   ??? UnifiedMetadataFunction.cs  # Uses IMetadataOrchestrator
?   ?   ?   ?   ??? UnifiedContentFunction.cs   # Uses IContentOrchestrator
?   ?   ?   ?   ??? WebServiceFunctions.cs      # SOAP (keep separate)
?   ?   ?   ??? Management/
?   ?   ?   ?   ??? UnifiedHealthFunction.cs    # Uses IHealthOrchestrator
?   ?   ?   ??? Intelligence/
?   ?   ?       ??? AnomalyDetectionFunctions.cs
?   ?   ?
?   ?   ??? Services/                            # Existing domain services
?   ?   ??? Helpers/
?   ?   ??? Models/
?   ?   ?
?   ?   ??? ?? WorkerService/                   # Worker Service Adapters (5% of code)
?   ?       ??? WorkerService.csproj             # NEW project
?   ?       ??? Program.cs                       # Worker Service host
?   ?       ?
?   ?       ??? Controllers/                     # ASP.NET Core controllers
?   ?       ?   ??? SyncController.cs           # Uses ISyncOrchestrator
?   ?       ?   ??? MetadataController.cs       # Uses IMetadataOrchestrator
?   ?       ?   ??? ContentController.cs        # Uses IContentOrchestrator
?   ?       ?   ??? HealthController.cs         # Uses IHealthOrchestrator
?   ?       ?
?   ?       ??? Workers/                         # Background services
?   ?           ??? SyncWorker.cs               # Uses ISyncOrchestrator
?   ?           ??? HealthCheckWorker.cs        # Uses IHealthOrchestrator
?   ?           ??? AnomalyDetectionWorker.cs
?   ?
?   ??? test/
?       ??? UpdateEngineTest.csproj
?       ??? Unit/
?           ??? OrchestratorTests/              # Test orchestrators (works for ALL hosts!)
?               ??? SyncOrchestratorTests.cs
?               ??? MetadataOrchestratorTests.cs
?               ??? HealthOrchestratorTests.cs
?
??? ?? update-cli/                              # Command-Line Interface
?   ??? src/
?       ??? update-cli.csproj
?       ??? Program.cs
?       ??? Commands/
?           ??? SyncCommand.cs                   # Uses ISyncOrchestrator ?
?           ??? QueryCommand.cs                  # Uses IMetadataOrchestrator ?
?           ??? HealthCommand.cs                 # Uses IHealthOrchestrator ?
?
??? ?? microsoft-update-partition/               # Existing Libraries
?   ??? src/ (metadata storage)
?
??? ?? microsoft-update-webservices/
?   ??? src/ (SOAP services)
?
??? ?? microsoft-update-upstream-source/
?   ??? src/ (upstream client)
?
??? ?? microsoft-update-endpoints/
    ??? src/ (ASP.NET Core endpoints)
```

## ?? Code Sharing Matrix

| Component | Azure Functions | Worker Service | CLI Tool | Tests |
|-----------|----------------|----------------|----------|-------|
| **Orchestrators** | ? 100% | ? 100% | ? 100% | ? 100% |
| **Models** | ? 100% | ? 100% | ? 100% | ? 100% |
| **Configuration** | ? 100% | ? 100% | ? 100% | ? 100% |
| **Domain Services** | ? 100% | ? 100% | ? 100% | ? 100% |
| **Hosting Layer** | ? Azure Functions specific | ? ASP.NET specific | ? Console specific | N/A |
| **DI Registration** | ? 100% (ServiceCollectionExtensions) | ? 100% (ServiceCollectionExtensions) | ? 100% (ServiceCollectionExtensions) | ? 100% |

**Result**: ~95% code reuse across all hosting models!

## ?? AppHost Integration

### What AppHost Does

```csharp
// AppHost/src/Program.cs

var builder = DistributedApplication.CreateBuilder(args);

// 1. Start Azurite (local Azure Storage)
var azurite = builder.AddAzureStorage("storage").RunAsEmulator();
var blobs = azurite.AddBlobs("blobs");

// 2. Start Azure Functions
var functions = builder.AddProject<Projects.UpdateEngine>("functions")
    .WithReference(blobs);  // Connects to Azurite

// 3. Start Worker Service (optional)
var worker = builder.AddProject<Projects.UpdateEngine_WorkerService>("worker")
    .WithReference(blobs)   // Connects to SAME Azurite
    .WithHttpEndpoint(8080);

builder.Build().Run();
```

**One Command Starts Everything:**
```bash
cd AppHost/src && dotnet run

# Result:
# ? Azurite running on port 10000
# ? Azure Functions running on port 7071
# ? Worker Service running on port 8080
# ? Aspire Dashboard at http://localhost:15888
```

## ?? Configuration Flow

```
???????????????????????????????????????????????????????????????
?                   Configuration Sources                      ?
???????????????????????????????????????????????????????????????
?  • appsettings.json                                          ?
?  • local.settings.json (Azure Functions)                     ?
?  • Environment variables                                     ?
?  • AppHost configuration                                     ?
???????????????????????????????????????????????????????????????
                   ?
                   ?
???????????????????????????????????????????????????????????????
?           .NET Options Pattern (IOptionsMonitor)             ?
?           services.Configure<AppConfig>(...)                 ?
???????????????????????????????????????????????????????????????
                   ?
                   ?
???????????????????????????????????????????????????????????????
?                    AppConfig (POCO)                          ?
?  • ServiceConfiguration (hot-reload support)                 ?
?  • SyncConfiguration (hot-reload support)                    ?
?  • StorageConfiguration (startup only)                       ?
?  • FeatureFlags (hot-reload support)                         ?
???????????????????????????????????????????????????????????????
                   ?
                   ?
???????????????????????????????????????????????????????????????
?              ServiceCollectionExtensions                     ?
?         .AddUpdateEngineCore(configuration)                  ?
?                                                              ?
?  • Registers IOptionsMonitor<AppConfig>                      ?
?  • Registers Orchestrators (use IOptionsMonitor)             ?
?  • Registers Domain Services                                 ?
?  • Registers Stores (use IOptions - no hot-reload)           ?
?  • Registers Health Checks (ASP.NET Core standard)           ?
???????????????????????????????????????????????????????????????
                   ?
       ?????????????????????????
       ?           ?           ?
       ?           ?           ?
???????????? ???????????? ????????????
?  Azure   ? ?  Worker  ? ?   CLI    ?
?Functions ? ? Service  ? ?   Tool   ?
???????????? ???????????? ????????????
```

## ?? Dependency Injection (Shared)

### ServiceCollectionExtensions.cs (Shared by ALL hosts)

```csharp
// UpdateEngine/src/Core/ServiceCollectionExtensions.cs

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUpdateEngineCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Configuration (.NET Options Pattern with hot-reload)
        services.Configure<AppConfig>(
            configuration.GetSection(AppConfig.SectionName));

        // IOptionsMonitor<AppConfig> provides hot-reload support
        // IOptions<AppConfig> for services that don't need hot-reload

        // 2. Orchestrators (host-agnostic, use IOptionsMonitor for hot-reload)
        services.AddSingleton<ISyncOrchestrator, SyncOrchestrator>();
        services.AddSingleton<IMetadataOrchestrator, MetadataOrchestrator>();
        services.AddSingleton<IHealthOrchestrator, HealthOrchestrator>();
        services.AddSingleton<IContentOrchestrator, ContentOrchestrator>();

        // 3. Domain services (existing)
        services.AddSingleton<ISyncService, SyncService>();
        services.AddSingleton<IMetadataQueryService, MetadataQueryService>();
        services.AddSingleton<IHealthService, HealthService>();

        // 4. Stores (from microsoft-update-partition project)
        // Use IOptions (startup only) since stores don't hot-reload
        services.AddSingleton<IMetadataStore>(provider =>
        {
            var config = provider.GetRequiredService<IOptions<AppConfig>>().Value;
            return config.StorageConfiguration.UseAzureStorageForMetadata
                ? Azure.PackageStore.Open(
                    config.StorageConfiguration.AzureStorageConnectionString,
                    config.StorageConfiguration.AzureContainerName)
                : PackageStore.Open(config.StorageConfiguration.MetadataPath);
        });

        services.AddSingleton<IContentStore?>(provider =>
        {
            var config = provider.GetRequiredService<IOptions<AppConfig>>().Value;
            if (string.IsNullOrEmpty(config.StorageConfiguration.ContentPath))
                return null;

            return config.StorageConfiguration.UseAzureStorageForContent
                ? new Azure.BlobContentStore(
                    config.StorageConfiguration.AzureStorageConnectionString,
                    config.StorageConfiguration.AzureContentContainer)
                : new FileSystemContentStore(config.StorageConfiguration.ContentPath);
        });

        // 5. ASP.NET Core Health Checks (industry standard)
        services.AddHealthChecks()
            .AddCheck<MetadataStoreHealthCheck>(
                name: "metadata-store",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "storage", "critical" })
            .AddCheck<ContentStoreHealthCheck>(
                name: "content-store",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "storage" })
            .AddCheck<UpstreamConnectionHealthCheck>(
                name: "upstream-connection",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "network" })
            .AddCheck<AzureBlobStorageHealthCheck>(
                name: "azure-storage",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "storage", "azure", "critical" });

        return services;
    }
}
```

### Usage in ALL Hosts

**Azure Functions:**
```csharp
services.AddUpdateEngineCore(context.Configuration);

// Orchestrators automatically get IOptionsMonitor<AppConfig>
// Hot-reload works out of the box!
```

**Worker Service:**
```csharp
services.AddUpdateEngineCore(builder.Configuration);

// Health check endpoints automatically available at:
// - /health (comprehensive)
// - /health/live (liveness probe)
// - /health/ready (readiness probe)
```

**CLI Tool:**
```csharp
services.AddUpdateEngineCore(configuration);

// CLI can inject IOptionsMonitor<AppConfig> for config access
// Or IOptions<AppConfig> if hot-reload not needed
```

**Same registration code = Same behavior!** ?

### Configuration Pattern Examples

```csharp
// ? Hot-reload support (singleton services)
public SyncOrchestrator(IOptionsMonitor<AppConfig> config)
{
    var current = config.CurrentValue; // Always up-to-date
    config.OnChange(newConfig => { /* react to changes */ });
}

// ? Per-request snapshot (scoped services)
public SyncController(IOptionsSnapshot<AppConfig> config)
{
    var current = config.Value; // Updated per request
}

// ? Startup only (stores, infrastructure)
public MetadataStoreFactory(IOptions<AppConfig> config)
{
    var current = config.Value; // Fixed at startup
}
```

### Health Checks Integration

```csharp
// Orchestrators can check health before operations
public async Task<SyncOperationResult> ExecuteSyncAsync(UnifiedSyncRequest request)
{
    // Check health before starting sync
    var health = await this.healthCheckService.CheckHealthAsync(
        predicate: check => check.Tags.Contains("critical"));

    if (health.Status != HealthStatus.Healthy)
    {
        return new SyncOperationResult
        {
            Success = false,
            Error = new ErrorDetails
            {
                Code = "SystemUnhealthy",
                Message = "Cannot start sync: System is not healthy",
                Details = health.Entries
                    .Where(e => e.Value.Status != HealthStatus.Healthy)
                    .Select(e => $"{e.Key}: {e.Value.Description}")
            }
        };
    }

    // Proceed with sync...
}
```

## ?? CLI Tool Integration

### Before (Duplicated Logic)
```csharp
// update-cli/src/Commands/SyncCommand.cs (OLD)

public async Task<int> ExecuteAsync()
{
    // Duplicate sync logic here
    var upstreamClient = new UpstreamServerClient(endpoint);
    var categoriesSource = new UpstreamCategoriesSource(upstreamClient);
    await categoriesSource.CopyTo(store);
    // ... 50+ lines of duplicated code
}
```

### After (Uses Orchestrator)
```csharp
// update-cli/src/Commands/SyncCommand.cs (NEW)

public async Task<int> ExecuteAsync()
{
    // Use SAME orchestrator as Azure Functions & Worker Service!
    var services = new ServiceCollection();
    services.AddUpdateEngineCore(configuration);
    var serviceProvider = services.BuildServiceProvider();

    var orchestrator = serviceProvider.GetRequiredService<ISyncOrchestrator>();

    var request = new UnifiedSyncRequest
    {
        SyncType = SyncType.Categories,
        Action = SyncAction.Start
    };

    var result = await orchestrator.ExecuteSyncAsync(request);
    
    return result.Success ? 0 : 1;
}
```

**Result**: CLI tool behavior is **identical** to Azure Functions and Worker Service!

## ?? Testing Strategy

### Test Orchestrators Once, Works Everywhere

```csharp
// UpdateEngine/test/Unit/OrchestratorTests/SyncOrchestratorTests.cs

public class SyncOrchestratorTests
{
    [Fact]
    public async Task ExecuteSyncAsync_WithCategories_ShouldSucceed()
    {
        // Arrange
        var orchestrator = new SyncOrchestrator(mockService, mockLogger, mockConfig);
        var request = new UnifiedSyncRequest
        {
            SyncType = SyncType.Categories,
            Action = SyncAction.Start
        };

        // Act
        var result = await orchestrator.ExecuteSyncAsync(request);

        // Assert
        Assert.True(result.Success);
    }
}
```

**This test validates:**
- ? Azure Functions behavior
- ? Worker Service behavior
- ? CLI tool behavior

**Because they all use the same orchestrator!**

## ?? Consolidation Summary

### Function Consolidation (35+ ? ~20)

| Domain | Before | After | Consolidation |
|--------|--------|-------|---------------|
| **Sync** | 11 functions | 3 functions | 73% reduction |
| **Metadata** | 8 functions | 3 functions | 63% reduction |
| **Health** | 8 functions | 3 functions | 63% reduction |
| **Content** | 6 functions | 4 functions | 33% reduction |
| **SOAP** | 5 functions | 5 functions | No change (required) |
| **Intelligence** | 2 functions | 2 functions | No change (optimal) |
| **Total** | **35+ functions** | **~20 functions** | **43% reduction** |

### Code Reuse (All Projects)

| Component | Lines of Code | Shared % |
|-----------|---------------|----------|
| **Orchestrators** | ~2,000 | 100% ? |
| **Models** | ~500 | 100% ? |
| **Configuration** | ~300 | 100% ? |
| **Domain Services** | ~3,000 | 100% ? |
| **Hosting Adapters** | ~1,000 | 0% (by design) |
| **Total** | **~6,800** | **~90%** ? |

## ?? Development Workflow

### Daily Development
```bash
# 1. Start everything with AppHost
cd AppHost/src
dotnet run

# AppHost Dashboard opens showing:
#   - Azure Functions: http://localhost:7071
#   - Worker Service: http://localhost:8080
#   - Azurite: http://localhost:10000
#   - Aspire Dashboard: http://localhost:15888
```

### Testing a Change
```bash
# 1. Edit SyncOrchestrator.cs
# 2. Restart AppHost (Ctrl+C, dotnet run)
# 3. Test Azure Functions:
curl http://localhost:7071/api/sync/status

# 4. Test Worker Service:
curl http://localhost:8080/api/sync/status

# 5. Test CLI:
cd update-cli/src
dotnet run -- sync --status

# All use the SAME orchestrator! ?
```

### Switching Hosting Models
```bash
# Deploy to Azure Functions
func azure functionapp publish my-functions-app

# OR deploy as Worker Service (Docker)
docker build -t update-engine-worker .
docker run -p 8080:8080 update-engine-worker

# OR deploy as Worker Service (Azure Container Apps)
az containerapp create --name update-engine --image my-image

# Same code works in all environments! ?
```

## ?? Implementation Roadmap

### Week 1: Core Infrastructure + Unit Tests
- [x] ? Create `SyncOrchestrator.cs`
- [x] ? Create `SyncModels.cs`
- [ ] ? **Update Configuration project** to use Options pattern POCOs
- [ ] ? Create `ServiceCollectionExtensions.cs` with IOptionsMonitor registration
- [ ] ? **Create health check implementations** (MetadataStoreHealthCheck, etc.)
- [ ] ? **Write `SyncOrchestratorTests.cs`** (Unit tests)
- [ ] ? Update Azure Functions to use `ISyncOrchestrator` and `IOptionsMonitor<AppConfig>`
- [ ] ? **Write `UnifiedSyncIntegrationTest.cs`** (Integration tests)
- [ ] ? Test with AppHost

### Week 2: Additional Orchestrators + Tests
- [ ] ? Create `MetadataOrchestrator.cs` + `MetadataOrchestratorTests.cs`
- [ ] ? Create `HealthOrchestrator.cs` + `HealthOrchestratorTests.cs`
- [ ] ? Create `ContentOrchestrator.cs` + `ContentOrchestratorTests.cs`
- [ ] ? **Integrate health checks into orchestrators** (pre-operation validation)
- [ ] ? Update all Azure Functions to use orchestrators
- [ ] ? **Write integration tests for all orchestrators**
- [ ] ? **Test configuration hot-reload** functionality

### Week 3: Worker Service + Dual Hosting Tests
- [ ] ? Create `WorkerService.csproj`
- [ ] ? Create ASP.NET Core controllers (use IOptionsSnapshot)
- [ ] ? Create background workers (use IOptionsMonitor)
- [ ] ? **Expose ASP.NET Core health check endpoints** (/health, /health/live, /health/ready)
- [ ] ? **Create `WorkerServiceTestFixture.cs`**
- [ ] ? **Write `WorkerServiceHostingE2ETest.cs`**
- [ ] ? Update AppHost to include Worker Service
- [ ] ? Test both hosting models

### Week 4: CLI Integration + E2E Tests
- [ ] ? Update CLI to use orchestrators and IOptions<AppConfig>
- [ ] ? Remove duplicate logic from CLI
- [ ] ? **Add health check command to CLI**
- [ ] ? **Write `CLIToolE2ETest.cs`**
- [ ] ? **Write `FullSyncWorkflowTest.cs`** (complete E2E)
- [ ] ? Test CLI commands
- [ ] ? **Test configuration hot-reload in Worker Service**

### Week 5: Testing & Documentation
- [ ] ? Achieve 90%+ code coverage on orchestrators
- [ ] ? **Write unit tests for health checks**
- [ ] ? Run full test suite (Unit + Integration + E2E)
- [ ] ? Performance and load testing
- [ ] ? **Document configuration hot-reload patterns**
- [ ] ? **Document health check integration with Kubernetes/Docker**
- [ ] ? Update documentation with test results
- [ ] ? Create deployment guides

## ?? Key Benefits

### 1. **Code Reuse (90%+)**
- Same orchestrators across Azure Functions, Worker Service, and CLI
- Same configuration from Configuration project (IOptionsMonitor pattern)
- Same domain services from existing projects
- Same health checks across all hosting models

### 2. **Consistent Behavior**
- Same sync logic everywhere
- Same error handling
- Same logging
- Same configuration (hot-reload support)
- Same health checks (ASP.NET Core standard)

### 3. **Easy Local Development**
- AppHost starts everything
- Test multiple hosts simultaneously
- Shared Azurite instance
- Aspire dashboard for monitoring
- Configuration hot-reload for rapid iteration

### 4. **Flexible Deployment**
- Choose hosting model per environment
- Easy migration between models
- No code changes required
- Health checks work with Kubernetes, Docker, Azure

### 5. **Future-Proof**
- Easy to add new hosting models
- Orchestrators work with any .NET host
- No technology lock-in
- Industry-standard patterns (Options, Health Checks)

### 6. **Production-Ready**
- ? **Hot-reload configuration** - Change settings without restart
- ? **Health checks** - Kubernetes/Docker integration
- ? **Monitoring** - Prometheus, Grafana, Azure Monitor compatible
- ? **Feature flags** - Toggle features at runtime
- ? **Pre-operation validation** - Check health before critical operations

## ?? Architecture Decisions

### Configuration: IOptionsMonitor Pattern

**Decision:** Use .NET Options Pattern with `IOptionsMonitor<T>` for hot-reload support.

**Rationale:**
- ? **Industry standard** - Built into .NET, widely understood
- ? **Hot-reload** - Change configuration without restart
- ? **Thread-safe** - Immutable by design
- ? **Flexible** - `IOptions`, `IOptionsSnapshot`, `IOptionsMonitor` for different scenarios
- ? **Testable** - Easy to mock and inject test configurations

**When to use each:**
- **`IOptionsMonitor<T>`** - Singleton services that need hot-reload (orchestrators, workers)
- **`IOptionsSnapshot<T>`** - Scoped services updated per-request (controllers)
- **`IOptions<T>`** - Services that only need startup config (stores, infrastructure)

**Example:**
```csharp
// appsettings.json
{
  "UpdateEngine": {
    "SyncConfiguration": {
      "SyncIntervalMinutes": 60,
      "EnableScheduledSync": true
    },
    "FeatureFlags": {
      "EnableEmergencySync": true
    }
  }
}

// Registration
services.Configure<AppConfig>(configuration.GetSection("UpdateEngine"));

// Usage with hot-reload
public SyncOrchestrator(IOptionsMonitor<AppConfig> config)
{
    var interval = config.CurrentValue.SyncConfiguration.SyncIntervalMinutes;
    
    // React to configuration changes
    config.OnChange(newConfig => 
    {
        logger.LogInformation("Sync interval changed to {Minutes}", 
            newConfig.SyncConfiguration.SyncIntervalMinutes);
    });
}
```

### Health Checks: ASP.NET Core Health Checks

**Decision:** Use ASP.NET Core Health Checks (Microsoft.Extensions.Diagnostics.HealthChecks).

**Rationale:**
- ? **Industry standard** - Used by Microsoft, AWS, Azure
- ? **Kubernetes/Docker** - Native integration with liveness/readiness probes
- ? **Monitoring tools** - Works with Prometheus, Grafana, Azure Monitor
- ? **Extensible** - Easy to add custom health checks
- ? **Dependency injection** - Integrates with existing services
- ? **Pre-operation validation** - Check health before critical operations

**Structure:**
```
UpdateEngine/src/Core/
??? HealthChecks/                    # ? Reusable health check implementations
?   ??? MetadataStoreHealthCheck.cs  # Check metadata store accessibility
?   ??? ContentStoreHealthCheck.cs   # Check content store accessibility
?   ??? UpstreamConnectionHealthCheck.cs # Check upstream server connectivity
?   ??? AzureBlobStorageHealthCheck.cs # Check Azure Storage health
```

**Usage:**
```csharp
// 1. Register health checks
services.AddHealthChecks()
    .AddCheck<MetadataStoreHealthCheck>("metadata-store", 
        tags: new[] { "storage", "critical" });

// 2. Expose via HTTP endpoint (Azure Functions)
[Function("Health")]
public async Task<HttpResponseData> GetHealth(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
{
    var report = await healthCheckService.CheckHealthAsync();
    return CreateHealthResponse(report);
}

// 3. Expose via ASP.NET Core (Worker Service)
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("critical")
});

// 4. Use in orchestrators for pre-operation validation
public async Task<SyncOperationResult> ExecuteSyncAsync(request)
{
    var health = await healthCheckService.CheckHealthAsync(
        predicate: check => check.Tags.Contains("critical"));
    
    if (health.Status != HealthStatus.Healthy)
        return CreateErrorResult("System unhealthy");
    
    // Proceed with sync...
}
```

**Benefits:**
- ? Works across Azure Functions, Worker Service, and CLI
- ? Kubernetes liveness/readiness probes
- ? Docker HEALTHCHECK directive
- ? Azure Monitor health checks
- ? Custom monitoring dashboards (Grafana, Prometheus)

## ?? Documentation

- **[ARCHITECTURE_DECISIONS.md](./ARCHITECTURE_DECISIONS.md)** - Configuration & Health Check decisions ? **NEW**
- **[TESTING_STRATEGY.md](./TESTING_STRATEGY.md)** - Comprehensive testing guide (Unit, Integration, E2E)
- **[DUAL_HOSTING_SOLUTION_INTEGRATION.md](./DUAL_HOSTING_SOLUTION_INTEGRATION.md)** - Complete integration guide
- **[DUAL_HOSTING_CONSOLIDATION_GUIDE.md](./DUAL_HOSTING_CONSOLIDATION_GUIDE.md)** - Dual hosting patterns
- **[DUAL_HOSTING_QUICK_SUMMARY.md](./DUAL_HOSTING_QUICK_SUMMARY.md)** - Quick reference
- **[FUNCTION_CONSOLIDATION_GUIDE.md](./FUNCTION_CONSOLIDATION_GUIDE.md)** - Consolidation patterns
- **[CONSOLIDATION_ANSWER.md](./CONSOLIDATION_ANSWER.md)** - Quick answers
