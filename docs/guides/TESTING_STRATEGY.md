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

## ?? Unit Tests (70%)

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
// UpdateEngine/test/Unit/Orchestrators/SyncOrchestratorTests.cs

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
// UpdateEngine/test/Integration/InMemory/UnifiedSyncIntegrationTest.cs

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
// UpdateEngine/test/Integration/Aspire/UnifiedSyncAspireTest.cs

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
// UpdateEngine/test/EndToEnd/DualHosting/FunctionsHostingE2ETest.cs

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
// UpdateEngine/test/EndToEnd/DualHosting/WorkerServiceHostingE2ETest.cs

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
// UpdateEngine/test/EndToEnd/DualHosting/CLIToolE2ETest.cs

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

## ??? Test Infrastructure

### Test Fixtures

#### 1. InMemoryFunctionsFixture
```csharp
/// <summary>
/// Fastest fixture - no external dependencies.
/// Perfect for unit and integration tests.
/// </summary>
[CollectionDefinition("InMemory")]
public class InMemoryCollection : ICollectionFixture<InMemoryFunctionsFixture>
{
}
```

**Use for:**
- Unit tests of orchestrators
- Integration tests of services
- Tests that don't need HTTP endpoints

#### 2. AspireTestFixture
```csharp
/// <summary>
/// Medium-speed fixture - uses Aspire with Azurite.
/// Perfect for Azure Functions integration tests.
/// </summary>
[CollectionDefinition("AspireIntegration")]
public class AspireIntegrationCollection : ICollectionFixture<AspireTestFixture>
{
}
```

**Use for:**
- Azure Functions adapter tests
- HTTP endpoint tests
- Aspire configuration tests

#### 3. AspireAppHostTestFixture
```csharp
/// <summary>
/// Full-stack fixture - complete AppHost with all services.
/// Perfect for end-to-end workflow tests.
/// </summary>
[CollectionDefinition("AspireAppHost")]
public class AspireAppHostCollection : ICollectionFixture<AspireAppHostTestFixture>
{
}
```

**Use for:**
- End-to-end workflow tests
- Multi-service integration tests
- Performance and load tests

#### 4. WorkerServiceTestFixture (NEW)
```csharp
/// <summary>
/// Worker Service fixture using WebApplicationFactory.
/// Perfect for testing Worker Service hosting model.
/// </summary>
public class WorkerServiceTestFixture : WebApplicationFactory<WorkerService.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Use in-memory storage for tests
            services.AddSingleton<IMetadataStore>(/* in-memory store */);
        });
    }
}
```

**Use for:**
- Worker Service controller tests
- Background worker tests
- ASP.NET Core integration tests

## ?? Test Execution Strategy

### Local Development
```bash
# Run fast tests only (unit + in-memory integration)
dotnet test --filter "Category!=AspireIntegration&Category!=E2E"

# Run all tests except E2E
dotnet test --filter "Category!=E2E"

# Run all tests
dotnet test
```

### CI/CD Pipeline
```yaml
# .github/workflows/test.yml

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - name: Run Unit Tests
        run: dotnet test --filter "Category=Unit"
        timeout-minutes: 5

  integration-tests:
    runs-on: ubuntu-latest
    needs: unit-tests
    steps:
      - name: Run Integration Tests
        run: dotnet test --filter "Category=Integration"
        timeout-minutes: 15

  e2e-tests:
    runs-on: ubuntu-latest
    needs: integration-tests
    steps:
      - name: Run E2E Tests
        run: dotnet test --filter "Category=E2E"
        timeout-minutes: 30
```

### Test Categories
```csharp
// Categorize tests using Traits
[Trait("Category", "Unit")]
public class SyncOrchestratorTests { }

[Trait("Category", "Integration")]
public class UnifiedSyncIntegrationTest { }

[Trait("Category", "E2E")]
public class FullSyncWorkflowTest { }
```

## ?? Testing Best Practices

### 1. Test Naming Convention
```csharp
// Pattern: MethodName_Scenario_ExpectedBehavior
[Fact]
public async Task ExecuteSyncAsync_WithStartAction_ShouldCallSyncService()

[Fact]
public async Task GetStatusAsync_WhenSyncRunning_ShouldReturnProgress()

[Fact]
public async Task ExecuteSyncAsync_WithInvalidRequest_ShouldReturnError()
```

### 2. Arrange-Act-Assert (AAA)
```csharp
[Fact]
public async Task ExecuteSyncAsync_WithStartAction_ShouldCallSyncService()
{
    // Arrange - Set up test data and mocks
    var request = new UnifiedSyncRequest { /* ... */ };
    this.mockService.Setup(/* ... */);

    // Act - Execute the method under test
    var result = await this.orchestrator.ExecuteSyncAsync(request);

    // Assert - Verify the outcome
    result.Success.Should().BeTrue();
    this.mockService.Verify(/* ... */);
}
```

### 3. Use FluentAssertions
```csharp
// ? BAD
Assert.True(result.Success);
Assert.Equal("expected", result.Message);

// ? GOOD
result.Success.Should().BeTrue();
result.Message.Should().Be("expected");
result.Errors.Should().BeEmpty();
```

### 4. Theory Tests for Multiple Inputs
```csharp
[Theory]
[InlineData(SyncType.Categories, "Categories sync")]
[InlineData(SyncType.Updates, "Updates sync")]
[InlineData(SyncType.Comprehensive, "Comprehensive sync")]
public async Task ExecuteSyncAsync_WithAllSyncTypes_ShouldSucceed(
    SyncType syncType,
    string expectedMessage)
{
    // Test logic for all sync types
}
```

### 5. Async All The Way
```csharp
// ? BAD - Blocking on async
var result = this.orchestrator.ExecuteSyncAsync(request).Result;

// ? GOOD - Async all the way
var result = await this.orchestrator.ExecuteSyncAsync(request);
```

### 6. Test Independence
```csharp
// ? BAD - Tests depend on each other
[Fact]
public async Task Test1_CreateUser() { }

[Fact]
public async Task Test2_UpdateUser() { } // Depends on Test1

// ? GOOD - Each test is independent
[Fact]
public async Task UpdateUser_WithExistingUser_ShouldSucceed()
{
    // Arrange - Create user in this test
    var user = await this.CreateTestUserAsync();
    
    // Act & Assert
}
```

## ?? Code Coverage Goals

| Component | Target Coverage | Critical Paths |
|-----------|----------------|----------------|
| **Orchestrators** | 90%+ | ? All business logic |
| **Services** | 85%+ | ? Core operations |
| **Models** | 80%+ | ? Validation logic |
| **Functions** | 70%+ | ? Adapter layer |
| **Infrastructure** | 60%+ | ? Configuration |

### Measuring Coverage
```bash
# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML report
reportgenerator \
  -reports:**/coverage.cobertura.xml \
  -targetdir:coverage-report \
  -reporttypes:Html

# Open report
start coverage-report/index.html
```

## ?? Testing Checklist

### When Creating New Orchestrator
- [ ] ? Write unit tests for all public methods
- [ ] ? Test all action types (Start, Pause, Resume, Cancel)
- [ ] ? Test invalid inputs and error handling
- [ ] ? Test concurrent operations
- [ ] ? Write integration test using InMemoryFixture
- [ ] ? Write E2E test using AspireAppHostFixture

### When Creating New Function
- [ ] ? Write unit test for orchestrator (already done)
- [ ] ? Write integration test for HTTP adapter
- [ ] ? Test invalid request handling (400 Bad Request)
- [ ] ? Test authentication/authorization (if applicable)
- [ ] ? Verify same behavior as Worker Service controller

### When Creating New Model
- [ ] ? Test serialization/deserialization
- [ ] ? Test validation attributes
- [ ] ? Test default values
- [ ] ? Test null handling

## ?? Running Tests

### Local Development Workflow
```bash
# 1. Quick feedback loop (unit tests only)
dotnet test --filter "Category=Unit"
# < 1 minute

# 2. Comprehensive testing (unit + integration)
dotnet test --filter "Category!=E2E"
# ~5 minutes

# 3. Full validation (before commit)
dotnet test
# ~15 minutes

# 4. Watch mode (for TDD)
dotnet watch test --filter "Category=Unit"
```

### CI/CD Pipeline
```bash
# Stage 1: Fast feedback (parallel)
dotnet test --filter "Category=Unit" --no-build

# Stage 2: Integration tests (after unit tests pass)
dotnet test --filter "Category=Integration" --no-build

# Stage 3: E2E tests (only on main branch or release)
dotnet test --filter "Category=E2E" --no-build
```

## ?? Summary

### Key Principles
1. ? **Test orchestrators once** - works for all hosting models
2. ? **Fast feedback loop** - unit tests < 1 minute
3. ? **Minimal E2E tests** - expensive, only for critical workflows
4. ? **Test independence** - each test runs in isolation
5. ? **Mock external dependencies** - unit tests have zero dependencies

### Test Distribution
- **70% Unit Tests**: Fast, isolated, test orchestrators and services
- **20% Integration Tests**: Moderate speed, test component interactions
- **10% E2E Tests**: Slow, expensive, test critical workflows

### Result
With this testing strategy:
- ? **Fast development**: Unit tests give instant feedback
- ? **High confidence**: Integration tests validate interactions
- ? **Production validation**: E2E tests catch system-level issues
- ? **Code reuse**: Test orchestrators once, deploy anywhere

---

**Last Updated**: 2025-01-XX  
**Status**: Ready for Implementation  
**Next Steps**: 
1. Create Unit/Orchestrators/ folder
2. Write SyncOrchestratorTests.cs
3. Create WorkerServiceTestFixture.cs
