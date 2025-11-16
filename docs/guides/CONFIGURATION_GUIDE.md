# UpdateEngine Configuration Guide

## Configuration Overview

The UpdateEngine uses a **simplified configuration approach** with direct IConfiguration binding.

## ⚠️ Important: Simplified Configuration

**As of .NET 9.0 upgrade, the complex mutable/immutable configuration pattern has been replaced with a simple POCO approach using `AppConfig` class.**

- ✅ **New**: Single `AppConfig` class with direct property binding  
- ✅ **New**: Standard .NET IConfiguration patterns
- ❌ **Removed**: Complex ServiceConfigurationMutable, SyncConfigMutable, etc.
- ❌ **Removed**: Mutable/immutable conversion methods

## Configuration Structure

### AppConfig Class

The simplified configuration uses a single `Configuration.AppConfig` class:

```csharp
public class AppConfig
{
    public string MetadataStorePath { get; set; } = "./store";
    public string? ContentStorePath { get; set; }
    public string ServiceUrl { get; set; } = "http://localhost:7071";
    public int HealthCheckIntervalMinutes { get; set; } = 5;
    public int SyncIntervalMinutes { get; set; } = 60;
    public bool EnableScheduledSync { get; set; } = true;
    // ... other properties
}
```

## Configuration Files

### **Development Only**
- `local.settings.json` - Local development configuration (ignored in production)
- `appsettings.Development.json` - Development-specific settings

### **Production Only** 
- `appsettings.Production.json` - Production-specific settings
- **Application Settings** - Configured in Azure Portal or deployment templates

## Environment Behavior

### **Local Development**

**File**: `local.settings.json`
```json
{
  "Values": {
    "MetadataStorePath": "./store",
    "ContentStorePath": "./content", 
    "ServiceUrl": "http://localhost:7071",
    "EnableScheduledSync": "true",
    "SyncIntervalMinutes": "60",
    "AzureWebJobsStorage": ""
  }
}
```

### **Aspire Development**

**Behavior**: AppHost provides `AzureWebJobsStorage` connection to Azurite container
- Uses containerized Azurite storage emulator for scale testing
- Configuration bound to AppConfig automatically
- Fast development schedules maintained

### **Production Deployment**

**Configuration Method**: Azure Portal Application Settings or deployment templates

**Example Azure CLI**:
```bash
az functionapp config appsettings set \
  --name myupdateserver \
  --settings \
    MetadataStorePath="/app/store" \
    ServiceUrl="https://myupdateserver.azurewebsites.net" \
    EnableScheduledSync=true
    'ServiceConfigurationJson={"ServiceUrl":"https://myupdateserver.azurewebsites.net"}' \
    SyncMetadataCriticalSchedule=02:00:00      # Production schedule

az functionapp config connection-string set \
  --name myupdateserver \
  --settings \
    MetadataStorageConnection="DefaultEndpointsProtocol=https;AccountName=..." \
    ContentStorageConnection="DefaultEndpointsProtocol=https;AccountName=..."
```

## Connection String Priority

The UpdateEngine resolves storage connections in this order:

1. **`MetadataStorageConnection`** (explicit connection string)
2. **`AzureWebJobsStorage`** (Functions runtime / Aspire)  
3. **`AZURE_STORAGE_CONNECTION_STRING`** (environment variable)
4. **Local file system** (if `UseLocalStorageForMetadata=true`)

## Configuration Validation

### **Development**
```bash
# Local development
func start --verbose

# Aspire development  
cd AppHost && dotnet run
```

### **Production**
```bash
# Check Application Settings
az functionapp config appsettings list --name myupdateserver

# Check Connection Strings
az functionapp config connection-string list --name myupdateserver

# Test health endpoint
curl https://myupdateserver.azurewebsites.net/api/HealthCheck
```

## Key Principles

1. **Production-First**: Default behavior optimized for production deployment
2. **Development Override**: Local settings override production defaults
3. **Environment Detection**: Automatic optimization based on `ASPNETCORE_ENVIRONMENT`
4. **No Mixed Files**: `local.settings.json` is development only, never deployed

---

**Configuration is now production-optimized with seamless development support!** 🎯