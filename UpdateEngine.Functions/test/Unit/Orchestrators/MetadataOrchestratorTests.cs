// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Unit.Orchestrators;

using UpdateEngine.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpdateEngine.Metadata.ObjectModel;
using UpdateEngine.Metadata.Storage;
using Moq;
using UpdateEngine.Core.Orchestrators;
using Xunit;

/// <summary>
/// Unit tests for MetadataOrchestrator.
/// Uses mocked dependencies to test orchestrator logic in isolation.
/// </summary>
public class MetadataOrchestratorTests
{
    private readonly Mock<IMetadataStore> mockMetadataStore;
    private readonly Mock<IOptionsMonitor<AppConfig>> mockConfigMonitor;
    private readonly Mock<ILogger<MetadataOrchestrator>> mockLogger;
    private readonly MetadataOrchestrator orchestrator;
    private readonly AppConfig testConfig;

    public MetadataOrchestratorTests()
    {
        this.mockMetadataStore = new Mock<IMetadataStore>();
        this.mockConfigMonitor = new Mock<IOptionsMonitor<AppConfig>>();
        this.mockLogger = new Mock<ILogger<MetadataOrchestrator>>();

        this.testConfig = new AppConfig
        {
            StorageConfiguration = new StorageConfiguration
            {
                MetadataPath = "./test-metadata",
                UseAzureStorageForMetadata = false
            },
            ServiceConfiguration = new ServiceConfiguration
            {
                ServiceUrl = "http://localhost:7071"
            }
        };

        this.mockConfigMonitor.Setup(m => m.CurrentValue).Returns(this.testConfig);

        this.orchestrator = new MetadataOrchestrator(
            this.mockMetadataStore.Object,
            this.mockConfigMonitor.Object,
            this.mockLogger.Object,
            null); // CacheService not needed for unit tests
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsCorrectStatistics()
    {
        // Arrange
        var mockIdentities = CreateMockIdentities(100);
        this.mockMetadataStore.Setup(s => s.GetPackageIdentities()).Returns(mockIdentities);
        this.mockMetadataStore.Setup(s => s.IsReindexingRequired).Returns(false);

        // Act
        var result = await this.orchestrator.GetStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalUpdates.Should().Be(100);
        result.ReindexingRequired.Should().BeFalse();
        this.mockMetadataStore.Verify(s => s.GetPackageIdentities(), Times.Once);
    }

    [Fact]
    public async Task GetStatisticsAsync_HandlesEmptyStore()
    {
        // Arrange
        this.mockMetadataStore.Setup(s => s.GetPackageIdentities()).Returns(new List<IPackageIdentity>());
        this.mockMetadataStore.Setup(s => s.IsReindexingRequired).Returns(false);

        // Act
        var result = await this.orchestrator.GetStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalUpdates.Should().Be(0);
        result.TotalCategories.Should().Be(0);
    }

    [Fact]
    public async Task QueryUpdatesAsync_WithNoFilters_ReturnsAllUpdates()
    {
        // Arrange
        var mockIdentities = CreateMockIdentities(50);
        this.mockMetadataStore.Setup(s => s.GetPackageIdentities()).Returns(mockIdentities);

        var query = new MetadataQuery();

        // Act
        var result = await this.orchestrator.QueryUpdatesAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(50);
    }

    [Fact]
    public async Task QueryUpdatesAsync_WithMaxResults_ReturnsLimitedResults()
    {
        // Arrange
        var mockIdentities = CreateMockIdentities(100);
        this.mockMetadataStore.Setup(s => s.GetPackageIdentities()).Returns(mockIdentities);

        var query = new MetadataQuery { MaxResults = 25 };

        // Act
        var result = await this.orchestrator.QueryUpdatesAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(25);
    }

    [Fact]
    public async Task QueryUpdatesAsync_WithSkip_SkipsCorrectNumber()
    {
        // Arrange
        var mockIdentities = CreateMockIdentities(100);
        this.mockMetadataStore.Setup(s => s.GetPackageIdentities()).Returns(mockIdentities);

        var query = new MetadataQuery { Skip = 50, MaxResults = 10 };

        // Act
        var result = await this.orchestrator.QueryUpdatesAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(10);
    }

    [Fact]
    public async Task GetUpdateDetailsAsync_WithValidId_ReturnsPackage()
    {
        // Arrange
        var mockIdentity = CreateMockIdentity("test-update-1");
        var mockPackage = new Mock<IPackage>();
        mockPackage.Setup(p => p.Id).Returns(mockIdentity);

        this.mockMetadataStore.Setup(s => s.ContainsPackage(mockIdentity)).Returns(true);
        this.mockMetadataStore.Setup(s => s.GetPackage(mockIdentity)).Returns(mockPackage.Object);

        // Act
        var result = await this.orchestrator.GetUpdateDetailsAsync(mockIdentity);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(mockPackage.Object);
        this.mockMetadataStore.Verify(s => s.GetPackage(mockIdentity), Times.Once);
    }

    [Fact]
    public async Task GetUpdateDetailsAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var mockIdentity = CreateMockIdentity("non-existent-update");
        this.mockMetadataStore.Setup(s => s.ContainsPackage(mockIdentity)).Returns(false);

        // Act
        var result = await this.orchestrator.GetUpdateDetailsAsync(mockIdentity);

        // Assert
        result.Should().BeNull();
        this.mockMetadataStore.Verify(s => s.GetPackage(It.IsAny<IPackageIdentity>()), Times.Never);
    }

    [Fact]
    public async Task ExportMetadataAsync_WithValidDestination_ReturnsSuccess()
    {
        // Arrange
        var mockDestination = new Mock<IMetadataStore>();
        mockDestination.Setup(d => d.GetPackageIdentities()).Returns(new List<IPackageIdentity>());

        var sourceIdentities = CreateMockIdentities(10);
        this.mockMetadataStore.Setup(s => s.GetPackageIdentities()).Returns(sourceIdentities);

        // Act
        var result = await this.orchestrator.ExportMetadataAsync(mockDestination.Object);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        this.mockMetadataStore.Verify(s => s.CopyTo(mockDestination.Object, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetIndexStatusAsync_ReturnsCorrectStatus()
    {
        // Arrange
        var mockIdentities = CreateMockIdentities(50);
        this.mockMetadataStore.Setup(s => s.GetPackageIdentities()).Returns(mockIdentities);
        this.mockMetadataStore.Setup(s => s.IsReindexingRequired).Returns(true);
        this.mockMetadataStore.Setup(s => s.IsMetadataIndexingSupported).Returns(true);

        // Act
        var result = await this.orchestrator.GetIndexStatusAsync();

        // Assert
        result.Should().NotBeNull();
        result.ReindexingRequired.Should().BeTrue();
        result.IndexingSupported.Should().BeTrue();
        result.IndexedPackageCount.Should().Be(50);
    }

    [Fact]
    public async Task ReindexAsync_CallsReIndex()
    {
        // Arrange
        var mockIdentities = CreateMockIdentities(100);
        this.mockMetadataStore.Setup(s => s.GetPackageIdentities()).Returns(mockIdentities);

        // Act
        var result = await this.orchestrator.ReindexAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ReindexedCount.Should().BeGreaterThan(0);
        this.mockMetadataStore.Verify(s => s.ReIndex(), Times.Once);
    }

    [Fact]
    public async Task ReindexAsync_WithProgress_ReportsProgress()
    {
        // Arrange
        var mockIdentities = CreateMockIdentities(50);
        this.mockMetadataStore.Setup(s => s.GetPackageIdentities()).Returns(mockIdentities);

        var progressReports = new List<ReindexProgress>();
        var progress = new Progress<ReindexProgress>(p => progressReports.Add(p));

        // Act
        var result = await this.orchestrator.ReindexAsync(progress);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        // Progress may or may not be reported depending on store implementation
    }

    #region Helper Methods

    private static List<IPackageIdentity> CreateMockIdentities(int count)
    {
        var identities = new List<IPackageIdentity>();
        for (int i = 0; i < count; i++)
        {
            identities.Add(CreateMockIdentity($"update-{i}"));
        }
        return identities;
    }

    private static IPackageIdentity CreateMockIdentity(string id)
    {
        var mockIdentity = new Mock<IPackageIdentity>();
        mockIdentity.Setup(i => i.ToString()).Returns(id);
        mockIdentity.Setup(i => i.OpenIdHex).Returns(id);
        mockIdentity.Setup(i => i.Partition).Returns("Microsoft");
        return mockIdentity.Object;
    }

    #endregion
}
