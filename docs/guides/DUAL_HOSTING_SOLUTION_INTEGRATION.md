# Dual Hosting Model: Integration with Existing Solution

## ?? Overview

This guide shows how to implement the dual hosting model while **maintaining compatibility** with your existing solution structure:

- **AppHost** - .NET Aspire orchestration for local development
- **Configuration** - Shared configuration project
- **update-cli** - Command-line tool
- **Existing libraries** - microsoft-update-partition, microsoft-update-webservices, etc.

## ?? Updated Solution Structure

```
update-server-server-sync/
?
??? AppHost/
?   ??? src/
?       ??? AppHost.csproj                      # .NET Aspire orchestration
?           ??? Program.cs                      # Orchestrates all projects
?           ??? ConfigurationHelper.cs          # Shared configuration
?
??? Configuration/
?   ??? Configuration.csproj                    # Shared configuration models
?       ??? AppConfig.cs                        # Immutable configuration
?       ??? MutableAppConfig.cs                 # Mutable configuration
?       ??? ConfigurationExtensions.cs          # Conversion helpers
?
??? UpdateEngine/
?   ??? src/
?   ?   ??? UpdateEngine.csproj                 # Azure Functions project
?   ?   ?   ??? Program.cs                      # Azure Functions host
?   ?   ?   ?
?   ?   ?   ??? Core/                          # NEW: Host-agnostic orchestrators
?   ?   ?   ?   ??? Orchestrators/
?   ?   ?   ?   ?   ??? ISyncOrchestrator.cs
?   ?   ?   ?   ?   ??? SyncOrchestrator.cs
?   ?   ?   ?   ?   ??? IMetadataOrchestrator.cs
?   ?   ?   ?   ?   ??? MetadataOrchestrator.cs
?   ?   ?   ?   ??? Models/
?   ?   ?   ?       ??? SyncModels.cs
?   ?   ?   ?       ??? MetadataModels.cs
?   ?   ?   ?
?   ?   ?   ??? Functions/                      # Azure Functions (thin adapters)
?   ?   ?   ?   ??? Core/
?   ?   ?   ?   ?   ??? UnifiedSyncFunction.cs
?   ?   ?   ?   ?   ??? UnifiedMetadataFunction.cs
?   ?   ?   ?   ?   ??? WebServiceFunctions.cs
?   ?   ?   ?   ??? Management/
?   ?   ?   ?   ?   ??? UnifiedHealthFunction.cs
?   ?   ?   ?   ??? Intelligence/
?   ?   ?   ?       ??? AnomalyDetectionFunctions.cs
?   ?   ?   ?
?   ?   ?   ??? Services/                       # Existing services
?   ?   ?   ??? Helpers/
?   ?   ?   ??? Models/
?   ?   ?
?   ?   ??? WorkerService/                      # NEW: Worker Service project
?   ?       ??? WorkerService.csproj
?   ?       ??? Program.cs                      # Worker Service host
?   ?       ??? Controllers/                    # ASP.NET Core controllers
?   ?       ?   ??? SyncController.cs
?   ?       ?   ??? MetadataController.cs
?   ?       ?   ??? ContentController.cs
?   ?       ?   ??? HealthController.cs
?   ?       ??? Workers/                        # Background services
?   ?           ??? SyncWorker.cs
?   ?           ??? HealthCheckWorker.cs
?   ?           ??? AnomalyDetectionWorker.cs
?   ?
?   ??? test/
?       ??? UpdateEngineTest.csproj
?       ??? (existing tests)
?
??? update-cli/
?   ??? src/
?       ??? update-cli.csproj                   # CLI tool
?           ??? Program.cs                      # Can use orchestrators too!
?           ??? Commands/
?               ??? SyncCommand.cs              # Uses ISyncOrchestrator
?               ??? QueryCommand.cs             # Uses IMetadataOrchestrator
?
??? microsoft-update-partition/
?   ??? src/                                    # Existing metadata storage
?
??? microsoft-update-webservices/
?   ??? src/                                    # Existing SOAP services
?
??? microsoft-update-upstream-source/
?   ??? src/                                    # Existing upstream client
?
??? microsoft-update-endpoints/
    ??? src/                                    # Existing ASP.NET Core endpoints
```

## ?? Integration Points

### 1. Configuration Project Integration

The **Configuration project** provides shared configuration models that work with ALL hosting models:

```csharp
// Configuration/AppConfig.cs (existing, immutable)
public record AppConfig
{
    public ServiceConfiguration ServiceConfiguration { get; init; } = new();
    public SyncConfiguration SyncConfiguration { get; init; } = new();
    public StorageConfiguration StorageConfiguration { get; init; } = new();
    public FeatureFlags FeatureFlags { get; init; } = new();
}

// Configuration/MutableAppConfig.cs (existing, mutable)
public class MutableAppConfig
{
    public MutableServiceConfiguration ServiceConfiguration { get; set; } = new();
    public MutableSyncConfiguration SyncConfiguration { get; set; } = new();
    public MutableStorageConfiguration StorageConfiguration { get; set; } = new();
    public MutableFeatureFlags FeatureFlags { get; set; } = new();
}

// Configuration/ConfigurationExtensions.cs (existing)
public static class ConfigurationExtensions
{
    public static AppConfig ToImmutable(this MutableAppConfig mutable) { /* ... */ }
    public static MutableAppConfig ToMutable(this AppConfig immutable) { /* ... */ }
}
```

**Key Insight**: The Configuration project already provides conversion between immutable (record) and mutable (class) configurations. This works perfectly with orchestrators!

### 2. Orchestrators Use Configuration Project

```csharp
// UpdateEngine.Functions/src/Core/Orchestrators/SyncOrchestrator.cs

using Configuration; // Reference Configuration project

public class SyncOrchestrator : ISyncOrchestrator
{
    private readonly ISyncService syncService;
    private readonly ILogger<SyncOrchestrator> logger;
    private readonly AppConfig appConfig; // ? Use existing AppConfig!

    public SyncOrchestrator(
        ISyncService syncService,
        ILogger<SyncOrchestrator> logger,
        AppConfig appConfig) // ? Inject AppConfig
    {
        this.syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
    }

    public async Task<SyncOperationResult> ExecuteSyncAsync(
        UnifiedSyncRequest request,
        CancellationToken cancellationToken = default)
    {
        // Use configuration from AppConfig
        var upstreamEndpoint = this.appConfig.ServiceConfiguration.UpstreamEndpoint;
        var syncInterval = this.appConfig.SyncConfiguration.DefaultSyncInterval;

        // ... orchestrator logic
    }
}
```

### 3. AppHost Integration (Local Development)

The **AppHost** orchestrates all projects for local development using .NET Aspire:

```csharp
// UpdateEngine.AppHost/src/Program.cs (updated)

using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Add Azurite for local Azure Storage emulation
var azurite = builder.AddAzureStorage("storage")
    .RunAsEmulator()
    .WithDataVolume();

var blobStorage = azurite.AddBlobs("blobs");

// Add Azure Functions project
var updateEngine = builder.AddProject<Projects.UpdateEngine>("updateengine")
    .WithReference(blobStorage)
    .WithEnvironment("UseAzureStorageForMetadata", "true")
    .WithEnvironment("UseAzureStorageForContent", "true");

// NEW: Add Worker Service project (optional)
var workerService = builder.AddProject<Projects.UpdateEngine_WorkerService>("workerservice")
    .WithReference(blobStorage)
    .WithEnvironment("UseAzureStorageForMetadata", "true")
    .WithEnvironment("UseAzureStorageForContent", "true")
    .WithHttpEndpoint(port: 8080, name: "http");

// Add configuration UI
builder.AddProject<Projects.ConfigurationUI>("config-ui");

builder.Build().Run();
```

**Benefits:**
- Start both Azure Functions AND Worker Service simultaneously
- Share same Azurite instance
- Easy switching between hosting models during development

### 4. Shared DI Registration Extension

Create a shared extension method that works with **all hosting models**:

```csharp
// UpdateEngine.Functions/src/Core/ServiceCollectionExtensions.cs (NEW)

using Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UpdateEngine.Core.Orchestrators;

namespace UpdateEngine.Core;

/// <summary>
/// Shared service registration for all hosting models (Azure Functions, Worker Service, CLI)
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register host-agnostic orchestrators and core services
    /// </summary>
    public static IServiceCollection AddUpdateEngineCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Register Configuration project models
        var appConfig = ConfigurationHelper.BuildAppConfig(configuration);
        services.AddSingleton(appConfig);

        // 2. Register host-agnostic orchestrators
        services.AddSingleton<ISyncOrchestrator, SyncOrchestrator>();
        services.AddSingleton<IMetadataOrchestrator, MetadataOrchestrator>();
        services.AddSingleton<IHealthOrchestrator, HealthOrchestrator>();
        services.AddSingleton<IContentOrchestrator, ContentOrchestrator>();

        // 3. Register domain services (existing)
        services.AddMicrosoftUpdateServices(configuration);

        // 4. Register stores
        services.AddSingleton<IMetadataStore>(provider =>
        {
            var config = provider.GetRequiredService<AppConfig>();
            return config.StorageConfiguration.UseAzureStorageForMetadata
                ? Azure.PackageStore.Open(/* Azure blob config */)
                : PackageStore.Open(config.StorageConfiguration.MetadataPath);
        });

        services.AddSingleton<IContentStore>(provider =>
        {
            var config = provider.GetRequiredService<AppConfig>();
            return config.StorageConfiguration.UseAzureStorageForContent
                ? new Azure.BlobContentStore(/* Azure blob config */)
                : new FileSystemContentStore(config.StorageConfiguration.ContentPath);
        });

        return services;
    }
}
```

### 5. Azure Functions Program.cs (Updated)

```csharp
// UpdateEngine.Functions/src/Program.cs

using Microsoft.Extensions.Hosting;
using UpdateEngine.Core; // ? Use shared extensions

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // Register JSON serialization (Azure Functions specific)
        services.AddSingleton<JsonSerializerOptions>(provider => new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        // Register core services (SHARED with Worker Service & CLI!)
        services.AddUpdateEngineCore(context.Configuration);
    })
    .Build();

host.Run();
```

### 6. Worker Service Program.cs (NEW)

```csharp
// UpdateEngine.Functions/src/WorkerService/Program.cs

using UpdateEngine.Core; // ? Use SAME shared extensions
using UpdateEngine.WorkerService.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add ASP.NET Core services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register core services (SHARED with Azure Functions & CLI!)
builder.Services.AddUpdateEngineCore(builder.Configuration);

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

### 7. CLI Tool Integration (Bonus!)

The CLI tool can **also use orchestrators** for consistency:

```csharp
// update-cli/src/Commands/SyncCommand.cs (updated)

using Microsoft.Extensions.DependencyInjection;
using UpdateEngine.Core; // ? Use SAME orchestrators!
using UpdateEngine.Core.Orchestrators;
using UpdateEngine.Core.Models;

public class SyncCommand
{
    public static async Task<int> ExecuteAsync(SyncOptions options)
    {
        // Build service provider with SAME DI registration
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(options);
        
        // Register core services (SHARED with Azure Functions & Worker Service!)
        services.AddUpdateEngineCore(configuration);

        var serviceProvider = services.BuildServiceProvider();

        // Use orchestrator (same logic as Azure Functions & Worker Service!)
        var orchestrator = serviceProvider.GetRequiredService<ISyncOrchestrator>();

        var request = new UnifiedSyncRequest
        {
            SyncType = options.SyncType switch
            {
                "categories" => SyncType.Categories,
                "updates" => SyncType.Updates,
                "comprehensive" => SyncType.Comprehensive,
                _ => throw new ArgumentException($"Unknown sync type: {options.SyncType}")
            },
            Action = SyncAction.Start,
            Filter = new SyncFilter
            {
                ProductTitles = options.Products,
                ClassificationIds = options.Classifications
            }
        };

        var result = await orchestrator.ExecuteSyncAsync(request);

        Console.WriteLine(result.Success
            ? $"? Sync completed: {result.ItemsSynced} items in {result.Duration}"
            : $"? Sync failed: {result.ErrorMessage}");

        return result.Success ? 0 : 1;
    }
}
```

**Benefits:**
- CLI tool uses **exact same orchestrators** as Azure Functions and Worker Service
- No code duplication between CLI and web hosts
- Same behavior across all entry points

## ?? Testing with AppHost

### Local Development Scenarios

#### Scenario 1: Test Azure Functions Locally
```bash
# Start AppHost (includes Azure Functions + Azurite)
cd UpdateEngine.AppHost/src
dotnet run

# Azure Functions available at http://localhost:7071
curl http://localhost:7071/api/sync -d '{"syncType":"categories","action":"start"}'
```

#### Scenario 2: Test Worker Service Locally
```bash
# Start AppHost (includes Worker Service + Azurite)
cd UpdateEngine.AppHost/src
dotnet run

# Worker Service available at http://localhost:8080
curl http://localhost:8080/api/sync -d '{"syncType":"categories","action":"start"}'
```

#### Scenario 3: Test Both Simultaneously
```bash
# AppHost can start BOTH at the same time!
cd UpdateEngine.AppHost/src
dotnet run

# Test Azure Functions
curl http://localhost:7071/api/sync/status

# Test Worker Service
curl http://localhost:8080/api/sync/status

# Both share same Azurite storage!
```

#### Scenario 4: Test CLI Tool
```bash
# CLI uses same orchestrators as web hosts
cd update-cli/src
dotnet run -- sync --type categories --products "Windows 10"
```

### AppHost Configuration

```csharp
// UpdateEngine.AppHost/src/Program.cs (complete example)

using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Shared Azurite for all projects
var azurite = builder.AddAzureStorage("storage")
    .RunAsEmulator()
    .WithDataVolume();

var blobStorage = azurite.AddBlobs("blobs");

// Shared configuration
var sharedConfig = new Dictionary<string, string>
{
    { "UseAzureStorageForMetadata", "true" },
    { "UseAzureStorageForContent", "true" },
    { "AzureBlobConnectionString", azurite.Resource.ConnectionStringExpression },
    { "MetadataContainerName", "metadata" },
    { "ContentContainerName", "content" }
};

// Azure Functions
var functions = builder.AddProject<Projects.UpdateEngine>("functions")
    .WithReference(blobStorage);

foreach (var kvp in sharedConfig)
{
    functions.WithEnvironment(kvp.Key, kvp.Value);
}

// Worker Service
var workerService = builder.AddProject<Projects.UpdateEngine_WorkerService>("worker")
    .WithReference(blobStorage)
    .WithHttpEndpoint(port: 8080, name: "http");

foreach (var kvp in sharedConfig)
{
    workerService.WithEnvironment(kvp.Key, kvp.Value);
}

// Dashboard
builder.AddProject<Projects.ConfigurationUI>("dashboard")
    .WithReference(functions)
    .WithReference(workerService);

builder.Build().Run();
```

## ?? Project References

### UpdateEngine.csproj (Azure Functions)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <!-- Azure Functions -->
    <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="2.0.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" Version="2.0.0" />
    
    <!-- Existing project references -->
    <ProjectReference Include="..\..\Configuration\Configuration.csproj" />
    <ProjectReference Include="..\..\microsoft-update-partition\src\microsoft-update-partition.csproj" />
    <ProjectReference Include="..\..\microsoft-update-webservices\src\microsoft-update-webservices.csproj" />
    <ProjectReference Include="..\..\microsoft-update-upstream-source\src\microsoft-update-upstream-source.csproj" />
  </ItemGroup>

  <!-- Core folder contains orchestrators (host-agnostic) -->
  <ItemGroup>
    <Compile Include="Core\**\*.cs" />
    <Compile Include="Functions\**\*.cs" />
    <Compile Include="Services\**\*.cs" />
  </ItemGroup>
</Project>
```

### WorkerService.csproj (NEW)

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <!-- ASP.NET Core -->
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="9.0.0" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="7.2.0" />
  </ItemGroup>

  <ItemGroup>
    <!-- Reference SAME projects as Azure Functions -->
    <ProjectReference Include="..\..\Configuration\Configuration.csproj" />
    <ProjectReference Include="..\..\microsoft-update-partition\src\microsoft-update-partition.csproj" />
    <ProjectReference Include="..\..\microsoft-update-webservices\src\microsoft-update-webservices.csproj" />
    <ProjectReference Include="..\..\microsoft-update-upstream-source\src\microsoft-update-upstream-source.csproj" />
  </ItemGroup>

  <!-- Link to Core folder from Azure Functions project (shared orchestrators!) -->
  <ItemGroup>
    <Compile Include="..\Core\**\*.cs" LinkBase="Core" />
  </ItemGroup>
</Project>
```

**Key Insight**: Worker Service **links** to the Core folder from UpdateEngine project, ensuring 100% code reuse of orchestrators!

## ?? Configuration Patterns

### Azure Functions (local.settings.json)

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    
    "UseAzureStorageForMetadata": "true",
    "UseAzureStorageForContent": "true",
    "AzureBlobConnectionString": "UseDevelopmentStorage=true",
    "MetadataContainerName": "metadata",
    "ContentContainerName": "content",
    
    "UpstreamEndpoint": "https://fe2.update.microsoft.com/v6",
    "DefaultSyncInterval": "02:00:00"
  }
}
```

### Worker Service (appsettings.Development.json)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  
  "UseAzureStorageForMetadata": true,
  "UseAzureStorageForContent": true,
  "AzureBlobConnectionString": "UseDevelopmentStorage=true",
  "MetadataContainerName": "metadata",
  "ContentContainerName": "content",
  
  "UpstreamEndpoint": "https://fe2.update.microsoft.com/v6",
  "DefaultSyncInterval": "02:00:00",
  
  "BackgroundSync": {
    "Interval": "02:00:00"
  }
}
```

**Key Insight**: Same configuration structure for both hosting models!

## ?? Migration Checklist

### Phase 1: Setup Core Infrastructure
- [x] Create `UpdateEngine.Functions/src/Core/` directory structure
- [x] Create `SyncOrchestrator.cs` and `ISyncOrchestrator.cs`
- [x] Create `SyncModels.cs` with shared models
- [ ] Create `ServiceCollectionExtensions.cs` with shared DI
- [ ] Update Azure Functions to use orchestrators
- [ ] Test with AppHost

### Phase 2: Add Worker Service Support
- [ ] Create `UpdateEngine.Functions/src/WorkerService/` project
- [ ] Add `WorkerService.csproj` with Core folder link
- [ ] Create ASP.NET Core controllers
- [ ] Create background workers
- [ ] Update AppHost to include Worker Service
- [ ] Test both hosting models simultaneously

### Phase 3: Update CLI Tool
- [ ] Update `update-cli` to use orchestrators
- [ ] Remove duplicate sync logic from CLI
- [ ] Test CLI commands use same logic as web hosts

### Phase 4: Additional Orchestrators
- [ ] Create `MetadataOrchestrator.cs`
- [ ] Create `HealthOrchestrator.cs`
- [ ] Create `ContentOrchestrator.cs`
- [ ] Update all functions to use orchestrators

### Phase 5: Testing & Validation
- [ ] Write unit tests for orchestrators
- [ ] Update integration tests for both hosting models
- [ ] Test with AppHost (all projects together)
- [ ] Validate configuration works across all hosts

## ?? Development Workflow

### Daily Development
```bash
# 1. Start AppHost (starts everything!)
cd UpdateEngine.AppHost/src
dotnet run

# 2. AppHost dashboard opens in browser showing:
#    - Azure Functions (http://localhost:7071)
#    - Worker Service (http://localhost:8080)
#    - Azurite (http://localhost:10000)
#    - Configuration UI (http://localhost:5000)

# 3. Test either hosting model
curl http://localhost:7071/api/sync/status  # Azure Functions
curl http://localhost:8080/api/sync/status  # Worker Service

# 4. Or use CLI
cd update-cli/src
dotnet run -- sync --type categories
```

### Testing Changes
```bash
# Test orchestrator changes work in ALL hosting models:

# 1. Make change to SyncOrchestrator.cs
# 2. Restart AppHost
# 3. Test Azure Functions endpoint
# 4. Test Worker Service endpoint
# 5. Test CLI command

# All use the same orchestrator! ?
```

## ?? Key Benefits

### 1. **Zero Code Duplication**
- ? Orchestrators: Used by Azure Functions, Worker Service, AND CLI
- ? Configuration: Shared Configuration project
- ? Services: Shared across all hosts
- ? Tests: Test orchestrators once

### 2. **Consistent Behavior**
- Same business logic across Azure Functions, Worker Service, and CLI
- Same configuration structure
- Same error handling
- Same logging

### 3. **Easy Local Development**
- AppHost starts everything with one command
- Test multiple hosting models simultaneously
- Shared Azurite instance
- Aspire dashboard for monitoring

### 4. **Flexible Deployment**
- Dev/Test: Azure Functions (via AppHost)
- Production: Choose Azure Functions OR Worker Service
- CLI: Works anywhere

### 5. **Future-Proof**
- Easy to add new hosting models (gRPC, Console, etc.)
- Orchestrators work with any host
- No lock-in to specific hosting technology

## ?? Related Documentation

- **[DUAL_HOSTING_QUICK_SUMMARY.md](./DUAL_HOSTING_QUICK_SUMMARY.md)** - Quick reference
- **[FUNCTION_CONSOLIDATION_GUIDE.md](./FUNCTION_CONSOLIDATION_GUIDE.md)** - Consolidation patterns
- **[Configuration Guide](./CONFIGURATION_GUIDE.md)** - Configuration project details
- **[AppHost Guide](../../../AppHost/README.md)** - Aspire orchestration details

---

**Last Updated**: 2025-01-XX  
**Status**: Recommended Architecture  
**Compatible With**: .NET 9, Aspire, Configuration project, update-cli
