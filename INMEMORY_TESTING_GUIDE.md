# In-Memory Testing Guide

## ?? Overview

This project includes **in-memory integration tests** that don't require:
- ? Azurite or Azure Storage Emulator
- ? Persistent storage or Docker volumes
- ? External dependencies or services

These tests are **fast**, **reliable**, and perfect for:
- ? **CI/CD pipelines** - No infrastructure setup required
- ? **Local development** - Test changes quickly
- ? **TDD workflow** - Rapid feedback loop

## ?? Quick Start

### Run In-Memory Tests Only

```powershell
# Run fast in-memory tests (no Azurite required)
.\Run-InMemoryTests.ps1
```

Or directly:
```powershell
dotnet test --filter "FullyQualifiedName~InMemoryIntegrationTests"
```

### Run All Unit Tests (No External Dependencies)

```powershell
# Runs all service and unit tests
dotnet test --filter "Category!=Integration"
```

## ?? Test Organization

```
tests/
??? Infrastructure/
?   ??? InMemoryFunctionsFixture.cs    # Fast, no dependencies
?   ??? AspireTestFixture.cs      # Uses func CLI (requires Azurite)
?   ??? AspireAppHostTestFixture.cs    # Full Aspire (requires Docker)
?
??? Integration/
?   ??? InMemoryIntegrationTests.cs    # ? Fast (uses InMemoryFunctionsFixture)
?   ??? MetadataSyncIntegrationTests.cs # ?? Slow (requires Azure Functions running)
?   ??? AppHostIntegrationTests.cs     # ?? Slow (requires full Aspire stack)
?
??? Services/
    ??? QueryServiceTests.cs            # ? Fast unit tests
    ??? SyncServiceTests.cs         # ? Fast unit tests
    ??? HealthServiceTests.cs     # ? Fast unit tests
```

## ?? Test Fixtures Comparison

| Fixture | Speed | Dependencies | Use Case |
|---------|-------|--------------|----------|
| `InMemoryFunctionsFixture` | ? **Fast** | None | Unit/integration tests, CI/CD |
| `AspireTestFixture` | ?? Slow | Azurite, func CLI | API endpoint testing |
| `AspireAppHostTestFixture` | ?? Very Slow | Docker, Aspire | Full system testing |

## ? Writing In-Memory Tests

### Example Test

```csharp
[Collection("InMemory")] // Use in-memory fixture
public class MyFeatureTests
{
    private readonly InMemoryFunctionsFixture fixture;

    public MyFeatureTests(InMemoryFunctionsFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task MyTest_ShouldWork()
    {
        // Arrange
        using var scope = this.fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMyService>();

      // Act
        var result = await service.DoSomethingAsync();

        // Assert
        result.Should().NotBeNull();
    }
}
```

### Key Benefits

1. **No Storage Setup**
   ```csharp
   // Fixture automatically creates temporary storage
   var store = fixture.GetMetadataStore();
   // Data is cleaned up after tests
   ```

2. **Isolated Tests**
   ```csharp
   using var scope = fixture.CreateScope();
   // Each test gets fresh service instances
   ```

3. **Fast Execution**
   ```
   ? In-Memory Tests: ~100ms per test
   ?? Azurite Tests: ~2-5 seconds per test
   ?? Full Integration: ~10-30 seconds per test
   ```

## ?? CI/CD Integration

### GitHub Actions Example

```yaml
name: Tests

on: [push, pull_request]

jobs:
  fast-tests:
    runs-on: ubuntu-latest
  steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
   with:
          dotnet-version: '9.0.x'
   
      # Run fast tests (no infrastructure needed)
      - name: Run Unit Tests
 run: dotnet test --filter "Category!=Integration"
      
      - name: Run In-Memory Integration Tests
     run: dotnet test --filter "FullyQualifiedName~InMemoryIntegrationTests"

  full-integration:
    runs-on: ubuntu-latest
    needs: fast-tests
    steps:
      - uses: actions/checkout@v4
- uses: actions/setup-dotnet@v4
      
      # Setup Azurite for full integration tests
      - name: Start Azurite
        run: |
          npm install -g azurite
          azurite --silent --location azurite-data &
      
      - name: Run Full Integration Tests
        run: dotnet test --filter "Category=Integration"
```

## ?? Test Categories

### Unit Tests (Fastest)
- **No external dependencies**
- Test individual services in isolation
- Use mocked dependencies

```powershell
dotnet test --filter "FullyQualifiedName~ServiceTests"
```

### In-Memory Integration Tests (Fast)
- **Temporary local storage only**
- Test service interactions
- No network calls

```powershell
dotnet test --filter "FullyQualifiedName~InMemoryIntegrationTests"
```

### API Integration Tests (Medium)
- **Requires Azurite**
- Tests HTTP endpoints
- Uses func CLI

```powershell
dotnet test --filter "Collection=AspireIntegration"
```

### System Integration Tests (Slow)
- **Requires full Aspire + Docker**
- End-to-end testing
- Production-like environment

```powershell
dotnet test --filter "Collection=AspireAppHost"
```

## ?? Best Practices

### 1. Start with In-Memory Tests

```csharp
// ? Good: Fast, isolated, no dependencies
[Collection("InMemory")]
public class FeatureTests { }
```

### 2. Use Real Integration Tests Selectively

```csharp
// ?? Use sparingly: Slow, requires infrastructure
[Collection("AspireIntegration")]
[Trait("Category", "Integration")]
public class ApiEndpointTests { }
```

### 3. Mock External Dependencies

```csharp
// ? Mock upstream Microsoft Update calls
var mockUpstream = new Mock<IUpstreamClient>();
mockUpstream.Setup(x => x.SyncAsync()).ReturnsAsync(testData);
```

### 4. Clean Test Data

```csharp
// Fixture handles cleanup automatically
public async Task DisposeAsync()
{
    // Temporary storage is deleted
}
```

## ?? Running Tests

### Quick Development Cycle

```powershell
# 1. Fast feedback while coding
dotnet test --filter "FullyQualifiedName~InMemoryIntegrationTests"

# 2. Verify changes work end-to-end (when needed)
dotnet test --filter "Collection=AspireIntegration"
```

### Before Committing

```powershell
# Run all fast tests
dotnet test --filter "Category!=Integration"
```

### Full Test Suite (CI/CD)

```powershell
# Run everything
dotnet test
```

## ?? Test Coverage

Current in-memory test coverage:
- ? Metadata store initialization
- ? Query service operations
- ? Health checks
- ? Filter building
- ? Export functionality
- ? Driver matching
- ? Concurrent operations
- ? Empty store scenarios

## ?? Debugging Tests

### View Test Output

```powershell
dotnet test --logger "console;verbosity=detailed"
```

### Run Single Test

```powershell
dotnet test --filter "FullyQualifiedName~MySpecificTest"
```

### Debug in VS Code

1. Set breakpoint in test
2. Open **Test Explorer**
3. Right-click test ? **Debug Test**

## ?? Additional Resources

- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions](https://fluentassertions.com/)
- [Aspire Testing](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/testing)
- [.NET Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

## ?? Summary

With in-memory tests, you can:
- ? **Develop faster** - No waiting for infrastructure
- ? **Test reliably** - No flaky external dependencies
- ? **Run anywhere** - No Docker or Azurite required
- ? **Get quick feedback** - Tests complete in milliseconds

**Use in-memory tests for 90% of your testing needs, save full integration tests for critical end-to-end scenarios!**
