# Copilot Instructions for Microsoft Update Server-Server Sync

## Project Overview

This is a comprehensive implementation of the Microsoft Update Server-Server sync protocol in .NET 8.0, providing both traditional ASP.NET Core web services and modern Azure Functions implementations. The project enables programmatic browsing of the Microsoft Update catalog, local metadata synchronization, and serving updates to downstream WSUS servers or Windows Update clients.

## Architecture

### Core Components

- **`src/microsoft-update-partition/`**: Core metadata storage engine with `IMetadataStore` and `IContentStore` abstractions
- **`src/microsoft-update-webservices/`**: SOAP web service implementations for client/server sync protocols
- **`src/microsoft-update-endpoints/`**: ASP.NET Core startup classes and endpoint configurations
- **`src/microsoft-update-upstream-package-source/`**: Client libraries for syncing from upstream Microsoft Update servers
- **`azure-functions/`**: Serverless Azure Functions implementation wrapping the web services
- **`src/tools/upsync/`**: CLI tool for metadata synchronization and server management

### Data Flow

```
Microsoft Update Catalog → UpstreamServerClient → IMetadataStore (local/Azure) → SOAP Endpoints → Windows Update Clients/WSUS
```

## Critical Development Patterns

### Dependency Injection Setup

The project uses a specific DI pattern across Azure Functions and ASP.NET Core:

```csharp
// Register metadata store (required)
services.AddSingleton<IMetadataStore>(provider => PackageStore.Open(metadataPath));

// Register content store (optional)
services.AddSingleton<IContentStore?>(provider => 
    !string.IsNullOrEmpty(contentPath) ? new FileSystemContentStore(contentPath) : null);

// Register service configurations as JSON
services.AddSingleton<Config?>(provider => 
    JsonSerializer.Deserialize<Config>(serviceConfigJson));
```

### JSON Serialization Standards

**CRITICAL**: The project is migrating from Newtonsoft.Json to System.Text.Json:

- ✅ **Azure Functions**: Use `System.Text.Json` for all new code
- ⚠️ **Legacy Libraries**: Some core libraries still use `Newtonsoft.Json`
- **Pattern**: Always check existing patterns in the file before choosing serializer

```csharp
// Preferred (System.Text.Json)
using System.Text.Json;
var data = JsonSerializer.Deserialize<T>(json);

// Legacy (still used in core libraries)
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

- **`tests/MicrosoftUpdateFunctions.Tests/Endpoints/`**: Individual endpoint testing
- **`tests/MicrosoftUpdateFunctions.Tests/Integration/`**: End-to-end system tests
- **`tests/MicrosoftUpdateFunctions.Tests/Infrastructure/`**: Test fixture implementations

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

## Development Workflows

### Local Development Setup

```bash
# 1. Build everything
dotnet build build/microsoft-update.sln

# 2. Start Azure Functions locally
cd azure-functions && func start

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

## Documentation

- **API docs**: `/docs/api/` contains comprehensive API documentation
- **Examples**: `/src/documentation/docfx-config/examples/` for usage patterns
- **TRIGGERS_GUIDE.md**: Azure Functions advanced trigger patterns
- **TESTING_GUIDE.md**: Comprehensive testing strategies and patterns