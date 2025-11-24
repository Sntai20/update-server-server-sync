# Configuration Troubleshooting Guide

## Quick Diagnostics

### Check if Configuration is Loading

**Look for these log entries:**

```
=== UpdateEngine Configuration ===
ServiceUrl: http://localhost:7071
UseAzureStorageForMetadata: True
UseAzureStorageForContent: True
MetadataContainerName: data
ContentContainerName: data
```

**If you see:**
- `ServiceUrl: (null)` ? Configuration not loading from hierarchical structure
- `UseAzureStorageForMetadata: False` when it should be `True` ? Check configuration files

### Common Issues and Solutions

## Issue: "ServiceUrl: (null)" in Logs

**Symptom:**
```
ServiceUrl: (null)
UseAzureStorageForMetadata: False
```

**Cause:** Configuration is not loading from the hierarchical `UpdateEngine` section.

**Solution:**
Ensure configuration files use the hierarchical structure:

```json
{
  "UpdateEngine": {
    "ServiceConfiguration": {
      "ServiceUrl": "http://localhost:7071"
    },
    "StorageConfiguration": {
      "UseAzureStorageForMetadata": true
    }
  }
}
```

**Verify in code:**
```csharp
// Correct - reads from hierarchical structure
var serviceConfig = configuration.GetSection("UpdateEngine:ServiceConfiguration");
var serviceUrl = serviceConfig["ServiceUrl"];

// Wrong - reads from flat structure
var serviceUrl = configuration["ServiceUrl"];
```

## Issue: "StorageConfiguration.AzureStorageConnectionString or AzureStorageAccountName is required"

**Symptom:**
```
System.InvalidOperationException: StorageConfiguration.AzureStorageConnectionString or AzureStorageAccountName is required when using Azure Storage
```

**Cause:** Configuration validation runs before Aspire provides connection strings.

**Solution:**
Connection strings can come from multiple sources:

1. **Aspire** (recommended for local development):
   ```csharp
   // AppHost configures connection strings via environment
   builder.AddAzureStorage("storage")
   ```

2. **Environment Variables**:
   ```bash
   ConnectionStrings__MetadataStorageConnection="..."
   ```

3. **Configuration File**:
   ```json
   {
     "UpdateEngine": {
       "StorageConfiguration": {
         "AzureStorageConnectionString": "..."
       }
     }
   }
   ```

**Priority Order:**
1. `ConnectionStrings:MetadataStorageConnection` (Aspire)
2. `UpdateEngine:StorageConfiguration:AzureStorageConnectionString`

## Issue: "The store does not exist or is corrupt: ./LocalMetadataStore"

**Symptom:**
```
System.IO.DirectoryNotFoundException: The store does not exist or is corrupt: ./LocalMetadataStore
```

**Cause:** Metadata store directory doesn't exist when using local file system storage.

**Solution:**

1. **Check configuration:**
   ```json
   {
     "UpdateEngine": {
       "StorageConfiguration": {
         "UseAzureStorageForMetadata": false,
         "MetadataPath": "./LocalMetadataStore"
       }
     }
   }
   ```

2. **Create directory manually:**
   ```bash
   mkdir LocalMetadataStore
   ```

3. **Or use Aspire storage** (automatically creates containers):
   ```json
   {
     "UpdateEngine": {
       "StorageConfiguration": {
         "UseAzureStorageForMetadata": true,
         "MetadataContainerName": "data"
       }
     }
   }
   ```

## Issue: Shared Configuration Not Loading

**Symptom:**
- Configuration from `Configuration/shared/appsettings.defaults.json` is not being applied
- Default values are missing

**Cause:** `AddSharedAppConfiguration()` not called in Program.cs

**Solution:**

**Azure Functions:**
```csharp
var hostBuilder = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((context, config) =>
    {
        // Add this line
        config.AddSharedAppConfiguration();
    });
```

**Worker Service:**
```csharp
var builder = WebApplication.CreateBuilder(args);

// Add this line
builder.Configuration.AddSharedAppConfiguration();
```

## Verification Steps

### 1. Check Configuration File Structure

All configuration files should use hierarchical structure:

```json
{
  "UpdateEngine": {
    "ServiceConfiguration": { },
    "StorageConfiguration": { },
    "SyncConfiguration": { },
    "FeatureFlags": { },
    "CacheConfiguration": { }
  }
}
```

### 2. Verify Configuration Loading Order

**Expected order:**
1. Base `appsettings.json`
2. Shared `Configuration/shared/appsettings.defaults.json`
3. Shared `Configuration/shared/appsettings.{Environment}.json`
4. App-specific `appsettings.{Environment}.json`
5. Aspire connection strings (`ConnectionStrings:*`)
6. Environment variables

### 3. Check Logs on Startup

Look for these key log entries:

**UpdateEngine:**
```
=== UpdateEngine Configuration ===
ServiceUrl: http://localhost:7071
UseAzureStorageForMetadata: True
Initializing storage services...
Metadata store initialized successfully
```

**WorkerService:**
```
Metadata Store Configuration:
  UseAzureStorageForMetadata: False
  MetadataPath: ./LocalMetadataStore
  Connection String from Aspire: True
Opening local file system metadata store at: ./LocalMetadataStore
```

### 4. Test Configuration Binding

Add temporary logging to verify configuration:

```csharp
var appConfig = configuration.BindToAppConfig();
logger.LogInformation("ServiceUrl: {ServiceUrl}", appConfig.ServiceConfiguration.ServiceUrl);
logger.LogInformation("MetadataPath: {MetadataPath}", appConfig.StorageConfiguration.MetadataPath);
logger.LogInformation("UseAzureStorage: {UseAzure}", appConfig.StorageConfiguration.UseAzureStorageForMetadata);
```

## Configuration File Locations

### Azure Functions
- `UpdateEngine.Functions/src/appsettings.json`
- `UpdateEngine.Functions/src/appsettings.Development.json`
- `UpdateEngine.Functions/src/local.settings.json` (local only)

### Worker Service
- `WorkerService/appsettings.json`
- `WorkerService/appsettings.Development.json`

### Shared Configuration
- `Configuration/shared/appsettings.defaults.json` (defaults for all environments)
- `Configuration/shared/appsettings.Development.json` (dev overrides)
- `Configuration/shared/appsettings.Production.json` (prod overrides)

## Environment Variables

### Aspire Connection Strings
```bash
ConnectionStrings__MetadataStorageConnection="..."
ConnectionStrings__ContentStorageConnection="..."
ConnectionStrings__Redis="localhost:6379"
```

### Override Configuration Values
```bash
UpdateEngine__ServiceConfiguration__ServiceUrl="http://localhost:7071"
UpdateEngine__StorageConfiguration__UseAzureStorageForMetadata="true"
UpdateEngine__StorageConfiguration__MetadataPath="./data"
```

## PowerShell Testing Script

```powershell
# Test-Configuration.ps1
# Quick script to verify configuration is loading correctly

Write-Host "Testing UpdateEngine Configuration..." -ForegroundColor Cyan

# Check if configuration files exist
$files = @(
    "UpdateEngine/src/appsettings.json",
    "UpdateEngine/src/local.settings.json",
    "Configuration/shared/appsettings.defaults.json",
    "WorkerService/appsettings.json"
)

foreach ($file in $files) {
    if (Test-Path $file) {
        Write-Host "? Found: $file" -ForegroundColor Green
        
        # Check if it uses hierarchical structure
        $content = Get-Content $file -Raw | ConvertFrom-Json
        if ($content.PSObject.Properties.Name -contains "UpdateEngine") {
            Write-Host "  ? Uses hierarchical structure" -ForegroundColor Green
        } else {
            Write-Host "  ? Missing UpdateEngine section" -ForegroundColor Red
        }
    } else {
        Write-Host "? Missing: $file" -ForegroundColor Red
    }
}

# Try to start services
Write-Host "`nStarting Aspire AppHost..." -ForegroundColor Cyan
Push-Location AppHost/src
try {
    dotnet run --no-build
} finally {
    Pop-Location
}
```

## Related Documentation

- [Configuration Migration Summary](CONFIGURATION_MIGRATION_SUMMARY.md)
- [Configuration Loading Fixes](CONFIGURATION_LOADING_FIXES.md)
- [Storage Guide](STORAGE_GUIDE.md)
- [Troubleshooting Storage](TROUBLESHOOTING_STORAGE.md)

---

**Last Updated**: 2025-01-20
**Status**: Current
