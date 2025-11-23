# Configuration Guide - Microsoft Update Server-Server Sync

## Overview

This guide explains how configuration works across the Update Server-Server Sync solution, including the hierarchical loading system, available settings, and best practices for different deployment scenarios.

## Configuration Architecture

### Hierarchical Configuration System

The solution uses .NET's built-in configuration system with the following precedence (highest to lowest):

```
1. Environment Variables (highest priority)
   ?
2. local.settings.json (Azure Functions only, local development)
   ?
3. appsettings.{Environment}.json (per-project + shared)
   ?
4. appsettings.json (per-project base)
   ?
5. Code Defaults (in Configuration model classes - lowest priority)
```

**Important**: Higher priority sources override lower priority sources. For example, an environment variable will override the same setting in `appsettings.json`.

### Configuration Layers

#### 1. Shared Configuration (`UpdateEngine.Configuration\src\shared\`)

Team-wide defaults that apply to all projects:

- **`appsettings.Development.json`**: Fast intervals for local development
- **`appsettings.Production.json`**: Production-safe slow intervals

Projects load shared configuration using the `AddSharedAppConfiguration()` extension method.

#### 2. Project-Specific Configuration

Each project has its own `appsettings.json` and environment-specific overrides:

- **UpdateEngine.Functions**: Azure Functions service
- **UpdateEngine.AppHost**: .NET Aspire orchestrator
- **UpdateEngine.WorkerService**: Background service
- **UpdateEngine.Cli**: Command-line tool

#### 3. Local Developer Overrides

- **`local.settings.json`**: Machine-specific settings (Azure Functions only)
- **User secrets**: Sensitive data (development only)
- **Environment variables**: Runtime overrides

---

## Configuration Sections

### ServiceConfiguration

Controls service URLs, upstream connections, and update limits.

```json
{
  "UpdateEngine": {
    "ServiceConfiguration": {
      "ServiceUrl": "http://localhost:7071",
      "ContentUrl": "http://localhost:7071/api/content",
      "UpstreamServerUrl": "https://fe2.update.microsoft.com/v6/windowsupdate/Services/",
      "MaxConcurrentSyncs": 5,
      "RequestTimeoutSeconds": 300,
      "MaxUpdateCount": 1000,
      "SupportedCategories": [
        "Security Updates",
        "Critical Updates",
        "Updates"
      ],
      "SupportedLanguages": [
        "en",
        "en-US",
        "neutral",
        ""
      ]
    }
  }
}
```

**Properties:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ServiceUrl` | string | `http://localhost:7071` | Base URL for the update service |
| `ContentUrl` | string | `http://localhost:7071/api/content` | URL for content downloads |
| `UpstreamServerUrl` | string | Microsoft Update URL | Upstream server to sync from |
| `MaxConcurrentSyncs` | int | `5` | Maximum parallel sync operations |
| `RequestTimeoutSeconds` | int | `300` | HTTP request timeout |
| `MaxUpdateCount` | int | `1000` | Maximum updates per sync |
| `SupportedCategories` | string[] | See defaults | Update categories to sync |
| `SupportedLanguages` | string[] | See defaults | Supported language codes |

---

### StorageConfiguration

Controls where metadata and content files are stored (local filesystem or Azure Blob Storage).

```json
{
  "UpdateEngine": {
    "StorageConfiguration": {
      "MetadataPath": "./LocalMetadataStore",
      "ContentPath": "./LocalContentStore",
      "UseAzureStorageForMetadata": false,
      "UseAzureStorageForContent": false,
      "MetadataContainerName": "data",
      "ContentContainerName": "data",
      "ContentPathPrefix": "Content",
      "ReindexOnStartup": false
    }
  }
}
```

**Properties:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MetadataPath` | string | `./LocalMetadataStore` | Local filesystem path for metadata |
| `ContentPath` | string | `./LocalContentStore` | Local filesystem path for content files |
| `UseAzureStorageForMetadata` | bool | `false` | Use Azure Blob Storage for metadata |
| `UseAzureStorageForContent` | bool | `false` | Use Azure Blob Storage for content |
| `MetadataContainerName` | string | `data` | Azure container name for metadata |
| `ContentContainerName` | string | `data` | Azure container name for content |
| `ContentPathPrefix` | string | `Content` | Path prefix within Azure container |
| `ReindexOnStartup` | bool | `false` | Rebuild metadata index on startup |

**Local Development:**
```json
{
  "StorageConfiguration": {
    "MetadataPath": "./LocalMetadataStore",
    "ContentPath": "./LocalContentStore",
    "UseAzureStorageForMetadata": false,
    "UseAzureStorageForContent": false
  }
}
```

**Azure Deployment:**
```json
{
  "StorageConfiguration": {
    "UseAzureStorageForMetadata": true,
    "UseAzureStorageForContent": true,
    "MetadataContainerName": "metadata",
    "ContentContainerName": "content"
  }
}
```

**Connection String Configuration:**
```json
{
  "ConnectionStrings": {
    "MetadataStorageConnection": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=key;EndpointSuffix=core.windows.net",
    "ContentStorageConnection": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=key;EndpointSuffix=core.windows.net"
  }
}
```

---

### SyncConfiguration

Controls timer schedules for automated sync operations (Azure Functions only).

```json
{
  "UpdateEngine": {
    "SyncConfiguration": {
      "SyncCriticalSchedule": "0 */3 * * * *",
      "SyncComprehensiveSchedule": "0 */6 * * * *",
      "SyncContentSchedule": "0 */9 * * * *",
      "ScheduledHealthCheckSchedule": "0 */5 * * * *",
      "MaintenanceSchedule": "0 0 */1 * * *",
      "AnomalyDetectionSchedule": "0 */10 * * * *",
      "EnableScheduledSync": true
    }
  }
}
```

**Properties (CRON Format):**

| Property | Type | Development Default | Production Default | Description |
|----------|------|---------------------|-------------------|-------------|
| `SyncCriticalSchedule` | string | `0 */3 * * * *` (3 min) | `0 0 */6 * * *` (6 hours) | Critical updates sync |
| `SyncComprehensiveSchedule` | string | `0 */6 * * * *` (6 min) | `0 0 2 * * *` (daily 2 AM) | All updates sync |
| `SyncContentSchedule` | string | `0 */9 * * * *` (9 min) | `0 0 1 * * *` (daily 1 AM) | Content files sync |
| `ScheduledHealthCheckSchedule` | string | `0 */5 * * * *` (5 min) | `0 */5 * * * *` (5 min) | Health monitoring |
| `MaintenanceSchedule` | string | `0 0 */1 * * *` (hourly) | `0 0 3 */7 * *` (weekly 3 AM) | Database maintenance |
| `AnomalyDetectionSchedule` | string | `0 */10 * * * *` (10 min) | `0 0 */2 * * *` (2 hours) | Anomaly detection |
| `EnableScheduledSync` | bool | `true` | `true` | Enable/disable all timers |

**CRON Format (6-part Azure Functions format):**
```
???????????????? second (0-59)
? ?????????????? minute (0-59)
? ? ???????????? hour (0-23)
? ? ? ?????????? day of month (1-31)
? ? ? ? ???????? month (1-12)
? ? ? ? ? ?????? day of week (0-6, Sunday=0)
? ? ? ? ? ?
* * * * * *
```

**Examples:**
- `0 */5 * * * *` = Every 5 minutes
- `0 0 */2 * * *` = Every 2 hours
- `0 0 2 * * *` = Daily at 2 AM
- `0 0 3 */7 * *` = Weekly (every 7 days) at 3 AM

**Disabling Timers:**
```json
{
  "SyncConfiguration": {
    "EnableScheduledSync": false
  }
}
```

---

### FeatureFlags

Runtime toggles for enabling/disabling features without code changes.

```json
{
  "UpdateEngine": {
    "FeatureFlags": {
      "EnableEmergencySync": true,
      "EnableComprehensiveSync": true,
      "EnableContentSync": true,
      "EnableAnomalyDetection": false,
      "EnableDeepHealthCheck": true,
      "EnableMaintenance": true,
      "EnableDetailedLogging": false,
      "EnableMetrics": true,
      "EnableOpenTelemetry": true,
      "EnableCaching": true,
      "EnableCompression": true,
      "EnableExperimentalFeatures": false
    }
  }
}
```

**Categories:**

**Sync Features:**
- `EnableEmergencySync`: Emergency critical updates sync
- `EnableComprehensiveSync`: Full catalog sync
- `EnableContentSync`: Download actual update files

**Monitoring Features:**
- `EnableAnomalyDetection`: Detect unusual sync patterns
- `EnableDeepHealthCheck`: Comprehensive health checks
- `EnableMaintenance`: Scheduled maintenance tasks

**Logging & Telemetry:**
- `EnableDetailedLogging`: Verbose debug logging (?? performance impact)
- `EnableMetrics`: Prometheus/metrics collection
- `EnableOpenTelemetry`: Distributed tracing

**Performance Features:**
- `EnableCaching`: In-memory and distributed caching
- `EnableCompression`: HTTP response compression

**Experimental:**
- `EnableExperimentalFeatures`: Unreleased features

**Development vs Production:**
```json
// Development
{
  "FeatureFlags": {
    "EnableDetailedLogging": true,
    "EnableMetrics": false,
    "EnableCaching": false
  }
}

// Production
{
  "FeatureFlags": {
    "EnableDetailedLogging": false,
    "EnableMetrics": true,
    "EnableCaching": true
  }
}
```

---

### CacheConfiguration

Distributed caching settings (Redis).

```json
{
  "UpdateEngine": {
    "CacheConfiguration": {
      "EnableDistributedCache": false,
      "RedisConnectionString": "localhost:6379",
      "DefaultExpirationMinutes": 60,
      "StatisticsCacheMinutes": 5,
      "UpdateDetailsCacheMinutes": 60,
      "ContentAvailabilityCacheMinutes": 15,
      "InvalidateOnSync": true,
      "KeyPrefix": "msupdate:"
    }
  }
}
```

**Properties:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableDistributedCache` | bool | `false` | Enable Redis caching |
| `RedisConnectionString` | string | `localhost:6379` | Redis connection |
| `DefaultExpirationMinutes` | int | `60` | Default TTL |
| `StatisticsCacheMinutes` | int | `5` | Stats cache TTL |
| `UpdateDetailsCacheMinutes` | int | `60` | Update details TTL |
| `ContentAvailabilityCacheMinutes` | int | `15` | Content availability TTL |
| `InvalidateOnSync` | bool | `true` | Clear cache after sync |
| `KeyPrefix` | string | `msupdate:` | Cache key prefix |

**Local Development (No Redis):**
```json
{
  "CacheConfiguration": {
    "EnableDistributedCache": false
  }
}
```

**Production (With Redis):**
```json
{
  "CacheConfiguration": {
    "EnableDistributedCache": true,
    "RedisConnectionString": "mycache.redis.cache.windows.net:6380,password=key,ssl=True,abortConnect=False",
    "KeyPrefix": "msupdate:prod:"
  }
}
```

---

## Configuration by Scenario

### Scenario 1: Local Development (Default)

**Characteristics:**
- Local filesystem storage
- Fast sync intervals (3-10 minutes)
- Detailed logging enabled
- No Redis caching
- Aspire orchestration

**Files:**
- `UpdateEngine.Configuration\src\shared\appsettings.Development.json`
- `UpdateEngine.Functions\src\local.settings.json`

**Example local.settings.json:**
```json
{
  "IsEncrypted": false,
  "Values": {
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AzureWebJobsStorage": ""
  },
  "UpdateEngine": {
    "ServiceConfiguration": {
      "ServiceUrl": "http://localhost:7071",
      "ContentUrl": "http://localhost:7071/api/content",
      "SupportedLanguages": ["en", "en-US", "neutral", ""]
    },
    "StorageConfiguration": {
      "MetadataPath": "./LocalMetadataStore",
      "ContentPath": "./LocalContentStore"
    },
    "FeatureFlags": {
      "EnableDetailedLogging": false,
      "EnableMetrics": true,
      "EnableCaching": true
    },
    "CacheConfiguration": {
      "EnableDistributedCache": false,
      "KeyPrefix": "msupdate:",
      "DefaultExpirationMinutes": 60
    }
  }
}
```

**Start with Aspire:**
```powershell
cd UpdateEngine.AppHost\src
dotnet run
```

---

### Scenario 2: Azure Functions Development

**Characteristics:**
- Azure Blob Storage (Azurite locally)
- Same fast sync intervals
- Connection strings from Aspire

**Environment Variables (set by Aspire):**
```
AzureWebJobsStorage=UseDevelopmentStorage=true
MetadataStorageConnection=UseDevelopmentStorage=true
ContentStorageConnection=UseDevelopmentStorage=true
```

**Shared Configuration:**
```json
{
  "UpdateEngine": {
    "StorageConfiguration": {
      "UseAzureStorageForMetadata": true,
      "UseAzureStorageForContent": true,
      "MetadataContainerName": "data",
      "ContentContainerName": "data",
      "ReindexOnStartup": true
    }
  }
}
```

---

### Scenario 3: Azure Production Deployment

**Characteristics:**
- Azure Blob Storage (production accounts)
- Slow sync intervals (hours/days)
- Minimal logging
- Redis caching enabled
- Connection strings in Key Vault

**appsettings.Production.json:**
```json
{
  "UpdateEngine": {
    "ServiceConfiguration": {
      "ServiceUrl": "https://myupdateserver.azurewebsites.net",
      "MaxUpdateCount": 10000
    },
    "StorageConfiguration": {
      "UseAzureStorageForMetadata": true,
      "UseAzureStorageForContent": true,
      "MetadataContainerName": "metadata",
      "ContentContainerName": "content"
    },
    "SyncConfiguration": {
      "SyncCriticalSchedule": "0 0 */6 * * *",
      "SyncComprehensiveSchedule": "0 0 2 * * *",
      "SyncContentSchedule": "0 0 1 * * *",
      "MaintenanceSchedule": "0 0 3 */7 * *"
    },
    "FeatureFlags": {
      "EnableDetailedLogging": false,
      "EnableMetrics": true,
      "EnableCaching": true
    },
    "CacheConfiguration": {
      "EnableDistributedCache": true,
      "RedisConnectionString": "",
      "KeyPrefix": "msupdate:prod:"
    }
  },
  "ConnectionStrings": {
    "MetadataStorageConnection": "",
    "ContentStorageConnection": "",
    "RedisConnectionString": ""
  }
}
```

**Azure App Settings (Key Vault References):**
```
ConnectionStrings__MetadataStorageConnection=@Microsoft.KeyVault(SecretUri=https://myvault.vault.azure.net/secrets/MetadataStorage/)
ConnectionStrings__ContentStorageConnection=@Microsoft.KeyVault(SecretUri=https://myvault.vault.azure.net/secrets/ContentStorage/)
CacheConfiguration__RedisConnectionString=@Microsoft.KeyVault(SecretUri=https://myvault.vault.azure.net/secrets/RedisConnection/)
```

---

### Scenario 4: Custom Testing Environment

**Characteristics:**
- Test specific configurations
- Override specific timers
- Mock external dependencies

**Environment Variables Override:**
```powershell
# PowerShell
$env:UpdateEngine__SyncConfiguration__EnableScheduledSync = "false"
$env:UpdateEngine__FeatureFlags__EnableDetailedLogging = "true"
$env:UpdateEngine__ServiceConfiguration__UpstreamServerUrl = "http://localhost:5000/mock"

dotnet run
```

**Linux/macOS:**
```bash
export UpdateEngine__SyncConfiguration__EnableScheduledSync=false
export UpdateEngine__FeatureFlags__EnableDetailedLogging=true
export UpdateEngine__ServiceConfiguration__UpstreamServerUrl=http://localhost:5000/mock

dotnet run
```

---

## Configuration Loading

### Azure Functions (`UpdateEngine.Functions`)

The Functions project loads configuration in this order:

```csharp
// Program.cs
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((context, config) =>
    {
        var env = context.HostingEnvironment;
        
        // 1. Load base appsettings.json
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        
        // 2. Load environment-specific appsettings
        config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
        
        // 3. Load SHARED configuration (team-wide defaults)
        config.AddSharedAppConfiguration();
        
        // 4. Load local.settings.json (local dev only)
        config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
        
        // 5. Add environment variables (highest priority)
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        // Bind configuration to strongly-typed models
        services.Configure<AppConfig>(
            context.Configuration.GetSection(AppConfig.SectionName));
        
        // Register services
        services.AddMicrosoftUpdateServices(context.Configuration);
    })
    .Build();
```

### Shared Configuration Extension

```csharp
// UpdateEngine.Configuration/ServiceCollectionExtensions.cs
public static IConfigurationBuilder AddSharedAppConfiguration(
    this IConfigurationBuilder builder)
{
    var sharedPath = Path.Combine(
        AppContext.BaseDirectory,
        "shared");
    
    var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
        ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? "Production";
    
    builder.AddJsonFile(
        Path.Combine(sharedPath, "appsettings.Development.json"),
        optional: true,
        reloadOnChange: true);
    
    builder.AddJsonFile(
        Path.Combine(sharedPath, $"appsettings.{env}.json"),
        optional: true,
        reloadOnChange: true);
    
    return builder;
}
```

---

## Environment Variables

### Naming Convention

Environment variables use double underscore (`__`) as section separators:

```
UpdateEngine__ServiceConfiguration__ServiceUrl = http://localhost:7071
UpdateEngine__StorageConfiguration__MetadataPath = ./data
UpdateEngine__FeatureFlags__EnableDetailedLogging = true
```

### Common Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment name | `Development`, `Production` |
| `DOTNET_ENVIRONMENT` | Alternative environment | `Development`, `Production` |
| `AzureWebJobsStorage` | Azure Functions storage | Connection string |
| `UpdateEngine__ServiceConfiguration__ServiceUrl` | Service URL override | `http://localhost:7071` |
| `UpdateEngine__StorageConfiguration__MetadataPath` | Metadata path | `./data/metadata` |
| `UpdateEngine__SyncConfiguration__EnableScheduledSync` | Toggle timers | `true`, `false` |
| `UpdateEngine__FeatureFlags__EnableDetailedLogging` | Verbose logging | `true`, `false` |

### Azure App Settings

In Azure Portal ? Configuration ? Application settings:

```
Name: UpdateEngine__ServiceConfiguration__ServiceUrl
Value: https://myupdateserver.azurewebsites.net

Name: UpdateEngine__StorageConfiguration__UseAzureStorageForMetadata
Value: true

Name: ConnectionStrings__MetadataStorageConnection
Value: @Microsoft.KeyVault(SecretUri=https://myvault.vault.azure.net/secrets/MetadataStorage/)
```

---

## Troubleshooting

### Issue: Configuration Not Loading

**Symptoms:**
- Default values being used instead of configured values
- Configuration changes not taking effect

**Solutions:**

1. **Check file location:**
   ```
   UpdateEngine.Functions\src\
   ??? appsettings.json ?
   ??? appsettings.Development.json ?
   ??? local.settings.json ?
   ??? shared\
       ??? appsettings.Development.json ?
       ??? appsettings.Production.json ?
   ```

2. **Verify build output includes config files:**
   ```xml
   <!-- UpdateEngine.csproj -->
   <ItemGroup>
     <None Update="appsettings*.json">
       <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
     </None>
     <None Update="local.settings.json">
       <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
     </None>
   </ItemGroup>
   ```

3. **Check environment name:**
   ```powershell
   # PowerShell
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   dotnet run
   ```

4. **Enable configuration debugging:**
   ```csharp
   var config = context.Configuration as IConfigurationRoot;
   Console.WriteLine(config.GetDebugView());
   ```

### Issue: Timer Triggers Not Firing

**Symptoms:**
- Scheduled functions never execute
- No timer trigger logs

**Solutions:**

1. **Verify `EnableScheduledSync` is `true`:**
   ```json
   {
     "SyncConfiguration": {
       "EnableScheduledSync": true
     }
   }
   ```

2. **Check CRON expression syntax:**
   ```
   Valid: "0 */5 * * * *" (6-part, includes seconds)
   Invalid: "*/5 * * * *" (5-part, Linux cron)
   ```

3. **Ensure `AzureWebJobsStorage` is set:**
   ```json
   {
     "Values": {
       "AzureWebJobsStorage": "UseDevelopmentStorage=true"
     }
   }
   ```

### Issue: Azure Storage Connection Fails

**Symptoms:**
- `Azure.RequestFailedException: Server failed to authenticate`
- Storage operations timing out

**Solutions:**

1. **Start Azurite (local development):**
   ```powershell
   azurite --silent --location ./azurite --debug ./azurite/debug.log
   ```

2. **Verify connection string:**
   ```json
   {
     "ConnectionStrings": {
       "MetadataStorageConnection": "UseDevelopmentStorage=true"
     }
   }
   ```

3. **Check Azure Portal connection string (production):**
   - Azure Portal ? Storage Account ? Access Keys
   - Copy entire connection string including `AccountKey`

### Issue: Cache Not Working

**Symptoms:**
- Same data fetched repeatedly
- No performance improvement

**Solutions:**

1. **Verify cache is enabled:**
   ```json
   {
     "FeatureFlags": {
       "EnableCaching": true
     },
     "CacheConfiguration": {
       "EnableDistributedCache": false
     }
   }
   ```

2. **Check Redis connection (if using distributed cache):**
   ```powershell
   # Test Redis connection
   redis-cli -h localhost -p 6379 ping
   # Expected: PONG
   ```

3. **Verify cache key prefix:**
   ```json
   {
     "CacheConfiguration": {
       "KeyPrefix": "msupdate:"
     }
   }
   ```

---

## Best Practices

### 1. Use Shared Configuration for Team Defaults

? **DO:** Put team-wide settings in `UpdateEngine.Configuration\src\shared\`
```json
// shared/appsettings.Development.json
{
  "UpdateEngine": {
    "SyncConfiguration": {
      "SyncCriticalSchedule": "0 */3 * * * *"
    }
  }
}
```

? **DON'T:** Duplicate same values in every project

### 2. Keep Secrets Out of Configuration Files

? **DO:** Use environment variables or Key Vault
```powershell
$env:ConnectionStrings__MetadataStorageConnection = "AccountName=...;AccountKey=..."
```

? **DON'T:** Commit connection strings to source control
```json
// ? DON'T DO THIS
{
  "ConnectionStrings": {
    "MetadataStorageConnection": "AccountName=production;AccountKey=real-key-here"
  }
}
```

### 3. Use local.settings.json for Machine-Specific Settings

? **DO:** Local paths, URLs, personal preferences
```json
{
  "UpdateEngine": {
    "StorageConfiguration": {
      "MetadataPath": "D:\\UpdateData\\Metadata"
    }
  }
}
```

? **DON'T:** Team-wide defaults

### 4. Validate Configuration on Startup

```csharp
// Program.cs
var config = services.BuildServiceProvider()
    .GetRequiredService<IOptions<AppConfig>>()
    .Value;

config.Validate(); // Throws if invalid
```

### 5. Use Feature Flags for Gradual Rollouts

```json
{
  "FeatureFlags": {
    "EnableExperimentalFeatures": false
  }
}
```

Enable in production gradually:
```
Day 1: 10% of users
Day 2: 25% of users
Day 3: 100% of users
```

### 6. Monitor Configuration Changes

```csharp
services.AddOptionsMonitor<AppConfig>((config, name) =>
{
    _logger.LogInformation("Configuration changed: {Name}", name);
    // React to configuration changes
});
```

---

## Configuration Reference

### Complete Example

```json
{
  "UpdateEngine": {
    "ServiceConfiguration": {
      "ServiceUrl": "http://localhost:7071",
      "ContentUrl": "http://localhost:7071/api/content",
      "UpstreamServerUrl": "https://fe2.update.microsoft.com/v6/windowsupdate/Services/",
      "MaxConcurrentSyncs": 5,
      "RequestTimeoutSeconds": 300,
      "MaxUpdateCount": 1000,
      "SupportedCategories": [
        "Critical Updates",
        "Security Updates",
        "Updates"
      ],
      "SupportedLanguages": [
        "en",
        "en-US",
        "neutral",
        ""
      ]
    },
    "StorageConfiguration": {
      "MetadataPath": "./LocalMetadataStore",
      "ContentPath": "./LocalContentStore",
      "UseAzureStorageForMetadata": false,
      "UseAzureStorageForContent": false,
      "MetadataContainerName": "data",
      "ContentContainerName": "data",
      "ContentPathPrefix": "Content",
      "ReindexOnStartup": false
    },
    "SyncConfiguration": {
      "SyncCriticalSchedule": "0 */3 * * * *",
      "SyncComprehensiveSchedule": "0 */6 * * * *",
      "SyncContentSchedule": "0 */9 * * * *",
      "ScheduledHealthCheckSchedule": "0 */5 * * * *",
      "MaintenanceSchedule": "0 0 */1 * * *",
      "AnomalyDetectionSchedule": "0 */10 * * * *",
      "EnableScheduledSync": true
    },
    "FeatureFlags": {
      "EnableEmergencySync": true,
      "EnableComprehensiveSync": true,
      "EnableContentSync": true,
      "EnableAnomalyDetection": false,
      "EnableDeepHealthCheck": true,
      "EnableMaintenance": true,
      "EnableDetailedLogging": false,
      "EnableMetrics": true,
      "EnableOpenTelemetry": true,
      "EnableCaching": true,
      "EnableCompression": true,
      "EnableExperimentalFeatures": false
    },
    "CacheConfiguration": {
      "EnableDistributedCache": false,
      "RedisConnectionString": "localhost:6379",
      "DefaultExpirationMinutes": 60,
      "StatisticsCacheMinutes": 5,
      "UpdateDetailsCacheMinutes": 60,
      "ContentAvailabilityCacheMinutes": 15,
      "InvalidateOnSync": true,
      "KeyPrefix": "msupdate:"
    }
  },
  "ConnectionStrings": {
    "MetadataStorageConnection": "",
    "ContentStorageConnection": "",
    "RedisConnectionString": ""
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "Microsoft.PackageGraph": "Debug",
      "UpdateEngine.Services": "Debug"
    }
  }
}
```

---

## Additional Resources

- [ASP.NET Core Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
- [Azure Functions Configuration](https://learn.microsoft.com/en-us/azure/azure-functions/functions-app-settings)
- [Azure Key Vault Configuration Provider](https://learn.microsoft.com/en-us/aspnet/core/security/key-vault-configuration)
- [.NET Options Pattern](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)
- [NCRONTAB - CRON Expression Reference](https://github.com/atifaziz/NCrontab)

---

**Last Updated**: 2025-11-23  
**Applies To**: .NET 9.0, Azure Functions v4
