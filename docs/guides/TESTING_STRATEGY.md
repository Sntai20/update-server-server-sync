# Testing Strategy for Dual Hosting Architecture

## ?? Testing Philosophy

With the dual hosting architecture (Azure Functions + Worker Service) and host-agnostic orchestrators, we achieve **write once, test once, deploy anywhere**. Our testing strategy follows the **Testing Pyramid** with an emphasis on fast, independent unit tests and targeted integration tests.

## ?? Testing Pyramid

```
                    ?
                   / \
                  /   \
                 /  E2E \    (10%) Expensive, Slow, Fragile
                /_______\
               /         \
              /Integration\  (20%) Moderate cost, Some dependencies
             /  Tests      \
            /______________\
           /                \
          /   Unit Tests      \ (70%) Cheap, Fast, Reliable
         /____________________\
```

### Test Distribution

| Test Type | Percentage | Count (est.) | Execution Time | Dependencies |
|-----------|-----------|--------------|----------------|--------------|
| **Unit Tests** | 70% | ~150 tests | < 1 minute | None (mocked) |
| **Integration Tests** | 20% | ~40 tests | 2-5 minutes | In-memory or Aspire |
| **End-to-End Tests** | 10% | ~20 tests | 10-30 minutes | Full Aspire stack |

## ?? Test Project Structure

```
UpdateEngine/
??? test/
?   ??? UpdateEngineTest.csproj               # Single test project
?   ?
?   ??? ?? Unit/                             # 70% - Fast, isolated tests
?   ?   ??? Orchestrators/                    # Host-agnostic business logic
?   ?   ?   ??? SyncOrchestratorTests.cs     ? Tests SHARED logic
?   ?   ?   ??? MetadataOrchestratorTests.cs ? Tests SHARED logic
?   ?   ?   ??? HealthOrchestratorTests.cs   ? Tests SHARED logic
?   ?   ?   ??? ContentOrchestratorTests.cs  ? Tests SHARED logic
?   ?   ?
?   ?   ??? Services/                         # Domain services (existing)
?   ?   ?   ??? SyncServiceTest.cs
?   ?   ?   ??? QueryServiceTest.cs
?   ?   ?   ??? HealthServiceTest.cs
?   ?   ?   ??? AnomalyDetectionServiceTest.cs
?   ?   ?
?   ?   ??? Models/                           # Model validation
?   ?       ??? SyncModelsTests.cs
?   ?       ??? MetadataModelsTests.cs
?   ?       ??? ConfigurationTests.cs
?   ?
?   ??? ?? Integration/                      # 20% - Component interaction
?   ?   ??? InMemory/                         # Fast integration tests
?   ?   ?   ??? InMemoryIntegrationTest.cs
?   ?   ?   ??? UnifiedSyncIntegrationTest.cs
?   ?   ?   ??? UnifiedHealthIntegrationTest.cs
?   ?   ?   ??? MetadataSyncIntegrationTest.cs
?   ?   ?
?   ?   ??? Aspire/                           # Aspire-based integration
?   ?   ?   ??? AppHostIntegrationTest.cs
?   ?   ?   ??? HybridIntegrationTest.cs
?   ?   ?   ??? ConsolidationValidationTest.cs
?   ?   ?
?   ?   ??? Functions/                        # Azure Functions adapters
?   ?       ??? UnifiedSyncFunctionsTest.cs
?   ?       ??? UnifiedHealthFunctionsTest.cs
?   ?       ??? MetadataQueryFunctionsTest.cs
?   ?
?   ??? ?? EndToEnd/                         # 10% - Full system tests
?   ?   ??? Workflows/                        # Business workflows
?   ?   ?   ??? FullSyncWorkflowTest.cs
?   ?   ?   ??? AppHostWorkflowTest.cs
?   ?   ?   ??? CompleteSystemIntegrationTest.cs
?   ?   ?
?   ?   ??? DualHosting/                      # NEW: Dual hosting validation
?   ?       ??? FunctionsHostingE2ETest.cs
?   ?       ??? WorkerServiceHostingE2ETest.cs
?   ?       ??? CLIToolE2ETest.cs
?   ?
?   ??? ??? Infrastructure/                   # Test fixtures and helpers
?   ?   ??? InMemoryFunctionsFixture.cs       # Fast, no dependencies
?   ?   ??? AspireTestFixture.cs              # Aspire + Azurite
?   ?   ??? AspireAppHostTestFixture.cs       # Full AppHost orchestration
?   ?   ??? WorkerServiceTestFixture.cs       # NEW: Worker Service testing
?   ?   ??? MicrosoftUpdateTestFixture.cs
?   ?   ??? SoapTestHelper.cs
?   ?
?   ??? ?? Performance/                       # Performance and load tests
?       ??? PerformanceAndLoadTest.cs
?       ??? ConcurrencyTests.cs
```

## 🧪 Unit Tests (70%)

### Purpose
- Test **orchestrators** (host-agnostic business logic)
- Test **services** (domain logic)
- Test **models** (validation, serialization)
- **Zero external dependencies** - all mocked

### Characteristics
- ? **Fast**: < 1 second per test
- ? **Isolated**: No database, no HTTP, no file system
- ? **Deterministic**: Same input = Same output
- ? **Mocked**: All dependencies use `Moq`

### Example: Orchestrator Unit Test

```csharp
// UpdateEngine.Functions/test/Unit/Orchestrators/SyncOrchestratorTests.cs

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UpdateEngine.Core.Models;
using UpdateEngine.Core.Orchestrators;
using UpdateEngine.Services;
using Xunit;

public class SyncOrchestratorTests
{
    private readonly Mock<ISyncService> mockSyncService;
    private readonly Mock<ILogger<SyncOrchestrator>> mockLogger;
    private readonly Mock<AppConfig> mockConfig;
    private readonly SyncOrchestrator orchestrator;

    public SyncOrchestratorTests()
    {
        this.mockSyncService = new Mock<ISyncService>();
        this.mockLogger = new Mock<ILogger<SyncOrchestrator>>();
        this.mockConfig = new Mock<AppConfig>();
        
        this.orchestrator = new SyncOrchestrator(
            this.mockSyncService.Object,
            this.mockLogger.Object,
            this.mockConfig.Object);
    }

    [Fact]
    public async Task ExecuteSyncAsync_WithStartAction_ShouldCallSyncService()
    {
        // Arrange
        var request = new UnifiedSyncRequest
        {
            SyncType = SyncType.Categories,
            Action = SyncAction.Start
        };

        this.mockSyncService
            .Setup(x => x.SyncCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await this.orchestrator.ExecuteSyncAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("started successfully");
        
        this.mockSyncService.Verify(
            x => x.SyncCategoriesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteSyncAsync_WithInvalidRequest_ShouldReturnError()
    {
        // Arrange
        var request = new UnifiedSyncRequest
        {
            SyncType = (SyncType)999, // Invalid
            Action = SyncAction.Start
        };

        // Act
        var result = await this.orchestrator.ExecuteSyncAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("InvalidSyncType");
        
        this.mockSyncService.Verify(
            x => x.SyncCategoriesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetStatusAsync_ShouldReturnCurrentState()
    {
        // Arrange
        this.mockSyncService
            .Setup(x => x.GetCurrentSyncStatusAsync())
            .ReturnsAsync(new SyncStatus
            {
                IsRunning = true,
                CurrentPhase = "Categories",
                Progress = 45
            });

        // Act
        var result = await this.orchestrator.GetStatusAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.IsRunning.Should().BeTrue();
        result.CurrentPhase.Should().Be("Categories");
        result.Progress.Should().Be(45);
    }

    [Theory]
    [InlineData(SyncType.Categories)]
    [InlineData(SyncType.Updates)]
    [InlineData(SyncType.Comprehensive)]
    public async Task ExecuteSyncAsync_WithAllSyncTypes_ShouldSucceed(SyncType syncType)
    {
        // Arrange
        var request = new UnifiedSyncRequest
        {
            SyncType = syncType,
            Action = SyncAction.Start
        };

        // Setup appropriate mock based on sync type
        switch (syncType)
        {
            case SyncType.Categories:
                this.mockSyncService
                    .Setup(x => x.SyncCategoriesAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                break;
            case SyncType.Updates:
                this.mockSyncService
                    .Setup(x => x.SyncUpdatesAsync(It.IsAny<UpdatesFilter>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                break;
            case SyncType.Comprehensive:
                this.mockSyncService
                    .Setup(x => x.SyncComprehensiveAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                break;
        }

        // Act
        var result = await this.orchestrator.ExecuteSyncAsync(request);

        // Assert
        result.Success.Should().BeTrue();
    }
}
```

### Key Patterns for Unit Tests

1. **Arrange-Act-Assert (AAA)** pattern
2. **Fluent Assertions** for readable assertions
3. **Theory tests** for multiple inputs
4. **Mock verification** to ensure correct service calls
5. **Test naming**: `MethodName_Scenario_ExpectedBehavior`

### Caching Unit Tests ✨ **NEW**

**Purpose:**
- Test `CacheService` cache-aside pattern
- Test cache invalidation strategies
- Test graceful degradation when Redis unavailable
- **Zero Redis dependency** - use `MemoryDistributedCache`

**Example: CacheService Unit Tests**

```csharp
// UpdateEngine.Functions/test/Unit/Services/CacheServiceTests.cs

using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using UpdateEngine.Core.Services;
using Configuration;
using Xunit;

public class CacheServiceTests
{
    private readonly Mock<IDistributedCache> mockCache;
    private readonly IOptionsMonitor<AppConfig> config;
    private readonly Mock<ILogger<CacheService>> mockLogger;
    private readonly JsonSerializerOptions jsonOptions;

    public CacheServiceTests()
    {
        this.mockCache = new Mock<IDistributedCache>();
        this.config = CreateTestConfig(enableCache: true);
        this.mockLogger = new Mock<ILogger<CacheService>>();
        this.jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    [Fact]
    public async Task GetOrSetAsync_CacheMiss_ShouldCallFactory()
    {
        // Arrange
        var cacheService = new CacheService(
            this.mockCache.Object,
            this.config,
            this.mockLogger.Object,
            this.jsonOptions);

        this.mockCache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null); // Cache miss

        var factoryCalled = false;
        Func<Task<TestData>> factory = () =>
        {
            factoryCalled = true;
            return Task.FromResult(new TestData { Value = "test" });
        };

        // Act
        var result = await cacheService.GetOrSetAsync("test-key", factory);

        // Assert
        factoryCalled.Should().BeTrue("factory should be called on cache miss");
        result.Value.Should().Be("test");
        
        // Verify value was stored in cache
        this.mockCache.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrSetAsync_CacheHit_ShouldNotCallFactory()
    {
        // Arrange
        var cachedValue = new TestData { Value = "cached" };
        var cachedBytes = JsonSerializer.SerializeToUtf8Bytes(cachedValue, this.jsonOptions);

        this.mockCache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedBytes); // Cache hit

        var cacheService = new CacheService(
            this.mockCache.Object,
            this.config,
            this.mockLogger.Object,
            this.jsonOptions);

        var factoryCalled = false;
        Func<Task<TestData>> factory = () =>
        {
            factoryCalled = true;
            return Task.FromResult(new TestData { Value = "fresh" });
        };

        // Act
        var result = await cacheService.GetOrSetAsync("test-key", factory);

        // Assert
        factoryCalled.Should().BeFalse("factory should NOT be called on cache hit");
        result.Value.Should().Be("cached", "should return cached value");
        
        // Verify SetAsync was NOT called
        this.mockCache.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InvalidateStatisticsCacheAsync_ShouldRemoveAllStatsCaches()
    {
        // Arrange
        var cacheService = new CacheService(
            this.mockCache.Object,
            this.config,
            this.mockLogger.Object,
            this.jsonOptions);

        // Act
        await cacheService.InvalidateStatisticsCacheAsync();

        // Assert - Verify all statistics caches removed
        this.mockCache.Verify(
            x => x.RemoveAsync("msupdate:metadata:stats", It.IsAny<CancellationToken>()),
            Times.Once);
        
        this.mockCache.Verify(
            x => x.RemoveAsync("msupdate:content:stats", It.IsAny<CancellationToken>()),
            Times.Once);
        
        this.mockCache.Verify(
            x => x.RemoveAsync("msupdate:sync:status", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrSetAsync_WhenCachingDisabled_ShouldAlwaysCallFactory()
    {
        // Arrange
        var disabledConfig = CreateTestConfig(enableCache: false);
        var cacheService = new CacheService(
            this.mockCache.Object,
            disabledConfig,
            this.mockLogger.Object,
            this.jsonOptions);

        var factoryCalled = false;
        Func<Task<TestData>> factory = () =>
        {
            factoryCalled = true;
            return Task.FromResult(new TestData { Value = "test" });
        };

        // Act
        var result = await cacheService.GetOrSetAsync("test-key", factory);

        // Assert
        factoryCalled.Should().BeTrue();
        
        // Verify cache was never touched
        this.mockCache.Verify(
            x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetOrSetAsync_WhenCacheThrows_ShouldFallBackToFactory()
    {
        // Arrange
        this.mockCache
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis unavailable"));

        var cacheService = new CacheService(
            this.mockCache.Object,
            this.config,
            this.mockLogger.Object,
            this.jsonOptions);

        var factoryCalled = false;
        Func<Task<TestData>> factory = () =>
        {
            factoryCalled = true;
            return Task.FromResult(new TestData { Value = "fallback" });
        };

        // Act
        var result = await cacheService.GetOrSetAsync("test-key", factory);

        // Assert
        factoryCalled.Should().BeTrue("should fall back to factory when cache fails");
        result.Value.Should().Be("fallback");
        
        // Verify warning was logged
        this.mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cache GET failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static IOptionsMonitor<AppConfig> CreateTestConfig(bool enableCache)
    {
        var appConfig = new AppConfig
        {
            CacheConfiguration = new CacheConfiguration
            {
                EnableDistributedCache = enableCache,
                KeyPrefix = "msupdate:",
                StatisticsCacheMinutes = 5,
                UpdateDetailsCacheMinutes = 60,
                ContentAvailabilityCacheMinutes = 15
            }
        };

        var mockMonitor = new Mock<IOptionsMonitor<AppConfig>>();
        mockMonitor.Setup(x => x.CurrentValue).Returns(appConfig);
        return mockMonitor.Object;
    }

    private class TestData
    {
        public string Value { get; set; } = string.Empty;
    }
}
```

## ?? Integration Tests (20%)

### Purpose
- Test **component interactions**
- Test **Azure Functions adapters**
- Test **configuration integration**
- **Minimal external dependencies** (in-memory or Aspire)

### Two Flavors

#### 1. In-Memory Integration Tests (Fast)
- ? **Speed**: 1-5 seconds per test
- ? **No Azurite**: Uses temporary file system
- ? **CI/CD Friendly**: No external services
- ? **Focus**: Service interactions, DI configuration

#### 2. Aspire Integration Tests (Realistic)
- ? **Speed**: 10-30 seconds per test
- ?? **Uses Aspire**: Full AppHost with Azurite
- ?? **HTTP Testing**: Real HTTP endpoints
- ?? **Focus**: End-to-end function behavior

### Example: In-Memory Integration Test

```csharp
// UpdateEngine.Functions/test/Integration/InMemory/UnifiedSyncIntegrationTest.cs

using Configuration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UpdateEngine.Core.Orchestrators;
using UpdateEngine.Core.Models;
using UpdateEngineTest.Infrastructure;
using Xunit;

/// <summary>
/// Fast integration tests using in-memory storage.
/// No Azurite, no HTTP - just service interactions.
/// </summary>
[Collection("InMemory")]
public class UnifiedSyncIntegrationTest
{
    private readonly InMemoryFunctionsFixture fixture;

    public UnifiedSyncIntegrationTest(InMemoryFunctionsFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task SyncOrchestrator_WithCategoriesSync_ShouldComplete()
    {
        // Arrange
        using var scope = this.fixture.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ISyncOrchestrator>();
        
        var request = new UnifiedSyncRequest
        {
            SyncType = SyncType.Categories,
            Action = SyncAction.Start
        };

        // Act
        var result = await orchestrator.ExecuteSyncAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Categories sync started successfully");
    }

    [Fact]
    public async Task GetStatus_AfterSync_ShouldReflectCurrentState()
    {
        // Arrange
        using var scope = this.fixture.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ISyncOrchestrator>();

        // Act
        var status = await orchestrator.GetStatusAsync();

        // Assert
        status.Should().NotBeNull();
        status.Success.Should().BeTrue();
        status.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task ServiceCollectionExtensions_ShouldRegisterAllOrchestrators()
    {
        // Arrange & Act
        using var scope = this.fixture.CreateScope();

        var syncOrchestrator = scope.ServiceProvider.GetService<ISyncOrchestrator>();
        var metadataOrchestrator = scope.ServiceProvider.GetService<IMetadataOrchestrator>();
        var healthOrchestrator = scope.ServiceProvider.GetService<IHealthOrchestrator>();
        var contentOrchestrator = scope.ServiceProvider.GetService<IContentOrchestrator>();

        // Assert
        syncOrchestrator.Should().NotBeNull("ISyncOrchestrator should be registered");
        metadataOrchestrator.Should().NotBeNull("IMetadataOrchestrator should be registered");
        healthOrchestrator.Should().NotBeNull("IHealthOrchestrator should be registered");
        contentOrchestrator.Should().NotBeNull("IContentOrchestrator should be registered");
    }
}
```

### Example: Aspire Integration Test

```csharp
// UpdateEngine.Functions/test/Integration/Aspire/UnifiedSyncAspireTest.cs

using FluentAssertions;
using System.Net.Http.Json;
using System.Text.Json;
using UpdateEngine.Core.Models;
using UpdateEngineTest.Infrastructure;
using Xunit;

/// <summary>
/// Integration tests using Aspire with real HTTP endpoints.
/// Validates Azure Functions adapters work correctly.
/// </summary>
[Collection("AspireIntegration")]
public class UnifiedSyncAspireTest
{
    private readonly AspireTestFixture fixture;
    private readonly HttpClient client;

    public UnifiedSyncAspireTest(AspireTestFixture fixture)
    {
        this.fixture = fixture;
        this.client = fixture.HttpClient;
    }

    [Fact]
    public async Task UnifiedSyncFunction_POST_ShouldStartSync()
    {
        // Arrange
        var request = new UnifiedSyncRequest
        {
            SyncType = SyncType.Categories,
            Action = SyncAction.Start
        };

        // Act
        var response = await this.client.PostAsJsonAsync("/api/sync", request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        
        var result = await response.Content.ReadFromJsonAsync<SyncOperationResult>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task UnifiedSyncStatus_GET_ShouldReturnStatus()
    {
        // Act
        var response = await this.client.GetAsync("/api/sync/status");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        
        var status = await response.Content.ReadFromJsonAsync<SyncStatusResult>();
        status.Should().NotBeNull();
        status!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task UnifiedSync_InvalidRequest_ShouldReturn400()
    {
        // Arrange
        var invalidRequest = new
        {
            syncType = 999, // Invalid
            action = "InvalidAction"
        };

        // Act
        var response = await this.client.PostAsJsonAsync("/api/sync", invalidRequest);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }
}
```

## ?? End-to-End Tests (10%)

### Purpose
- Test **complete workflows**
- Test **both hosting models** (Functions + Worker Service)
- Test **CLI tool integration**
- Validate **deployment scenarios**

### Characteristics
- ?? **Slow**: 1-5 minutes per test
- ?? **Full Stack**: AppHost + Azurite + Functions + Worker Service
- ?? **Expensive**: High setup/teardown cost
- ?? **Focus**: Critical business workflows

### Example: Dual Hosting E2E Test

```csharp
// UpdateEngine.Functions/test/EndToEnd/DualHosting/FunctionsHostingE2ETest.cs

using FluentAssertions;
using System.Net.Http.Json;
using UpdateEngine.Core.Models;
using UpdateEngineTest.Infrastructure;
using Xunit;

/// <summary>
/// End-to-end tests validating Azure Functions hosting model.
/// Tests complete workflows from HTTP request to response.
/// </summary>
[Collection("AspireAppHost")]
public class FunctionsHostingE2ETest
{
    private readonly AspireAppHostTestFixture fixture;
    private readonly HttpClient client;

    public FunctionsHostingE2ETest(AspireAppHostTestFixture fixture)
    {
        this.fixture = fixture;
        this.client = fixture.HttpClient;
    }

    [Fact]
    public async Task FullSyncWorkflow_WithCategoriesAndUpdates_ShouldComplete()
    {
        // Step 1: Sync categories
        var categoriesRequest = new UnifiedSyncRequest
        {
            SyncType = SyncType.Categories,
            Action = SyncAction.Start
        };

        var categoriesResponse = await this.client.PostAsJsonAsync("/api/sync", categoriesRequest);
        categoriesResponse.IsSuccessStatusCode.Should().BeTrue();

        var categoriesResult = await categoriesResponse.Content.ReadFromJsonAsync<SyncOperationResult>();
        categoriesResult!.Success.Should().BeTrue();

        // Step 2: Wait for categories sync to complete
        await this.WaitForSyncCompletionAsync();

        // Step 3: Sync updates
        var updatesRequest = new UnifiedSyncRequest
        {
            SyncType = SyncType.Updates,
            Action = SyncAction.Start,
            Filter = new SyncFilter
            {
                ProductTitles = new[] { "Windows 10" },
                ClassificationIds = new[] { "E6CF1350-C01B-414D-A61F-263D14D133B4" } // Critical Updates
            }
        };

        var updatesResponse = await this.client.PostAsJsonAsync("/api/sync", updatesRequest);
        updatesResponse.IsSuccessStatusCode.Should().BeTrue();

        // Step 4: Verify sync status
        var statusResponse = await this.client.GetAsync("/api/sync/status");
        var status = await statusResponse.Content.ReadFromJsonAsync<SyncStatusResult>();
        status!.Success.Should().BeTrue();
        status.LastSyncTime.Should().NotBeNull();
    }

    [Fact]
    public async Task HealthCheck_AfterSync_ShouldBeHealthy()
    {
        // Arrange - Perform a sync first
        var syncRequest = new UnifiedSyncRequest
        {
            SyncType = SyncType.Categories,
            Action = SyncAction.Start
        };
        await this.client.PostAsJsonAsync("/api/sync", syncRequest);
        await this.WaitForSyncCompletionAsync();

        // Act - Check health
        var response = await this.client.GetAsync("/api/health?scope=comprehensive");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        
        var health = await response.Content.ReadFromJsonAsync<HealthCheckResult>();
        health!.IsHealthy.Should().BeTrue();
        health.ComponentStatuses.Should().ContainKey("MetadataStore");
        health.ComponentStatuses["MetadataStore"].IsHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task MetadataQuery_AfterSync_ShouldReturnResults()
    {
        // Arrange - Sync categories
        var syncRequest = new UnifiedSyncRequest
        {
            SyncType = SyncType.Categories,
            Action = SyncAction.Start
        };
        await this.client.PostAsJsonAsync("/api/sync", syncRequest);
        await this.WaitForSyncCompletionAsync();

        // Act - Query metadata
        var queryRequest = new UnifiedMetadataRequest
        {
            Action = MetadataAction.Query,
            OutputFormat = OutputFormat.Json,
            MaxResults = 10
        };

        var response = await this.client.PostAsJsonAsync("/api/metadata", queryRequest);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        
        var result = await response.Content.ReadFromJsonAsync<MetadataQueryResult>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    private async Task WaitForSyncCompletionAsync()
    {
        const int maxAttempts = 30;
        const int delayMs = 2000;

        for (int i = 0; i < maxAttempts; i++)
        {
            var response = await this.client.GetAsync("/api/sync/status");
            var status = await response.Content.ReadFromJsonAsync<SyncStatusResult>();

            if (!status!.IsRunning)
            {
                return;
            }

            await Task.Delay(delayMs);
        }

        throw new TimeoutException("Sync did not complete within expected time");
    }
}
```

### Worker Service E2E Tests

```csharp
// UpdateEngine.Functions/test/EndToEnd/DualHosting/WorkerServiceHostingE2ETest.cs

using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using UpdateEngine.Core.Models;
using UpdateEngine.WorkerService;
using Xunit;

/// <summary>
/// End-to-end tests validating Worker Service hosting model.
/// Tests ASP.NET Core controllers and background workers.
/// </summary>
public class WorkerServiceHostingE2ETest : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public WorkerServiceHostingE2ETest(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        this.client = factory.CreateClient();
    }

    [Fact]
    public async Task SyncController_POST_ShouldStartSync()
    {
        // Arrange
        var request = new UnifiedSyncRequest
        {
            SyncType = SyncType.Categories,
            Action = SyncAction.Start
        };

        // Act
        var response = await this.client.PostAsJsonAsync("/api/sync", request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        
        var result = await response.Content.ReadFromJsonAsync<SyncOperationResult>();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task HealthController_GET_ShouldReturnHealthy()
    {
        // Act
        var response = await this.client.GetAsync("/api/health");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        
        var health = await response.Content.ReadFromJsonAsync<HealthCheckResult>();
        health!.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task MetadataController_Query_ShouldReturnResults()
    {
        // Arrange
        var request = new UnifiedMetadataRequest
        {
            Action = MetadataAction.Query,
            OutputFormat = OutputFormat.Json,
            MaxResults = 10
        };

        // Act
        var response = await this.client.PostAsJsonAsync("/api/metadata", request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        
        var result = await response.Content.ReadFromJsonAsync<MetadataQueryResult>();
        result!.Success.Should().BeTrue();
    }
}
```

### CLI Tool E2E Tests

```csharp
// UpdateEngine.Functions/test/EndToEnd/DualHosting/CLIToolE2ETest.cs

using FluentAssertions;
using System.Diagnostics;
using Xunit;

/// <summary>
/// End-to-end tests for the CLI tool.
/// Validates that CLI commands use the same orchestrators as Functions/Worker Service.
/// </summary>
public class CLIToolE2ETest
{
    private readonly string cliPath;

    public CLIToolE2ETest()
    {
        // Assume CLI is built and available
        this.cliPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "update-cli", "src", "bin", "Debug", "net9.0",
            "update-cli.exe");
    }

    [Fact]
    public async Task CLI_SyncCommand_ShouldExecuteSuccessfully()
    {
        // Arrange
        var startInfo = new ProcessStartInfo
        {
            FileName = this.cliPath,
            Arguments = "sync --type categories --no-wait",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Act
        using var process = Process.Start(startInfo);
        var output = await process!.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        // Assert
        process.ExitCode.Should().Be(0, because: "sync command should succeed");
        output.Should().Contain("Categories sync started successfully");
    }

    [Fact]
    public async Task CLI_QueryCommand_ShouldReturnResults()
    {
        // Arrange
        var startInfo = new ProcessStartInfo
        {
            FileName = this.cliPath,
            Arguments = "query --format json --max-results 10",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Act
        using var process = Process.Start(startInfo);
        var output = await process!.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        // Assert
        process.ExitCode.Should().Be(0);
        output.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CLI_HealthCommand_ShouldReportHealthy()
    {
        // Arrange
        var startInfo = new ProcessStartInfo
        {
            FileName = this.cliPath,
            Arguments = "health --scope basic",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Act
        using var process = Process.Start(startInfo);
        var output = await process!.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        // Assert
        process.ExitCode.Should().Be(0);
        output.Should().Contain("Healthy");
    }
}
```

### Caching Integration Tests ✨ **NEW**

**Purpose:**
- Test orchestrators with caching enabled
- Test cache invalidation after sync operations
- Test hot-reload of cache configuration
- **Use MemoryDistributedCache** - No Redis required

**Example: Orchestrator Caching Integration Test**

```csharp
// UpdateEngine.Functions/test/Integration/Orchestrators/MetadataOrchestratorCachingTests.cs

using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UpdateEngine.Core.Orchestrators;
using UpdateEngine.Core.Services;
using UpdateEngineTest.Infrastructure;
using Xunit;

[Collection("InMemory")]
public class MetadataOrchestratorCachingTests
{
    private readonly InMemoryFunctionsFixture fixture;

    public MetadataOrchestratorCachingTests(InMemoryFunctionsFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task GetStatisticsAsync_WithCachingEnabled_ShouldCacheResults()
    {
        // Arrange
        using var scope = this.fixture.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IMetadataOrchestrator>();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

        // Act - First call (cache miss)
        var result1 = await orchestrator.GetStatisticsAsync();
        
        // Assert - Value should be cached
        var cachedBytes = await cache.GetAsync("msupdate:metadata:stats");
        cachedBytes.Should().NotBeNull("statistics should be cached after first call");

        // Act - Second call (cache hit)
        var result2 = await orchestrator.GetStatisticsAsync();

        // Assert - Both results should be identical
        result2.TotalPackages.Should().Be(result1.TotalPackages);
        result2.TotalCategories.Should().Be(result1.TotalCategories);
    }

    [Fact]
    public async Task GetStatisticsAsync_AfterCacheInvalidation_ShouldRecompute()
    {
        // Arrange
        using var scope = this.fixture.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IMetadataOrchestrator>();
        var cacheService = scope.ServiceProvider.GetRequiredService<CacheService>();

        // Act - Get statistics (caches result)
        var result1 = await orchestrator.GetStatisticsAsync();

        // Invalidate cache
        await cacheService.InvalidateStatisticsCacheAsync();

        // Get statistics again (should recompute)
        var result2 = await orchestrator.GetStatisticsAsync();

        // Assert - Results should still be valid
        result2.Should().NotBeNull();
        result2.TotalPackages.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetUpdateDetailsAsync_ShouldCacheIndividualUpdates()
    {
        // Arrange
        using var scope = this.fixture.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IMetadataOrchestrator>();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        
        var updateId = Guid.NewGuid(); // Test update ID

        // Act - Get update details
        var update = await orchestrator.GetUpdateDetailsAsync(updateId);

        // Assert - Should be cached with correct key
        var cacheKey = $"msupdate:metadata:update:{updateId}";
        var cachedBytes = await cache.GetAsync(cacheKey);
        
        if (update != null)
        {
            cachedBytes.Should().NotBeNull("update should be cached");
        }
    }
}
```

### Redis Health Check Integration Tests ✨ **NEW**

```csharp
// UpdateEngine.Functions/test/Integration/HealthChecks/RedisHealthCheckTests.cs

using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using UpdateEngine.Core.HealthChecks;
using Xunit;

public class RedisHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WithHealthyCache_ShouldReturnHealthy()
    {
        // Arrange
        var memoryCache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));
        
        var healthCheck = new RedisHealthCheck(
            memoryCache,
            Mock.Of<ILogger<RedisHealthCheck>>());

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().ContainKey("ResponseTimeMs");
        result.Data.Should().ContainKey("CacheType");
        result.Data["CacheType"].Should().Be("Redis");
    }

    [Fact]
    public async Task CheckHealthAsync_WithTimeout_ShouldReturnUnhealthy()
    {
        // Arrange
        var mockCache = new Mock<IDistributedCache>();
        mockCache
            .Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Redis timeout"));

        var healthCheck = new RedisHealthCheck(
            mockCache.Object,
            Mock.Of<ILogger<RedisHealthCheck>>());

