// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core.HealthChecks;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Health check for Redis distributed cache connectivity and responsiveness.
/// Verifies that the cache can perform read/write operations successfully.
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly IDistributedCache cache;
    private readonly ILogger<RedisHealthCheck> logger;
    private const string HealthCheckKey = "health-check-test";

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisHealthCheck"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache to health check</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public RedisHealthCheck(
        IDistributedCache cache,
        ILogger<RedisHealthCheck> logger)
    {
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Checks the health of the Redis cache by performing a write/read/delete operation.
    /// </summary>
    /// <param name="context">Health check context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result indicating cache status</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate unique test value with timestamp
            var testValue = $"health-check-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss-fff}";
            var startTime = DateTime.UtcNow;

            // Step 1: Write test value to cache
            await this.cache.SetStringAsync(
                HealthCheckKey,
                testValue,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
                },
                cancellationToken);

            // Step 2: Read back the test value
            var retrieved = await this.cache.GetStringAsync(HealthCheckKey, cancellationToken);

            // Step 3: Clean up test key
            await this.cache.RemoveAsync(HealthCheckKey, cancellationToken);

            var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            // Step 4: Verify the value matches
            if (retrieved == testValue)
            {
                this.logger.LogDebug(
                    "Redis health check passed. Response time: {ResponseTime}ms",
                    responseTime);

                return HealthCheckResult.Healthy(
                    $"Redis is responding correctly. Response time: {responseTime:F2}ms",
                    new Dictionary<string, object>
                    {
                        { "ResponseTimeMs", responseTime },
                        { "CacheType", "Redis" },
                        { "LastCheckTime", DateTime.UtcNow }
                    });
            }

            // Value mismatch indicates cache corruption or issue
            this.logger.LogWarning(
                "Redis health check degraded: Value mismatch. Expected: {Expected}, Got: {Actual}",
                testValue,
                retrieved);

            return HealthCheckResult.Degraded(
                $"Redis responded but value mismatch. Expected: {testValue}, Got: {retrieved}",
                data: new Dictionary<string, object>
                {
                    { "ResponseTimeMs", responseTime },
                    { "ExpectedValue", testValue },
                    { "ActualValue", retrieved ?? "null" }
                });
        }
        catch (OperationCanceledException)
        {
            // Cancellation is not an error
            this.logger.LogInformation("Redis health check was cancelled");
            return HealthCheckResult.Healthy("Health check cancelled");
        }
        catch (TimeoutException ex)
        {
            this.logger.LogError(ex, "Redis health check timed out");
            return HealthCheckResult.Unhealthy(
                "Redis connection timeout",
                ex,
                new Dictionary<string, object>
                {
                    { "ErrorType", "Timeout" },
                    { "LastCheckTime", DateTime.UtcNow }
                });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Redis health check failed with exception");
            return HealthCheckResult.Unhealthy(
                $"Redis health check failed: {ex.Message}",
                ex,
                new Dictionary<string, object>
                {
                    { "ErrorType", ex.GetType().Name },
                    { "ErrorMessage", ex.Message },
                    { "LastCheckTime", DateTime.UtcNow }
                });
        }
    }
}
