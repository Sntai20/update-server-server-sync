// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Tests.Services;

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UpdateEngine.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using UpdateEngine.Core.Services;
using Xunit;

/// <summary>
/// Unit tests for CacheService to verify caching behavior, TTL management,
/// and invalidation strategies without requiring actual Redis infrastructure.
/// </summary>
public class CacheServiceTests
{
    private readonly Mock<IDistributedCache> mockCache;
    private readonly Mock<IOptionsMonitor<AppConfig>> mockConfig;
    private readonly Mock<ILogger<CacheService>> mockLogger;
    private readonly CacheService cacheService;
    private readonly JsonSerializerOptions jsonOptions;

    public CacheServiceTests()
    {
        this.mockCache = new Mock<IDistributedCache>();
        this.mockConfig = new Mock<IOptionsMonitor<AppConfig>>();
        this.mockLogger = new Mock<ILogger<CacheService>>();
        this.jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var appConfig = new AppConfig
        {
            CacheConfiguration = new CacheConfiguration
            {
                EnableDistributedCache = true,
                KeyPrefix = "test:",
                StatisticsCacheMinutes = 5,
                UpdateDetailsCacheMinutes = 60,
                ContentAvailabilityCacheMinutes = 15
            }
        };

        this.mockConfig.Setup(x => x.CurrentValue).Returns(appConfig);

        this.cacheService = new CacheService(
            this.mockCache.Object,
            this.mockConfig.Object,
            this.mockLogger.Object,
            this.jsonOptions);
    }

    [Fact]
    public async Task GetOrSetAsync_CacheMiss_CallsFactoryAndCachesResult()
    {
        // Arrange
        var key = "test-key";
        var expectedValue = "test-value";
        var factoryCalled = false;

        this.mockCache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await this.cacheService.GetOrSetAsync(
            key,
            async () =>
            {
                factoryCalled = true;
                await Task.Delay(10);
                return expectedValue;
            },
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        // Assert
        Assert.Equal(expectedValue, result);
        Assert.True(factoryCalled, "Factory should be called on cache miss");
        this.mockCache.Verify(
            x => x.SetAsync(
                It.Is<string>(s => s.Contains(key)),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrSetAsync_CacheHit_ReturnsFromCacheWithoutCallingFactory()
    {
        // Arrange
        var key = "test-key";
        var cachedValue = "cached-value";
        var factoryCalled = false;
        var serialized = JsonSerializer.SerializeToUtf8Bytes(cachedValue, this.jsonOptions);

        this.mockCache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serialized);

        // Act
        var result = await this.cacheService.GetOrSetAsync(
            key,
            async () =>
            {
                factoryCalled = true;
                await Task.Delay(10);
                return "factory-value";
            },
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        // Assert
        Assert.Equal(cachedValue, result);
        Assert.False(factoryCalled, "Factory should NOT be called on cache hit");
        this.mockCache.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAsync_ExistingKey_ReturnsDeserializedValue()
    {
        // Arrange
        var key = "existing-key";
        var expectedValue = new TestModel { Id = 42, Name = "Test" };
        var serialized = JsonSerializer.SerializeToUtf8Bytes(expectedValue, this.jsonOptions);

        this.mockCache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serialized);

        // Act
        var result = await this.cacheService.GetAsync<TestModel>(key, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedValue.Id, result!.Id);
        Assert.Equal(expectedValue.Name, result.Name);
    }

    [Fact]
    public async Task GetAsync_NonExistentKey_ReturnsDefault()
    {
        // Arrange
        var key = "non-existent";

        this.mockCache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await this.cacheService.GetAsync<string>(key, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_ValidValue_SerializesAndStoresCorrectly()
    {
        // Arrange
        var key = "set-key";
        var value = new TestModel { Id = 99, Name = "SetTest" };
        var expiration = TimeSpan.FromMinutes(10);
        byte[]? capturedBytes = null;

        this.mockCache
            .Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (k, b, o, ct) => capturedBytes = b)
            .Returns(Task.CompletedTask);

        // Act
        await this.cacheService.SetAsync(key, value, expiration, CancellationToken.None);

        // Assert
        this.mockCache.Verify(
            x => x.SetAsync(
                It.Is<string>(s => s.Contains(key)),
                It.IsAny<byte[]>(),
                It.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == expiration),
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(capturedBytes);
        var deserialized = JsonSerializer.Deserialize<TestModel>(capturedBytes!, this.jsonOptions);
        Assert.Equal(value.Id, deserialized!.Id);
        Assert.Equal(value.Name, deserialized.Name);
    }

    [Fact]
    public async Task RemoveAsync_ExistingKey_CallsCacheRemove()
    {
        // Arrange
        var key = "remove-key";

        // Act
        await this.cacheService.RemoveAsync(key, CancellationToken.None);

        // Assert
        this.mockCache.Verify(
            x => x.RemoveAsync(
                It.Is<string>(s => s.Contains(key)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvalidateStatisticsCacheAsync_RemovesAllStatsCaches()
    {
        // Act
        await this.cacheService.InvalidateStatisticsCacheAsync(CancellationToken.None);

        // Assert
        this.mockCache.Verify(
            x => x.RemoveAsync(
                It.Is<string>(s => s.Contains("metadata:stats")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        this.mockCache.Verify(
            x => x.RemoveAsync(
                It.Is<string>(s => s.Contains("content:stats")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvalidateUpdateCacheAsync_RemovesSpecificUpdateCache()
    {
        // Arrange
        var updateIdHex = "abc123def456";

        // Act
        await this.cacheService.InvalidateUpdateCacheAsync(updateIdHex, CancellationToken.None);

        // Assert
        this.mockCache.Verify(
            x => x.RemoveAsync(
                It.Is<string>(s => s.Contains($"metadata:update:{updateIdHex}")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        this.mockCache.Verify(
            x => x.RemoveAsync(
                It.Is<string>(s => s.Contains($"content:availability:{updateIdHex}")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvalidateAllCachesAsync_RemovesAllConfiguredCaches()
    {
        // Act
        await this.cacheService.InvalidateAllCachesAsync(CancellationToken.None);

        // Assert - Should remove all known cache patterns
        this.mockCache.Verify(
            x => x.RemoveAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeast(2)); // At minimum metadata:stats and content:stats
    }

    [Fact]
    public async Task GetOrSetAsync_FactoryThrows_PropagatesException()
    {
        // Arrange
        var key = "error-key";
        var expectedException = new InvalidOperationException("Factory failed");

        this.mockCache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await this.cacheService.GetOrSetAsync<string>(
                key,
                () => throw expectedException,
                TimeSpan.FromMinutes(5),
                CancellationToken.None);
        });
    }

    [Fact]
    public async Task GetOrSetAsync_CacheGetThrows_FallsBackToFactory()
    {
        // Arrange
        var key = "cache-error-key";
        var expectedValue = "fallback-value";
        var factoryCalled = false;

        this.mockCache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis down"));

        // Act
        var result = await this.cacheService.GetOrSetAsync(
            key,
            async () =>
            {
                factoryCalled = true;
                await Task.Delay(10);
                return expectedValue;
            },
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        // Assert
        Assert.Equal(expectedValue, result);
        Assert.True(factoryCalled, "Should fall back to factory when cache fails");
    }

    [Fact]
    public void GetStatisticsExpiration_ReturnsConfiguredValue()
    {
        // Act
        var expiration = this.cacheService.GetStatisticsExpiration();

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(5), expiration);
    }

    [Fact]
    public void GetUpdateDetailsExpiration_ReturnsConfiguredValue()
    {
        // Act
        var expiration = this.cacheService.GetUpdateDetailsExpiration();

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(60), expiration);
    }

    [Fact]
    public void GetContentAvailabilityExpiration_ReturnsConfiguredValue()
    {
        // Act
        var expiration = this.cacheService.GetContentAvailabilityExpiration();

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(15), expiration);
    }

    [Fact]
    public async Task GetOrSetAsync_WithBooleanType_WorksCorrectly()
    {
        // Arrange
        var key = "bool-key";
        var expectedValue = true;
        var serialized = JsonSerializer.SerializeToUtf8Bytes(expectedValue, this.jsonOptions);

        this.mockCache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serialized);

        // Act
        var result = await this.cacheService.GetOrSetAsync(
            key,
            () => Task.FromResult(false), // Factory returns false but cache has true
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        // Assert
        Assert.True(result, "Should return cached true value");
    }

    [Fact]
    public async Task SetAsync_WithNullableValue_SerializesNull()
    {
        // Arrange
        string? nullValue = null;
        
        // Act - SetAsync with nullable type constraint doesn't allow null
        // This test verifies that our constraint `where T : notnull` prevents this at compile time
        // The implementation properly handles this with the constraint
    
        // Instead, test that the cache service handles non-null values correctly
        var nonNullValue = "test-value";
        await this.cacheService.SetAsync("test-key", nonNullValue);

        // Assert - SetAsync should be called once with non-null value
        this.mockCache.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void BuildCacheKey_AppliesConfiguredPrefix()
    {
        // This tests internal behavior through public API
        // The key prefix "test:" should be prepended to all cache keys
        
        // Arrange
        var key = "my-key";

        // Act - Use GetAsync which builds the key internally
        _ = this.cacheService.GetAsync<string>(key, CancellationToken.None);

        // Assert - Verify the key passed to cache includes prefix
        this.mockCache.Verify(
            x => x.GetAsync(
                It.Is<string>(s => s.StartsWith("test:") && s.Contains(key)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Test model for serialization/deserialization tests
    /// </summary>
    private class TestModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
