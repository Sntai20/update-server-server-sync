# Dual Hosting Model: Azure Functions + Worker Service

## ?? Architecture Overview

This guide shows how to consolidate functions while supporting **both Azure Functions and Worker Service** hosting models.

## Key Principle: Host-Agnostic Core

```
???????????????????????????????????????????????????????????
?                   Hosting Layer                          ?
?  ????????????????????      ????????????????????        ?
?  ? Azure Functions  ?      ?  Worker Service  ?        ?
?  ?   (Thin Layer)   ?      ?   (Thin Layer)   ?        ?
?  ????????????????????      ????????????????????        ?
?           ?                         ?                   ?
?           ???????????????????????????                   ?
?                    ?                                    ?
???????????????????????????????????????????????????????????
?              Core Business Logic                        ?
?  ??????????????????????????????????????                ?
?  ?      Orchestrators (Host-Agnostic)  ?                ?
?  ?  - ISyncOrchestrator                ?                ?
?  ?  - IMetadataOrchestrator            ?                ?
?  ?  - IHealthOrchestrator              ?                ?
?  ?  - IContentOrchestrator             ?                ?
?  ??????????????????????????????????????                ?
?                    ?                                    ?
?  ??????????????????????????????????????                ?
?  ?      Domain Services                ?                ?
?  ?  - ISyncService                     ?                ?
?  ?  - IMetadataStore                   ?                ?
?  ?  - IContentStore                    ?                ?
?  ???????????????????????????????????????                ?
???????????????????????????????????????????????????????????
```

## ?? Project Structure

```
UpdateEngine/
??? src/
?   ??? Core/                           # Host-agnostic business logic
?   ?   ??? Orchestrators/
?   ?   ?   ??? ISyncOrchestrator.cs
?   ?   ?   ??? SyncOrchestrator.cs
?   ?   ?   ??? IMetadataOrchestrator.cs
?   ?   ?   ??? MetadataOrchestrator.cs
?   ?   ?   ??? IHealthOrchestrator.cs
?   ?   ?   ??? HealthOrchestrator.cs
?   ?   ?   ??? IContentOrchestrator.cs
?   ?   ??? Models/
?   ?   ?   ??? SyncModels.cs           # Shared request/response models
?   ?   ?   ??? MetadataModels.cs
?   ?   ?   ??? HealthModels.cs
?   ?   ?   ??? ContentModels.cs
?   ?   ??? Services/                   # Existing services
?   ?
?   ??? Functions/                      # Azure Functions hosting layer
?   ?   ??? Core/
?   ?   ?   ??? UnifiedSyncFunction.cs
?   ?   ?   ??? UnifiedMetadataFunction.cs
?   ?   ?   ??? UnifiedContentFunction.cs
?   ?   ??? Management/
?   ?       ??? UnifiedHealthFunction.cs
?   ?
?   ??? WorkerService/                  # Worker Service hosting layer
?       ??? Program.cs
?       ??? Workers/
?       ?   ??? SyncWorker.cs
?       ?   ??? HealthCheckWorker.cs
?       ?   ??? AnomalyDetectionWorker.cs
?       ??? Controllers/                # ASP.NET Core controllers
?           ??? SyncController.cs
?           ??? MetadataController.cs
?           ??? ContentController.cs
?           ??? HealthController.cs
?
??? test/
    ??? Unit/
    ?   ??? OrchestratorTests/          # Test orchestrators directly
    ?   ?   ??? SyncOrchestratorTests.cs
    ?   ?   ??? MetadataOrchestratorTests.cs
    ?   ??? ServiceTests/
    ??? Integration/
        ??? FunctionTests/              # Azure Functions integration tests
        ??? WorkerServiceTests/         # Worker Service integration tests
```

## ??? Implementation Pattern

### Step 1: Create Host-Agnostic Orchestrators

These orchestrators contain all business logic and have NO dependencies on Azure Functions or ASP.NET Core:

```csharp
// UpdateEngine.Functions/src/Core/Orchestrators/ISyncOrchestrator.cs

namespace UpdateEngine.Core.Orchestrators;

/// <summary>
/// Host-agnostic sync orchestrator. Can be used from Azure Functions, Worker Service, or any other host.
/// </summary>
public interface ISyncOrchestrator
{
    /// <summary>
    /// Execute a sync operation based on request parameters
    /// </summary>
    Task<SyncOperationResult> ExecuteSyncAsync(
        UnifiedSyncRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current sync status
    /// </summary>
    Task<SyncStatusResult> GetStatusAsync(
        CancellationToken cancellationToken = default);
}

// UpdateEngine.Functions/src/Core/Orchestrators/SyncOrchestrator.cs

public class SyncOrchestrator : ISyncOrchestrator
{
    private readonly ISyncService syncService;
    private readonly IMetadataStore metadataStore;
    private readonly ILogger<SyncOrchestrator> logger;

    public SyncOrchestrator(
        ISyncService syncService,
        IMetadataStore metadataStore,
        ILogger<SyncOrchestrator> logger)
    {
        this.syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        this.metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SyncOperationResult> ExecuteSyncAsync(
        UnifiedSyncRequest request,
        CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation(
            "Executing sync: Type={SyncType}, Action={Action}",
            request.SyncType,
            request.Action);

        try
        {
            // Route based on action (no HTTP/Azure Functions dependencies!)
            return request.Action switch
            {
                SyncAction.Start => await this.HandleStartAsync(request, cancellationToken),
                SyncAction.Pause => await this.HandlePauseAsync(cancellationToken),
                SyncAction.Resume => await this.HandleResumeAsync(cancellationToken),
                SyncAction.Cancel => await this.HandleCancelAsync(cancellationToken),
                _ => throw new ArgumentException($"Unknown action: {request.Action}")
            };
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Sync execution failed");
            return new SyncOperationResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    private async Task<SyncOperationResult> HandleStartAsync(
        UnifiedSyncRequest request,
        CancellationToken cancellationToken)
    {
        // Validate request
        if (request.SyncType == null)
        {
            return new SyncOperationResult
            {
                Success = false,
                ErrorMessage = "SyncType is required for start action",
                Timestamp = DateTime.UtcNow
            };
        }

        // Execute based on sync type
        return request.SyncType switch
        {
            SyncType.Categories => await this.SyncCategoriesAsync(cancellationToken),
            SyncType.Updates => await this.SyncUpdatesAsync(request.Filter, cancellationToken),
            SyncType.Comprehensive => await this.SyncComprehensiveAsync(request.Filter, cancellationToken),
            _ => throw new ArgumentException($"Unknown sync type: {request.SyncType}")
        };
    }

    private async Task<SyncOperationResult> SyncCategoriesAsync(CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        
        await this.syncService.SyncCategoriesAsync(cancellationToken);

        return new SyncOperationResult
        {
            Success = true,
            Message = "Categories synchronized successfully",
            Timestamp = DateTime.UtcNow,
            Duration = DateTime.UtcNow - startTime,
            ItemsSynced = await this.metadataStore.GetCategoriesCount(cancellationToken)
        };
    }

    // ... more implementation methods
}
```

### Step 2: Create Shared Models (Host-Agnostic)

```csharp
// UpdateEngine.Functions/src/Core/Models/SyncModels.cs

namespace UpdateEngine.Core.Models;

/// <summary>
/// Unified sync request - works with any hosting model
/// </summary>
public class UnifiedSyncRequest
{
    /// <summary>Type of sync to perform</summary>
    public SyncType? SyncType { get; set; }

    /// <summary>Action to perform</summary>
    public SyncAction Action { get; set; } = SyncAction.Start;

    /// <summary>Filter for update sync</summary>
    public SyncFilter? Filter { get; set; }
}

public enum SyncType
{
    Categories,
    Updates,
    Comprehensive
}

public enum SyncAction
{
    Start,
    Pause,
    Resume,
    Cancel
}

public class SyncFilter
{
    public List<string>? ProductTitles { get; set; }
    public List<Guid>? ClassificationIds { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

/// <summary>
/// Sync operation result - host-agnostic
/// </summary>
public class SyncOperationResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; }
    public TimeSpan? Duration { get; set; }
    public int? ItemsSynced { get; set; }
    public Dictionary<string, object>? Statistics { get; set; }
}

public class SyncStatusResult
{
    public bool IsRunning { get; set; }
    public SyncType? CurrentSyncType { get; set; }
    public DateTime? StartTime { get; set; }
    public int? Progress { get; set; }
    public string? CurrentPhase { get; set; }
}
```

### Step 3a: Azure Functions Hosting Layer (Thin Adapter)

```csharp
// UpdateEngine.Functions/src/Functions/Core/UnifiedSyncFunction.cs

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using UpdateEngine.Core.Orchestrators;
using UpdateEngine.Core.Models;

namespace UpdateEngine.Functions.Core;

/// <summary>
/// Azure Functions adapter for sync operations.
/// All business logic is in SyncOrchestrator - this is just a thin hosting layer.
/// </summary>
public class UnifiedSyncFunction
{
    private readonly ISyncOrchestrator syncOrchestrator;
    private readonly ILogger<UnifiedSyncFunction> logger;
    private readonly JsonSerializerOptions jsonOptions;

    public UnifiedSyncFunction(
        ISyncOrchestrator syncOrchestrator,
        ILogger<UnifiedSyncFunction> logger,
        JsonSerializerOptions jsonOptions)
    {
        this.syncOrchestrator = syncOrchestrator ?? throw new ArgumentNullException(nameof(syncOrchestrator));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
    }

    /// <summary>
    /// HTTP-triggered sync endpoint
    /// </summary>
    [Function("Sync")]
    public async Task<HttpResponseData> Sync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sync")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            // Parse request (Azure Functions specific)
            var request = await JsonSerializer.DeserializeAsync<UnifiedSyncRequest>(
                req.Body,
                this.jsonOptions,
                cancellationToken);

            if (request == null)
            {
                return await CreateErrorResponseAsync(req, "Invalid request body", HttpStatusCode.BadRequest);
            }

            // Execute via orchestrator (host-agnostic)
            var result = await this.syncOrchestrator.ExecuteSyncAsync(request, cancellationToken);

            // Return response (Azure Functions specific)
            var response = req.CreateResponse(
                result.Success ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
            await response.WriteAsJsonAsync(result, this.jsonOptions, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Sync function failed");
            return await CreateErrorResponseAsync(req, ex.Message, HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Get sync status
    /// </summary>
    [Function("SyncStatus")]
    public async Task<HttpResponseData> GetStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sync/status")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get status via orchestrator (host-agnostic)
            var status = await this.syncOrchestrator.GetStatusAsync(cancellationToken);

            // Return response (Azure Functions specific)
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(status, this.jsonOptions, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Get status failed");
            return await CreateErrorResponseAsync(req, ex.Message, HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Timer-triggered background sync
    /// </summary>
    [Function("BackgroundSync")]
    public async Task BackgroundSync(
        [TimerTrigger("0 0 2 * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Background sync triggered at {Time}", DateTime.UtcNow);

        try
        {
            // Create request for comprehensive sync
            var request = new UnifiedSyncRequest
            {
                SyncType = SyncType.Comprehensive,
                Action = SyncAction.Start,
                Filter = null // Use default filter from configuration
            };

            // Execute via orchestrator (host-agnostic)
            var result = await this.syncOrchestrator.ExecuteSyncAsync(request, cancellationToken);

            if (result.Success)
            {
                this.logger.LogInformation(
                    "Background sync completed: {ItemsSynced} items synced in {Duration}",
                    result.ItemsSynced,
                    result.Duration);
            }
            else
            {
                this.logger.LogError("Background sync failed: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Background sync failed with exception");
        }
    }

    private async Task<HttpResponseData> CreateErrorResponseAsync(
        HttpRequestData req,
        string message,
        HttpStatusCode statusCode)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(new
        {
            Error = message,
            Timestamp = DateTime.UtcNow
        }, this.jsonOptions);
        return response;
    }
}
```

### Step 3b: Worker Service Hosting Layer (Thin Adapter)

```csharp
// UpdateEngine.Functions/src/WorkerService/Controllers/SyncController.cs

using Microsoft.AspNetCore.Mvc;
using UpdateEngine.Core.Orchestrators;
using UpdateEngine.Core.Models;

namespace UpdateEngine.WorkerService.Controllers;

/// <summary>
/// ASP.NET Core controller adapter for sync operations.
/// All business logic is in SyncOrchestrator - this is just a thin hosting layer.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly ISyncOrchestrator syncOrchestrator;
    private readonly ILogger<SyncController> logger;

    public SyncController(
        ISyncOrchestrator syncOrchestrator,
        ILogger<SyncController> logger)
    {
        this.syncOrchestrator = syncOrchestrator ?? throw new ArgumentNullException(nameof(syncOrchestrator));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Execute sync operation
    /// POST /api/sync
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SyncOperationResult>> Sync(
        [FromBody] UnifiedSyncRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new { Error = "Invalid request body" });
            }

            // Execute via orchestrator (host-agnostic)
            var result = await this.syncOrchestrator.ExecuteSyncAsync(request, cancellationToken);

            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Sync operation failed");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Get sync status
    /// GET /api/sync/status
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<SyncStatusResult>> GetStatus(CancellationToken cancellationToken)
    {
        try
        {
            // Get status via orchestrator (host-agnostic)
            var status = await this.syncOrchestrator.GetStatusAsync(cancellationToken);
            return Ok(status);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Get status failed");
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}

// UpdateEngine.Functions/src/WorkerService/Workers/SyncWorker.cs

using UpdateEngine.Core.Orchestrators;
using UpdateEngine.Core.Models;

namespace UpdateEngine.WorkerService.Workers;

/// <summary>
/// Background worker for scheduled sync operations.
/// Equivalent to Azure Functions timer trigger.
/// </summary>
public class SyncWorker : BackgroundService
{
    private readonly ISyncOrchestrator syncOrchestrator;
    private readonly IConfiguration configuration;
    private readonly ILogger<SyncWorker> logger;
    private readonly TimeSpan syncInterval;

    public SyncWorker(
        ISyncOrchestrator syncOrchestrator,
        IConfiguration configuration,
        ILogger<SyncWorker> logger)
    {
        this.syncOrchestrator = syncOrchestrator ?? throw new ArgumentNullException(nameof(syncOrchestrator));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Read sync interval from configuration (default: 2 hours)
        this.syncInterval = configuration.GetValue<TimeSpan?>("BackgroundSync:Interval") 
            ?? TimeSpan.FromHours(2);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.logger.LogInformation(
            "Sync worker starting. Interval: {Interval}",
            this.syncInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                this.logger.LogInformation("Background sync triggered at {Time}", DateTime.UtcNow);

                // Create request for comprehensive sync
                var request = new UnifiedSyncRequest
                {
                    SyncType = SyncType.Comprehensive,
                    Action = SyncAction.Start,
                    Filter = null // Use default filter
                };

                // Execute via orchestrator (host-agnostic)
                var result = await this.syncOrchestrator.ExecuteSyncAsync(request, stoppingToken);

                if (result.Success)
                {
                    this.logger.LogInformation(
                        "Background sync completed: {ItemsSynced} items in {Duration}",
                        result.ItemsSynced,
                        result.Duration);
                }
                else
                {
                    this.logger.LogError("Background sync failed: {Error}", result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Background sync failed with exception");
            }

            // Wait for next sync interval
            await Task.Delay(this.syncInterval, stoppingToken);
        }

        this.logger.LogInformation("Sync worker stopping");
    }
}
```

### Step 4: Worker Service Program.cs

```csharp
// UpdateEngine.Functions/src/WorkerService/Program.cs

using UpdateEngine.Core.Orchestrators;
using UpdateEngine.WorkerService.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register host-agnostic orchestrators
builder.Services.AddSingleton<ISyncOrchestrator, SyncOrchestrator>();
builder.Services.AddSingleton<IMetadataOrchestrator, MetadataOrchestrator>();
builder.Services.AddSingleton<IHealthOrchestrator, HealthOrchestrator>();
builder.Services.AddSingleton<IContentOrchestrator, ContentOrchestrator>();

// Register domain services (same as Azure Functions)
builder.Services.AddMicrosoftUpdateServices(builder.Configuration);

// Register background workers
builder.Services.AddHostedService<SyncWorker>();
builder.Services.AddHostedService<HealthCheckWorker>();
builder.Services.AddHostedService<AnomalyDetectionWorker>();

var app = builder.Build();

// Configure HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### Step 5: Azure Functions Program.cs (Updated)

```csharp
// UpdateEngine.Functions/src/Program.cs

using Microsoft.Extensions.Hosting;
using UpdateEngine.Core.Orchestrators;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // Register JSON serialization
        services.AddSingleton<JsonSerializerOptions>(provider => new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        // Register host-agnostic orchestrators (SAME as Worker Service!)
        services.AddSingleton<ISyncOrchestrator, SyncOrchestrator>();
        services.AddSingleton<IMetadataOrchestrator, MetadataOrchestrator>();
        services.AddSingleton<IHealthOrchestrator, HealthOrchestrator>();
        services.AddSingleton<IContentOrchestrator, ContentOrchestrator>();

        // Register domain services (SAME as Worker Service!)
        services.AddMicrosoftUpdateServices(context.Configuration);
    })
    .Build();

host.Run();
```

## ?? Testing Strategy

### Test Orchestrators Directly (Host-Agnostic)

```csharp
// UpdateEngine.Functions/test/Unit/OrchestratorTests/SyncOrchestratorTests.cs

public class SyncOrchestratorTests
{
    private readonly Mock<ISyncService> mockSyncService;
    private readonly Mock<IMetadataStore> mockMetadataStore;
    private readonly Mock<ILogger<SyncOrchestrator>> mockLogger;
    private readonly SyncOrchestrator orchestrator;

    public SyncOrchestratorTests()
    {
        this.mockSyncService = new Mock<ISyncService>();
        this.mockMetadataStore = new Mock<IMetadataStore>();
        this.mockLogger = new Mock<ILogger<SyncOrchestrator>>();

        this.orchestrator = new SyncOrchestrator(
            this.mockSyncService.Object,
            this.mockMetadataStore.Object,
            this.mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteSyncAsync_WithCategoriesStart_ShouldSucceed()
    {
        // Arrange
        var request = new UnifiedSyncRequest
        {
            SyncType = SyncType.Categories,
            Action = SyncAction.Start
        };

        this.mockSyncService
            .Setup(s => s.SyncCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult { Success = true });

        this.mockMetadataStore
            .Setup(m => m.GetCategoriesCount(It.IsAny<CancellationToken>()))
            .ReturnsAsync(150);

        // Act
        var result = await this.orchestrator.ExecuteSyncAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(150, result.ItemsSynced);
        this.mockSyncService.Verify(
            s => s.SyncCategoriesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteSyncAsync_WithInvalidSyncType_ShouldReturnError()
    {
        // Arrange
        var request = new UnifiedSyncRequest
        {
            SyncType = null,
            Action = SyncAction.Start
        };

        // Act
        var result = await this.orchestrator.ExecuteSyncAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("SyncType is required", result.ErrorMessage);
    }
}
```

## ?? Benefits of Dual Hosting Model

### 1. **Deployment Flexibility**

| Scenario | Hosting Model | Why |
|----------|---------------|-----|
| Serverless, event-driven | Azure Functions | Auto-scaling, pay-per-execution |
| Always-on, high throughput | Worker Service | Lower latency, predictable costs |
| Hybrid cloud | Worker Service | Deploy on-premises or any cloud |
| Development/Testing | Either | Choose based on preference |

### 2. **Cost Optimization**

- **Azure Functions**: Best for variable workloads, low traffic
- **Worker Service**: Best for steady workloads, high traffic

### 3. **Code Reuse** (90%+!)

- ? Orchestrators: 100% shared
- ? Domain services: 100% shared  
- ? Models: 100% shared
- ? Tests: 90% shared (unit tests work for both)
- ? Hosting adapters: ~10% different (thin layers)

### 4. **Testing Benefits**

- Test orchestrators once, works for both hosting models
- Integration tests can target either host
- No mocking of Azure Functions or ASP.NET Core in unit tests

### 5. **Migration Path**

Easy to migrate between hosting models:
```bash
# Start with Azure Functions
dotnet publish UpdateEngine.Functions/src/UpdateEngine.csproj

# Switch to Worker Service later
dotnet publish UpdateEngine.Functions/src/WorkerService/WorkerService.csproj
```

## ?? Deployment Options

### Option 1: Azure Functions

```bash
# Deploy Azure Functions
cd UpdateEngine.Functions/src
func azure functionapp publish <your-function-app>
```

### Option 2: Worker Service (Docker)

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY publish/ .
ENTRYPOINT ["dotnet", "UpdateEngine.WorkerService.dll"]
```

```bash
# Build and run
docker build -t update-engine-worker .
docker run -p 8080:8080 update-engine-worker
```

### Option 3: Worker Service (Azure Container Apps)

```bash
# Deploy to Azure Container Apps
az containerapp create \
  --name update-engine \
  --resource-group my-rg \
  --image myregistry.azurecr.io/update-engine-worker:latest \
  --target-port 8080
```

### Option 4: Worker Service (Kubernetes)

```yaml
# deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: update-engine
spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: update-engine
        image: update-engine-worker:latest
        ports:
        - containerPort: 8080
```

## ?? Migration Checklist

- [ ] Create `UpdateEngine.Functions/src/Core/Orchestrators/` directory
- [ ] Move business logic to orchestrators
- [ ] Create host-agnostic models in `UpdateEngine.Functions/src/Core/Models/`
- [ ] Update Azure Functions to use orchestrators
- [ ] Create `UpdateEngine.Functions/src/WorkerService/` project
- [ ] Implement ASP.NET Core controllers
- [ ] Implement background workers
- [ ] Update DI registration (same for both hosts)
- [ ] Write unit tests for orchestrators
- [ ] Write integration tests for both hosting models
- [ ] Update documentation

## ?? Recommended Approach

1. **Phase 1**: Extract orchestrators from existing functions
2. **Phase 2**: Update Azure Functions to use orchestrators
3. **Phase 3**: Test thoroughly with Azure Functions
4. **Phase 4**: Create Worker Service project in parallel
5. **Phase 5**: Validate both hosting models work identically
6. **Phase 6**: Choose deployment model per environment

---

**Last Updated**: 2025-01-XX  
**Status**: Recommended Architecture  
**Next**: Start with SyncOrchestrator extraction
