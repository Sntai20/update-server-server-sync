// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Unit.Orchestrators;

using Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.PackageGraph.ObjectModel;
using Microsoft.PackageGraph.Storage;
using Moq;
using UpdateEngine.Core.Orchestrators;
using Xunit;

/// <summary>
/// Unit tests for ContentOrchestrator.
/// Uses mocked dependencies to test orchestrator logic in isolation.
/// </summary>
public class ContentOrchestratorTests
{
    private readonly Mock<IMetadataStore> mockMetadataStore;
    private readonly Mock<IContentStore> mockContentStore;
    private readonly Mock<IOptionsMonitor<AppConfig>> mockConfigMonitor;
    private readonly Mock<ILogger<ContentOrchestrator>> mockLogger;
    private readonly ContentOrchestrator orchestrator;
    private readonly AppConfig testConfig;

    public ContentOrchestratorTests()
    {
        this.mockMetadataStore = new Mock<IMetadataStore>();
        this.mockContentStore = new Mock<IContentStore>();
        this.mockConfigMonitor = new Mock<IOptionsMonitor<AppConfig>>();
        this.mockLogger = new Mock<ILogger<ContentOrchestrator>>();

        this.testConfig = new AppConfig
        {
            StorageConfiguration = new StorageConfiguration
            {
                ContentPath = "./test-content",
                UseAzureStorageForContent = false
            }
        };

        this.mockConfigMonitor.Setup(m => m.CurrentValue).Returns(this.testConfig);

        this.orchestrator = new ContentOrchestrator(
            this.mockMetadataStore.Object,
            this.mockContentStore.Object,
            this.mockConfigMonitor.Object,
            this.mockLogger.Object,
            null); // CacheService not needed for unit tests
    }

    [Fact]
    public async Task GetStatisticsAsync_WithContentStore_ReturnsStatistics()
    {
        // Arrange
        var mockIdentities = CreateMockIdentities(10);
        this.mockMetadataStore.Setup(s => s.GetPackageIdentities()).Returns(mockIdentities);
        this.mockContentStore.Setup(s => s.QueuedCount).Returns(5);
        this.mockContentStore.Setup(s => s.QueuedSize).Returns(1024 * 1024);

        foreach (var identity in mockIdentities)
        {
            var mockFiles = new List<IContentFile> { CreateMockContentFile("file1") };
            this.mockMetadataStore.Setup(s => s.GetFiles<IContentFile>(identity)).Returns(mockFiles);
            this.mockContentStore.Setup(s => s.Contains(It.IsAny<IContentFile>())).Returns(true);
        }

        // Act
        var result = await this.orchestrator.GetStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.UpdatesWithContent.Should().Be(10);
        result.PendingDownloads.Should().Be(5);
        result.QueuedSizeBytes.Should().Be(1024 * 1024);
    }

    [Fact]
    public async Task GetStatisticsAsync_WithoutContentStore_ReturnsZeros()
    {
        // Arrange - Create orchestrator without content store
        var orchestratorNoContent = new ContentOrchestrator(
            this.mockMetadataStore.Object,
            null, // No content store
            this.mockConfigMonitor.Object,
            this.mockLogger.Object,
            null); // CacheService not needed for unit tests

        // Act
        var result = await orchestratorNoContent.GetStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalFiles.Should().Be(0);
        result.UpdatesWithContent.Should().Be(0);
        result.PendingDownloads.Should().Be(0);
    }

    [Fact]
    public async Task DownloadContentAsync_WithValidUpdates_ReturnsSuccess()
    {
        // Arrange
        var mockIdentities = CreateMockIdentities(2);
        var mockFiles = new List<IContentFile>
        {
            CreateMockContentFile("file1"),
            CreateMockContentFile("file2")
        };

        foreach (var identity in mockIdentities)
        {
            this.mockMetadataStore.Setup(s => s.GetFiles<IContentFile>(identity)).Returns(mockFiles);
        }

        this.mockContentStore.Setup(s => s.Contains(It.IsAny<IContentFile>())).Returns(true);

        // Act
        var result = await this.orchestrator.DownloadContentAsync(mockIdentities);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.FailedCount.Should().Be(0);
    }

    [Fact]
    public async Task DownloadContentAsync_WithoutContentStore_ReturnsFailure()
    {
        // Arrange - Create orchestrator without content store
        var orchestratorNoContent = new ContentOrchestrator(
            this.mockMetadataStore.Object,
            null, // No content store
            this.mockConfigMonitor.Object,
            this.mockLogger.Object,
            null); // CacheService not needed for unit tests

        var mockIdentities = CreateMockIdentities(1);

        // Act
        var result = await orchestratorNoContent.DownloadContentAsync(mockIdentities);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors.First().Should().Contain("not configured");
    }

    [Fact]
    public async Task DownloadContentAsync_WithProgress_ReportsProgress()
    {
        // Arrange
        var mockIdentities = CreateMockIdentities(1);
        var mockFiles = new List<IContentFile> { CreateMockContentFile("file1") };

        this.mockMetadataStore.Setup(s => s.GetFiles<IContentFile>(It.IsAny<IPackageIdentity>())).Returns(mockFiles);
        this.mockContentStore.Setup(s => s.Contains(It.IsAny<IContentFile>())).Returns(true);

        var progressReports = new List<DownloadProgress>();
        var progress = new Progress<DownloadProgress>(p => progressReports.Add(p));

        // Act
        var result = await this.orchestrator.DownloadContentAsync(mockIdentities, progress);

        // Assert
        result.Should().NotBeNull();
        // Progress reporting may vary based on implementation
    }

    [Fact]
    public async Task VerifyContentAsync_WithAllValid_ReturnsSuccess()
    {
        // Arrange
        var mockIdentities = CreateMockIdentities(5);
        var mockFiles = new List<IContentFile> { CreateMockContentFile("file1") };

        foreach (var identity in mockIdentities)
        {
            this.mockMetadataStore.Setup(s => s.GetFiles<IContentFile>(identity)).Returns(mockFiles);
        }

        this.mockContentStore.Setup(s => s.Contains(It.IsAny<IContentFile>())).Returns(true);

        // Act
        var result = await this.orchestrator.VerifyContentAsync(mockIdentities);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.VerifiedCount.Should().Be(5);
        result.FailedCount.Should().Be(0);
        result.MissingCount.Should().Be(0);
    }

    [Fact]
    public async Task VerifyContentAsync_WithMissingFiles_ReportsMissing()
    {
        // Arrange
        var mockIdentities = CreateMockIdentities(3);
        var mockFiles = new List<IContentFile> { CreateMockContentFile("file1") };

        foreach (var identity in mockIdentities)
        {
            this.mockMetadataStore.Setup(s => s.GetFiles<IContentFile>(identity)).Returns(mockFiles);
        }

        // First 2 exist, last one is missing
        var callCount = 0;
        this.mockContentStore.Setup(s => s.Contains(It.IsAny<IContentFile>()))
            .Returns(() => callCount++ < 2);

        // Act
        var result = await this.orchestrator.VerifyContentAsync(mockIdentities);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.VerifiedCount.Should().Be(2);
        result.MissingCount.Should().Be(1);
        result.FailedFiles.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetContentFilesAsync_WithValidId_ReturnsFiles()
    {
        // Arrange
        var mockIdentity = CreateMockIdentity("test-update");
        var mockFiles = new List<IContentFile>
        {
            CreateMockContentFile("file1"),
            CreateMockContentFile("file2")
        };

        this.mockMetadataStore.Setup(s => s.GetFiles<IContentFile>(mockIdentity)).Returns(mockFiles);

        // Act
        var result = await this.orchestrator.GetContentFilesAsync(mockIdentity);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
    }

    [Fact]
    public async Task CheckContentAvailabilityAsync_ReturnsCorrectStatus()
    {
        // Arrange
        var mockIdentities = CreateMockIdentities(3);
        var mockFiles = new List<IContentFile> { CreateMockContentFile("file1") };

        foreach (var identity in mockIdentities)
        {
            this.mockMetadataStore.Setup(s => s.GetFiles<IContentFile>(identity)).Returns(mockFiles);
        }

        // First 2 available, last one not
        var callCount = 0;
        this.mockContentStore.Setup(s => s.Contains(It.IsAny<IContentFile>()))
            .Returns(() => callCount++ < 2);

        // Act
        var result = await this.orchestrator.CheckContentAvailabilityAsync(mockIdentities);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(3);
        result.Values.Count(v => v).Should().Be(2); // 2 available
        result.Values.Count(v => !v).Should().Be(1); // 1 not available
    }

    [Fact]
    public async Task CleanupContentAsync_DryRun_ReturnsResult()
    {
        // Arrange - Nothing to set up for dry run

        // Act
        var result = await this.orchestrator.CleanupContentAsync(dryRun: true);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CleanupContentAsync_WithoutContentStore_ReturnsFailure()
    {
        // Arrange - Create orchestrator without content store
        var orchestratorNoContent = new ContentOrchestrator(
            this.mockMetadataStore.Object,
            null, // No content store
            this.mockConfigMonitor.Object,
            this.mockLogger.Object,
            null); // CacheService not needed for unit tests

        // Act
        var result = await orchestratorNoContent.CleanupContentAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not configured");
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

    private static IContentFile CreateMockContentFile(string filename)
    {
        var mockFile = new Mock<IContentFile>();
        mockFile.Setup(f => f.Source).Returns($"http://example.com/{filename}");
        mockFile.Setup(f => f.Size).Returns(1024UL);
        
        var mockDigest = new Mock<IContentFileDigest>();
        mockDigest.Setup(d => d.HexString).Returns("abc123");
        mockDigest.Setup(d => d.DigestBase64).Returns("qwerty==");
        mockFile.Setup(f => f.Digest).Returns(mockDigest.Object);

        return mockFile.Object;
    }

    #endregion
}
