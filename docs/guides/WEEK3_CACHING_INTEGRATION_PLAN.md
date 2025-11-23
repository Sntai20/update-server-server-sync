# Week 3: Caching Integration & Enhancements

**Date**: 2025-01-17  
**Status**: ?? PLANNED  
**Duration**: 3-4 days  
**Overall Progress**: 0% (Not Started)

---

## Executive Summary

Week 3 focuses on integrating the Redis caching infrastructure (created in Week 2) into the orchestrators, adding Redis to the AppHost, creating comprehensive caching tests, and implementing cache invalidation strategies. This phase will complete the distributed caching feature and ensure optimal performance for frequently accessed data.

---

## Objectives

1. **Integrate Caching into Orchestrators** - Add CacheService to metadata and content operations
2. **Cache Invalidation** - Implement automatic cache invalidation after sync operations
3. **Redis Infrastructure** - Add Redis container to Aspire AppHost
4. **Testing** - Create comprehensive unit and integration tests for caching
5. **Configuration** - Update configuration examples and documentation
6. **Health Checks** - Add Redis health monitoring
7. **Documentation** - Complete caching patterns and usage guides

---

## Phase 1: Orchestrator Caching Integration (Day 1)

### Task 1.1: MetadataOrchestrator Caching ?

**File**: `UpdateEngine/src/Core/Orchestrators/MetadataOrchestrator.cs`

#### Changes Needed:

1. **Add CacheService Dependency**
   ```csharp
   private readonly CacheService? cacheService;
   
   public MetadataOrchestrator(
       IMetadataStore metadataStore,
       IOptionsMonitor<AppConfig> configMonitor,
       ILogger<MetadataOrchestrator> logger,
       CacheService? cacheService = null)  // Optional for backward compat
   {
       // ...
       this.cacheService = cacheService;
   }
   ```

2. **Cache GetStatisticsAsync**
   ```csharp
   public async Task<MetadataStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
   {
       // Check if caching enabled
       if (this.cacheService != null && this.configMonitor.CurrentValue.CacheConfiguration.EnableDistributedCache)
       {
           return await this.cacheService.GetOrSetAsync(
               "metadata:stats",
               async () => await ComputeStatisticsAsync(),
               this.cacheService.GetStatisticsExpiration(),
               cancellationToken);
       }
       
       return await ComputeStatisticsAsync();
   }
   
   private async Task<MetadataStatistics> ComputeStatisticsAsync()
   {
       // Existing statistics logic
   }
   ```

3. **Cache GetUpdateDetailsAsync**
   ```csharp
   public async Task<IPackage?> GetUpdateDetailsAsync(
       IPackageIdentity updateId,
       CancellationToken cancellationToken = default)
   {
       var cacheKey = $"metadata:update:{updateId.OpenIdHex}";
       
       if (this.cacheService != null && this.configMonitor.CurrentValue.CacheConfiguration.EnableDistributedCache)
       {
           return await this.cacheService.GetOrSetAsync(
               cacheKey,
               async () => await FetchUpdateDetailsAsync(updateId),
               this.cacheService.GetUpdateDetailsExpiration(),
               cancellationToken);
       }
       
       return await FetchUpdateDetailsAsync(updateId);
   }
   
   private async Task<IPackage?> FetchUpdateDetailsAsync(IPackageIdentity updateId)
   {
       // Existing fetch logic
   }
   ```

**Estimated Time**: 2 hours  
**Tests to Update**: `MetadataOrchestratorTests.cs`

---

### Task 1.2: ContentOrchestrator Caching ?

**File**: `UpdateEngine/src/Core/Orchestrators/ContentOrchestrator.cs`

#### Changes Needed:

1. **Add CacheService Dependency**
   ```csharp
   private readonly CacheService? cacheService;
   
   public ContentOrchestrator(
       IMetadataStore metadataStore,
       IContentStore? contentStore,
       IOptionsMonitor<AppConfig> configMonitor,
       ILogger<ContentOrchestrator> logger,
       CacheService? cacheService = null)
   {
       // ...
       this.cacheService = cacheService;
   }
   ```

2. **Cache GetStatisticsAsync**
   ```csharp
   public async Task<ContentStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
   {
       if (this.cacheService != null && this.configMonitor.CurrentValue.CacheConfiguration.EnableDistributedCache)
       {
           return await this.cacheService.GetOrSetAsync(
               "content:stats",
               async () => await ComputeContentStatisticsAsync(),
               this.cacheService.GetStatisticsExpiration(),
               cancellationToken);
       }
       
       return await ComputeContentStatisticsAsync();
   }
   ```

3. **Cache CheckContentAvailabilityAsync**
   ```csharp
   public async Task<IReadOnlyDictionary<IPackageIdentity, bool>> CheckContentAvailabilityAsync(
       IReadOnlyList<IPackageIdentity> updateIds,
       CancellationToken cancellationToken = default)
   {
       var results = new Dictionary<IPackageIdentity, bool>();
       
       foreach (var updateId in updateIds)
       {
           var cacheKey = $"content:availability:{updateId.OpenIdHex}";
           
           if (this.cacheService != null && this.configMonitor.CurrentValue.CacheConfiguration.EnableDistributedCache)
           {
               var available = await this.cacheService.GetOrSetAsync(
                   cacheKey,
                   async () => await CheckSingleContentAvailabilityAsync(updateId),
                   this.cacheService.GetContentAvailabilityExpiration(),
                   cancellationToken);
               results[updateId] = available;
           }
           else
           {
               results[updateId] = await CheckSingleContentAvailabilityAsync(updateId);
           }
       }
       
       return results;
   }
   
   private async Task<bool> CheckSingleContentAvailabilityAsync(IPackageIdentity updateId)
   {
       // Existing availability check logic
   }
   ```

**Estimated Time**: 2 hours  
**Tests to Update**: `ContentOrchestratorTests.cs`

---

### Task 1.3: SyncOrchestrator Cache Invalidation ?

**File**: `UpdateEngine/src/Core/Orchestrators/SyncOrchestrator.cs`

#### Changes Needed:

1. **Add CacheService Dependency**
   ```csharp
   private readonly CacheService? cacheService;
   
   public SyncOrchestrator(
       IMetadataStore metadataStore,
       IContentStore? contentStore,
       IOptionsMonitor<AppConfig> configMonitor,
       ILogger<SyncOrchestrator> logger,
       CacheService? cacheService = null)
   {
       // ...
       this.cacheService = cacheService;
   }
   ```

2. **Invalidate After Metadata Sync**
   ```csharp
   public async Task<SyncResult> SyncMetadataAsync(
       SyncFilter? filter = null,
       IProgress<SyncProgress>? progress = null,
       CancellationToken cancellationToken = default)
   {
       try
       {
           // Existing sync logic
           var result = await PerformMetadataSyncAsync(filter, progress, cancellationToken);
           
           // Invalidate caches if sync successful and caching enabled
           if (result.Success && this.cacheService != null)
           {
               var config = this.configMonitor.CurrentValue.CacheConfiguration;
               if (config.EnableDistributedCache && config.InvalidateOnSync)
               {
                   await this.cacheService.InvalidateAllCachesAsync();
                   this.logger.LogInformation("Invalidated all caches after successful metadata sync");
               }
           }
           
           return result;
       }
       catch (Exception ex)
       {
           this.logger.LogError(ex, "Metadata sync failed");
           throw;
       }
   }
   ```

3. **Invalidate After Content Download**
   ```csharp
   public async Task<DownloadResult> DownloadContentAsync(
       IReadOnlyList<IPackageIdentity> updateIds,
       IProgress<DownloadProgress>? progress = null,
       CancellationToken cancellationToken = default)
   {
       // Existing download logic
       var result = await PerformContentDownloadAsync(updateIds, progress, cancellationToken);
       
       // Invalidate content caches if successful
       if (result.Success && this.cacheService != null)
       {
           var config = this.configMonitor.CurrentValue.CacheConfiguration;
           if (config.EnableDistributedCache && config.InvalidateOnSync)
           {
               // Invalidate content stats
               await this.cacheService.RemoveAsync("content:stats");
               
               // Invalidate availability for affected updates
               foreach (var updateId in updateIds)
               {
                   await this.cacheService.RemoveAsync($"content:availability:{updateId.OpenIdHex}");
               }
               
               this.logger.LogInformation("Invalidated content caches after download");
           }
       }
       
       return result;
   }
   ```

**Estimated Time**: 1.5 hours  
**Tests to Create**: New tests for cache invalidation

---

## Phase 2: AppHost Redis Integration (Day 1-2)

### Task 2.1: Add Redis Container to AppHost ?

**File**: `AppHost/src/Program.cs`

#### Changes Needed:

```csharp
// Add Redis container for distributed caching
var redis = builder.AddRedis("redis")
    .WithRedisCommander();  // Optional: Web UI for Redis

// Add Redis reference to UpdateEngine
updateFunctions
    .WithReference(redis, "RedisConnection")
    .WaitFor(redis);
```

**Configuration Update**:
- Redis will be available on default port 6379 in development
- Connection string automatically injected as `RedisConnection`

**Estimated Time**: 30 minutes

---

### Task 2.2: Update ConfigurationHelper ?

**File**: `AppHost/src/ConfigurationHelper.cs`

#### Changes Needed:

```csharp
public static void ConfigureUpdateFunctions(
    IResourceBuilder<AzureFunctionsProjectResource> functions,
    IConfiguration configuration)
{
    // ...existing code...
    
    // Set cache configuration from CacheConfiguration
    functions
        .WithEnvironment("EnableDistributedCache", appConfig.CacheConfiguration.EnableDistributedCache.ToString())
        .WithEnvironment("DefaultExpirationMinutes", appConfig.CacheConfiguration.DefaultExpirationMinutes.ToString())
        .WithEnvironment("StatisticsCacheMinutes", appConfig.CacheConfiguration.StatisticsCacheMinutes.ToString())
        .WithEnvironment("UpdateDetailsCacheMinutes", appConfig.CacheConfiguration.UpdateDetailsCacheMinutes.ToString())
        .WithEnvironment("ContentAvailabilityCacheMinutes", appConfig.CacheConfiguration.ContentAvailabilityCacheMinutes.ToString())
        .WithEnvironment("InvalidateOnSync", appConfig.CacheConfiguration.InvalidateOnSync.ToString())
        .WithEnvironment("CacheKeyPrefix", appConfig.CacheConfiguration.KeyPrefix);
    
    // Redis connection string will be set by .WithReference(redis)
}
```

**Estimated Time**: 30 minutes

---

### Task 2.3: Update ServiceCollectionExtensions ?

**File**: `UpdateEngine/src/Core/ServiceCollectionExtensions.cs`

#### Changes Needed:

Update Redis registration to use connection string from environment:

```csharp
// Register Redis distributed cache if enabled
var cacheConfig = configuration.GetSection("CacheConfiguration").Get<CacheConfiguration>() ?? new CacheConfiguration();

if (cacheConfig.EnableDistributedCache)
{
    var redisConnectionString = configuration.GetConnectionString("RedisConnection") 
        ?? cacheConfig.RedisConnectionString;
    
    if (!string.IsNullOrEmpty(redisConnectionString))
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = $"{cacheConfig.KeyPrefix}:";
        });
        
        logger?.LogInformation("Redis distributed cache enabled with connection: {Connection}", 
            redisConnectionString.Split(',')[0]); // Log only server part
    }
    else
    {
        logger?.LogWarning("Redis connection string not configured, caching will be disabled");
        services.AddSingleton<IDistributedCache>(new NullDistributedCache());
    }
}
else
{
    services.AddSingleton<IDistributedCache>(new NullDistributedCache());
}
```

**Estimated Time**: 30 minutes

---

## Phase 3: Configuration Updates (Day 2)

### Task 3.1: Update appsettings.example.json ?

**File**: `Configuration/appsettings.example.json`

#### Changes Needed:

```json
{
  "AppConfig": {
    "StorageConfiguration": {
      // ...existing...
    },
    "ServiceConfiguration": {
      // ...existing...
    },
    "SyncConfiguration": {
      // ...existing...
    },
    "FeatureFlags": {
      // ...existing...
    },
    "CacheConfiguration": {
      "EnableDistributedCache": false,
      "RedisConnectionString": "localhost:6379",
      "DefaultExpirationMinutes": 30,
      "StatisticsCacheMinutes": 5,
      "UpdateDetailsCacheMinutes": 60,
      "ContentAvailabilityCacheMinutes": 15,
      "InvalidateOnSync": true,
      "KeyPrefix": "update-server"
    }
  },
  "ConnectionStrings": {
    "RedisConnection": "localhost:6379",
    "MetadataStorageConnection": "",
    "ContentStorageConnection": ""
  }
}
```

**Estimated Time**: 15 minutes

---

### Task 3.2: Create Cache Configuration Guide ?

**File**: `docs/guides/CACHING_GUIDE.md`

#### Content Outline:

1. **Overview**
   - What is cached
   - Why caching is important
   - Cache key structure

2. **Configuration**
   - EnableDistributedCache flag
   - Redis connection strings
   - TTL settings
   - Cache key prefix

3. **Cache Patterns**
   - Cache-aside pattern
   - Automatic invalidation
   - Manual invalidation

4. **Best Practices**
   - When to enable caching
   - TTL tuning
   - Monitoring cache hit rates
   - Troubleshooting

5. **Development Setup**
   - Running Redis locally
   - Using Aspire Redis container
   - Testing with/without cache

6. **Production Deployment**
   - Azure Cache for Redis
   - Connection string security
   - Monitoring and alerts

**Estimated Time**: 2 hours

---

## Phase 4: Testing (Day 2-3)

### Task 4.1: CacheService Unit Tests ?

**File**: `UpdateEngine/test/Unit/Services/CacheServiceTests.cs`

#### Test Cases:

```csharp
public class CacheServiceTests
{
    [Fact]
    public async Task GetOrSetAsync_CacheMiss_CallsFactory() { }
    
    [Fact]
    public async Task GetOrSetAsync_CacheHit_ReturnsFromCache() { }
    
    [Fact]
    public async Task GetAsync_ExistingKey_ReturnsValue() { }
    
    [Fact]
    public async Task GetAsync_NonExistentKey_ReturnsDefault() { }
    
    [Fact]
    public async Task SetAsync_ValidValue_Succeeds() { }
    
    [Fact]
    public async Task RemoveAsync_ExistingKey_RemovesValue() { }
    
    [Fact]
    public async Task InvalidateStatisticsCacheAsync_RemovesAllStatsCaches() { }
    
    [Fact]
    public async Task InvalidateUpdateCacheAsync_RemovesSpecificUpdate() { }
    
    [Fact]
    public async Task InvalidateAllCachesAsync_RemovesAllCaches() { }
    
    [Fact]
    public async Task GetOrSetAsync_FactoryThrows_PropagatesException() { }
    
    [Fact]
    public async Task GetOrSetAsync_CacheFailure_FallsBackToFactory() { }
    
    [Fact]
    public void GetStatisticsExpiration_ReturnsConfiguredValue() { }
    
    [Fact]
    public void GetUpdateDetailsExpiration_ReturnsConfiguredValue() { }
    
    [Fact]
    public void GetContentAvailabilityExpiration_ReturnsConfiguredValue() { }
}
```

**Estimated Time**: 3 hours

---

### Task 4.2: Orchestrator Caching Integration Tests ?

**File**: `UpdateEngine/test/Integration/Orchestrators/CachingIntegrationTests.cs`

#### Test Cases:

```csharp
[Collection("Integration")]
public class CachingIntegrationTests : IAsyncLifetime
{
    [Fact]
    public async Task MetadataOrchestrator_GetStatistics_CachesResult() { }
    
    [Fact]
    public async Task MetadataOrchestrator_GetStatistics_CacheHit_ReturnsCachedValue() { }
    
    [Fact]
    public async Task MetadataOrchestrator_GetUpdateDetails_CachesResult() { }
    
    [Fact]
    public async Task ContentOrchestrator_GetStatistics_CachesResult() { }
    
    [Fact]
    public async Task ContentOrchestrator_CheckAvailability_CachesResult() { }
    
    [Fact]
    public async Task SyncOrchestrator_AfterSync_InvalidatesCaches() { }
    
    [Fact]
    public async Task CacheDisabled_OrchestratorsFunctionNormally() { }
}
```

**Estimated Time**: 3 hours

---

### Task 4.3: AppHost Integration Test ?

**File**: `UpdateEngine/test/Integration/AppHostCachingTest.cs`

#### Test Cases:

```csharp
[Collection("AspireAppHost")]
public class AppHostCachingTest
{
    [Fact]
    public async Task AppHost_StartsWithRedis_Successfully() { }
    
    [Fact]
    public async Task Functions_ConnectToRedis_Successfully() { }
    
    [Fact]
    public async Task GetStatistics_WithRedis_ReturnsCorrectly() { }
    
    [Fact]
    public async Task CacheInvalidation_AfterSync_WorksCorrectly() { }
}
```

**Estimated Time**: 2 hours

---

## Phase 5: Health Checks (Day 3)

### Task 5.1: Redis Health Check ?

**File**: `UpdateEngine/src/Core/HealthChecks/RedisHealthCheck.cs`

#### Implementation:

```csharp
public class RedisHealthCheck : IHealthCheck
{
    private readonly IDistributedCache cache;
    private readonly ILogger<RedisHealthCheck> logger;
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to write and read a test value
            var testKey = "health-check-test";
            var testValue = DateTime.UtcNow.ToString("O");
            
            await this.cache.SetStringAsync(testKey, testValue, cancellationToken);
            var retrieved = await this.cache.GetStringAsync(testKey, cancellationToken);
            await this.cache.RemoveAsync(testKey, cancellationToken);
            
            if (retrieved == testValue)
            {
                return HealthCheckResult.Healthy("Redis is responding correctly");
            }
            
            return HealthCheckResult.Degraded("Redis responded but value mismatch");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis health check failed", ex);
        }
    }
}
```

**Registration**:
```csharp
// In ServiceCollectionExtensions.cs
if (cacheConfig.EnableDistributedCache)
{
    builder.Services.AddHealthChecks()
        .AddCheck<RedisHealthCheck>("redis", tags: new[] { "cache", "redis" });
}
```

**Estimated Time**: 1 hour

---

## Phase 6: Documentation (Day 3-4)

### Task 6.1: Create Caching Architecture Document ?

**File**: `docs/guides/CACHING_ARCHITECTURE.md`

#### Content:

1. **Architecture Overview**
   - Diagram of caching layers
   - Data flow with cache
   - Cache key hierarchy

2. **Implementation Details**
   - CacheService design
   - Cache-aside pattern
   - Serialization strategy

3. **Performance Impact**
   - Expected cache hit rates
   - Latency improvements
   - Memory considerations

4. **Monitoring**
   - Key metrics to track
   - Health check interpretation
   - Troubleshooting

**Estimated Time**: 2 hours

---

### Task 6.2: Update Main Documentation ?

**Files to Update**:
- `README.md` - Add caching feature
- `docs/guides/CONFIGURATION_GUIDE.md` - Add cache configuration
- `docs/guides/DEPLOYMENT_GUIDE.md` - Add Redis deployment
- `.github/copilot-instructions.md` - Add caching patterns

**Estimated Time**: 1.5 hours

---

### Task 6.3: Create Week 3 Completion Summary ?

**File**: `docs/guides/WEEK3_COMPLETION_SUMMARY.md`

#### Content:

- Executive summary
- Features implemented
- Tests created
- Performance improvements
- Configuration changes
- Deployment notes
- Next steps

**Estimated Time**: 1 hour

---

## Testing Strategy

### Test Categories

1. **Unit Tests** (Fast, Isolated)
   - CacheService behavior
   - Orchestrator caching logic
   - Cache key generation
   - TTL calculations

2. **Integration Tests** (With Infrastructure)
   - Real Redis instance
   - Cache hit/miss scenarios
   - Invalidation verification
   - AppHost orchestration

3. **Performance Tests** (Optional)
   - Cache hit rate measurement
   - Latency improvements
   - Memory usage

### Test Execution Plan

```powershell
# Run all cache-related tests
dotnet test --filter "FullyQualifiedName~Caching"

# Run with real Redis (requires Docker)
docker run -d -p 6379:6379 redis:latest
dotnet test --filter "Category=CacheIntegration"

# Run AppHost tests
dotnet test --filter "Collection=AspireAppHost"
```

---

## Dependencies

### Required
- ? Week 2 Phase 3 complete (CacheService created)
- ? Redis package added (Microsoft.Extensions.Caching.StackExchangeRedis)
- ? Docker for local Redis testing
- ? .NET Aspire Redis component

### Optional
- Redis Commander (Web UI)
- Azure Cache for Redis (production)
- Application Insights (monitoring)

---

## Success Criteria

### Functional
- ? CacheService integrated into all orchestrators
- ? Cache invalidation working after sync
- ? Redis container running in AppHost
- ? Configuration properly passed to Functions
- ? All tests passing (100%)

### Performance
- ? Statistics queries cached (5 min TTL)
- ? Update details cached (60 min TTL)
- ? Content availability cached (15 min TTL)
- ? Cache hit rate > 70% for common queries

### Quality
- ? Unit tests for CacheService (>15 tests)
- ? Integration tests for caching (>7 tests)
- ? Health check for Redis
- ? Documentation complete
- ? Configuration examples updated

---

## Risk Mitigation

### Risk 1: Redis Connection Failures
**Mitigation**: Graceful fallback to non-cached operations

### Risk 2: Cache Invalidation Bugs
**Mitigation**: Comprehensive invalidation tests, logging

### Risk 3: Memory Pressure from Caching
**Mitigation**: Conservative TTL values, monitoring

### Risk 4: Serialization Issues
**Mitigation**: Unit tests for all cached types, JSON serialization settings

---

## Timeline

### Day 1 (8 hours)
- Morning: Orchestrator caching integration (Tasks 1.1-1.3) - 5.5 hours
- Afternoon: AppHost Redis setup (Tasks 2.1-2.2) - 1 hour
- Evening: ServiceCollectionExtensions updates (Task 2.3) - 0.5 hour
- Buffer: 1 hour

### Day 2 (8 hours)
- Morning: Configuration updates (Tasks 3.1-3.2) - 2.25 hours
- Midday: CacheService unit tests (Task 4.1) - 3 hours
- Afternoon: Caching integration tests (Task 4.2) - 2 hours
- Buffer: 0.75 hours

### Day 3 (8 hours)
- Morning: AppHost integration test (Task 4.3) - 2 hours
- Midday: Redis health check (Task 5.1) - 1 hour
- Afternoon: Documentation (Tasks 6.1-6.2) - 3.5 hours
- Evening: Week 3 summary (Task 6.3) - 1 hour
- Buffer: 0.5 hours

### Day 4 (Optional, 4 hours)
- Final testing and verification
- Performance testing
- Documentation polish
- Demo preparation

---

## Deliverables

### Code
- ? Orchestrators with caching
- ? AppHost with Redis
- ? Health checks
- ? Configuration updates

### Tests
- ? CacheService unit tests (15+)
- ? Caching integration tests (7+)
- ? AppHost tests (4+)
- ? All tests passing

### Documentation
- ? CACHING_GUIDE.md
- ? CACHING_ARCHITECTURE.md
- ? Configuration examples
- ? WEEK3_COMPLETION_SUMMARY.md
- ? Updated README and main docs

---

**Plan Created**: 2025-01-17  
**Estimated Duration**: 3-4 days  
**Total Estimated Hours**: 24-32 hours  
**Complexity**: Medium  
**Priority**: High
