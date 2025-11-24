# Configuration Quick Reference

**Quick lookup for common configuration tasks**

## ?? Quick Start

### Local Development (Default)
1. No configuration needed - shared defaults work out of the box
2. Start with Aspire:
   ```powershell
   cd UpdateEngine.AppHost\src
   dotnet run
   ```

### Override Specific Settings
Create/edit `UpdateEngine.Functions\src\local.settings.json`:
```json
{
  "UpdateEngine": {
    "ServiceConfiguration": {
      "ServiceUrl": "http://localhost:7071"
    }
  }
}
```

---

## ?? Common Configuration Tasks

### Change Timer Intervals

**Make syncs faster (development):**
```json
{
  "SyncConfiguration": {
    "SyncCriticalSchedule": "0 */1 * * * *"  // Every 1 minute
  }
}
```

**Disable all timers:**
```json
{
  "SyncConfiguration": {
    "EnableScheduledSync": false
  }
}
```

**CRON Quick Reference:**
```
Format: second minute hour day month dayOfWeek

Examples:
"0 */5 * * * *"  = Every 5 minutes
"0 0 */2 * * *"  = Every 2 hours  
"0 0 2 * * *"    = Daily at 2 AM
"0 0 3 */7 * *"  = Weekly at 3 AM
```

---

### Enable/Disable Features

**Enable verbose logging:**
```json
{
  "FeatureFlags": {
    "EnableDetailedLogging": true
  }
}
```

**Enable metrics collection:**
```json
{
  "FeatureFlags": {
    "EnableMetrics": true
  }
}
```

**Disable caching (debugging):**
```json
{
  "FeatureFlags": {
    "EnableCaching": false
  }
}
```

---

### Storage Configuration

**Use local filesystem:**
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

**Use Azure Blob Storage:**
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

---

### Cache Configuration

**Disable Redis (local development):**
```json
{
  "CacheConfiguration": {
    "EnableDistributedCache": false
  }
}
```

**Enable Redis (production):**
```json
{
  "CacheConfiguration": {
    "EnableDistributedCache": true,
    "RedisConnectionString": "mycache.redis.cache.windows.net:6380,password=...,ssl=True"
  }
}
```

**Change cache expiration:**
```json
{
  "CacheConfiguration": {
    "DefaultExpirationMinutes": 60,
    "StatisticsCacheMinutes": 5,
    "UpdateDetailsCacheMinutes": 60
  }
}
```

---

## ?? Environment Variables

### Override Any Setting

Use double underscore (`__`) as separator:

**PowerShell:**
```powershell
$env:UpdateEngine__ServiceConfiguration__ServiceUrl = "http://localhost:7071"
$env:UpdateEngine__FeatureFlags__EnableDetailedLogging = "true"
$env:UpdateEngine__SyncConfiguration__EnableScheduledSync = "false"
```

**Linux/macOS:**
```bash
export UpdateEngine__ServiceConfiguration__ServiceUrl=http://localhost:7071
export UpdateEngine__FeatureFlags__EnableDetailedLogging=true
export UpdateEngine__SyncConfiguration__EnableScheduledSync=false
```

---

## ?? Configuration File Locations

### Azure Functions
```
UpdateEngine.Functions\src\
??? local.settings.json          (Your machine-specific overrides)
??? appsettings.Development.json (Loads shared/appsettings.Development.json)
??? appsettings.Production.json  (Loads shared/appsettings.Production.json)
??? shared\                      (Team-wide defaults - DO NOT EDIT locally)
    ??? appsettings.Development.json
    ??? appsettings.Production.json
```

### Configuration Precedence (Highest to Lowest)
1. Environment Variables
2. `local.settings.json`
3. `appsettings.{Environment}.json` (per-project)
4. `shared/appsettings.{Environment}.json`
5. `appsettings.json` (per-project)
6. Code defaults

---

## ?? Troubleshooting

### Timers Not Firing
```json
// Check these settings:
{
  "Values": {
    "AzureWebJobsStorage": ""  // Must be set (empty for Aspire)
  },
  "SyncConfiguration": {
    "EnableScheduledSync": true  // Must be true
  }
}
```

### Configuration Not Loading
```powershell
# Check environment name
$env:ASPNETCORE_ENVIRONMENT
# Expected: "Development" or "Production"

# Set if needed
$env:ASPNETCORE_ENVIRONMENT = "Development"
```

### Storage Connection Fails
```json
// Local development
{
  "ConnectionStrings": {
    "MetadataStorageConnection": "UseDevelopmentStorage=true"
  }
}

// Verify Azurite is running:
// azurite --silent
```

---

## ?? Full Documentation

For complete details, see:
- **Configuration Guide**: `docs/CONFIGURATION_GUIDE.md`
- **Unification Proposal**: `docs/proposals/2025-11-23-configuration-unification.md`
- **Implementation Summary**: `docs/implementations/2025-11-23-configuration-improvements.md`

---

## ?? Configuration Sections

| Section | Purpose | Key Properties |
|---------|---------|----------------|
| `ServiceConfiguration` | Service URLs and limits | `ServiceUrl`, `MaxUpdateCount` |
| `StorageConfiguration` | Where data is stored | `MetadataPath`, `UseAzureStorage` |
| `SyncConfiguration` | Timer schedules | `SyncCriticalSchedule`, `EnableScheduledSync` |
| `FeatureFlags` | Enable/disable features | `EnableDetailedLogging`, `EnableCaching` |
| `CacheConfiguration` | Redis caching | `EnableDistributedCache`, `RedisConnectionString` |

---

## ?? Pro Tips

1. **Don't edit shared config files** - use `local.settings.json` for overrides
2. **Use environment variables** for secrets - never commit connection strings
3. **Disable timers during debugging** - set `EnableScheduledSync: false`
4. **Enable detailed logging** for troubleshooting - but disable in production
5. **Check cache keys** - use `updateengine:dev:` prefix for development

---

**Quick Links**:
- [Full Configuration Guide](./CONFIGURATION_GUIDE.md)
- [Feature Flags Reference](./CONFIGURATION_GUIDE.md#featureflags)
- [Timer Schedules (CRON)](./CONFIGURATION_GUIDE.md#syncconfiguration)
- [Storage Options](./CONFIGURATION_GUIDE.md#storageconfiguration)
