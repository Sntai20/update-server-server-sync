# Configuration Documentation

This document describes all configuration settings for the Microsoft Update Server-Server Sync application.

## Configuration Best Practices Implemented

1. ✅ **Strongly Typed Classes** - All configuration uses strongly typed classes with IOptions<T>
2. ✅ **Centralized Configuration** - Single appsettings.json with environment overrides
3. ✅ **Dependency Injection** - Configuration classes registered with DI container
4. ✅ **Multiple Sources** - JSON files, environment variables, user secrets
5. ✅ **Secure Sensitive Data** - User secrets for development, Azure Key Vault for production
6. ✅ **Reload on Change** - appsettings.json supports runtime reload
7. ✅ **Validation** - Data annotations and startup validation
8. ✅ **Documentation** - This file documents all settings
9. ✅ **Immutable Settings** - Configuration objects are immutable after binding
10. ✅ **Environment-Specific Files** - Development and Production overrides
11. ✅ **No Hardcoding** - All values come from configuration providers
12. ✅ **Test Configuration** - Comprehensive tests for configuration binding

## Configuration Sections

### UpdateServer

Main service configuration for the update server.

```json
{
  "UpdateServer": {
    "ServiceUrl": "http://localhost:7071",          // Base URL for the service
    "ContentUrl": "http://localhost:7071/api/content", // URL for serving content
    "MaxUpdateCount": 1000,                         // Max updates per operation
    "SupportedCategories": [                        // Update categories to sync
      "Security Updates",
      "Critical Updates",
      "Feature Packs",
      "Updates",
      "Drivers"
    ],
    "SupportedLanguages": ["en", "en-US", "neutral", ""] // Language filtering
  }
}
```

**Validation Rules:**
- `ServiceUrl`: Required, must be valid URL
- `ContentUrl`: Required, must be valid URL  
- `MaxUpdateCount`: Required, must be 1-100000
- `SupportedCategories`: Array of category names
- `SupportedLanguages`: Array of language codes

### Storage

Configuration for metadata and content storage backends.

```json
{
  "Storage": {
    "MetadataPath": "./store",                      // Local path or Azure container
    "ContentPath": "./content",                     // Local path or Azure container
    "UseAzureStorageForMetadata": true,            // Use Azure Storage vs local
    "UseAzureStorageForContent": true,             // Use Azure Storage vs local
    "MetadataContainerName": "metadata",           // Azure container name
    "ContentContainerName": "content",             // Azure container name
    "ContentPathPrefix": "Content",                // Path prefix for content
    "ReindexOnStartup": false                      // Reindex metadata on startup
  }
}
```

**Validation Rules:**
- `MetadataPath`: Required
- `ContentPath`: Required
- `MetadataContainerName`: Must be valid Azure container name (3-63 chars, lowercase, alphanumeric + hyphens)
- `ContentContainerName`: Must be valid Azure container name (3-63 chars, lowercase, alphanumeric + hyphens)

### FunctionSchedules

Timer trigger schedules for Azure Functions (TimeSpan format).

```json
{
  "FunctionSchedules": {
    "SyncCritical": "02:00:00",                    // Critical updates every 2 hours
    "SyncComprehensive": "1.00:00:00",            // Full sync daily
    "SyncContent": "7.00:00:00",                  // Content sync weekly
    "HealthCheck": "00:15:00",                    // Health checks every 15 min
    "WeeklyMaintenance": "7.00:00:00"             // Maintenance weekly
  }
}
```

**Validation Rules:**
- All schedules: Required, must be valid TimeSpan format (e.g., "02:00:00" or "1.00:00:00")

### Features

Feature flags and behavior toggles.

```json
{
  "Features": {
    "EnableScheduledSync": true,                   // Enable automatic sync timers
    "EnableDetailedLogging": false,               // Enable verbose logging
    "EnableMetrics": true,                        // Enable metrics collection
    "EnableCaching": true                         // Enable response caching
  }
}
```

### AzureWebJobs

Azure Functions specific configuration for disabling individual functions.

```json
{
  "AzureWebJobs": {
    "SyncCritical": { "Disabled": false },        // Critical sync function
    "SyncComprehensive": { "Disabled": true },    // Comprehensive sync function
    "SyncContent": { "Disabled": false },         // Content sync function
    "HealthCheck": { "Disabled": false }          // Health check function
  }
}
```

## Environment-Specific Configuration

### Development (appsettings.Development.json)

Optimized for fast development cycles:
- Smaller `MaxUpdateCount` (5 vs 1000)
- Faster sync schedules (30s vs 2h)
- Development-specific container names
- `ReindexOnStartup: true` for clean state
- Detailed logging enabled
- Some heavy functions disabled

### Production (appsettings.Production.json)

Optimized for production workloads:
- Full `MaxUpdateCount` (1000+)
- Production sync schedules
- Production container names
- Metrics and monitoring enabled
- All functions enabled

## Security Configuration

### User Secrets (Development)

Store sensitive development values using user secrets:

```bash
# Initialize user secrets
dotnet user-secrets init --project AppHost/src

# Set development connection strings
dotnet user-secrets set "ConnectionStrings:MetadataStorage" "development-connection-string"
dotnet user-secrets set "ConnectionStrings:ContentStorage" "development-connection-string"
```

### Environment Variables (Production)

Production secrets via environment variables:

```bash
ConnectionStrings__MetadataStorage=production-connection-string
ConnectionStrings__ContentStorage=production-connection-string
UpdateServer__ServiceUrl=https://production.example.com
```

### Azure Key Vault (Production)

For production, use Azure Key Vault for sensitive values:

```csharp
builder.Configuration.AddAzureKeyVault(
    "https://your-keyvault.vault.azure.net/",
    new DefaultAzureCredential());
```

## Configuration Loading Order

1. `appsettings.json` (base configuration)
2. `appsettings.{Environment}.json` (environment overrides)
3. Environment variables (deployment overrides)
4. User secrets (development only)
5. Azure Key Vault (production only)
6. Command line arguments (highest priority)

## Validation

Configuration is validated at two levels:

1. **Data Annotations**: Attribute-based validation (Required, Range, Url, etc.)
2. **Custom Validation**: Business logic validation in `ValidateConfiguration()` methods

Validation occurs:
- At startup (`ValidateOnStart()`)
- When options are first accessed
- During configuration binding

## Testing

Configuration tests verify:
- Proper binding of all configuration sections
- Validation rules work correctly
- Invalid configurations fail as expected
- Environment-specific overrides work

Run configuration tests:
```bash
dotnet test test/Configuration.Tests/
```

## Environment Variables

All configuration can be overridden with environment variables using double underscore syntax:

```bash
UpdateServer__MaxUpdateCount=500
Storage__UseAzureStorageForMetadata=true
FunctionSchedules__SyncCritical=01:00:00
```