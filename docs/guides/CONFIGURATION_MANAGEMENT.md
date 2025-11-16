# Configuration Management Guide

## Overview

This guide explains how configuration works in the Microsoft Update Server-Server Sync solution, particularly when using the Aspire AppHost orchestration versus running Azure Functions directly.

## Configuration Architecture

### Configuration Sources (Priority Order)

1. **🏆 AppHost Environment Variables** (Highest Priority)
   - Injected by `AppHost/src/ConfigurationHelper.cs`
   - Source: `appsettings.{Environment}.json` files
   - Used when running via Aspire orchestration

2. **🥈 Azure Functions Environment Variables**
   - From `UpdateEngine/src/local.settings.json`
   - Used when running Functions directly via `func start`

3. **🥉 AppHost Configuration Files**
   - `appsettings.Development.json` (Development environment)
   - `appsettings.IntegrationTest.json` (Integration testing)
   - `appsettings.Production.json` (Production deployment)
   - `appsettings.json` (Base configuration)

## Configuration Flow

```mermaid
graph TD
    A[appsettings.{Environment}.json] --> B[AppHost ConfigurationHelper.cs]
    B --> C[Environment Variables] 
    C --> D[Azure Functions Process]
    D --> E[Overrides local.settings.json]
    
    F[local.settings.json] --> G[Direct Functions Execution]
    G --> H[Used when running func start directly]
```

## Environment-Specific Schedules

### Development Environment (`appsettings.Development.json`)

**Ultra-fast testing schedules for rapid development iteration:**

| Function | Schedule | Interval | Purpose |
|----------|----------|----------|---------|
| Critical Metadata Sync | `"00:02:00"` | 2 minutes | Quick security update testing |
| Content Sync | `"00:03:00"` | 3 minutes | Rapid content download validation |
| Comprehensive Metadata | `"00:05:00"` | 5 minutes | Full metadata sync testing |
| Anomaly Detection | `"00:02:00"` | 2 minutes | ML.NET pattern analysis testing |
| Health Monitoring | `"00:01:00"` | 1 minute | System health validation |

**Development Configuration Features:**
- `MaxUpdateCount: 5` (vs 1000 production)
- `SupportedCategories: ["Security Updates", "Critical Updates"]` (reduced scope)
- Enhanced debug logging for anomaly detection
- Azurite storage emulator integration

### Production Environment (`appsettings.json`)

**Production-ready schedules optimized for performance and resource usage:**

| Function | Schedule | Interval | Purpose |
|----------|----------|----------|---------|
| Critical Metadata Sync | `"02:00:00"` | 2 hours | Security update deployment |
| Content Sync | `"3.00:00:00"` | 3 days | Content availability balance |
| Comprehensive Metadata | `"1.00:00:00"` | 24 hours | Complete metadata coverage |
| Anomaly Detection | `"00:30:00"` | 30 minutes | Production monitoring |
| Health Monitoring | `"00:15:00"` | 15 minutes | System reliability |

## Configuration Override Mechanism

### When Using AppHost (`cd AppHost && dotnet run`)

The `ConfigurationHelper.cs` automatically injects environment variables that override `local.settings.json`:

```csharp
// From AppHost/src/ConfigurationHelper.cs
functions
    .WithEnvironment("SyncMetadataCriticalSchedule", schedules.SyncMetadataCriticalSchedule)
    .WithEnvironment("SyncContentSchedule", schedules.SyncContentSchedule)
    .WithEnvironment("AnomalyDetectionSchedule", schedules.AnomalyDetectionSchedule)
    .WithEnvironment("ScheduledHealthCheckSchedule", schedules.ScheduledHealthCheckSchedule);
```

### Environment Variable Mapping

| AppHost Configuration | Environment Variable | Azure Function Timer Trigger |
|----------------------|---------------------|------------------------------|
| `FunctionSchedules.SyncMetadataCriticalSchedule` | `SyncCriticalSchedule` | `[TimerTrigger("%SyncCriticalSchedule%")]` |
| `FunctionSchedules.SyncContentSchedule` | `SyncContentSchedule` | `[TimerTrigger("%SyncContentSchedule%")]` |
| `FunctionSchedules.AnomalyDetectionSchedule` | `AnomalyDetectionSchedule` | `[TimerTrigger("%AnomalyDetectionSchedule%")]` |
| `FunctionSchedules.ScheduledHealthCheckSchedule` | `ScheduledHealthCheckSchedule` | `[TimerTrigger("%ScheduledHealthCheckSchedule%")]` |

## Configuration Files Reference

### AppHost Configuration Structure

```json
{
  "AzureWebJobs": {
    "ScheduledSyncCritical": { "Disabled": false },
    "ScheduledSyncContent": { "Disabled": false },
    "ScheduledAnomalyDetection": { "Disabled": false },
    "ScheduledHealthCheck": { "Disabled": false }
  },
  "FunctionSchedules": {
    "SyncMetadataCriticalSchedule": "00:02:00",
    "SyncContentSchedule": "00:03:00",
    "AnomalyDetectionSchedule": "00:02:00",
    "ScheduledHealthCheckSchedule": "00:01:00"
  },
  "Storage": {
    "UseAzureStorageForMetadata": true,
    "UseAzureStorageForContent": true,
    "MetadataContainerName": "data",
    "ContentContainerName": "data"
  }
}
```

### Functions Local Configuration Structure

```json
{
  "Values": {
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AzureWebJobsStorage": "",
    "SyncMetadataCriticalSchedule": "00:02:00",
    "SyncContentSchedule": "00:03:00", 
    "AnomalyDetectionSchedule": "00:02:00",
    "ScheduledHealthCheckSchedule": "00:01:00"
  }
}
```

## Development Workflows

### Rapid Development Testing

1. **Start AppHost**: `cd AppHost && dotnet run`
   - Automatically uses Development configuration
   - Functions trigger every 1-5 minutes
   - Azurite storage emulator starts automatically

2. **Monitor Function Execution**:
   ```
   [12:00:00] SyncCritical triggered (every 2 minutes)
   [12:01:00] ScheduledHealthCheck triggered (every 1 minute)  
   [12:02:00] ScheduledAnomalyDetection triggered (every 2 minutes)
   [12:03:00] SyncContent triggered (every 3 minutes)
   ```

3. **Verify Configuration Override**:
   - Check Azure Functions logs for environment variable usage
   - Confirm rapid schedules are active (not production schedules)

### Direct Functions Testing

1. **Start Functions Directly**: `cd UpdateEngine && func start`
   - Uses `local.settings.json` configuration
   - No AppHost orchestration
   - Manual storage setup required

2. **Configuration Source**: Functions read directly from `local.settings.json`

## Storage Configuration Integration

### Azurite Integration (Development)

When using AppHost, storage configuration is automatically managed:

```csharp
// Automatic Azurite container setup
var storage = builder.AddAzureStorage("Storage").RunAsEmulator();
var updateFunctions = builder.AddAzureFunctionsProject<Projects.UpdateEngine>("UpdateEngine")
    .WithHostStorage(storage);
```

**Containers Created:**
- `data` - Unified container for metadata and content
- Automatic blob storage structure
- No manual setup required

### Azure Storage (Production)

Production deployments use actual Azure Storage accounts:
- Separate containers for metadata and content (configurable)
- Connection strings managed via Azure configuration
- Automatic scaling and redundancy

## Configuration Validation

### Health Check Endpoints

Verify configuration is working correctly:

```bash
# Storage diagnostics
curl "http://localhost:7071/api/StorageDiagnostics"

# Function status
curl "http://localhost:7071/api/QueryMetadataStoreStatus"

# Health monitoring
curl "http://localhost:7071/api/UniversalHealth"
```

### Expected Responses

**Storage Diagnostics Success:**
```json
{
  "metadataStore": {
    "isConnected": true,
    "storageType": "Azure Blob",
    "containerName": "data"
  },
  "contentStore": {
    "isConnected": true,
    "storageType": "Azure Blob", 
    "containerName": "data"
  }
}
```

## Troubleshooting Configuration Issues

### Problem: Functions Using Wrong Schedules

**Symptom**: Functions trigger at production intervals instead of development intervals

**Solution**: 
1. Verify running via AppHost: `cd AppHost && dotnet run`
2. Check AppHost logs for environment variable injection
3. Confirm `appsettings.Development.json` has correct schedules

### Problem: Storage Configuration Not Found

**Symptom**: Functions fail with storage connection errors

**Solution**:
1. Ensure Azurite is started (automatic with AppHost)
2. Verify `UseAzureStorageFor*: true` in configuration
3. Check container creation in Azurite web interface

### Problem: Timer Triggers Not Working

**Symptom**: Scheduled functions never execute

**Solution**:
1. Ensure `AzureWebJobsStorage` is configured (empty string for local dev)
2. Verify function is not disabled in `AzureWebJobs` section
3. Check timer trigger parameter name matches environment variable

## Best Practices

### Development Configuration
- Use AppHost for full orchestration testing
- Keep development schedules fast (1-5 minutes) for rapid iteration
- Limit update counts and categories for faster testing
- Enable debug logging for troubleshooting

### Production Configuration  
- Use appropriate intervals balancing freshness with resource usage
- Configure proper Azure Storage accounts
- Enable comprehensive monitoring and health checks
- Use feature flags to disable unnecessary functions

### Configuration Management
- Keep environment-specific settings in appropriate `appsettings.{Environment}.json` files
- Use `local.settings.json` only for direct Functions testing
- Document configuration changes and their impact
- Test configuration changes in development environment first

---

**Quick Reference**: When using AppHost, `appsettings.Development.json` automatically overrides `local.settings.json` via environment variable injection through `ConfigurationHelper.cs`.