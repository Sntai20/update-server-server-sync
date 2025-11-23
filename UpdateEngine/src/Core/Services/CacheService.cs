// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core.Services;

using Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service for distributed caching with Redis.
/// Implements cache-aside pattern with automatic invalidation.
/// </summary>
public class CacheService
{
    private readonly IDistributedCache? distributedCache;
    private readonly IOptionsMonitor<AppConfig> config;
    private readonly ILogger<CacheService> logger;
    private readonly JsonSerializerOptions jsonOptions;

    public CacheService(
        IDistributedCache? distributedCache,
        IOptionsMonitor<AppConfig> config,
        ILogger<CacheService> logger,
        JsonSerializerOptions jsonOptions)
    {
        this.distributedCache = distributedCache;
        this.config = config;
        this.logger = logger;
        this.jsonOptions = jsonOptions;
    }

    /// <summary>
    /// Gets whether caching is enabled.
    /// </summary>
    public bool IsCachingEnabled =>
        this.distributedCache != null &&
        this.config.CurrentValue.CacheConfiguration.EnableDistributedCache;

    /// <summary>
    /// Gets a value from cache or computes it if not found (cache-aside pattern).
    /// </summary>
    public async Task<T?> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> valueFactory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default) where T : notnull
    {
        if (!this.IsCachingEnabled)
        {
            return await valueFactory();
        }

        try
        {
            // Try to get from cache
            var cachedValue = await this.GetAsync<T>(key, cancellationToken);
            if (cachedValue != null)
            {
                this.logger.LogDebug("Cache hit for key: {Key}", key);
                return cachedValue;
            }

            this.logger.LogDebug("Cache miss for key: {Key}", key);

            // Compute the value
            var value = await valueFactory();
            if (value != null)
            {
                await this.SetAsync(key, value, expiration, cancellationToken);
            }

            return value;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Cache operation failed for key: {Key}, falling back to direct computation", key);
            return await valueFactory();
        }
    }

    /// <summary>
    /// Gets a value from cache.
    /// </summary>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : notnull
    {
        if (!this.IsCachingEnabled || this.distributedCache == null)
        {
            return default;
        }

        try
        {
            var fullKey = this.GetFullKey(key);
            var cachedBytes = await this.distributedCache.GetAsync(fullKey, cancellationToken);

            if (cachedBytes == null || cachedBytes.Length == 0)
            {
                return default;
            }

            var value = JsonSerializer.Deserialize<T>(cachedBytes, this.jsonOptions);
            return value;
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to get cache value for key: {Key}", key);
            return default;
        }
    }

    /// <summary>
    /// Sets a value in cache.
    /// </summary>
    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default) where T : notnull
    {
        if (!this.IsCachingEnabled || this.distributedCache == null)
        {
            return;
        }

        try
        {
            var fullKey = this.GetFullKey(key);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, this.jsonOptions);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ??
                    TimeSpan.FromMinutes(this.config.CurrentValue.CacheConfiguration.DefaultExpirationMinutes)
            };

            await this.distributedCache.SetAsync(fullKey, bytes, options, cancellationToken);
            this.logger.LogDebug("Cached value for key: {Key} with expiration: {Expiration}", key, options.AbsoluteExpirationRelativeToNow);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to set cache value for key: {Key}", key);
        }
    }

    /// <summary>
    /// Removes a value from cache.
    /// </summary>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!this.IsCachingEnabled || this.distributedCache == null)
        {
            return;
        }

        try
        {
            var fullKey = this.GetFullKey(key);
            await this.distributedCache.RemoveAsync(fullKey, cancellationToken);
            this.logger.LogDebug("Removed cache entry for key: {Key}", key);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to remove cache value for key: {Key}", key);
        }
    }

    /// <summary>
    /// Invalidates all statistics caches.
    /// </summary>
    public async Task InvalidateStatisticsCacheAsync(CancellationToken cancellationToken = default)
    {
        await this.RemoveAsync("metadata:stats", cancellationToken);
        await this.RemoveAsync("content:stats", cancellationToken);
        await this.RemoveAsync("sync:status", cancellationToken);
        this.logger.LogInformation("Invalidated statistics cache");
    }

    /// <summary>
    /// Invalidates cache for a specific update.
    /// </summary>
    public async Task InvalidateUpdateCacheAsync(string updateId, CancellationToken cancellationToken = default)
    {
        await this.RemoveAsync($"metadata:update:{updateId}", cancellationToken);
        await this.RemoveAsync($"content:availability:{updateId}", cancellationToken);
        this.logger.LogDebug("Invalidated cache for update: {UpdateId}", updateId);
    }

    /// <summary>
    /// Invalidates all caches (typically after sync operations).
    /// </summary>
    public async Task InvalidateAllCachesAsync(CancellationToken cancellationToken = default)
    {
        if (!this.config.CurrentValue.CacheConfiguration.InvalidateOnSync)
        {
            return;
        }

        await this.InvalidateStatisticsCacheAsync(cancellationToken);
        this.logger.LogInformation("Invalidated all caches after sync operation");
    }

    /// <summary>
    /// Builds the full cache key with prefix.
    /// </summary>
    private string GetFullKey(string key)
    {
        var prefix = this.config.CurrentValue.CacheConfiguration.KeyPrefix;
        return $"{prefix}:{key}";
    }

    /// <summary>
    /// Gets cache options for statistics (short-lived).
    /// </summary>
    public TimeSpan GetStatisticsExpiration() =>
        TimeSpan.FromMinutes(this.config.CurrentValue.CacheConfiguration.StatisticsCacheMinutes);

    /// <summary>
    /// Gets cache options for update details (longer-lived).
    /// </summary>
    public TimeSpan GetUpdateDetailsExpiration() =>
        TimeSpan.FromMinutes(this.config.CurrentValue.CacheConfiguration.UpdateDetailsCacheMinutes);

    /// <summary>
    /// Gets cache options for content availability (medium-lived).
    /// </summary>
    public TimeSpan GetContentAvailabilityExpiration() =>
        TimeSpan.FromMinutes(this.config.CurrentValue.CacheConfiguration.ContentAvailabilityCacheMinutes);
}
