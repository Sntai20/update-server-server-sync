// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Services;

using System;
using System.IO;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using Moq;
using UpdateEngine.Services;
using Xunit;

public class StorageFactoryTests : IDisposable
{
    private readonly Mock<ILogger> mockLogger;
    private readonly string testDirectory;

    public StorageFactoryTests()
    {
        this.mockLogger = new Mock<ILogger>();
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"StorageFactoryTests_{Guid.NewGuid()}");
    }

    public void Dispose()
    {
        // Cleanup test directories
        if (Directory.Exists(this.testDirectory))
        {
            try
            {
                Directory.Delete(this.testDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void CreateMetadataStore_LocalType_CreatesDirectory()
    {
        // Arrange
        var storePath = Path.Combine(this.testDirectory, "metadata");

        // Act
        var store = StorageFactory.CreateMetadataStore(
            storePath,
            "local",
            connectionString: null,
            containerName: null,
            createIfNotExists: true,
            this.mockLogger.Object);

        // Assert
        Assert.NotNull(store);
        Assert.True(Directory.Exists(storePath));
        this.mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Created metadata directory")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void CreateMetadataStore_LocalType_ExistingDirectory_OpensStore()
    {
        // Arrange
        var storePath = Path.Combine(this.testDirectory, "existing_metadata");
        Directory.CreateDirectory(storePath);

        // Act
        var store = StorageFactory.CreateMetadataStore(
            storePath,
            "local",
            connectionString: null,
            containerName: null,
            createIfNotExists: false,
            this.mockLogger.Object);

        // Assert
        Assert.NotNull(store);
        this.mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Using local file system for metadata store")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void CreateMetadataStore_LocalType_NonExistentDirectory_ThrowsException()
    {
        // Arrange
        var storePath = Path.Combine(this.testDirectory, "nonexistent");

        // Act & Assert
        var exception = Assert.Throws<DirectoryNotFoundException>(() =>
            StorageFactory.CreateMetadataStore(
                storePath,
                "local",
                connectionString: null,
                containerName: null,
                createIfNotExists: false,
                this.mockLogger.Object));

        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void CreateMetadataStore_AzureType_NoConnectionString_ThrowsException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            StorageFactory.CreateMetadataStore(
                "container-name",
                "azure",
                connectionString: null,
                containerName: "metadata",
                createIfNotExists: true,
                this.mockLogger.Object));

        Assert.Contains("Connection string required", exception.Message);
    }

    [Fact]
    public void CreateMetadataStore_InvalidType_ThrowsException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            StorageFactory.CreateMetadataStore(
                "some-path",
                "invalid-type",
                connectionString: null,
                containerName: null,
                createIfNotExists: true,
                this.mockLogger.Object));

        Assert.Contains("Unsupported metadata store type", exception.Message);
    }

    [Theory]
    [InlineData("local")]
    [InlineData("LOCAL")]
    [InlineData("filesystem")]
    [InlineData("FILESYSTEM")]
    public void CreateMetadataStore_LocalTypeVariants_WorksCorrectly(string storeType)
    {
        // Arrange
        var storePath = Path.Combine(this.testDirectory, $"metadata_{storeType}");

        // Act
        var store = StorageFactory.CreateMetadataStore(
            storePath,
            storeType,
            connectionString: null,
            containerName: null,
            createIfNotExists: true,
            this.mockLogger.Object);

        // Assert
        Assert.NotNull(store);
        Assert.True(Directory.Exists(storePath));
    }

    [Theory]
    [InlineData("azure")]
    [InlineData("AZURE")]
    [InlineData("azureblob")]
    [InlineData("AZUREBLOB")]
    public void CreateMetadataStore_AzureTypeVariants_RequiresConnectionString(string storeType)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            StorageFactory.CreateMetadataStore(
                "container",
                storeType,
                connectionString: null,
                containerName: "metadata",
                createIfNotExists: true,
                this.mockLogger.Object));

        Assert.Contains("Connection string required", exception.Message);
    }

    [Fact]
    public void CreateContentStore_NullPath_ReturnsNull()
    {
        // Act
        var store = StorageFactory.CreateContentStore(
            storePath: null,
            "local",
            connectionString: null,
            containerName: null,
            createIfNotExists: true,
            this.mockLogger.Object);

        // Assert
        Assert.Null(store);
        this.mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("catalog-only mode")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void CreateContentStore_EmptyPath_ReturnsNull()
    {
        // Act
        var store = StorageFactory.CreateContentStore(
            storePath: string.Empty,
            "local",
            connectionString: null,
            containerName: null,
            createIfNotExists: true,
            this.mockLogger.Object);

        // Assert
        Assert.Null(store);
    }

    [Fact]
    public void CreateContentStore_LocalType_CreatesDirectory()
    {
        // Arrange
        var storePath = Path.Combine(this.testDirectory, "content");

        // Act
        var store = StorageFactory.CreateContentStore(
            storePath,
            "local",
            connectionString: null,
            containerName: null,
            createIfNotExists: true,
            this.mockLogger.Object);

        // Assert
        Assert.NotNull(store);
        Assert.True(Directory.Exists(storePath));
    }

    [Fact]
    public void CreateContentStore_LocalType_NonExistentDirectory_ThrowsException()
    {
        // Arrange
        var storePath = Path.Combine(this.testDirectory, "nonexistent_content");

        // Act & Assert
        var exception = Assert.Throws<DirectoryNotFoundException>(() =>
            StorageFactory.CreateContentStore(
                storePath,
                "local",
                connectionString: null,
                containerName: null,
                createIfNotExists: false,
                this.mockLogger.Object));

        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void CreateContentStore_AzureType_NoConnectionString_ThrowsException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            StorageFactory.CreateContentStore(
                "container-name",
                "azure",
                connectionString: null,
                containerName: "content",
                createIfNotExists: true,
                this.mockLogger.Object));

        Assert.Contains("Connection string required", exception.Message);
    }

    [Fact]
    public void CreateContentStore_InvalidType_ThrowsException()
    {
        // Arrange
        var storePath = Path.Combine(this.testDirectory, "content");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            StorageFactory.CreateContentStore(
                storePath,
                "invalid-type",
                connectionString: null,
                containerName: null,
                createIfNotExists: true,
                this.mockLogger.Object));

        Assert.Contains("Unsupported content store type", exception.Message);
    }

    [Fact(Skip = "Requires Azurite emulator running on localhost")]
    public void CreateMetadataStore_AzureType_WithEmulator_CreatesStore()
    {
        // Arrange
        var connectionString = "UseDevelopmentStorage=true";
        var containerName = $"test-metadata-{Guid.NewGuid()}";

        try
        {
            // Act
            var store = StorageFactory.CreateMetadataStore(
                containerName,
                "azure",
                connectionString,
                containerName,
                createIfNotExists: true,
                this.mockLogger.Object);

            // Assert
            Assert.NotNull(store);
            this.mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully connected to Azure Storage")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            // Cleanup - delete test container
            try
            {
                var client = new BlobServiceClient(connectionString);
                client.GetBlobContainerClient(containerName).DeleteIfExists();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact(Skip = "Requires Azurite emulator running on localhost")]
    public void CreateContentStore_AzureType_WithEmulator_CreatesStore()
    {
        // Arrange
        var connectionString = "UseDevelopmentStorage=true";
        var containerName = $"test-content-{Guid.NewGuid()}";

        try
        {
            // Act
            var store = StorageFactory.CreateContentStore(
                containerName,
                "azure",
                connectionString,
                containerName,
                createIfNotExists: true,
                this.mockLogger.Object);

            // Assert
            Assert.NotNull(store);
        }
        finally
        {
            // Cleanup
            try
            {
                var client = new BlobServiceClient(connectionString);
                client.GetBlobContainerClient(containerName).DeleteIfExists();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void CreateMetadataStore_LogsCreationType()
    {
        // Arrange
        var storePath = Path.Combine(this.testDirectory, "logged_metadata");

        // Act
        StorageFactory.CreateMetadataStore(
            storePath,
            "local",
            connectionString: null,
            containerName: null,
            createIfNotExists: true,
            this.mockLogger.Object);

        // Assert
        this.mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Creating metadata store")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void CreateContentStore_LogsCreationType()
    {
        // Arrange
        var storePath = Path.Combine(this.testDirectory, "logged_content");

        // Act
        StorageFactory.CreateContentStore(
            storePath,
            "local",
            connectionString: null,
            containerName: null,
            createIfNotExists: true,
            this.mockLogger.Object);

        // Assert
        this.mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Creating content store")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}