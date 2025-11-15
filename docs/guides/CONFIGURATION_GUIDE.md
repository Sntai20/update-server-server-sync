# UpdateEngine Configuration Guide

## Configuration Overview

The UpdateEngine uses a **production-first** configuration approach with environment-specific overrides.

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
    "UseLocalStorageForMetadata": "true",     // Override production default
    "UseLocalStorageForContent": "true",      // Use local file system
    "MetadataStorePath": "../../store",       // Local path
    "SyncMetadataCriticalSchedule": "00:02:00" // Fast testing schedule
  }
}
```

### **Aspire Development**

**Behavior**: AppHost provides `AzureWebJobsStorage` connection to Azurite container
- Overrides local storage settings automatically
- Uses containerized Azurite storage emulator
- Fast development schedules maintained

### **Production Deployment**

**Configuration Method**: Azure Portal Application Settings or deployment templates

**Example Azure CLI**:
```bash
az functionapp config appsettings set \
  --name myupdateserver \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    UseLocalStorageForMetadata=false \        # Production default
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