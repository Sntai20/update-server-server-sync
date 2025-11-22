# Architecture Decisions: Configuration & Health Checks

## ?? Overview

This document explains the architectural decisions for **configuration management** and **health checks** in the dual hosting architecture (Azure Functions + Worker Service + CLI).

---

## ?? Table of Contents

1. [Configuration: IOptionsMonitor Pattern](#configuration-ioptionsmonitor-pattern)
2. [Health Checks: ASP.NET Core Standard](#health-checks-aspnet-core-standard)
3. [Implementation Examples](#implementation-examples)
4. [Migration Guide](#migration-guide)

---

## ?? Configuration: IOptionsMonitor Pattern

### Decision

**Use .NET Options Pattern with `IOptionsMonitor<T>` for hot-reload support.**

### Rationale

| Requirement | Solution | Benefit |
|------------|----------|---------|
| **Hot-reload configuration** | `IOptionsMonitor<T>` | Change settings without restart |
| **Thread-safe access** | Immutable POCOs | No race conditions |
| **Industry standard** | Built into .NET | Widely understood, well-documented |
| **Flexible scenarios** | `IOptions`, `IOptionsSnapshot`, `IOptionsMonitor` | Right tool for each use case |
| **Easy testing** | Mockable interfaces | Simple unit tests |

### Configuration Structure

```
Configuration/
??? Configuration.csproj
??? AppConfig.cs                 # Root configuration class
??? ServiceConfiguration.cs      # Service-level settings (URLs, timeouts)
??? SyncConfiguration.cs         # Sync operation settings (intervals, flags)
??? StorageConfiguration.cs      # Storage settings (paths, connection strings)
??? FeatureFlags.cs             # Runtime feature toggles
```

### Configuration Classes

```csharp
// Configuration/AppConfig.cs
public class AppConfig
{
    public const string SectionName = "UpdateEngine";
    
    public ServiceConfiguration ServiceConfiguration { get; set; } = new();
    public SyncConfiguration SyncConfiguration { get; set; } = new();
    public StorageConfiguration StorageConfiguration { get; set; } = new();
    public FeatureFlags FeatureFlags { get; set; } = new();
}

// Configuration/ServiceConfiguration.cs
public class ServiceConfiguration
{
    public string ServiceUrl { get; set; } = string.Empty;
    public string UpstreamServerUrl { get; set; } = "https://fe2.update.microsoft.com/v6/windowsupdate/Services/";
    public int MaxConcurrentSyncs { get; set; } = 5;
    public int RequestTimeoutSeconds { get; set; } = 300;
}

// Configuration/SyncConfiguration.cs
public class SyncConfiguration
{
    public int SyncIntervalMinutes { get; set; } = 60;
    public int EmergencySyncIntervalMinutes { get; set; } = 15;
    public int HealthCheckIntervalMinutes { get; set; } = 5;
    public bool EnableScheduledSync { get; set; } = true;
}

// Configuration/StorageConfiguration.cs
public class StorageConfiguration
{
    public string MetadataPath { get; set; } = "./metadata";
    public string ContentPath { get; set; } = "./content";
    public bool UseAzureStorageForMetadata { get; set; }
    public bool UseAzureStorageForContent { get; set; }
    public string AzureStorageConnectionString { get; set; } = string.Empty;
    public string AzureContainerName { get; set; } = "metadata";
    public string AzureContentContainer { get; set; } = "content";
}

// Configuration/FeatureFlags.cs
public class FeatureFlags
{
    public bool EnableEmergencySync { get; set; } = true;
    public bool EnableAnomalyDetection { get; set; }
    public bool EnableDeepHealthCheck { get; set; } = true;
    public bool EnableMaintenance { get; set; } = true;
}
```

### appsettings.json Structure

```json
{
  "UpdateEngine": {
    "ServiceConfiguration": {
      "ServiceUrl": "http://localhost:7071",
      "UpstreamServerUrl": "https://fe2.update.microsoft.com/v6/windowsupdate/Services/",
      "MaxConcurrentSyncs": 5,
      "RequestTimeoutSeconds": 300
    },
    "SyncConfiguration": {
      "SyncIntervalMinutes": 60,
      "EmergencySyncIntervalMinutes": 15,
      "HealthCheckIntervalMinutes": 5,
      "EnableScheduledSync": true
    },
    "StorageConfiguration": {
      "MetadataPath": "./metadata",
      "ContentPath": "./content",
      "UseAzureStorageForMetadata": false,
      "UseAzureStorageForContent": false
    },
    "FeatureFlags": {
      "EnableEmergencySync": true,
      "EnableAnomalyDetection": false,
      "EnableDeepHealthCheck": true,
      "EnableMaintenance": true
    }
  }
}
```

### Registration in DI

```csharp
// UpdateEngine/src/Core/ServiceCollectionExtensions.cs

public static IServiceCollection AddUpdateEngineCore(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // ? Register Options pattern with hot-reload
    services.Configure<AppConfig>(
        configuration.GetSection(AppConfig.SectionName));

    // Services can now inject:
    // - IOptions<AppConfig> for startup-only config
    // - IOptionsSnapshot<AppConfig> for per-request config
    // - IOptionsMonitor<AppConfig> for hot-reload config

    // ... other registrations
}
```

### Usage Patterns

#### Pattern 1: Hot-Reload (Singleton Services)

```csharp
// UpdateEngine/src/Core/Orchestrators/SyncOrchestrator.cs

public class SyncOrchestrator : ISyncOrchestrator
{
    private readonly ISyncService syncService;
    private readonly ILogger<SyncOrchestrator> logger;
    private readonly IOptionsMonitor<AppConfig> configMonitor;

    public SyncOrchestrator(
        ISyncService syncService,
        ILogger<SyncOrchestrator> logger,
        IOptionsMonitor<AppConfig> configMonitor)
    {
        this.syncService = syncService;
        this.logger = logger;
        this.configMonitor = configMonitor;

        // ? React to configuration changes
        this.configMonitor.OnChange(config =>
        {
            this.logger.LogInformation(
                "Configuration changed: SyncInterval={Interval} minutes",
                config.SyncConfiguration.SyncIntervalMinutes);
        });
    }

    public async Task<SyncOperationResult> ExecuteSyncAsync(UnifiedSyncRequest request)
    {
        // ? Get current configuration (always up-to-date)
        var config = this.configMonitor.CurrentValue;

        // Check feature flag
        if (!config.FeatureFlags.EnableEmergencySync && 
            request.SyncType == SyncType.Emergency)
        {
            return new SyncOperationResult
            {
                Success = false,
                Error = new ErrorDetails
                {
                    Code = "FeatureDisabled",
                    Message = "Emergency sync is currently disabled"
                }
            };
        }

        // Use configuration
        var maxConcurrent = config.ServiceConfiguration.MaxConcurrentSyncs;
        
        // Execute sync with current config...
        return await this.ExecuteSyncInternalAsync(request, config);
    }
}
```

#### Pattern 2: Per-Request Snapshot (Scoped Services)

```csharp
// UpdateEngine/src/WorkerService/Controllers/SyncController.cs

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly ISyncOrchestrator orchestrator;
    private readonly IOptionsSnapshot<AppConfig> configSnapshot;
    private readonly ILogger<SyncController> logger;

    public SyncController(
        ISyncOrchestrator orchestrator,
        IOptionsSnapshot<AppConfig> configSnapshot,
        ILogger<SyncController> logger)
    {
        this.orchestrator = orchestrator;
        this.configSnapshot = configSnapshot;
        this.logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<SyncOperationResult>> StartSync(
        [FromBody] UnifiedSyncRequest request)
    {
        // ? Get config snapshot for this request
        var config = this.configSnapshot.Value;

        this.logger.LogInformation(
            "Starting sync with interval: {Interval} minutes",
            config.SyncConfiguration.SyncIntervalMinutes);

        var result = await this.orchestrator.ExecuteSyncAsync(request);
        
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
```

#### Pattern 3: Startup Only (Infrastructure Services)

```csharp
// UpdateEngine/src/Core/ServiceCollectionExtensions.cs

services.AddSingleton<IMetadataStore>(provider =>
{
    // ? Use IOptions for startup-only config
    // Stores don't hot-reload - they're initialized once
    var config = provider.GetRequiredService<IOptions<AppConfig>>().Value;
    
    return config.StorageConfiguration.UseAzureStorageForMetadata
        ? Azure.PackageStore.Open(
            config.StorageConfiguration.AzureStorageConnectionString,
            config.StorageConfiguration.AzureContainerName)
        : PackageStore.Open(config.StorageConfiguration.MetadataPath);
});
```

### Configuration Hot-Reload Testing

```csharp
// UpdateEngine/test/Integration/ConfigurationHotReloadTest.cs

[Collection("InMemory")]
public class ConfigurationHotReloadTest
{
    [Fact]
    public async Task Configuration_WhenChanged_ShouldHotReload()
    {
        // Arrange
        var configBuilder = new ConfigurationBuilder();
        var configSource = new MemoryConfigurationSource
        {
            InitialData = new Dictionary<string, string>
            {
                ["UpdateEngine:SyncConfiguration:SyncIntervalMinutes"] = "60"
            }
        };
        configBuilder.Add(configSource);

        var services = new ServiceCollection();
        services.Configure<AppConfig>(configBuilder.Build().GetSection("UpdateEngine"));
        services.AddSingleton<ISyncOrchestrator, SyncOrchestrator>();
        
        var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<AppConfig>>();

        // Act - Change configuration
        configSource.InitialData["UpdateEngine:SyncConfiguration:SyncIntervalMinutes"] = "30";
        configBuilder.Build().Reload();

        // Assert
        var newConfig = monitor.CurrentValue;
        Assert.Equal(30, newConfig.SyncConfiguration.SyncIntervalMinutes);
    }
}
```

### When to Use Each Options Interface

| Interface | Lifetime | Hot-Reload | Use Case | Example |
|-----------|----------|------------|----------|---------|
| **`IOptions<T>`** | Singleton | ? No | Startup-only config | Store initialization, infrastructure setup |
| **`IOptionsSnapshot<T>`** | Scoped | ? Per request | Request-scoped services | Controllers, HTTP handlers |
| **`IOptionsMonitor<T>`** | Singleton | ? Always | Singleton services | Orchestrators, background workers, hosted services |

---

## ?? Health Checks: ASP.NET Core Standard

### Decision

**Use ASP.NET Core Health Checks (Microsoft.Extensions.Diagnostics.HealthChecks).**

### Rationale

| Requirement | Solution | Benefit |
|------------|----------|---------|
| **Industry standard** | ASP.NET Core Health Checks | Used by Microsoft, AWS, Azure |
| **Kubernetes integration** | Built-in support | Liveness/readiness probes |
| **Monitoring tools** | Standard JSON format | Prometheus, Grafana, Azure Monitor |
| **Extensible** | `IHealthCheck` interface | Easy custom checks |
| **Dependency injection** | Full DI support | Integrates with existing services |
| **Pre-operation validation** | `HealthCheckService` | Check before critical operations |

### Health Check Structure

```
UpdateEngine/src/Core/HealthChecks/
??? MetadataStoreHealthCheck.cs      # Check metadata store accessibility
??? ContentStoreHealthCheck.cs       # Check content store accessibility
??? UpstreamConnectionHealthCheck.cs # Check upstream server connectivity
??? AzureBlobStorageHealthCheck.cs   # Check Azure Storage health
```

### Health Check Implementation

```csharp
// UpdateEngine/src/Core/HealthChecks/MetadataStoreHealthCheck.cs

using Microsoft.Extensions.Diagnostics.HealthChecks;

public class MetadataStoreHealthCheck : IHealthCheck
{
    private readonly IMetadataStore store;
    private readonly ILogger<MetadataStoreHealthCheck> logger;

    public MetadataStoreHealthCheck(
        IMetadataStore store,
        ILogger<MetadataStoreHealthCheck> logger)
    {
        this.store = store;
        this.logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Perform health check
            var packageCount = this.store.Cast<IPackage>().Count();
            var isReindexingRequired = this.store.IsReindexingRequired;

            var data = new Dictionary<string, object>
            {
                ["packageCount"] = packageCount,
                ["reindexingRequired"] = isReindexingRequired,
                ["timestamp"] = DateTime.UtcNow
            };

            // Determine health status
            if (isReindexingRequired)
            {
                return HealthCheckResult.Degraded(
                    "Metadata store requires reindexing",
                    data: data);
            }

            if (packageCount == 0)
            {
                return HealthCheckResult.Degraded(
                    "Metadata store is empty",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                "Metadata store is healthy",
                data: data);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Metadata store health check failed");
            return HealthCheckResult.Unhealthy(
                "Metadata store is unavailable",
                exception: ex);
        }
    }
}
```

```csharp
// UpdateEngine/src/Core/HealthChecks/UpstreamConnectionHealthCheck.cs

public class UpstreamConnectionHealthCheck : IHealthCheck
{
    private readonly IOptionsMonitor<AppConfig> config;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<UpstreamConnectionHealthCheck> logger;

    public UpstreamConnectionHealthCheck(
        IOptionsMonitor<AppConfig> config,
        IHttpClientFactory httpClientFactory,
        ILogger<UpstreamConnectionHealthCheck> logger)
    {
        this.config = config;
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var upstreamUrl = this.config.CurrentValue
                .ServiceConfiguration.UpstreamServerUrl;

            var client = this.httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var response = await client.GetAsync(upstreamUrl, cancellationToken);

            var data = new Dictionary<string, object>
            {
                ["upstreamUrl"] = upstreamUrl,
                ["statusCode"] = (int)response.StatusCode,
                ["responseTime"] = DateTime.UtcNow
            };

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy(
                    "Upstream server is reachable",
                    data: data);
            }

            return HealthCheckResult.Degraded(
                $"Upstream server returned {response.StatusCode}",
                data: data);
        }
        catch (HttpRequestException ex)
        {
            this.logger.LogWarning(ex, "Upstream server is unreachable");
            return HealthCheckResult.Unhealthy(
                "Upstream server is unreachable",
                exception: ex);
        }
    }
}
```

### Registration in DI

```csharp
// UpdateEngine/src/Core/ServiceCollectionExtensions.cs

public static IServiceCollection AddUpdateEngineCore(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // ... other registrations ...

    // ? Register ASP.NET Core Health Checks
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
```

### Usage in Azure Functions

```csharp
// UpdateEngine/src/Functions/Management/HealthFunction.cs

public class HealthFunction
{
    private readonly HealthCheckService healthCheckService;
    private readonly ILogger<HealthFunction> logger;

    public HealthFunction(
        HealthCheckService healthCheckService,
        ILogger<HealthFunction> logger)
    {
        this.healthCheckService = healthCheckService;
        this.logger = logger;
    }

    [Function("Health")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")]
        HttpRequestData req)
    {
        var scope = req.Query["scope"] ?? "basic";

        // Filter checks based on scope
        Func<HealthCheckRegistration, bool>? predicate = scope.ToLowerInvariant() switch
        {
            "basic" => check => check.Tags.Contains("critical"),
            "storage" => check => check.Tags.Contains("storage"),
            "network" => check => check.Tags.Contains("network"),
            "comprehensive" => null, // All checks
            _ => check => check.Tags.Contains("critical")
        };

        var report = await this.healthCheckService.CheckHealthAsync(predicate);

        this.logger.LogInformation(
            "Health check completed: Status={Status}, Duration={Duration}ms",
            report.Status,
            report.TotalDuration.TotalMilliseconds);

        var statusCode = report.Status switch
        {
            HealthStatus.Healthy => HttpStatusCode.OK,
            HealthStatus.Degraded => HttpStatusCode.OK,
            HealthStatus.Unhealthy => HttpStatusCode.ServiceUnavailable,
            _ => HttpStatusCode.InternalServerError
        };

        var response = req.CreateResponse(statusCode);

        await response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            entries = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds,
                data = e.Value.Data
            })
        });

        return response;
    }
}
```

### Usage in Worker Service

```csharp
// UpdateEngine/src/WorkerService/Program.cs

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddUpdateEngineCore(builder.Configuration);
builder.Services.AddControllers();

var app = builder.Build();

// ? ASP.NET Core health check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds,
                data = e.Value.Data
            })
        });
    }
});

// ? Kubernetes liveness probe
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("critical")
});

// ? Kubernetes readiness probe
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = _ => true
});

app.Run();
```

### Pre-Operation Validation in Orchestrators

```csharp
// UpdateEngine/src/Core/Orchestrators/SyncOrchestrator.cs

public class SyncOrchestrator : ISyncOrchestrator
{
    private readonly ISyncService syncService;
    private readonly HealthCheckService healthCheckService;
    private readonly ILogger<SyncOrchestrator> logger;

    public SyncOrchestrator(
        ISyncService syncService,
        HealthCheckService healthCheckService,
        ILogger<SyncOrchestrator> logger)
    {
        this.syncService = syncService;
        this.healthCheckService = healthCheckService;
        this.logger = logger;
    }

    public async Task<SyncOperationResult> ExecuteSyncAsync(UnifiedSyncRequest request)
    {
        // ? Check health before starting sync
        var healthReport = await this.healthCheckService.CheckHealthAsync(
            predicate: check => check.Tags.Contains("critical"));

        if (healthReport.Status == HealthStatus.Unhealthy)
        {
            this.logger.LogError(
                "Cannot start sync: System is unhealthy - {UnhealthyComponents}",
                string.Join(", ", healthReport.Entries
                    .Where(e => e.Value.Status == HealthStatus.Unhealthy)
                    .Select(e => e.Key)));

            return new SyncOperationResult
            {
                Success = false,
                Error = new ErrorDetails
                {
                    Code = "SystemUnhealthy",
                    Message = "Cannot start sync: System is not healthy",
                    Details = healthReport.Entries
                        .Where(e => e.Value.Status == HealthStatus.Unhealthy)
                        .Select(e => $"{e.Key}: {e.Value.Description}")
                        .ToList()
                }
            };
        }

        if (healthReport.Status == HealthStatus.Degraded)
        {
            this.logger.LogWarning(
                "Starting sync with degraded health: {DegradedComponents}",
                string.Join(", ", healthReport.Entries
                    .Where(e => e.Value.Status == HealthStatus.Degraded)
                    .Select(e => e.Key)));
        }

        // Proceed with sync...
        return await this.ExecuteSyncInternalAsync(request);
    }
}
```

### Kubernetes Integration

```yaml
# deployment.yaml

apiVersion: apps/v1
kind: Deployment
metadata:
  name: update-engine
spec:
  template:
    spec:
      containers:
      - name: worker-service
        image: update-engine:latest
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

### Docker Integration

```dockerfile
# Dockerfile

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

# ? Docker health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "UpdateEngine.WorkerService.dll"]
```

---

## ?? Benefits Summary

### Configuration Benefits

? **Hot-reload** - Change configuration without restart  
? **Thread-safe** - Immutable POCOs prevent race conditions  
? **Flexible** - Right interface for each scenario  
? **Testable** - Easy to mock and inject  
? **Standard** - Built into .NET, widely understood  

### Health Check Benefits

? **Industry standard** - ASP.NET Core Health Checks  
? **Kubernetes/Docker** - Native integration  
? **Monitoring** - Works with Prometheus, Grafana, Azure Monitor  
? **Pre-operation validation** - Check before critical operations  
? **Extensible** - Easy to add custom checks  

---

## ?? Resources

- **[.NET Options Pattern](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options)**
- **[ASP.NET Core Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)**
- **[Kubernetes Probes](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)**
- **[Docker HEALTHCHECK](https://docs.docker.com/engine/reference/builder/#healthcheck)**

---

**Last Updated**: 2025-01-XX  
**Status**: Approved  
**Next Steps**: Implement in Week 1 of roadmap
