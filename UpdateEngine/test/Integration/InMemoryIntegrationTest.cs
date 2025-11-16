// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Integration;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PackageGraph.Storage;
using UpdateEngine.Services;
using Configuration;
using UpdateEngineTest.Infrastructure;
using Xunit;

/// <summary>
/// In-memory integration tests that don't require Azurite or persistent storage.
/// These tests are fast and can run in CI/CD without external dependencies.
/// </summary>
[Collection("InMemory")]
public class InMemoryIntegrationTest
{
    private readonly InMemoryFunctionsFixture fixture;

    public InMemoryIntegrationTest(InMemoryFunctionsFixture fixture)
  {
        this.fixture = fixture;
    }

    [Fact]
    public void MetadataStore_ShouldBeInitialized()
    {
        // Arrange & Act
   var store = this.fixture.GetMetadataStore();

        // Assert
        store.Should().NotBeNull();
     store.IsReindexingRequired.Should().BeFalse();
    }

    [Fact]
    public async Task QueryService_WithEmptyStore_ShouldReturnZeroPackages()
    {
        // Arrange
        using var scope = this.fixture.CreateScope();
        var queryService = scope.ServiceProvider.GetRequiredService<IQueryService>();

    var request = new MetadataQueryRequest
   {
        MaxResults = 100,
            IncludeSuperseded = true,
      ProductFilters = new List<string>(),
    ClassificationFilters = new List<string>()
        };

    // Act
    var result = await queryService.QueryMetadataAsync(request);

        // Assert
        result.Should().NotBeNull();
 result.TotalMatches.Should().Be(0);
        result.Packages.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStoreStatus_ShouldReturnCurrentState()
    {
        // Arrange
     using var scope = this.fixture.CreateScope();
        var queryService = scope.ServiceProvider.GetRequiredService<IQueryService>();

        // Act
        var status = await queryService.GetStoreStatusAsync();

  // Assert
        status.Should().NotBeNull();
  status.TotalPackageCount.Should().BeGreaterThanOrEqualTo(0);
   status.PackageIdIndexed.Should().BeTrue();
        status.ReindexingRequired.Should().BeFalse();
    }

    [Fact]
    public async Task GetAvailableFilters_ShouldReturnFilterOptions()
  {
      // Arrange
        using var scope = this.fixture.CreateScope();
        var queryService = scope.ServiceProvider.GetRequiredService<IQueryService>();

      // Act
var filters = await queryService.GetAvailableFiltersAsync();

        // Assert
 filters.Should().NotBeNull();
        filters.Products.Should().NotBeNull();
  filters.Classifications.Should().NotBeNull();
        filters.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task HealthService_ShouldReturnHealthyStatus()
    {
        // Arrange
        using var scope = this.fixture.CreateScope();
      var healthService = scope.ServiceProvider.GetRequiredService<IHealthService>();

     // Act
     var health = await healthService.PerformHealthCheckAsync();

        // Assert
      health.Should().NotBeNull();
      health.IsHealthy.Should().BeTrue();
        health.Status.Should().NotBeNullOrEmpty();
    }

    [Fact]
 public async Task QueryService_BuildFilter_WithValidInput_ShouldCreateFilter()
    {
    // Arrange
    using var scope = this.fixture.CreateScope();
        var queryService = scope.ServiceProvider.GetRequiredService<IQueryService>();

  var request = new MetadataFilterRequest
        {
   TitleFilter = "Security",
       SkipSuperseded = true,
         FirstX = 10
        };

        // Act
        var filter = queryService.BuildFilterFromRequest(request);

        // Assert
   filter.Should().NotBeNull();
  filter!.TitleFilter.Should().Be("Security");
  filter.SkipSuperseded.Should().BeTrue();
        filter.FirstX.Should().Be(10);
  }

    [Fact]
    public async Task QueryService_ExportMetadata_ToJson_ShouldSucceed()
    {
 // Arrange
      using var scope = this.fixture.CreateScope();
        var queryService = scope.ServiceProvider.GetRequiredService<IQueryService>();

  var request = new MetadataExportRequest
     {
            Format = "json",
        ProductsFilter = new List<string>(),
    ClassificationsFilter = new List<string>(),
            IncludeSuperseded = true
        };

        // Act
        var result = await queryService.ExportMetadataAsync(request);

 // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
 result.Format.Should().Be("json");
    result.ExportData.Should().NotBeNull();
    }

[Fact]
    public async Task QueryService_ExportMetadata_ToCsv_ShouldSucceed()
    {
        // Arrange
        using var scope = this.fixture.CreateScope();
        var queryService = scope.ServiceProvider.GetRequiredService<IQueryService>();

      var request = new MetadataExportRequest
        {
   Format = "csv",
            ProductsFilter = new List<string>(),
        ClassificationsFilter = new List<string>(),
            IncludeSuperseded = true
      };

// Act
      var result = await queryService.ExportMetadataAsync(request);

        // Assert
        result.Should().NotBeNull();
   result.Success.Should().BeTrue();
        result.Format.Should().Be("csv");
        result.ExportData.Should().Contain("Id,Title,Type");
    }

    [Fact]
    public async Task DriverMatching_WithEmptyStore_ShouldReturnNoMatch()
    {
    // Arrange
        using var scope = this.fixture.CreateScope();
   var queryService = scope.ServiceProvider.GetRequiredService<IQueryService>();

        var request = new DriverMatchRequest
        {
    HardwareIds = new List<string> { "PCI\\VEN_8086&DEV_1234" }
        };

        // Act
        var result = await queryService.MatchDriversAsync(request);

        // Assert
  result.Should().NotBeNull();
        result.MatchFound.Should().BeFalse();
    }

    [Fact]
    public async Task MetadataStore_MultipleConcurrentReads_ShouldSucceed()
    {
        // Arrange
     var store = this.fixture.GetMetadataStore();

        // Act - Simulate concurrent reads
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
       await Task.Delay(Random.Shared.Next(0, 50));
            var count = store.Cast<Microsoft.PackageGraph.ObjectModel.IPackage>().Count();
          return count;
        });

     // Assert
        var results = await Task.WhenAll(tasks);
   results.Should().AllSatisfy(count => count.Should().BeGreaterThanOrEqualTo(0));
    }
}

/// <summary>
/// Metadata filter request for testing
/// </summary>
public class MetadataFilterRequest : IMetadataFilterRequest
{
    public IEnumerable<string>? ProductsFilter { get; set; }
    public IEnumerable<string>? ClassificationsFilter { get; set; }
public IEnumerable<string>? IdFilter { get; set; }
    public string? TitleFilter { get; set; }
    public string? HardwareIdFilter { get; set; }
    public string? ComputerHardwareIdFilter { get; set; }
    public IEnumerable<string>? KbArticleFilter { get; set; }
    public bool SkipSuperseded { get; set; }
    public int FirstX { get; set; }
}
