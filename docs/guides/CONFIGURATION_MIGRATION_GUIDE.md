# Configuration Migration Guide

## Overview

This guide covers the migration from CRON-based timer schedules to TimeSpan-based schedules in Azure Functions, along with best practices for configuration management across different environments. This migration improves readability, reduces errors, and provides better integration with Azure Functions runtime.

## Background

The Microsoft Update Server-Server Sync implementation originally used CRON expressions for timer scheduling, which proved difficult to read and maintain. The migration to TimeSpan expressions provides several benefits:

- **Human Readable**: `"06:00:00"` vs `"0 0 */6 * * *"`
- **IDE Support**: IntelliSense and validation in modern IDEs
- **Error Reduction**: Compile-time validation instead of runtime errors
- **Azure Integration**: Better integration with Azure Functions scaling

## Migration Strategy

### Phase 1: CRON to TimeSpan Conversion

#### Before (CRON Format)
```json
{
  "Values": {
    "CategorySyncSchedule": "0 0 */6 * * *",
    "UpdateSyncSchedule": "0 0 */1 * * *",
    "HealthCheckSchedule": "0 */5 * * * *",
    "MetadataCleanupSchedule": "0 0 2 * * *"
  }
}
```

#### After (TimeSpan Format)
```json
{
  "Values": {
    "CategorySyncInterval": "06:00:00",
    "UpdateSyncInterval": "01:00:00", 
    "HealthCheckInterval": "00:05:00",
    "MetadataCleanupInterval": "1.00:00:00"
  }
}
```

#### Conversion Reference Table

| CRON Expression | Description | TimeSpan Equivalent | Notes |
|-----------------|-------------|---------------------|-------|
| `0 0 */6 * * *` | Every 6 hours | `06:00:00` | Simple interval |
| `0 0 */1 * * *` | Every hour | `01:00:00` | Simple interval |
| `0 */5 * * * *` | Every 5 minutes | `00:05:00` | Simple interval |
| `0 0 2 * * *` | Daily at 2 AM | `1.00:00:00` | Daily interval |
| `0 0 0 */3 * *` | Every 3 days | `3.00:00:00` | Multi-day interval |

### Phase 2: Timer Function Updates

#### Before (CRON-based)
```csharp
[Function("CategorySync")]
public async Task RunCategorySync(
    [TimerTrigger("0 0 */6 * * *")] TimerInfo timer,
    FunctionContext context)
{
    // Function implementation
}
```

#### After (TimeSpan-based)
```csharp
[Function("CategorySync")]
public async Task RunCategorySync(
    [TimerTrigger("%CategorySyncInterval%")] TimerInfo timer,
    FunctionContext context)
{
    // Function implementation
}
```

### Phase 3: Configuration Structure Migration

#### Environment-Specific Configuration

**Development Environment** (`local.settings.json`):
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "FUNCTIONS_EXTENSION_VERSION": "~4",
    
    "CategorySyncInterval": "06:00:00",
    "UpdateSyncInterval": "01:00:00",
    "HealthCheckInterval": "00:05:00",
    "MetadataCleanupInterval": "1.00:00:00",
    
    "MetadataStorePath": "./store",
    "ContentStorePath": "./content",
    "ServiceConfigurationJson": "{\"ServiceUrl\":\"http://localhost:7071\"}",
    
    "WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT": "5"
  }
}
```

**Production Environment** (Azure App Settings):
```json
{
  "CategorySyncInterval": "04:00:00",
  "UpdateSyncInterval": "00:30:00",
  "HealthCheckInterval": "00:02:00",
  "MetadataCleanupInterval": "1.00:00:00",
  
  "MetadataStorePath": "/app/store",
  "ContentStorePath": "/app/content",
  "ServiceConfigurationJson": "{\"ServiceUrl\":\"https://msupdate-prod.azurewebsites.net\"}",
  
  "WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT": "25"
}
```

## Configuration Management Strategy

### 1. Environment Configuration Hierarchy

```
Base Configuration (appsettings.json)
    ↓
Environment Specific (appsettings.{Environment}.json)
    ↓
Local Development (local.settings.json)
    ↓
Azure App Settings (Runtime Override)
```

### 2. Configuration Provider Setup

```csharp
public class ConfigurationHelper
{
    public static IConfiguration BuildConfiguration(string environment = "Development")
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);

        // Add local.settings.json for local development
        if (environment == "Development")
        {
            builder.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
        }

        // Add environment variables (Azure App Settings)
        builder.AddEnvironmentVariables();

        return builder.Build();
    }
}
```

### 3. Strongly Typed Configuration

#### Configuration Models
```csharp
public class TimerConfiguration
{
    public TimeSpan CategorySyncInterval { get; set; } = TimeSpan.FromHours(6);
    public TimeSpan UpdateSyncInterval { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan MetadataCleanupInterval { get; set; } = TimeSpan.FromDays(1);
}

public class StorageConfiguration
{
    public string MetadataStorePath { get; set; } = "./store";
    public string ContentStorePath { get; set; } = "./content";
    public string? AzureStorageConnectionString { get; set; }
}

public class ServiceConfiguration
{
    public string ServiceUrl { get; set; } = "http://localhost:7071";
    public string UpstreamEndpoint { get; set; } = "https://www.update.microsoft.com/v6/";
    public int MaxConcurrentOperations { get; set; } = 8;
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromMinutes(10);
}
```

#### Dependency Injection Setup
```csharp
public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    // Register configuration sections
    services.Configure<TimerConfiguration>(configuration.GetSection("Timers"));
    services.Configure<StorageConfiguration>(configuration.GetSection("Storage"));
    services.Configure<ServiceConfiguration>(configuration.GetSection("Service"));
    
    // Register as singletons for direct injection
    services.AddSingleton(provider => provider.GetRequiredService<IOptions<TimerConfiguration>>().Value);
    services.AddSingleton(provider => provider.GetRequiredService<IOptions<StorageConfiguration>>().Value);
    services.AddSingleton(provider => provider.GetRequiredService<IOptions<ServiceConfiguration>>().Value);
}
```

## Environment-Specific Configurations

### Development Environment

**Purpose**: Local development and testing with relaxed schedules and reduced resource usage.

```json
{
  "Timers": {
    "CategorySyncInterval": "12:00:00",
    "UpdateSyncInterval": "02:00:00", 
    "HealthCheckInterval": "00:10:00",
    "MetadataCleanupInterval": "7.00:00:00"
  },
  "Storage": {
    "MetadataStorePath": "./store",
    "ContentStorePath": "./content"
  },
  "Service": {
    "ServiceUrl": "http://localhost:7071",
    "MaxConcurrentOperations": 2,
    "OperationTimeout": "00:05:00"
  },
  "Scaling": {
    "MaxInstances": 2
  }
}
```

### Staging Environment

**Purpose**: Production-like testing with moderate schedules and resource allocation.

```json
{
  "Timers": {
    "CategorySyncInterval": "08:00:00",
    "UpdateSyncInterval": "01:30:00",
    "HealthCheckInterval": "00:05:00", 
    "MetadataCleanupInterval": "1.00:00:00"
  },
  "Storage": {
    "MetadataStorePath": "/app/store",
    "ContentStorePath": "/app/content",
    "AzureStorageConnectionString": "$(AzureStorageConnectionString)"
  },
  "Service": {
    "ServiceUrl": "https://msupdate-staging.azurewebsites.net",
    "MaxConcurrentOperations": 4,
    "OperationTimeout": "00:08:00"
  },
  "Scaling": {
    "MaxInstances": 10
  }
}
```

### Production Environment

**Purpose**: High-frequency synchronization with maximum performance and reliability.

```json
{
  "Timers": {
    "CategorySyncInterval": "04:00:00",
    "UpdateSyncInterval": "00:30:00",
    "HealthCheckInterval": "00:02:00",
    "MetadataCleanupInterval": "1.00:00:00"
  },
  "Storage": {
    "MetadataStorePath": "/app/store",
    "ContentStorePath": "/app/content",
    "AzureStorageConnectionString": "$(AzureStorageConnectionString)"
  },
  "Service": {
    "ServiceUrl": "https://msupdate-prod.azurewebsites.net",
    "MaxConcurrentOperations": 8,
    "OperationTimeout": "00:10:00"
  },
  "Scaling": {
    "MaxInstances": 25
  }
}
```

## Configuration Validation

### Runtime Validation
```csharp
public class ConfigurationValidator
{
    public static void ValidateTimerConfiguration(TimerConfiguration config)
    {
        if (config.CategorySyncInterval < TimeSpan.FromMinutes(30))
            throw new InvalidOperationException("CategorySyncInterval must be at least 30 minutes");
            
        if (config.UpdateSyncInterval < TimeSpan.FromMinutes(10))
            throw new InvalidOperationException("UpdateSyncInterval must be at least 10 minutes");
            
        if (config.HealthCheckInterval < TimeSpan.FromMinutes(1))
            throw new InvalidOperationException("HealthCheckInterval must be at least 1 minute");
    }
    
    public static void ValidateStorageConfiguration(StorageConfiguration config)
    {
        if (string.IsNullOrEmpty(config.MetadataStorePath))
            throw new InvalidOperationException("MetadataStorePath is required");
            
        if (!string.IsNullOrEmpty(config.AzureStorageConnectionString))
        {
            // Validate Azure Storage connection string format
            if (!config.AzureStorageConnectionString.Contains("AccountName="))
                throw new InvalidOperationException("Invalid Azure Storage connection string");
        }
    }
}
```

### Startup Validation
```csharp
public static void Main(string[] args)
{
    var host = new HostBuilder()
        .ConfigureFunctionsWorkerDefaults()
        .ConfigureServices((context, services) =>
        {
            var configuration = context.Configuration;
            
            // Configure and validate settings
            var timerConfig = configuration.GetSection("Timers").Get<TimerConfiguration>();
            ConfigurationValidator.ValidateTimerConfiguration(timerConfig);
            
            var storageConfig = configuration.GetSection("Storage").Get<StorageConfiguration>();
            ConfigurationValidator.ValidateStorageConfiguration(storageConfig);
            
            services.AddSingleton(timerConfig);
            services.AddSingleton(storageConfig);
        })
        .Build();

    host.Run();
}
```

## Migration Checklist

### Pre-Migration Tasks

- [ ] **Backup Current Configuration**: Save existing CRON-based configurations
- [ ] **Document Current Schedules**: Record when each function currently runs
- [ ] **Test TimeSpan Parsing**: Validate TimeSpan format in development environment
- [ ] **Update Documentation**: Modify deployment and configuration documentation

### Migration Steps

1. **Update Timer Attributes**
   ```csharp
   // Replace hard-coded CRON with configuration reference
   [TimerTrigger("%CategorySyncInterval%")] 
   ```

2. **Update Configuration Files**
   ```json
   // Replace CRON expressions with TimeSpan intervals
   "CategorySyncInterval": "06:00:00"
   ```

3. **Deploy to Development**
   ```bash
   func azure functionapp publish msupdate-functions-dev
   ```

4. **Validate Development Environment**
   ```bash
   # Check function execution logs
   func azure functionapp logstream msupdate-functions-dev
   ```

5. **Deploy to Staging**
   ```bash
   func azure functionapp publish msupdate-functions-staging
   ```

6. **Production Deployment**
   ```bash
   func azure functionapp publish msupdate-functions-prod
   ```

### Post-Migration Validation

- [ ] **Verify Function Execution**: Confirm functions run at expected intervals
- [ ] **Monitor Performance**: Check for any performance impact from schedule changes
- [ ] **Validate Logs**: Ensure logging shows correct timer information
- [ ] **Test Manual Triggers**: Verify functions can still be manually triggered

## Troubleshooting

### Common Issues

#### 1. TimeSpan Format Errors
```bash
Error: Unable to parse '6:00:00' as TimeSpan
```

**Solution**: Use proper TimeSpan format with leading zeros:
```json
"CategorySyncInterval": "06:00:00"  // Correct
"CategorySyncInterval": "6:00:00"   // May cause issues
```

#### 2. Configuration Not Loading
```bash
Error: Configuration value 'CategorySyncInterval' not found
```

**Solution**: Verify configuration hierarchy and environment variables:
```csharp
// Add logging to debug configuration loading
var categoryInterval = configuration["CategorySyncInterval"];
logger.LogInformation("CategorySyncInterval loaded as: {Interval}", categoryInterval);
```

#### 3. Timer Not Triggering
```bash
Warning: Timer function has not executed in expected interval
```

**Solution**: Check Azure Functions scaling and timer accuracy:
```json
{
  "WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT": "1",  // Prevent multiple instances
  "AzureWebJobsStorage": "UseDevelopmentStorage=true"  // Required for timers
}
```

### Debugging Commands

```bash
# View current configuration
az functionapp config appsettings list --name msupdate-functions --resource-group rg-msupdate

# Check function execution history
az monitor activity-log list --resource-group rg-msupdate --start-time 2025-10-28T00:00:00Z

# Monitor function logs in real-time
func azure functionapp logstream msupdate-functions
```

## Best Practices

### 1. Configuration Organization

- **Group Related Settings**: Use configuration sections for logical grouping
- **Use Environment Variables**: Override settings without code changes
- **Validate Early**: Validate configuration at startup, not at runtime
- **Document Defaults**: Clearly document default values and their rationale

### 2. Schedule Planning

- **Avoid Overlap**: Ensure functions complete before next trigger
- **Consider Time Zones**: Use UTC for consistency across regions
- **Plan for Peak Load**: Schedule intensive operations during low-traffic periods
- **Monitor Performance**: Track execution duration vs. schedule intervals

### 3. Environment Management

- **Separate Configurations**: Maintain distinct configs for each environment
- **Automate Deployment**: Use infrastructure as code for configuration deployment
- **Version Control**: Track configuration changes through version control
- **Test Configuration**: Validate configuration changes in non-production environments

### 4. Security Considerations

- **Secure Secrets**: Use Azure Key Vault for sensitive configuration
- **Limit Access**: Restrict who can modify production configuration
- **Audit Changes**: Log all configuration modifications
- **Encrypt at Rest**: Enable encryption for stored configuration values

---

**Last Updated**: October 28, 2025  
**Migration Status**: Complete  
**Framework**: .NET 9.0 Azure Functions v4  
**Configuration Model**: TimeSpan-based with environment-specific overrides