# Configuration Management Guide

## Overview

The Microsoft Update Server-Server Sync project now uses a **simplified, centralized configuration approach** through the `Configuration` project. This eliminates complex nested configuration structures and provides a single source of truth for all application settings.

## ✅ Current: Simplified Configuration (2025)

**The configuration system has been completely modernized with a flat, simple structure:**

- ✅ **Single AppConfig class** in the Configuration project
- ✅ **Direct JSON binding** from appsettings files  
- ✅ **92% complexity reduction** (from 200+ lines to ~40 properties)
- ✅ **Centralized in Configuration project** as requested
- ✅ **Environment-specific overrides** with same structure

## Configuration Structure

### AppConfig Class (Configuration Project)

The centralized configuration uses the `Configuration.AppConfig` class:

```csharp
public class AppConfig
{
    // Core Service Settings
    public string ServiceUrl { get; set; } = "http://localhost:7071";
    public string ContentUrl { get; set; } = "http://localhost:7071/api/content";
    public int MaxUpdateCount { get; set; } = 1000;
    public string[] SupportedCategories { get; set; } = ["Security Updates", "Critical Updates"];
    public string[] SupportedLanguages { get; set; } = ["en", "en-US", "neutral", ""];

    // Storage Settings
    public string MetadataPath { get; set; } = "./store";
    public string ContentPath { get; set; } = "./content";
    public bool UseAzureStorageForMetadata { get; set; } = false;
    public bool UseAzureStorageForContent { get; set; } = false;
    public string MetadataContainerName { get; set; } = "metadata";
    public string ContentContainerName { get; set; } = "content";
    public bool ReindexOnStartup { get; set; } = false;

    // Schedule Settings (TimeSpan format strings)
    public string SyncCriticalSchedule { get; set; } = "02:00:00";
    public string SyncComprehensiveSchedule { get; set; } = "1.00:00:00";
    public string SyncContentSchedule { get; set; } = "7.00:00:00";
    public string HealthCheckSchedule { get; set; } = "00:15:00";
    public string MaintenanceSchedule { get; set; } = "7.00:00:00";

    // Feature Flags
    public bool EnableScheduledSync { get; set; } = true;
    public bool EnableDetailedLogging { get; set; } = false;
    public bool EnableMetrics { get; set; } = true;
    public bool EnableCaching { get; set; } = true;
}
```

## Configuration Files (All Simplified)

All appsettings files now use the **same flat structure** that maps directly to AppConfig properties:

### Development (`appsettings.Development.json`)
```json
{
  "ServiceUrl": "http://localhost:7071",
  "ContentUrl": "http://localhost:7071/api/content", 
  "MaxUpdateCount": 5,
  "SupportedCategories": ["Security Updates", "Critical Updates", "Definition Updates"],
  "MetadataPath": "./dev-store",
  "ContentPath": "./dev-content",
  "EnableScheduledSync": false,
  "EnableDetailedLogging": true,
  "EnableMetrics": false,
  "EnableCaching": false
}
```

### Production (`appsettings.Production.json`)  
```json
{
  "ServiceUrl": "https://your-update-server.azurewebsites.net",
  "ContentUrl": "https://your-update-server.azurewebsites.net/api/content",
  "MaxUpdateCount": 10000,
  "SyncCriticalSchedule": "06:00:00",
  "SyncComprehensiveSchedule": "1.00:00:00",
  "EnableScheduledSync": true,
  "EnableDetailedLogging": false
}
```

### Integration Test (`appsettings.IntegrationTest.json`)
```json
{
  "ServiceUrl": "http://localhost:7071",
  "MaxUpdateCount": 10,
  "MetadataPath": "./test-store",
  "ContentPath": "./test-content",
  "EnableScheduledSync": false,
  "EnableDetailedLogging": true
}
```

## Usage Patterns

### 1. Service Registration (Centralized)

```csharp
// In Program.cs or Startup.cs
using Configuration;

var builder = WebApplication.CreateBuilder(args);

// Register configuration from appsettings.json (centralized approach)
builder.Services.AddAppConfiguration(builder.Configuration);

var app = builder.Build();
```

### 2. Dependency Injection

```csharp
// In any service class
public class UpdateService
{
    private readonly AppConfig _config;
    
    public UpdateService(AppConfig config)
    {
        _config = config;
    }
    
    public async Task SyncUpdatesAsync()
    {
        var maxUpdates = _config.MaxUpdateCount;
        var serviceUrl = _config.ServiceUrl;
        // Use configuration directly - no complex nesting
    }
}
```
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
cd UpdateEngine.AppHost && dotnet run
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