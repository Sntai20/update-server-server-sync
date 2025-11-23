// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Integration.Orchestrators;

using Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.PackageGraph.ObjectModel;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.Storage.Local;
using UpdateEngine.Core.Orchestrators;
using Xunit;

/// <summary>
/// Integration tests for MetadataOrchestrator using real metadata stores.
/// Tests full orchestrator functionality with actual storage backends.
/// </summary>
[Collection("Integration")]
public class MetadataOrchestratorIntegrationTests : IAsyncLifetime
{
    private readonly string tempStorePath;
    private IMetadataStore? metadataStore;
    private MetadataOrchestrator? orchestrator;
    private readonly AppConfig testConfig;

    public MetadataOrchestratorIntegrationTests()
    {
        this.tempStorePath = Path.Combine(Path.GetTempPath(), $"metadata-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(this.tempStorePath);

        this.testConfig = new AppConfig
        {
            StorageConfiguration = new StorageConfiguration
            {
                MetadataPath = this.tempStorePath,
                UseAzureStorageForMetadata = false
            },
            ServiceConfiguration = new ServiceConfiguration
            {
                ServiceUrl = "http://localhost:7071"
            }
        };
    }

    public async Task InitializeAsync()
    {
        // Initialize real metadata store - OpenOrCreate will create empty store if it doesn't exist
        this.metadataStore = PackageStore.OpenOrCreate(this.tempStorePath);

        var configMonitor = new OptionsMonitorWrapper<AppConfig>(this.testConfig);
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<MetadataOrchestrator>();

        this.orchestrator = new MetadataOrchestrator(
            this.metadataStore,
            configMonitor,
            logger,
            null); // CacheService not needed for integration tests

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Clean up
        if (Directory.Exists(this.tempStorePath))
        {
            try
            {
                Directory.Delete(this.tempStorePath, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }

        await Task.CompletedTask;
    }

    [Fact]
    public async Task GetStatisticsAsync_WithEmptyStore_ReturnsZeroStatistics()
    {
        // Act
        var result = await this.orchestrator!.GetStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalUpdates.Should().Be(0);
        result.TotalCategories.Should().Be(0);
    }

    [Fact]
    public async Task QueryUpdatesAsync_WithEmptyStore_ReturnsEmptyList()
    {
        // Arrange
        var query = new MetadataQuery();

        // Act
        var result = await this.orchestrator!.QueryUpdatesAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUpdateDetailsAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var mockIdentity = CreateMockIdentity("non-existent");

        // Act
        var result = await this.orchestrator!.GetUpdateDetailsAsync(mockIdentity);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetIndexStatusAsync_WithNewStore_ReturnsCorrectStatus()
    {
        // Act
        var result = await this.orchestrator!.GetIndexStatusAsync();

        // Assert
        result.Should().NotBeNull();
        result.IndexingSupported.Should().BeTrue();
        result.IndexedPackageCount.Should().Be(0);
    }

    [Fact]
    public async Task ReindexAsync_WithEmptyStore_CompletesSuccessfully()
    {
        // Act
        var result = await this.orchestrator!.ReindexAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ReindexedCount.Should().Be(0);
    }

    [Fact]
    public async Task ExportMetadataAsync_ToNewStore_CompletesSuccessfully()
    {
        // Arrange
        var destPath = Path.Combine(Path.GetTempPath(), $"metadata-export-{Guid.NewGuid()}");
        
        try
        {
            // Use OpenOrCreate to create the destination store
            var destStore = PackageStore.OpenOrCreate(destPath);

            // Act
            var result = await this.orchestrator!.ExportMetadataAsync(destStore);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.ExportedCount.Should().Be(0); // Empty store
        }
        finally
        {
            if (Directory.Exists(destPath))
            {
                try
                {
                    Directory.Delete(destPath, recursive: true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }
    }

    #region Helper Methods

    private static IPackageIdentity CreateMockIdentity(string id)
    {
        // For integration tests, we would ideally use real package identities
        // For now, create a simple mock
        return new TestPackageIdentity(id);
    }

    private class TestPackageIdentity : IPackageIdentity
    {
        private readonly byte[] openId;

        public TestPackageIdentity(string id)
        {
            this.OpenIdHex = id;
            // Create a simple byte array from the string for testing
            this.openId = System.Text.Encoding.UTF8.GetBytes(id);
        }

        public string OpenIdHex { get; }
        public byte[] OpenId => this.openId;
        public string Partition => "Microsoft";

        public int CompareTo(object? obj)
        {
            if (obj is IPackageIdentity other)
            {
                return string.Compare(this.OpenIdHex, other.OpenIdHex, StringComparison.Ordinal);
            }
            return 1;
        }

        public override string ToString() => this.OpenIdHex;
    }

    #endregion
}

/// <summary>
/// Simple wrapper to convert IOptions to IOptionsMonitor for testing.
/// </summary>
internal class OptionsMonitorWrapper<T> : IOptionsMonitor<T>
{
    private readonly T currentValue;

    public OptionsMonitorWrapper(T value)
    {
        this.currentValue = value;
    }

    public T CurrentValue => this.currentValue;

    public T Get(string? name) => this.currentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
