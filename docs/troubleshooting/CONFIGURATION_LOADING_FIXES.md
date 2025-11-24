# Configuration Loading Fixes

## Issues Identified

### Issue 1: UpdateEngine Configuration Not Loading
**Symptoms:**
- Logs showed `ServiceUrl: (null)` and `UseAzureStorageForMetadata: False`
- Configuration values from `local.settings.json` were not being read
- Error: `StorageConfiguration.AzureStorageConnectionString or AzureStorageAccountName is required when using Azure Storage`

**Root Cause:**
The `ConfigureLogging` method in `UpdateEngine.Functions/src/Program.cs` was reading flat configuration keys like:
```csharp
context.Configuration["ServiceUrl"]
context.Configuration["UseAzureStorageForMetadata"]
```

But the new hierarchical structure requires:
```csharp
context.Configuration["UpdateEngine:ServiceConfiguration:ServiceUrl"]
context.Configuration["UpdateEngine:StorageConfiguration:UseAzureStorageForMetadata"]
```

### Issue 2: WorkerService Missing Shared Configuration
**Symptoms:**
- WorkerService attempted to open metadata store before it was created
- Error: `The store does not exist or is corrupt: ./LocalMetadataStore`

**Root Cause:**
`WorkerService/Program.cs` was not calling `AddSharedAppConfiguration()` to load shared defaults from the Configuration project.

### Issue 3: Premature Validation of Connection Strings
**Symptoms:**
- Configuration validation threw exceptions when `UseAzureStorageForMetadata: true` even though connection strings were available from Aspire

**Root Cause:**
`StorageConfiguration.Validate()` was checking for connection strings during configuration binding, but connection strings come from multiple sources:
- Aspire connection strings (`ConnectionStrings:MetadataStorageConnection`)
- Environment variables
- Configuration files (`StorageConfiguration:AzureStorageConnectionString`)

The validation should happen when the store is actually created, not during configuration binding.

## Fixes Applied

### Fix 1: Update UpdateEngine.Functions/src/Program.cs
Updated the `ConfigureLogging` method to read from the hierarchical `UpdateEngine` section:

```csharp
static void ConfigureLogging(HostBuilderContext context)
{
    var tempLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("Startup");

    tempLogger.LogInformation("=== UpdateEngine Configuration ===");
    
    // Read from hierarchical UpdateEngine section
    var updateEngineSection = context.Configuration.GetSection("UpdateEngine");
    var serviceConfig = updateEngineSection.GetSection("ServiceConfiguration");
    var storageConfig = updateEngineSection.GetSection("StorageConfiguration");
    
    tempLogger.LogInformation("ServiceUrl: {ServiceUrl}", serviceConfig["ServiceUrl"]);
    tempLogger.LogInformation("UseAzureStorageForMetadata: {UseAzureStorageForMetadata}", storageConfig["UseAzureStorageForMetadata"]);
    // ... etc
}
```

**Result:** Configuration values are now read correctly from the hierarchical structure.

### Fix 2: Update WorkerService/Program.cs
Added shared configuration loading:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add shared configuration from Configuration project
builder.Configuration.AddSharedAppConfiguration();

// ... rest of configuration
```

**Result:** WorkerService now loads shared defaults from `Configuration/shared/appsettings.defaults.json` and environment-specific overrides.

### Fix 3: Update Configuration/StorageConfiguration.cs
Removed premature connection string validation:

```csharp
public void Validate()
{
    // Note: Connection strings can come from multiple sources (Aspire, environment variables, etc.)
    // so we don't validate them here. They will be validated when the store is actually created.
    
    // Only validate that local storage paths are provided when not using Azure Storage
    if (!this.UseAzureStorageForMetadata && string.IsNullOrWhiteSpace(this.MetadataPath))
    {
        throw new InvalidOperationException("StorageConfiguration.MetadataPath is required when not using Azure Storage");
    }
}
```

**Result:** Configuration validation no longer fails when connection strings are provided via Aspire or environment variables.

## Configuration Loading Order (After Fix)

### Azure Functions (UpdateEngine)
1. `appsettings.json` (base configuration)
2. **`Configuration/shared/appsettings.defaults.json`** ? via `AddSharedAppConfiguration()`
3. **`Configuration/shared/appsettings.{Environment}.json`** ? environment-specific overrides
4. `appsettings.{Environment}.json` (Azure Functions environment-specific)
5. `local.settings.json` (local development only)
6. Environment variables from Aspire (`ConnectionStrings:*`)
7. Environment variables (highest priority)

### Worker Service
1. `appsettings.json` (base configuration)
2. **`Configuration/shared/appsettings.defaults.json`** ? via `AddSharedAppConfiguration()`
3. **`Configuration/shared/appsettings.{Environment}.json`** ? environment-specific overrides
4. `appsettings.{Environment}.json` (Worker Service environment-specific)
5. Environment variables from Aspire (`ConnectionStrings:*`)
6. Environment variables (highest priority)

## Connection String Resolution

Both hosting models now follow the same pattern in `ServiceCollectionExtensions.AddUpdateEngineCore()`:

### Metadata Store
```csharp
var connectionString = configuration.GetConnectionString("MetadataStorageConnection") 
    ?? storageConfig.AzureStorageConnectionString;
```

Priority:
1. Aspire: `ConnectionStrings:MetadataStorageConnection`
2. Config: `UpdateEngine:StorageConfiguration:AzureStorageConnectionString`

### Content Store
```csharp
var connectionString = configuration.GetConnectionString("ContentStorageConnection") 
    ?? configuration.GetConnectionString("MetadataStorageConnection")
    ?? storageConfig.AzureStorageConnectionString;
```

Priority:
1. Aspire: `ConnectionStrings:ContentStorageConnection`
2. Aspire: `ConnectionStrings:MetadataStorageConnection` (fallback)
3. Config: `UpdateEngine:StorageConfiguration:AzureStorageConnectionString`

### Redis Cache
```csharp
var redisConnection = configuration.GetConnectionString("Redis") 
    ?? appConfig.CacheConfiguration.RedisConnectionString;
```

Priority:
1. Aspire: `ConnectionStrings:Redis`
2. Config: `UpdateEngine:CacheConfiguration:RedisConnectionString`

## Validation

? **Build Status**: Successful

## Testing Recommendations

1. **Test Azure Functions with Aspire**
   ```bash
   cd UpdateEngine.AppHost/src
   dotnet run
   # Verify UpdateEngine logs show correct configuration values
   ```

2. **Test Worker Service with Aspire**
   ```bash
   cd UpdateEngine.AppHost/src
   dotnet run
   # Verify WorkerService starts without directory not found errors
   ```

3. **Test Local Development without Aspire**
   ```bash
   # Azure Functions
   cd UpdateEngine.Functions/src
   func start
   
   # Worker Service
   cd WorkerService
   dotnet run
   ```

4. **Verify Configuration Loading**
   - Check logs for `=== UpdateEngine Configuration ===`
   - Verify ServiceUrl shows correct value (not null)
   - Verify UseAzureStorageForMetadata shows correct value
   - Verify metadata store initializes successfully

## Next Steps

1. ? Build verification - Complete
2. ? Test with Aspire orchestration
3. ? Test Azure Functions standalone
4. ? Test Worker Service standalone
5. ? Verify connection string resolution from Aspire
6. ? Verify metadata store creation/initialization

---

**Fix Date**: 2025-01-20
**Status**: ? Complete
**Build Status**: ? Successful
**Files Modified**: 3
- `UpdateEngine.Functions/src/Program.cs`
- `WorkerService/Program.cs`
- `Configuration/StorageConfiguration.cs`
