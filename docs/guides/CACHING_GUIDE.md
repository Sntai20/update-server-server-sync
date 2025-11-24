# Caching Guide: Redis Integration for Update Engine

## ?? Overview

The Update Engine implements a **distributed caching layer** using Redis to improve performance and reduce load on metadata/content stores and upstream servers. This guide covers architecture, configuration, patterns, and operational considerations.

### What is Cached?

| Data Type | Cache Key Pattern | TTL | Rationale |
|-----------|-------------------|-----|-----------|
| **Metadata Statistics** | `msupdate:metadata:stats` | 5 minutes | Frequently queried, changes with sync operations |
| **Content Statistics** | `msupdate:content:stats` | 5 minutes | Frequently queried, changes with content sync |
| **Update Package Details** | `msupdate:metadata:update:{id}` | 60 minutes | Stable data, large objects benefit from caching |
| **Content Availability** | `msupdate:content:availability:{id}` | 15 minutes | Balance between freshness and performance |
| **Sync Status** | `msupdate:sync:status` | 5 minutes | Operational data, needs frequent updates |

### Why Cache?

**Performance Benefits:**
- ? **50-90% reduction** in metadata store queries for statistics
- ? **70-95% reduction** in content availability checks
- ? **Sub-millisecond** response times for cached data vs. 10-100ms for store queries

**Scalability Benefits:**
- ?? Supports higher concurrent request volumes
- ?? Reduces load on Azure Blob Storage (lower costs)
- ?? Enables horizontal scaling of Azure Functions

**Availability Benefits:**
- ??? Graceful degradation if Redis unavailable (falls back to direct queries)
- ??? Reduces impact of temporary store unavailability

## ??? Architecture

### Cache-Aside Pattern

The Update Engine uses the **cache-aside** (lazy loading) pattern:

```
???????????????
?   Request   ?
???????????????
       ?
       ?
????????????????????????????
?  Check Redis Cache       ???????
????????????????????????????     ?
       ?                         ?
       ? Cache Miss              ? Cache Hit
       ?                         ?
????????????????????????????    ?
?  Query Store/Compute     ?    ?
????????????????????????????    ?
       ?                         ?
       ? Store in Cache          ?
       ?                         ?
????????????????????????????    ?
?  Return Value            ??????
????????????????????????????
```

### Implementation Example

```csharp
// From MetadataOrchestrator.cs
public async Task<MetadataStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
{
    if (this.cacheService != null && this.config.CurrentValue.CacheConfiguration.EnableDistributedCache)
    {
        return await this.cacheService.GetOrSetAsync(
            "metadata:stats",
            async () => await this.ComputeStatisticsAsync(),
            this.cacheService.GetStatisticsExpiration(),
            cancellationToken);
    }
    
    return await this.ComputeStatisticsAsync();
}
```

**Flow:**
1. Check if caching enabled
2. Try to get value from Redis using `GetOrSetAsync`
3. On cache miss, execute factory function (`ComputeStatisticsAsync`)
4. Store result in Redis with configured TTL
5. Return value to caller

### Cache Invalidation Strategy

**Automatic Invalidation After Sync:**
```csharp
// From SyncOrchestrator.cs
private async Task InvalidateCachesAfterSyncAsync()
{
    if (this.cacheService != null && 
        this.config.CurrentValue.CacheConfiguration.InvalidateOnSync)
    {
        await this.cacheService.InvalidateStatisticsCacheAsync();
        // Additional invalidation logic...
    }
}
```

**Invalidation Patterns:**
- ? **Statistics**: Invalidated after every sync operation
- ? **Update Details**: Invalidated only for affected updates
- ? **Content Availability**: Invalidated after content sync
- ? **Sync Status**: Invalidated when sync completes

### Cache Key Hierarchy

```
msupdate:                          # Key prefix (configurable)
??? metadata:
?   ??? stats                      # Metadata statistics
?   ??? update:{updateId}          # Individual update details
??? content:
?   ??? stats                      # Content statistics
?   ??? availability:{updateId}    # Content availability flags
??? sync:
    ??? status                     # Current sync operation status
```

## ?? Configuration

### appsettings.json Configuration

```json
{
  "UpdateEngine": {
    "CacheConfiguration": {
      "EnableDistributedCache": true,
      "KeyPrefix": "msupdate:",
      "DefaultExpirationMinutes": 60,
      "StatisticsCacheMinutes": 5,
      "UpdateDetailsCacheMinutes": 60,
      "ContentAvailabilityCacheMinutes": 15,
      "InvalidateOnSync": true
    }
  },
  "ConnectionStrings": {
    "RedisConnection": "localhost:6379"
  }
}
```

### Configuration Options Explained

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `EnableDistributedCache` | bool | `false` | Master switch for caching. Set to `true` to enable Redis. |
| `KeyPrefix` | string | `"msupdate:"` | Prefix for all cache keys to avoid collisions. |
| `DefaultExpirationMinutes` | int | `60` | Default TTL when not specified explicitly. |
| `StatisticsCacheMinutes` | int | `5` | TTL for statistics (frequently changing data). |
| `UpdateDetailsCacheMinutes` | int | `60` | TTL for update package details (stable data). |
| `ContentAvailabilityCacheMinutes` | int | `15` | TTL for content availability checks (balance). |
| `InvalidateOnSync` | bool | `true` | Auto-invalidate caches after successful sync. |

### Redis Connection Strings

**Local Development (Aspire):**
```json
"RedisConnection": "localhost:6379"
```

**Azure Cache for Redis (Standard):**
```json
"RedisConnection": "your-cache.redis.cache.windows.net:6380,password=your-key,ssl=True"
```

**Azure Cache for Redis (Connection String from Portal):**
```json
"RedisConnection": "your-cache.redis.cache.windows.net:6380,password=your-primary-key,ssl=True,abortConnect=False"
```

### Environment Variables (Azure Functions)

Set in `local.settings.json` or Azure Portal:

```json
{
  "Values": {
    "UpdateEngine__CacheConfiguration__EnableDistributedCache": "true",
    "UpdateEngine__CacheConfiguration__KeyPrefix": "msupdate:",
    "ConnectionStrings__RedisConnection": "localhost:6379"
  }
}
```

## ?? Development Setup

### Option 1: Aspire (Recommended)

The easiest way to run Redis locally is using .NET Aspire:

```bash
# 1. Start AppHost (includes Redis container)
cd UpdateEngine.AppHost/src
dotnet run

# Result:
# ? Redis container started on localhost:6379
# ? Azure Functions running on localhost:7071
# ? Aspire Dashboard at http://localhost:15888
```

**AppHost automatically:**
- Starts Redis container via Docker
- Configures connection string
- Passes configuration to Azure Functions
- Provides monitoring dashboard

**View in Aspire Dashboard:**
- Navigate to http://localhost:15888
- See Redis resource status
- View logs and metrics
- Monitor cache operations

### Option 2: Docker

Run Redis manually with Docker:

```bash
# Start Redis container
docker run -d -p 6379:6379 --name redis redis:latest

# Verify running
docker ps | grep redis

# View logs
docker logs redis

# Stop container
docker stop redis

# Remove container
docker rm redis
```

### Option 3: Redis CLI (Testing)

Install Redis locally and use CLI:

```bash
# Install Redis (Windows - requires WSL or native port)
# Install Redis (macOS)
brew install redis

# Install Redis (Linux)
sudo apt-get install redis-server

# Start Redis server
redis-server

# Connect with CLI
redis-cli

# Test commands
SET test "Hello"
GET test
KEYS msupdate:*
TTL msupdate:metadata:stats
FLUSHDB
```

### Development Workflow

**1. Start with Aspire:**
```bash
cd UpdateEngine.AppHost/src && dotnet run
```

**2. Verify Redis Connection:**
```bash
# Check health endpoint
curl http://localhost:7071/api/health

# Look for "redis-cache" in response:
{
  "status": "Healthy",
  "checks": [
    {
      "name": "redis-cache",
      "status": "Healthy",
      "data": {
        "ResponseTimeMs": "12",
        "CacheType": "Redis",
        "LastCheckTime": "2025-01-16T10:30:00Z"
      }
    }
  ]
}
```

**3. Test Cache Operations:**
```bash
# First request (cache miss)
curl http://localhost:7071/api/metadata/statistics
# Response time: ~50ms (queries store)

# Second request (cache hit)
curl http://localhost:7071/api/metadata/statistics
# Response time: ~5ms (from Redis)
```

**4. Monitor Cache with Redis CLI:**
```bash
redis-cli

# View all cache keys
KEYS msupdate:*

# Check specific key
GET msupdate:metadata:stats

# Check TTL
TTL msupdate:metadata:stats

# Clear all caches
KEYS msupdate:* | xargs redis-cli DEL
```

## ?? Cache Patterns & Best Practices

### Pattern 1: Cache-Aside for Expensive Operations

**When to Use:**
- Expensive database queries
- Complex computations
- Aggregations across multiple entities

**Example:**
```csharp
public async Task<MetadataStatistics> GetStatisticsAsync()
{
    return await this.cacheService.GetOrSetAsync(
        "metadata:stats",
        async () => await this.ComputeExpensiveStatisticsAsync(),
        this.cacheService.GetStatisticsExpiration(),
        cancellationToken);
}
```

### Pattern 2: Direct Cache for Simple Lookups

**When to Use:**
- Simple key-value lookups
- Boolean flags
- Small data structures

**Example:**
```csharp
public async Task<bool> CheckContentAvailabilityAsync(Guid updateId)
{
    var cacheKey = $"content:availability:{updateId}";
    
    var cached = await this.cacheService.GetAsync<bool?>(cacheKey);
    if (cached.HasValue)
    {
        return cached.Value;
    }
    
    var isAvailable = await this.CheckStoreForContentAsync(updateId);
    
    await this.cacheService.SetAsync(
        cacheKey,
        isAvailable,
        this.cacheService.GetContentAvailabilityExpiration());
    
    return isAvailable;
}
```

### Pattern 3: Selective Invalidation

**When to Use:**
- Only specific data changed
- Want to preserve most cached data

**Example:**
```csharp
public async Task UpdateSinglePackageAsync(Guid updateId)
{
    await this.metadataStore.UpdatePackageAsync(updateId, data);
    
    // Only invalidate this specific update's cache
    await this.cacheService.InvalidateUpdateCacheAsync(updateId);
    
    // Statistics still cached until next sync
}
```

### Pattern 4: Bulk Invalidation

**When to Use:**
- Major data changes (sync operations)
- Ensure consistency after updates

**Example:**
```csharp
private async Task InvalidateCachesAfterSyncAsync()
{
    if (this.config.CurrentValue.CacheConfiguration.InvalidateOnSync)
    {
        // Invalidate all cached statistics
        await this.cacheService.InvalidateStatisticsCacheAsync();
        
        // Optionally: Full cache clear
        // await this.cacheService.InvalidateAllCachesAsync();
    }
}
```

### Best Practices

? **DO:**
- Use caching for read-heavy operations
- Set appropriate TTLs based on data volatility
- Implement graceful degradation (cache failures shouldn't break app)
- Monitor cache hit rates in production
- Use cache key prefixes to avoid collisions
- Invalidate caches after data changes

? **DON'T:**
- Cache frequently changing data (use short TTLs)
- Cache sensitive data without encryption
- Rely solely on cache for critical data (always have fallback)
- Use overly long TTLs (stale data issues)
- Cache large objects unnecessarily (consider cost)

### TTL Tuning Guidelines

| Data Volatility | Recommended TTL | Example |
|-----------------|-----------------|---------|
| **High** (changes every minute) | 1-5 minutes | Sync status, active operations |
| **Medium** (changes hourly/daily) | 15-60 minutes | Statistics, aggregations |
| **Low** (changes infrequently) | 1-24 hours | Update package metadata |
| **Static** (rarely changes) | 24+ hours | Categories, classifications |

## ?? Production Deployment

### Azure Cache for Redis Setup

**1. Create Azure Cache for Redis:**
```bash
az redis create \
  --name update-engine-cache \
  --resource-group update-engine-rg \
  --location eastus \
  --sku Standard \
  --vm-size C1
```

**2. Get Connection String:**
```bash
az redis list-keys \
  --name update-engine-cache \
  --resource-group update-engine-rg
```

**3. Configure Azure Functions:**
```bash
az functionapp config appsettings set \
  --name update-engine-functions \
  --resource-group update-engine-rg \
  --settings \
    "ConnectionStrings__RedisConnection=update-engine-cache.redis.cache.windows.net:6380,password=YOUR_KEY,ssl=True" \
    "UpdateEngine__CacheConfiguration__EnableDistributedCache=true"
```

### Recommended SKUs

| SKU | RAM | Connections | Use Case |
|-----|-----|-------------|----------|
| **Basic C0** | 250 MB | 256 | Development only |
| **Standard C1** | 1 GB | 1,000 | Small production |
| **Standard C2** | 2.5 GB | 2,000 | Medium production |
| **Standard C3** | 6 GB | 5,000 | Large production |
| **Premium P1** | 6 GB | 7,500 | High availability required |

**Recommendations:**
- **Development**: Use Aspire/Docker (free)
- **Staging**: Standard C1 ($73/month)
- **Production**: Standard C2+ ($183/month) with clustering
- **Mission-Critical**: Premium P1+ ($295/month) with geo-replication

### Security Configuration

**1. Enable SSL/TLS:**
```json
"RedisConnection": "your-cache.redis.cache.windows.net:6380,password=key,ssl=True"
```

**2. Use Managed Identity (Recommended):**
```csharp
// In ServiceCollectionExtensions.cs
services.AddStackExchangeRedisCache(options =>
{
    options.ConfigurationOptions = ConfigurationOptions.Parse(connectionString);
    options.ConfigurationOptions.AbortOnConnectFail = false;
    
    // Use Managed Identity
    options.ConfigurationOptions.Ssl = true;
    options.ConfigurationOptions.DefaultDatabase = 0;
});
```

**3. Network Isolation:**
- Use Azure Private Link for Redis
- Restrict access to specific VNets
- Enable firewall rules for specific IP ranges

**4. Key Rotation:**
- Rotate Redis keys every 90 days
- Use Azure Key Vault for storing connection strings
- Update Azure Functions configuration after rotation

### Monitoring & Alerting

**Key Metrics to Monitor:**

| Metric | Threshold | Action |
|--------|-----------|--------|
| Cache Hit Rate | < 70% | Review caching strategy, adjust TTLs |
| Server Load | > 80% | Scale up Redis SKU |
| Connected Clients | Near max | Scale up or implement connection pooling |
| Memory Usage | > 85% | Scale up SKU or reduce TTLs |
| Evictions | > 0 | Increase memory or reduce cache size |

**Azure Monitor Alerts:**
```bash
az monitor metrics alert create \
  --name redis-high-memory \
  --resource-group update-engine-rg \
  --scopes /subscriptions/{sub}/resourceGroups/update-engine-rg/providers/Microsoft.Cache/Redis/update-engine-cache \
  --condition "avg Percentage Memory Usage > 85" \
  --window-size 5m \
  --evaluation-frequency 1m
```

**Application Insights Integration:**
```csharp
// CacheService logs operations to Application Insights
this.logger.LogInformation(
    "Cache {Operation} for key {Key} took {Duration}ms",
    "GET", cacheKey, duration);
```

### Performance Optimization

**Connection Pooling:**
```csharp
services.AddStackExchangeRedisCache(options =>
{
    options.ConfigurationOptions = ConfigurationOptions.Parse(connectionString);
    options.ConfigurationOptions.AbortOnConnectFail = false;
    options.ConfigurationOptions.ConnectTimeout = 5000;
    options.ConfigurationOptions.SyncTimeout = 5000;
    
    // Connection pool settings
    options.InstanceName = "msupdate";
});
```

**Compression for Large Objects:**
```csharp
// Consider compressing large cached objects
var json = JsonSerializer.Serialize(largeObject);
var compressed = await CompressAsync(json);
await cache.SetAsync(key, compressed);
```

**Batch Operations:**
```csharp
// Invalidate multiple keys in one operation
var keysToInvalidate = new[] 
{ 
    "metadata:stats", 
    "content:stats", 
    "sync:status" 
};

await Task.WhenAll(
    keysToInvalidate.Select(key => this.cacheService.RemoveAsync(key))
);
```

## ?? Troubleshooting

### Issue: Redis Connection Fails

**Symptoms:**
- Health check shows "redis-cache: Unhealthy"
- Logs show "It was not possible to connect to the redis server(s)"

**Solutions:**
1. **Verify Redis is running:**
   ```bash
   # Docker
   docker ps | grep redis
   
   # Aspire Dashboard
   # Check http://localhost:15888 for Redis status
   ```

2. **Check connection string:**
   ```bash
   # Test connection
   redis-cli -h localhost -p 6379 ping
   # Should return "PONG"
   ```

3. **Verify firewall rules (Azure):**
   ```bash
   az redis firewall-rules list \
     --name update-engine-cache \
     --resource-group update-engine-rg
   ```

4. **Check SSL configuration:**
   - Azure Cache requires SSL (port 6380)
   - Local Redis doesn't use SSL (port 6379)

### Issue: Low Cache Hit Rate

**Symptoms:**
- Performance not improving
- Cache hit rate < 50% in metrics

**Solutions:**
1. **Increase TTLs:**
   ```json
   {
     "StatisticsCacheMinutes": 10,  // Was 5
     "UpdateDetailsCacheMinutes": 120  // Was 60
   }
   ```

2. **Check invalidation strategy:**
   - Is `InvalidateOnSync` too aggressive?
   - Consider selective invalidation instead of full clear

3. **Monitor request patterns:**
   - Are most requests for unique data?
   - Consider caching different data types

### Issue: Stale Data in Cache

**Symptoms:**
- UI shows old data after sync
- Statistics don't update

**Solutions:**
1. **Enable automatic invalidation:**
   ```json
   {
     "InvalidateOnSync": true
   }
   ```

2. **Reduce TTLs for volatile data:**
   ```json
   {
     "StatisticsCacheMinutes": 2,  // Was 5
     "SyncStatusCacheMinutes": 1   // Very short TTL
   }
   ```

3. **Manual cache clear:**
   ```bash
   redis-cli KEYS "msupdate:*" | xargs redis-cli DEL
   ```

### Issue: Memory Usage Too High

**Symptoms:**
- Azure Redis showing high memory usage
- Evictions occurring

**Solutions:**
1. **Reduce TTLs:**
   - Shorter TTLs = less data stored
   - Review which data really needs caching

2. **Implement cache size limits:**
   ```csharp
   // Only cache objects smaller than 1MB
   if (json.Length < 1024 * 1024)
   {
       await cache.SetAsync(key, bytes);
   }
   ```

3. **Scale up Redis SKU:**
   ```bash
   az redis update \
     --name update-engine-cache \
     --resource-group update-engine-rg \
     --sku Standard \
     --vm-size C2  # Upgrade to 2.5 GB
   ```

### Issue: Cache Operations Slow

**Symptoms:**
- Cache operations taking > 50ms
- Timeouts in logs

**Solutions:**
1. **Check Redis server load:**
   - View metrics in Azure Portal or Aspire Dashboard
   - Look for high CPU or network usage

2. **Optimize network latency:**
   - Ensure Azure Functions and Redis in same region
   - Use Premium tier for better network performance

3. **Implement connection resilience:**
   ```csharp
   options.ConfigurationOptions.AbortOnConnectFail = false;
   options.ConfigurationOptions.ConnectRetry = 3;
   options.ConfigurationOptions.ReconnectRetryPolicy = new ExponentialRetry(5000);
   ```

### Health Check Status Codes

| Status | Meaning | Action |
|--------|---------|--------|
| **Healthy** | Redis working normally | No action |
| **Degraded** | Redis slow or value mismatch | Monitor, may need scaling |
| **Unhealthy** | Cannot connect to Redis | Check connection, firewall, Redis status |

**Health Check Endpoint:**
```bash
curl http://localhost:7071/api/health

# Filter for cache only
curl http://localhost:7071/api/health?tags=cache
```

## ?? Monitoring & Observability

### Cache Metrics Dashboard

**Key Metrics:**
- Cache hit rate (target: > 80%)
- Average cache response time (target: < 10ms)
- Cache evictions (target: 0)
- Memory usage (target: < 80%)
- Connected clients

**Logging:**
```csharp
// CacheService logs all operations
this.logger.LogDebug("Cache HIT for key {Key}", cacheKey);
this.logger.LogDebug("Cache MISS for key {Key}", cacheKey);
this.logger.LogWarning("Cache operation failed: {Error}", ex.Message);
```

**Application Insights Queries:**
```kusto
// Cache hit rate
traces
| where message contains "Cache HIT" or message contains "Cache MISS"
| summarize hits = countif(message contains "HIT"), 
           misses = countif(message contains "MISS")
| extend hit_rate = hits * 100.0 / (hits + misses)

// Cache operation duration
traces
| where message contains "Cache operation"
| extend duration = extract(@"took (\d+)ms", 1, message)
| summarize avg(duration), percentile(duration, 95)
```

## ?? Additional Resources

- [Microsoft.Extensions.Caching.Distributed Documentation](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed)
- [Azure Cache for Redis Documentation](https://learn.microsoft.com/en-us/azure/azure-cache-for-redis/)
- [.NET Aspire Redis Component](https://learn.microsoft.com/en-us/dotnet/aspire/caching/stackexchange-redis-component)
- [Redis Best Practices](https://redis.io/docs/manual/patterns/)
- [Cache-Aside Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/cache-aside)

## ?? Summary

**Key Takeaways:**
- ? Caching improves performance by 50-95% for read operations
- ? Use cache-aside pattern with graceful degradation
- ? Configure TTLs based on data volatility
- ? Invalidate caches after data changes
- ? Monitor cache hit rates and adjust strategy
- ? Use Aspire for easy local development
- ? Azure Cache for Redis for production with proper security

**Quick Start:**
1. Enable caching in `appsettings.json`
2. Start AppHost (`dotnet run`)
3. Test with `/api/health` endpoint
4. Monitor cache hit rates
5. Tune TTLs based on metrics

**Next Steps:**
- See [WEEK3_COMPLETION_SUMMARY.md](./WEEK3_COMPLETION_SUMMARY.md) for deployment checklist
- See [ARCHITECTURE_DECISIONS.md](./ARCHITECTURE_DECISIONS.md) for design rationale
- See [TESTING_STRATEGY.md](./TESTING_STRATEGY.md) for cache testing patterns
