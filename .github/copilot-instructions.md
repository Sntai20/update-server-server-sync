# Copilot Instructions for Microsoft Update Server-Server Sync

## Project Overview

This is a comprehensive implementation of the Microsoft Update Server-Server sync protocol in .NET 9.0, providing both traditional ASP.NET Core web services and modern Azure Functions implementations. The project enables programmatic browsing of the Microsoft Update catalog, local metadata synchronization, and serving updates to downstream WSUS servers or Windows Update clients.

## Architecture

### Core Components

- **`src/microsoft-update-partition/`**: Core metadata storage engine with `IMetadataStore` and `IContentStore` abstractions
- **`src/microsoft-update-webservices/`**: SOAP web service implementations for client/server sync protocols
- **`src/microsoft-update-endpoints/`**: ASP.NET Core startup classes and endpoint configurations
- **`src/microsoft-update-upstream-package-source/`**: Client libraries for syncing from upstream Microsoft Update servers
- **`UpdateEngine/`**: Serverless Azure Functions implementation wrapping the web services (.NET 9)
- **`AppHost/`**: .NET Aspire application host for orchestrating the distributed application
- **`src/tools/upsync/`**: CLI tool for metadata synchronization and server management

### Data Flow

```
Microsoft Update Catalog → UpstreamServerClient → IMetadataStore (local/Azure) → SOAP Endpoints → Windows Update Clients/WSUS
```

## Critical Development Patterns

### Dependency Injection Setup

The project uses a specific DI pattern across Azure Functions and ASP.NET Core:

```csharp
// Register JSON serialization options (REQUIRED for Azure Functions)
services.AddSingleton<JsonSerializerOptions>(provider => new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
});

// Register metadata store (required)
services.AddSingleton<IMetadataStore>(provider => PackageStore.Open(metadataPath));

// Register content store (optional)
services.AddSingleton<IContentStore?>(provider => 
    !string.IsNullOrEmpty(contentPath) ? new FileSystemContentStore(contentPath) : null);

// Register service configurations as JSON
services.AddSingleton<Config?>(provider => 
    JsonSerializer.Deserialize<Config>(serviceConfigJson));

// Use centralized service registration (recommended approach)
services.AddMicrosoftUpdateServices(configuration);  // Includes all above + JSON options
```

### JSON Serialization Standards

**CRITICAL**: The project uses a layered JSON serialization approach:

- ✅ **Azure Functions (UpdateEngine)**: Use `System.Text.Json` for all new HTTP APIs, configuration, and response models
- ✅ **Core Libraries (microsoft-update-*)**: Continue using `Newtonsoft.Json` for metadata storage, SOAP services, and compatibility
- **Pattern**: Check existing patterns in the file before choosing serializer

```csharp
// Azure Functions layer (System.Text.Json)
using System.Text.Json;
var data = JsonSerializer.Deserialize<T>(json);

// Core libraries (Newtonsoft.Json for compatibility)
using Newtonsoft.Json;
var data = JsonConvert.DeserializeObject<T>(json);
```

### Code Style Conventions

- **`this` keyword**: Always explicitly use `this.` for member access
- **No regions**: Avoid using regions in code; organize with proper class structure
- **Nullable reference types**: Enabled - use `?` annotations consistently
- **Async patterns**: Use `async`/`await` throughout; avoid `.Result` or `.Wait()`

### Storage Abstractions

The project uses two main storage types:

```csharp
// Metadata storage (updates, categories, applicability rules)
IMetadataStore store = PackageStore.Open("./store");                    // Local
IMetadataStore store = Azure.PackageStore.Open(cloudBlobContainer);     // Azure Blob

// Content storage (actual update files)
IContentStore content = new FileSystemContentStore("./content");        // Local
IContentStore content = Azure.BlobContentStore.Open(blobClient, path);  // Azure Blob
```

## Testing Infrastructure

### Test Organization

- **`test/MicrosoftUpdateFunctions.Tests/Endpoints/`**: Individual endpoint testing
- **`test/MicrosoftUpdateFunctions.Tests/Integration/`**: End-to-end system tests
- **`test/MicrosoftUpdateFunctions.Tests/Infrastructure/`**: Test fixture implementations

### Test Fixtures

```csharp
// Lightweight Azure Functions testing
[Collection("AspireIntegration")]
public class MyTests
{
    private readonly AspireTestFixture fixture;
    
    // Uses func CLI to start Azure Functions on localhost:7071
    // Includes automatic health checking and retry logic
}

// Full orchestration testing
[Collection("AspireAppHost")]
public class SystemTests
{
    private readonly AspireAppHostTestFixture fixture;
    
    // Full distributed application testing with Aspire
    // Production-like environment simulation
}
```

### Running Tests

```bash
# Core library tests (fast)
dotnet test --filter "Category!=Integration"

# Integration tests (requires Azure Functions running)
dotnet test --filter "Category=Integration" 

# Specific test patterns
dotnet test --filter "FullyQualifiedName~MetadataSyncIntegrationTests"
```

# Run in-memory tests (no infrastructure required)
./scripts/test/Run-InMemoryTests.ps1

## Development Workflows

### Local Development Setup

```bash
# 1. Build everything
dotnet build build/microsoft-update.sln

# 2. Start Azure Functions locally via Aspire
cd AppHost && dotnet run --project src/AppHost.csproj

# Alternative: Start Functions directly
cd UpdateEngine && func start

# 3. Test endpoints
curl http://localhost:7071/api/ClientWebService/client.asmx
curl http://localhost:7071/api/GetStoreStatus
```

### Configuration Patterns

**Azure Functions** (`local.settings.json`):
```json
{
  "Values": {
    "MetadataStorePath": "./store",
    "ContentStorePath": "./content", 
    "ServiceConfigurationJson": "{\"ServiceUrl\":\"http://localhost:7071\"}",
    "AzureWebJobsStorage": ""  // Empty for HTTP-only functions
  }
}
```

**ASP.NET Core**:
```csharp
var configDictionary = new Dictionary<string, string>()
{
    { "metadata-path", metadataPath },
    { "content-path", contentPath },
    { "service-config-json", serviceConfigurationJson }
};
config.AddInMemoryCollection(configDictionary);
```

### SOAP Endpoint Patterns

All SOAP endpoints follow this pattern:

```csharp
[Function("FunctionName")]
public async Task<HttpResponseData> HandleSoapRequest(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
{
    // 1. Extract SOAP body from request
    var requestBody = await GetRequestBodyAsync(req);
    
    // 2. Parse action from SOAPAction header
    var soapAction = ExtractSoapAction(req);
    
    // 3. Delegate to appropriate web service method
    var result = await this.webService.ProcessRequestAsync(soapAction, requestBody);
    
    // 4. Return properly formatted SOAP response
    return await CreateSoapResponseAsync(req, result);
}
```

## Key Gotchas

### Metadata Store Requirements

- **Must call `PackageStore.Open()` before any operations** - no lazy loading
- **Reindexing**: Store may require reindexing after metadata changes (`store.IsReindexingRequired`)
- **Thread safety**: Stores are thread-safe for reads, serialize writes

### Content Addressing

- **SHA1 vs SHA256**: Both supported, check `contentHash` length to determine type
- **File paths**: Content stored by hash value: `/content/{sha1-or-sha256-hex}`
- **Range requests**: Support HTTP range headers for partial downloads

### Configuration Dependencies

- **ServiceConfigurationJson**: Required for SOAP endpoints to function
- **ContentStorePath**: Optional - only needed if serving actual update files
- **MetadataStorePath**: Required - functions will fail without valid metadata store

### Azure Functions Limitations

- **Timer triggers**: Require storage account connection (use Azurite for local dev)
- **Cold start**: First request after idle may take 10-15 seconds
- **Memory limits**: Monitor memory usage during large sync operations

### Dependency Injection Issues

**COMMON ERROR**: `Unable to resolve service for type 'System.Text.Json.JsonSerializerOptions'`

**Solution**: Ensure `AddMicrosoftUpdateServices()` is called, which includes JsonSerializerOptions registration:

```csharp
// In Program.cs or service configuration
services.AddMicrosoftUpdateServices(configuration);

// Or manually register JsonSerializerOptions if not using the extension:
services.AddSingleton<JsonSerializerOptions>(/* configuration */);
```

**Root Cause**: Services like `QueueService`, `AnomalyDetectionService` depend on `JsonSerializerOptions` but it's not automatically registered in DI container.

**Prevention**: Always use `AddMicrosoftUpdateServices()` extension method for proper service registration.

## Common Operations

### Sync from Microsoft Update

```csharp
var upstreamClient = new UpstreamServerClient(Endpoint.Default);
var configData = await upstreamClient.GetServerConfigData();

// Sync categories first
var categoriesSource = new UpstreamCategoriesSource(endpoint);
await categoriesSource.CopyTo(metadataStore, cancellationToken);

// Then sync updates with filters
var updatesSource = new UpstreamUpdatesSource(endpoint);
await updatesSource.CopyTo(metadataStore, filter, cancellationToken);
```

### Error Handling Patterns

```csharp
try
{
    // Operation
}
catch (Exception ex)
{
    logger.LogError(ex, "Operation failed: {Message}", ex.Message);
    return CreateErrorResponse("Internal server error", HttpStatusCode.InternalServerError);
}
```

### Performance Considerations

- **Batch operations**: Use `CopyTo` methods for bulk metadata operations
- **Filtering**: Apply filters early to reduce memory usage
- **Concurrent requests**: Design for 50+ concurrent SOAP requests
- **Caching**: HTTP responses include appropriate caching headers

## Project Structure

### Directory Organization

update-server-server-sync/
├── docs/                          # Centralized documentation
│   ├── guides/                    # All documentation guides (centralized)
│   └── api/                       # API documentation
│
├── scripts/                       # Centralized scripts
│   ├── setup/                     # Setup and configuration scripts
│   ├── build/                     # Build and validation scripts
│   ├── test/                      # Testing scripts
│   └── maintenance/               # Maintenance scripts
│
├── UpdateEngine/                  # Azure Functions implementation (.NET 9)
│   ├── src/                       # Function implementations
│   └── test/                      # Function tests
│
├── UpdateEngine.Core/             # Shared UpdateEngine core library
│   └── src/                       # Core orchestrators, services, models
│
├── AppHost/                       # .NET Aspire application host
│   └── src/                       # Aspire orchestration
│
├── src/                          # Core libraries
│   ├── microsoft-update-partition/
│   ├── microsoft-update-webservices/
│   ├── microsoft-update-endpoints/
│   ├── microsoft-update-upstream-package-source/
│   └── tools/upsync/
│
└── test/                         # Test projects
    └── MicrosoftUpdateFunctions.Tests/

### Key Paths

- **Azure Functions**: `UpdateEngine/src/`
- **UpdateEngine Core**: `UpdateEngine.Core/src/` (orchestrators, services, models)
- **Aspire AppHost**: `AppHost/src/AppHost.csproj`
- **Core Libraries**: `src/microsoft-update-*/`
- **Tests**: `UpdateEngine/test/`
- **Scripts**: `scripts/{setup|build|test|maintenance}/`
- **Documentation**: `docs/{guides|troubleshooting|development}/`

## Documentation

### Essential Guides

- **Setup & Configuration**
  - `docs/guides/STORAGE_GUIDE.md` - Storage configuration
  - `docs/guides/MIGRATION_SUMMARY.md` - Migration guide
  - `scripts/setup/Configure-Storage.ps1` - Storage setup script

- **Development**
  - `docs/guides/INMEMORY_TESTING_GUIDE.md` - In-memory testing
  - `docs/guides/TRIGGERS_GUIDE.md` - Azure Functions triggers
  - `docs/guides/TESTING_GUIDE.md` - Testing strategies

- **Troubleshooting**
  - `docs/guides/WCF_NET9_FIX_GUIDE.md` - WCF .NET 9 fixes
  - `docs/guides/SYNC_TROUBLESHOOTING.md` - Sync issues
  - `docs/guides/TROUBLESHOOTING_STORAGE.md` - Storage issues
  - `docs/guides/CONTAINER_VERIFICATION.md` - Container testing

- **API Documentation**
  - `docs/api/` - Comprehensive API documentation
  - `src/documentation/docfx-config/examples/` - Code examples

### Scripts Reference

- **Setup**: `scripts/setup/Configure-Storage.ps1`
- **Build**: `scripts/build/Validate-Build.ps1`
- **Test**: `scripts/test/Run-InMemoryTests.ps1`
- **Maintenance**: `scripts/maintenance/Regenerate-WCF-Net9.ps1`

## .NET 9 Migration

This project has been upgraded to .NET 9.0. Key changes:

- All projects now target `.NET 9.0`
- Azure Functions use `Microsoft.Azure.Functions.Worker` v2.0+
- WCF service references updated for .NET 9 compatibility
- System.Text.Json preferred over Newtonsoft.Json for new code
- Aspire orchestration for local development and testing

### Breaking Changes

- Some WCF service reference code required regeneration
- Configuration patterns updated for Azure Functions v4
- Test fixtures updated for .NET 9 compatibility

See `.github/upgrades/dotnet-upgrade-report.md` for complete migration details.

---

## Recent Updates

### November 2025
- **Fixed JsonSerializerOptions DI issue**: Added proper JSON serialization registration in `ServiceCollectionExtensions.cs` to resolve dependency injection errors in AppHost
- **Improved service registration**: `AddMicrosoftUpdateServices()` now includes all required dependencies including JSON options
- **Enhanced troubleshooting**: Added guidance for common DI issues and their solutions

---

**Last Updated**: 2025-11-16  
**Target Framework**: .NET 9.0  
**Azure Functions**: v4 with isolated worker model  
**Aspire**: .NET Aspire for orchestration