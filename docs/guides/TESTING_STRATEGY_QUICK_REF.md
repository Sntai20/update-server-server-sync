# Testing Strategy Quick Reference

## ?? Test Pyramid Distribution

```
                    E2E Tests (10%)
                   ?? 1-5 min/test
                  ?? Full stack
                 ?? High cost
                /_____________\
               /               \
              / Integration (20%) \
             ?? 1-30 sec/test     \
            ?? Component tests      \
           /________________________\
          /                          \
         /     Unit Tests (70%)       \
        ?? < 1 sec/test                \
       ?? Orchestrators & Services      \
      /___________________________________\
```

## ?? Test Types at a Glance

| Type | % | Speed | Dependencies | What to Test |
|------|---|-------|--------------|--------------|
| **Unit** | 70% | ? <1s | None (mocked) | Orchestrators, Services, Models |
| **Integration** | 20% | ?? 1-30s | In-memory or Aspire | Component interactions, DI, HTTP |
| **E2E** | 10% | ?? 1-5min | Full Aspire stack | Workflows, dual hosting, CLI |

## ??? Test Infrastructure

### Quick Guide: Which Fixture to Use?

```
???????????????????????????????????????????????????????????????
? Need to test...                 ? Use Fixture               ?
???????????????????????????????????????????????????????????????
? Orchestrator logic              ? ? None (pure unit test)  ?
? Service interactions            ? InMemoryFunctionsFixture  ?
? Azure Functions HTTP endpoints  ? AspireTestFixture         ?
? Complete workflows              ? AspireAppHostTestFixture  ?
? Worker Service controllers      ? WorkerServiceTestFixture  ?
? CLI tool commands               ? Process.Start (E2E)       ?
???????????????????????????????????????????????????????????????
```

### Fixture Characteristics

#### 1?? InMemoryFunctionsFixture
```csharp
[Collection("InMemory")]
public class MyTest
{
    // ? Speed: < 1 second setup
    // ? No Azurite required
    // ? Perfect for: Service integration tests
}
```

#### 2?? AspireTestFixture
```csharp
[Collection("AspireIntegration")]
public class MyTest
{
    // ? Speed: 10-30 seconds setup
    // ?? Requires: Aspire + Azurite
    // ? Perfect for: HTTP endpoint tests
}
```

#### 3?? AspireAppHostTestFixture
```csharp
[Collection("AspireAppHost")]
public class MyTest
{
    // ?? Speed: 30-60 seconds setup
    // ?? Requires: Full AppHost
    // ? Perfect for: Complete workflows
}
```

#### 4?? WorkerServiceTestFixture
```csharp
public class MyTest : IClassFixture<WorkerServiceTestFixture>
{
    // ? Speed: 5-10 seconds setup
    // ?? Uses: WebApplicationFactory
    // ? Perfect for: Worker Service tests
}
```

## ?? Test Structure

```
UpdateEngine/test/
?
??? Unit/                        # 70% - FAST (< 1 sec)
?   ??? Orchestrators/           # ? Host-agnostic business logic
?   ?   ??? SyncOrchestratorTests.cs
?   ?   ??? MetadataOrchestratorTests.cs
?   ?   ??? HealthOrchestratorTests.cs
?   ?   ??? ContentOrchestratorTests.cs
?   ?
?   ??? Services/                # ? Domain services
?   ?   ??? SyncServiceTest.cs
?   ?   ??? QueryServiceTest.cs
?   ?   ??? HealthServiceTest.cs
?   ?
?   ??? Models/                  # ? Validation, serialization
?       ??? SyncModelsTests.cs
?
??? Integration/                 # 20% - MODERATE (1-30 sec)
?   ??? InMemory/                # ? Fast integration
?   ?   ??? InMemoryIntegrationTest.cs
?   ?   ??? UnifiedSyncIntegrationTest.cs
?   ?
?   ??? Aspire/                  # ? HTTP endpoints
?   ?   ??? AppHostIntegrationTest.cs
?   ?   ??? UnifiedSyncAspireTest.cs
?   ?
?   ??? Functions/               # ? Azure Functions adapters
?       ??? UnifiedSyncFunctionsTest.cs
?
??? EndToEnd/                    # 10% - SLOW (1-5 min)
    ??? Workflows/               # ? Business workflows
    ?   ??? FullSyncWorkflowTest.cs
    ?   ??? CompleteSystemIntegrationTest.cs
    ?
    ??? DualHosting/             # ? Hosting model validation
        ??? FunctionsHostingE2ETest.cs
        ??? WorkerServiceHostingE2ETest.cs
        ??? CLIToolE2ETest.cs
```

## ?? Running Tests

### Development (Fast Feedback)
```bash
# Unit tests only (< 1 minute)
dotnet test --filter "Category=Unit"

# Unit + InMemory integration (< 2 minutes)
dotnet test --filter "Category!=E2E&Category!=AspireIntegration"

# Everything except E2E (~5 minutes)
dotnet test --filter "Category!=E2E"
```

### CI/CD Pipeline
```bash
# Stage 1: Unit tests (parallel)
dotnet test --filter "Category=Unit"

# Stage 2: Integration tests (sequential)
dotnet test --filter "Category=Integration"

# Stage 3: E2E tests (only on main branch)
dotnet test --filter "Category=E2E"
```

## ? Testing Checklist

### New Orchestrator
- [ ] Unit tests for all public methods
- [ ] Test all action types (Start/Pause/Resume/Cancel)
- [ ] Test invalid inputs
- [ ] Test error handling
- [ ] Integration test (InMemoryFixture)
- [ ] E2E test (AspireAppHostFixture)

### New Function/Controller
- [ ] Orchestrator unit test (already done ?)
- [ ] Integration test for HTTP adapter
- [ ] Test invalid request handling (400)
- [ ] Verify parity with other hosting models

### New Model
- [ ] Test serialization/deserialization
- [ ] Test validation attributes
- [ ] Test default values

## ?? Code Coverage Goals

| Component | Target | Critical |
|-----------|--------|----------|
| Orchestrators | 90%+ | All business logic |
| Services | 85%+ | Core operations |
| Models | 80%+ | Validation |
| Functions | 70%+ | Adapters |

## ?? Key Testing Principles

### 1. Test Orchestrators Once ?
```csharp
// Unit test the orchestrator
[Fact]
public async Task ExecuteSyncAsync_WithStartAction_ShouldSucceed()
{
    var orchestrator = new SyncOrchestrator(mockService, mockLogger, mockConfig);
    var result = await orchestrator.ExecuteSyncAsync(request);
    result.Success.Should().BeTrue();
}

// This validates:
// ? Azure Functions behavior
// ? Worker Service behavior
// ? CLI tool behavior
```

### 2. Mock External Dependencies ?
```csharp
// ? BAD - Real dependency
var store = PackageStore.Open("./real-path");

// ? GOOD - Mocked dependency
var mockStore = new Mock<IMetadataStore>();
mockStore.Setup(x => x.IsReindexingRequired).Returns(false);
```

### 3. Use AAA Pattern ?
```csharp
[Fact]
public async Task TestName()
{
    // Arrange - Set up test data
    var request = new UnifiedSyncRequest { /* ... */ };
    
    // Act - Execute the method
    var result = await orchestrator.ExecuteSyncAsync(request);
    
    // Assert - Verify the outcome
    result.Success.Should().BeTrue();
}
```

### 4. Fast Feedback Loop ?
```bash
# Watch mode for TDD
dotnet watch test --filter "FullyQualifiedName~SyncOrchestratorTests"

# Re-runs tests automatically on code changes
```

### 5. Test Independence ?
```csharp
// Each test should:
// ? Set up its own data
// ? Not depend on other tests
// ? Clean up after itself
// ? Run in any order
```

## ?? Common Test Patterns

### Testing Orchestrators (Unit)
```csharp
public class SyncOrchestratorTests
{
    private readonly Mock<ISyncService> mockService;
    private readonly Mock<ILogger<SyncOrchestrator>> mockLogger;
    private readonly SyncOrchestrator orchestrator;

    public SyncOrchestratorTests()
    {
        this.mockService = new Mock<ISyncService>();
        this.mockLogger = new Mock<ILogger<SyncOrchestrator>>();
        this.orchestrator = new SyncOrchestrator(
            this.mockService.Object,
            this.mockLogger.Object,
            Mock.Of<AppConfig>());
    }

    [Fact]
    public async Task ExecuteSyncAsync_WithStartAction_ShouldCallService()
    {
        // Arrange
        var request = new UnifiedSyncRequest
        {
            SyncType = SyncType.Categories,
            Action = SyncAction.Start
        };

        this.mockService
            .Setup(x => x.SyncCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await this.orchestrator.ExecuteSyncAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        this.mockService.Verify(
            x => x.SyncCategoriesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
```

### Testing Azure Functions (Integration)
```csharp
[Collection("AspireIntegration")]
public class UnifiedSyncFunctionsTest
{
    private readonly AspireTestFixture fixture;
    private readonly HttpClient client;

    public UnifiedSyncFunctionsTest(AspireTestFixture fixture)
    {
        this.fixture = fixture;
        this.client = fixture.HttpClient;
    }

    [Fact]
    public async Task POST_Sync_WithValidRequest_ShouldReturn200()
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
    }
}
```

### Testing Worker Service (Integration)
```csharp
public class SyncControllerTest : IClassFixture<WorkerServiceTestFixture>
{
    private readonly WorkerServiceTestFixture factory;
    private readonly HttpClient client;

    public SyncControllerTest(WorkerServiceTestFixture factory)
    {
        this.factory = factory;
        this.client = factory.CreateClient();
    }

    [Fact]
    public async Task POST_Sync_ShouldStartSync()
    {
        // Arrange
        var request = new UnifiedSyncRequest { /* ... */ };

        // Act
        var response = await this.client.PostAsJsonAsync("/api/sync", request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }
}
```

### Testing CLI Tool (E2E)
```csharp
public class CLIToolE2ETest
{
    [Fact]
    public async Task CLI_SyncCommand_ShouldExecuteSuccessfully()
    {
        // Arrange
        var startInfo = new ProcessStartInfo
        {
            FileName = "update-cli.exe",
            Arguments = "sync --type categories",
            RedirectStandardOutput = true
        };

        // Act
        using var process = Process.Start(startInfo);
        await process!.WaitForExitAsync();

        // Assert
        process.ExitCode.Should().Be(0);
    }
}
```

## ?? Resources

- **[TESTING_STRATEGY.md](./TESTING_STRATEGY.md)** - Complete testing guide
- **[IMPLEMENTATION_SUMMARY.md](./IMPLEMENTATION_SUMMARY.md)** - Architecture overview
- **xUnit Documentation**: https://xunit.net/
- **Fluent Assertions**: https://fluentassertions.com/
- **Moq Documentation**: https://github.com/moq/moq4

---

**Last Updated**: 2025-01-XX  
**Quick Tip**: Start with unit tests (70%), add integration tests as needed (20%), only write E2E for critical workflows (10%)
