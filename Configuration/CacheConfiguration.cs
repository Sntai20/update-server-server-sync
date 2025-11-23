// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Configuration;

/// <summary>
/// Configuration for distributed caching (Redis).
/// </summary>
public class CacheConfiguration
{
    /// <summary>
    /// Gets or sets whether distributed caching is enabled.
    /// </summary>
    public bool EnableDistributedCache { get; set; } = false;

    /// <summary>
    /// Gets or sets the Redis connection string.
    /// </summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the default cache expiration in minutes.
    /// </summary>
    public int DefaultExpirationMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets the metadata statistics cache expiration in minutes.
    /// </summary>
    public int StatisticsCacheMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the update details cache expiration in minutes.
    /// </summary>
    public int UpdateDetailsCacheMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the content availability cache expiration in minutes.
    /// </summary>
    public int ContentAvailabilityCacheMinutes { get; set; } = 15;

    /// <summary>
    /// Gets or sets whether to invalidate cache on sync operations.
    /// </summary>
    public bool InvalidateOnSync { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache key prefix.
    /// </summary>
    public string KeyPrefix { get; set; } = "update-server";
}
