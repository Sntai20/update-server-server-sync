# Configuration Unification Proposal

**Date**: 2025-11-23  
**Status**: Proposed  
**Affects**: All UpdateEngine projects

## Problem Statement

The solution currently has inconsistent configuration across projects:

1. **Timer Schedule Model Inconsistency**: Azure Functions uses CRON expressions while WorkerService/CLI use minute intervals
2. **Feature Flag Duplication**: Not all feature flags are consistently defined across projects
3. **Configuration Duplication**: Many settings are duplicated across project-specific and shared configs
4. **Cache Configuration Variations**: Different projects use different cache key prefixes and property names

## Proposed Changes

### 1. Unify Timer Configuration Model

**Current State:**
```csharp
// Azure Functions (SyncConfiguration.cs)
public string SyncCriticalSchedule { get; set; } = "0 */2 * * * *"; // CRON

// WorkerService/CLI (different properties)
public int SyncIntervalMinutes { get; set; } = 60; // Minutes
public int HealthCheckIntervalMinutes { get; set; } = 5;
```

**Proposed Solution:**

#### Option A: CRON Everywhere (Recommended)
Add CRON support to WorkerService/CLI using NCronTab library:

```csharp
public class SyncConfiguration
{
    // Timer schedules (CRON format for all projects)
    public string SyncCriticalSchedule { get; set; } = "0 */2 * * * *";
    public string SyncComprehensiveSchedule { get; set; } = "0 0 */1 * * *";
    public string SyncContentSchedule { get; set; } = "0 0 0 */1 * *";
    public string ScheduledHealthCheckSchedule { get; set; } = "0 */15 * * * *";
    public string MaintenanceSchedule { get; set; } = "0 0 2 */7 * *";
    public string AnomalyDetectionSchedule { get; set; } = "0 0 */1 * * *";
    
    // Enable/disable
    public bool EnableScheduledSync { get; set; } = true;
    
    // Legacy properties for backward compatibility (will be removed in v2.0)
    [Obsolete("Use SyncCriticalSchedule with CRON format")]
    public int SyncIntervalMinutes { get; set; } = 60;
    
    [Obsolete("Use ScheduledHealthCheckSchedule with CRON format")]
    public int HealthCheckIntervalMinutes { get; set; } = 5;
}
```

**Implementation:**
1. Add `NCronTab` NuGet package to WorkerService and CLI projects
2. Create `CronScheduler` helper class to parse CRON and calculate next execution
3. Update WorkerService background tasks to use CRON-based scheduling
4. Mark old properties as `[Obsolete]` with migration path

#### Option B: Dual Support (Compatibility)
Support both CRON and minute intervals:

```csharp
public class SyncConfiguration
{
    // CRON format (preferred)
    public string? SyncCriticalSchedule { get; set; }
    
    // Minute intervals (legacy)
    public int? SyncIntervalMinutes { get; set; }
    
    // Helper to get effective schedule
    public TimeSpan GetSyncInterval()
    {
        if (!string.IsNullOrEmpty(SyncCriticalSchedule))
        {
            // Parse CRON and return time until next occurrence
            return CronHelper.GetNextInterval(SyncCriticalSchedule);
        }
        
        return TimeSpan.FromMinutes(SyncIntervalMinutes ?? 60);
    }
}
```

**Recommendation**: Go with Option A (CRON Everywhere) for consistency and flexibility.

---

### 2. Consolidate Feature Flags

**Current Issues:**
- `EnableMetrics` only in Functions
- `EnableComprehensiveSync`/`EnableContentSync` only in Worker/CLI
- Many flags defined in model but never configured

**Proposed Complete FeatureFlags:**

```csharp
public class FeatureFlags
{
    // Sync features (all projects)
    public bool EnableEmergencySync { get; set; } = true;
    public bool EnableComprehensiveSync { get; set; } = true;
    public bool EnableContentSync { get; set; } = true;
    
    // Monitoring features (all projects)
    public bool EnableAnomalyDetection { get; set; } = false;
    public bool EnableDeepHealthCheck { get; set; } = true;
    public bool EnableMaintenance { get; set; } = true;
    
    // Logging and telemetry (all projects)
    public bool EnableDetailedLogging { get; set; } = false;
    public bool EnableMetrics { get; set; } = true;
    public bool EnableOpenTelemetry { get; set; } = true;
    
    // Performance features (all projects)
    public bool EnableCaching { get; set; } = true;
    public bool EnableCompression { get; set; } = true;
    
    // Experimental features (all projects)
    public bool EnableExperimentalFeatures { get; set; } = false;
}
```

**Add to Shared Configuration:**

```json
// shared/appsettings.Development.json
{
  "UpdateEngine": {
    "FeatureFlags": {
      "EnableEmergencySync": true,
      "EnableComprehensiveSync": true,
      "EnableContentSync": true,
      "EnableAnomalyDetection": true,
      "EnableDeepHealthCheck": true,
      "EnableMaintenance": true,
      "EnableDetailedLogging": true,
      "EnableMetrics": false,
      "EnableOpenTelemetry": false,
      "EnableCaching": false,
      "EnableCompression": true,
      "EnableExperimentalFeatures": false
    }
  }
}

// shared/appsettings.Production.json
{
  "UpdateEngine": {
    "FeatureFlags": {
      "EnableEmergencySync": true,
      "EnableComprehensiveSync": true,
      "EnableContentSync": true,
      "EnableAnomalyDetection": true,
      "EnableDeepHealthCheck": false,
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

---

### 3. Complete Shared Configuration

**Add Missing Properties to Shared Defaults:**

```json
// shared/appsettings.Development.json
{
  "UpdateEngine": {
    "ServiceConfiguration": {
      "ServiceUrl": "http://localhost:7071",
      "ContentUrl": "http://localhost:7071/api/content",
      "UpstreamServerUrl": "https://fe2.update.microsoft.com/v6/windowsupdate/Services/",
      "MaxConcurrentSyncs": 5,
      "RequestTimeoutSeconds": 300,
      "MaxUpdateCount": 5,
      "SupportedCategories": [
        "Security Updates",
        "Critical Updates",
        "Definition Updates"
      ],
      "SupportedLanguages": [
        "en",
        "en-US",
        "neutral",
        ""
      ]
    },
    "StorageConfiguration": {
      "UseAzureStorageForMetadata": true,
      "UseAzureStorageForContent": true,
      "MetadataContainerName": "data",
      "ContentContainerName": "data",
      "ContentPathPrefix": "Content",
      "ReindexOnStartup": true
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
    "CacheConfiguration": {
      "EnableDistributedCache": false,
      "RedisConnectionString": "localhost:6379",
      "KeyPrefix": "updateengine:dev:",
      "DefaultExpirationMinutes": 30,
      "StatisticsCacheMinutes": 5,
      "UpdateDetailsCacheMinutes": 60,
      "ContentAvailabilityCacheMinutes": 15,
      "InvalidateOnSync": true
    },
    "FeatureFlags": {
      "EnableDetailedLogging": true,
      "EnableMetrics": false,
      "EnableCaching": false
    }
  }
}
```

---

### 4. Standardize Cache Key Prefixes

**Current State:**
- Functions: `"msupdate:"`
- WorkerService: `"updateengine"`
- CLI: `"updateengine-cli"`

**Proposed Standard:**
```
Format: updateengine:{environment}:{project}:
Examples:
- updateengine:dev:functions:
- updateengine:prod:functions:
- updateengine:dev:worker:
- updateengine:dev:cli:
```

**Implementation:**

```csharp
public class CacheConfiguration
{
    private string? _keyPrefix;
    
    public string KeyPrefix
    {
        get
        {
            if (string.IsNullOrEmpty(_keyPrefix))
            {
                var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
                var project = GetProjectName();
                return $"updateengine:{env.ToLower()}:{project}:";
            }
            return _keyPrefix;
        }
        set => _keyPrefix = value;
    }
    
    private static string GetProjectName()
    {
        var assembly = Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown";
        return assembly.ToLower() switch
        {
            "updateengine" => "functions",
            "workerservice" => "worker",
            "update-cli" => "cli",
            _ => "default"
        };
    }
}
```

---

## Migration Plan

### Phase 1: Documentation (Immediate)
- ? Create `docs/CONFIGURATION_GUIDE.md`
- ? Document current state and issues
- ? Propose unification strategy

### Phase 2: Add Complete Feature Flags (Week 1)
1. Update `FeatureFlags.cs` with all properties documented
2. Add complete `FeatureFlags` section to shared configs
3. Update Functions, WorkerService, and CLI to use all flags
4. Test that existing functionality continues to work

### Phase 3: Unify Timer Configuration (Week 2)
1. Add `NCronTab` package to WorkerService and CLI
2. Create `CronScheduler` helper class
3. Update WorkerService background tasks to use CRON
4. Mark old properties as `[Obsolete]`
5. Update documentation with migration examples

### Phase 4: Consolidate Shared Configuration (Week 3)
1. Move `CacheConfiguration` to shared defaults
2. Add missing `ServiceConfiguration` properties to shared
3. Remove duplicates from per-project configs
4. Update all projects to use shared defaults
5. Test all deployment scenarios

### Phase 5: Standardize Cache Keys (Week 4)
1. Update `CacheConfiguration.KeyPrefix` with smart defaults
2. Test cache isolation between projects
3. Update documentation
4. Verify no cache collisions in shared Redis scenarios

---

## Breaking Changes

### For Consumers

**None** - All changes are backward compatible:
- Old timer properties marked `[Obsolete]` but still functional
- Existing configuration files continue to work
- New properties have sensible defaults

### For Developers

**Minor** - Configuration structure changes:
- Must update shared configuration when adding new settings
- Feature flags now expected in all projects
- Cache key prefixes follow new standard

---

## Testing Strategy

### Unit Tests
- Configuration loading and precedence
- CRON schedule parsing
- Feature flag resolution
- Cache key generation

### Integration Tests
- Functions timer triggers with new CRON schedules
- WorkerService background tasks with CRON
- Cache isolation between projects
- Configuration hot-reload

### Manual Testing
- Deploy to local Aspire environment
- Deploy to Azure (dev environment)
- Verify all timer triggers fire correctly
- Verify cache keys don't collide

---

## Rollback Plan

If issues are discovered:

1. **Phase 2-3**: Revert feature flag and timer changes, restore old properties
2. **Phase 4**: Revert shared configuration consolidation, restore per-project configs
3. **Phase 5**: Revert cache key changes, restore old prefixes

All changes are additive and maintain backward compatibility.

---

## Success Criteria

- [ ] All projects use consistent timer scheduling (CRON or unified model)
- [ ] All feature flags defined and documented
- [ ] Shared configuration contains all common settings
- [ ] Cache keys follow standard naming convention
- [ ] Configuration guide complete and accurate
- [ ] All existing tests pass
- [ ] Manual testing confirms no regressions

---

## Open Questions

1. **CRON Format**: Should we use 6-part (with seconds) or 5-part (without seconds)?
   - **Recommendation**: 6-part for consistency with Azure Functions

2. **Cache Key Separator**: Colon (`:`) or dash (`-`)?
   - **Recommendation**: Colon (`:`) is Redis convention

3. **Obsolete Timeline**: When to remove obsolete properties?
   - **Recommendation**: Mark v1.1, deprecate warnings v1.2, remove v2.0

4. **WorkerService Timer Precision**: Does it need second-level precision?
   - **Recommendation**: No, minute-level is sufficient but CRON provides flexibility

---

## Implementation Checklist

### Step 2: Unify Timer Configuration
- [ ] Add NCronTab NuGet package to WorkerService
- [ ] Add NCronTab NuGet package to CLI
- [ ] Create `CronScheduler` helper class
- [ ] Update `SyncConfiguration` with CRON properties
- [ ] Mark old properties as `[Obsolete]`
- [ ] Update WorkerService to use CRON scheduling
- [ ] Update CLI to use CRON scheduling (if applicable)
- [ ] Add unit tests for CRON parsing
- [ ] Update documentation

### Step 3: Complete Shared Configuration
- [ ] Add complete `ServiceConfiguration` to shared defaults
- [ ] Add complete `CacheConfiguration` to shared defaults
- [ ] Add all feature flags to shared defaults
- [ ] Remove duplicates from Functions `appsettings.json`
- [ ] Remove duplicates from WorkerService `appsettings.json`
- [ ] Remove duplicates from CLI `appsettings.json`
- [ ] Test configuration loading in all projects
- [ ] Update configuration guide

### Step 4: Add Missing Feature Flags
- [ ] Update shared `appsettings.Development.json` with all flags
- [ ] Update shared `appsettings.Production.json` with all flags
- [ ] Update Functions to check all feature flags
- [ ] Update WorkerService to check all feature flags
- [ ] Update CLI to check all feature flags
- [ ] Add feature flag unit tests
- [ ] Document feature flag behavior

---

**Next Steps**: Begin with Step 2 (Timer Configuration Unification) as it's the highest impact change.
