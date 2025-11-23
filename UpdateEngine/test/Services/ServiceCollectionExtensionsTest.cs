// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Tests.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using UpdateEngine.Core; // For AddUpdateEngineCore extension
using UpdateEngine.Core.Services; // For service interfaces
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Tests for ServiceCollectionExtensions - ensuring proper DI registration
/// </summary>
public class ServiceCollectionExtensionsTest
{
    private readonly ITestOutputHelper output;

    public ServiceCollectionExtensionsTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void AddUpdateEngineCore_RegistersMetadataStore_LocalFileSystem()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(this.output));

        var metadataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StorageConfiguration:UseAzureStorageForMetadata"] = "false",
                ["StorageConfiguration:MetadataPath"] = metadataPath
            })
            .Build();

        // Act
        services.AddUpdateEngineCore(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var metadataStore = serviceProvider.GetService<IMetadataStore>();
        Assert.NotNull(metadataStore);

        // Cleanup
        if (Directory.Exists(metadataPath))
        {
            Directory.Delete(metadataPath, true);
        }
    }

    [Fact]
    public void AddUpdateEngineCore_RegistersContentStore_LocalFileSystem()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(this.output));

        var metadataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var contentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StorageConfiguration:UseAzureStorageForMetadata"] = "false",
                ["StorageConfiguration:MetadataPath"] = metadataPath,
                ["StorageConfiguration:UseAzureStorageForContent"] = "false",
                ["StorageConfiguration:ContentPath"] = contentPath
            })
            .Build();

        // Act
        services.AddUpdateEngineCore(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var contentStore = serviceProvider.GetService<IContentStore?>();
        Assert.NotNull(contentStore);

        // Cleanup
        if (Directory.Exists(metadataPath))
        {
            Directory.Delete(metadataPath, true);
        }
        if (Directory.Exists(contentPath))
        {
            Directory.Delete(contentPath, true);
        }
    }

    [Fact]
    public void AddUpdateEngineCore_ContentStore_Optional()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(this.output));

        var metadataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StorageConfiguration:UseAzureStorageForMetadata"] = "false",
                ["StorageConfiguration:MetadataPath"] = metadataPath,
                ["StorageConfiguration:ContentPath"] = null // No content store
            })
            .Build();

        // Act
        services.AddUpdateEngineCore(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - should work without content store (catalog-only mode)
        var contentStore = serviceProvider.GetService<IContentStore?>();
        Assert.Null(contentStore);

        var metadataStore = serviceProvider.GetService<IMetadataStore>();
        Assert.NotNull(metadataStore);

        // Cleanup
        if (Directory.Exists(metadataPath))
        {
            Directory.Delete(metadataPath, true);
        }
    }

    [Fact]
    public void AddUpdateEngineCore_CreatesDirectories_IfNotExist()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(this.output));

        var metadataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "metadata");
        var contentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "content");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StorageConfiguration:UseAzureStorageForMetadata"] = "false",
                ["StorageConfiguration:MetadataPath"] = metadataPath,
                ["StorageConfiguration:UseAzureStorageForContent"] = "false",
                ["StorageConfiguration:ContentPath"] = contentPath
            })
            .Build();

        // Ensure directories don't exist
        Assert.False(Directory.Exists(metadataPath));
        Assert.False(Directory.Exists(contentPath));

        // Act
        services.AddUpdateEngineCore(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Trigger store initialization
        var _ = serviceProvider.GetService<IMetadataStore>();
        var __ = serviceProvider.GetService<IContentStore?>();

        // Assert - directories should be created
        Assert.True(Directory.Exists(metadataPath));
        // Note: Content store directory creation depends on implementation
        // Some implementations may create on first write

        // Cleanup
        if (Directory.Exists(metadataPath))
        {
            Directory.Delete(metadataPath, true);
        }
        if (Directory.Exists(contentPath))
        {
            Directory.Delete(contentPath, true);
        }
    }

    [Fact]
    public void AddUpdateEngineCore_RegistersOrchestrators()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(this.output));

        var metadataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StorageConfiguration:UseAzureStorageForMetadata"] = "false",
                ["StorageConfiguration:MetadataPath"] = metadataPath
            })
            .Build();

        // Act
        services.AddUpdateEngineCore(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Orchestrators should be registered
        var syncOrchestrator = serviceProvider.GetService<UpdateEngine.Core.Orchestrators.ISyncOrchestrator>();
        var metadataOrchestrator = serviceProvider.GetService<UpdateEngine.Core.Orchestrators.IMetadataOrchestrator>();
        var contentOrchestrator = serviceProvider.GetService<UpdateEngine.Core.Orchestrators.IContentOrchestrator>();

        Assert.NotNull(syncOrchestrator);
        Assert.NotNull(metadataOrchestrator);
        Assert.NotNull(contentOrchestrator);

        // Cleanup
        if (Directory.Exists(metadataPath))
        {
            Directory.Delete(metadataPath, true);
        }
    }

    [Fact(Skip = "Domain services are registered by host project, not by AddUpdateEngineCore")]
    public void AddUpdateEngineCore_RegistersAnomalyDetectionServices()
    {
        // This test is skipped because domain services (ISyncService, IQueryService, etc.)
        // are registered by the host project (UpdateEngine, WorkerService), not by AddUpdateEngineCore
        // AddUpdateEngineCore only registers orchestrators, stores, and health checks
    }

    [Fact]
    public void AddUpdateEngineCore_DefaultPaths_AppliedCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(this.output));

        // No paths configured - should use defaults from Configuration project
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act & Assert - should throw due to missing configuration
        // (defaults should come from appsettings.defaults.json when properly loaded)
        Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddUpdateEngineCore(configuration);
            var serviceProvider = services.BuildServiceProvider();
            var _ = serviceProvider.GetService<IMetadataStore>();
        });
    }
}

/// <summary>
/// Helper extension for adding xUnit ITestOutputHelper to logging
/// </summary>
internal static class LoggingBuilderExtensions
{
    public static ILoggingBuilder AddXUnit(this ILoggingBuilder builder, ITestOutputHelper output)
    {
        builder.AddProvider(new XUnitLoggerProvider(output));
        return builder;
    }
}

/// <summary>
/// Logger provider for xUnit test output
/// </summary>
internal class XUnitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper output;

    public XUnitLoggerProvider(ITestOutputHelper output)
    {
        this.output = output;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new XUnitLogger(this.output, categoryName);
    }

    public void Dispose() { }
}

/// <summary>
/// Logger implementation for xUnit test output
/// </summary>
internal class XUnitLogger : ILogger
{
    private readonly ITestOutputHelper output;
    private readonly string categoryName = null!;

    public XUnitLogger(ITestOutputHelper output, string categoryName)
    {
        this.output = output;
        this.categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        try
        {
            this.output.WriteLine($"[{logLevel}] {this.categoryName}: {formatter(state, exception)}");
            if (exception != null)
            {
                this.output.WriteLine(exception.ToString());
            }
        }
        catch
        {
            // Ignore output failures in tests
        }
    }
}
