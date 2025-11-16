// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Tests.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using UpdateEngine.Services;
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
    public void AddMicrosoftUpdateServices_RegistersMetadataStore_LocalFileSystem()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(this.output));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseAzureStorageForMetadata"] = "false",
                ["MetadataStorePath"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
            })
            .Build();

        // Act
        services.AddMicrosoftUpdateServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var metadataStore = serviceProvider.GetService<IMetadataStore>();
        Assert.NotNull(metadataStore);
        Assert.Equal("LocalMetadataStore", metadataStore.GetType().Name);

        // Cleanup
        var storePath = configuration["MetadataStorePath"];
        if (storePath != null && Directory.Exists(storePath))
        {
            Directory.Delete(storePath, true);
        }
    }

    [Fact]
    public void AddMicrosoftUpdateServices_RegistersContentStore_LocalFileSystem()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(this.output));

        var metadataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var contentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseAzureStorageForMetadata"] = "false",
                ["MetadataStorePath"] = metadataPath,
                ["UseAzureStorageForContent"] = "false",
                ["ContentStorePath"] = contentPath
            })
            .Build();

        // Act
        services.AddMicrosoftUpdateServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var contentStore = serviceProvider.GetService<IContentStore?>();
        Assert.NotNull(contentStore);
        Assert.Equal("FileSystemContentStore", contentStore.GetType().Name);

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
    public void AddMicrosoftUpdateServices_ContentStore_Optional()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(this.output));

        var metadataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseAzureStorageForMetadata"] = "false",
                ["MetadataStorePath"] = metadataPath,
                ["ContentStorePath"] = null // No content store
            })
            .Build();

        // Act
        services.AddMicrosoftUpdateServices(configuration);
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
    public void AddMicrosoftUpdateServices_CreatesDirectories_IfNotExist()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(this.output));

        var metadataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "metadata");
        var contentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "content");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseAzureStorageForMetadata"] = "false",
                ["MetadataStorePath"] = metadataPath,
                ["UseAzureStorageForContent"] = "false",
                ["ContentStorePath"] = contentPath
            })
            .Build();

        // Ensure directories don't exist
        Assert.False(Directory.Exists(metadataPath));
        Assert.False(Directory.Exists(contentPath));

        // Act
        services.AddMicrosoftUpdateServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Trigger store initialization
        var _ = serviceProvider.GetService<IMetadataStore>();
        var __ = serviceProvider.GetService<IContentStore?>();

        // Assert - directories should be created
        Assert.True(Directory.Exists(metadataPath));
        Assert.True(Directory.Exists(contentPath));

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
    public void AddMicrosoftUpdateServices_RegistersWebServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(this.output));

        var metadataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseAzureStorageForMetadata"] = "false",
                ["MetadataStorePath"] = metadataPath
            })
            .Build();

        // Act
        services.AddMicrosoftUpdateServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Web services should be registered
        var clientSyncType = Type.GetType("Microsoft.UpdateServices.WebServices.ClientSync.ClientSyncWebService, Microsoft.UpdateServices.WebServices.ClientSync");
        var serverSyncType = Type.GetType("Microsoft.UpdateServices.WebServices.ServerSync.ServerSyncWebService, Microsoft.UpdateServices.WebServices.ServerSync");

        Assert.NotNull(clientSyncType);
        Assert.NotNull(serverSyncType);

        Assert.NotNull(serviceProvider.GetService(clientSyncType));
        Assert.NotNull(serviceProvider.GetService(serverSyncType));

        // Cleanup
        if (Directory.Exists(metadataPath))
        {
            Directory.Delete(metadataPath, true);
        }
    }

    [Fact]
    public void AddMicrosoftUpdateServices_RegistersAnomalyDetectionServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(this.output));

        var metadataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseAzureStorageForMetadata"] = "false",
                ["MetadataStorePath"] = metadataPath
            })
            .Build();

        // Act
        services.AddMicrosoftUpdateServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Anomaly detection services should be registered
        Assert.NotNull(serviceProvider.GetService<ISyncService>());
        Assert.NotNull(serviceProvider.GetService<IQueryService>());
        Assert.NotNull(serviceProvider.GetService<IHealthService>());
        Assert.NotNull(serviceProvider.GetService<IAnomalyDetectionService>());
        Assert.NotNull(serviceProvider.GetService<IQueueService>());

        // Cleanup
        if (Directory.Exists(metadataPath))
        {
            Directory.Delete(metadataPath, true);
        }
    }

    [Fact]
    public void AddMicrosoftUpdateServices_DefaultPaths_AppliedCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(this.output));

        // No paths configured - should use defaults
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        services.AddMicrosoftUpdateServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - should use default path "./store"
        var metadataStore = serviceProvider.GetService<IMetadataStore>();
        Assert.NotNull(metadataStore);

        // Cleanup
        if (Directory.Exists("./store"))
        {
            Directory.Delete("./store", true);
        }
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