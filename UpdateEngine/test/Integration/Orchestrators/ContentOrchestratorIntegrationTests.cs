// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Integration.Orchestrators;

using Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.Storage.Local;
using UpdateEngine.Core.Orchestrators;
using Xunit;

/// <summary>
/// Integration tests for ContentOrchestrator using real content stores.
/// Tests full orchestrator functionality with actual storage backends.
/// </summary>
[Collection("Integration")]
public class ContentOrchestratorIntegrationTests : IAsyncLifetime
{
    private readonly string tempMetadataPath;
    private readonly string tempContentPath;
    private IMetadataStore? metadataStore;
    private IContentStore? contentStore;
    private ContentOrchestrator? orchestrator;
    private readonly AppConfig testConfig;

    public ContentOrchestratorIntegrationTests()
    {
        this.tempMetadataPath = Path.Combine(Path.GetTempPath(), $"content-metadata-test-{Guid.NewGuid()}");
        this.tempContentPath = Path.Combine(Path.GetTempPath(), $"content-test-{Guid.NewGuid()}");
        
        Directory.CreateDirectory(this.tempMetadataPath);
        Directory.CreateDirectory(this.tempContentPath);

        this.testConfig = new AppConfig
        {
            StorageConfiguration = new StorageConfiguration
            {
                MetadataPath = this.tempMetadataPath,
                ContentPath = this.tempContentPath,
                UseAzureStorageForMetadata = false,
                UseAzureStorageForContent = false
            }
        };
    }

    public async Task InitializeAsync()
    {
        // Initialize real stores - OpenOrCreate will create empty store if it doesn't exist
        this.metadataStore = PackageStore.OpenOrCreate(this.tempMetadataPath);
        this.contentStore = new FileSystemContentStore(this.tempContentPath);

        var configMonitor = new OptionsMonitorWrapper<AppConfig>(this.testConfig);
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<ContentOrchestrator>();

        this.orchestrator = new ContentOrchestrator(
            this.metadataStore,
            this.contentStore,
            configMonitor,
            logger,
            null); // CacheService not needed for integration tests

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Clean up
        if (Directory.Exists(this.tempMetadataPath))
        {
            try
            {
                Directory.Delete(this.tempMetadataPath, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }

        if (Directory.Exists(this.tempContentPath))
        {
            try
            {
                Directory.Delete(this.tempContentPath, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }

        await Task.CompletedTask;
    }

    [Fact]
    public async Task GetStatisticsAsync_WithEmptyStores_ReturnsZeroStatistics()
    {
        // Act
        var result = await this.orchestrator!.GetStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalFiles.Should().Be(0);
        result.UpdatesWithContent.Should().Be(0);
        result.PendingDownloads.Should().Be(0);
    }

    [Fact]
    public async Task GetStatisticsAsync_WithContentStore_ReturnsContentStoreStatistics()
    {
        // Act
        var result = await this.orchestrator!.GetStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.PendingDownloads.Should().Be(0); // Empty queue
        result.QueuedSizeBytes.Should().Be(0);
    }

    [Fact]
    public async Task DownloadContentAsync_WithEmptyUpdateList_CompletesSuccessfully()
    {
        // Arrange
        var emptyList = new List<Microsoft.PackageGraph.ObjectModel.IPackageIdentity>();

        // Act
        var result = await this.orchestrator!.DownloadContentAsync(emptyList);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.DownloadedCount.Should().Be(0);
        result.FailedCount.Should().Be(0);
    }

    [Fact]
    public async Task VerifyContentAsync_WithEmptyUpdateList_CompletesSuccessfully()
    {
        // Arrange
        var emptyList = new List<Microsoft.PackageGraph.ObjectModel.IPackageIdentity>();

        // Act
        var result = await this.orchestrator!.VerifyContentAsync(emptyList);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.VerifiedCount.Should().Be(0);
        result.MissingCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckContentAvailabilityAsync_WithEmptyList_ReturnsEmptyDictionary()
    {
        // Arrange
        var emptyList = new List<Microsoft.PackageGraph.ObjectModel.IPackageIdentity>();

        // Act
        var result = await this.orchestrator!.CheckContentAvailabilityAsync(emptyList);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CleanupContentAsync_DryRun_CompletesSuccessfully()
    {
        // Act
        var result = await this.orchestrator!.CleanupContentAsync(dryRun: true);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.DeletedCount.Should().Be(0);
    }

    [Fact]
    public async Task CleanupContentAsync_WithEmptyStore_CompletesSuccessfully()
    {
        // Act
        var result = await this.orchestrator!.CleanupContentAsync(dryRun: false);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.DeletedCount.Should().Be(0);
    }
}
