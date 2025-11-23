// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngineTest.Infrastructure;

using Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.Storage.Local;
using UpdateEngine.Core.Services;
using Xunit;

/// <summary>
/// In-memory test fixture that provides a fully configured service provider
/// without requiring Azure Storage, Azurite, or any external dependencies.
/// Perfect for fast unit and integration tests.
/// </summary>
public class InMemoryFunctionsFixture : IAsyncLifetime
{
    private ServiceProvider? serviceProvider;
    private string? tempStorePath;

    public IServiceProvider Services => this.serviceProvider ?? throw new InvalidOperationException("Fixture not initialized");

    public async Task InitializeAsync()
    {
        // Create a temporary directory for in-memory storage
        this.tempStorePath = Path.Combine(Path.GetTempPath(), $"msupdate-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(this.tempStorePath);

        // Configure services with in-memory/local storage
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning); // Reduce noise in tests
        });

        // Add in-memory metadata store (uses temporary local directory)
        services.AddSingleton<IMetadataStore>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<InMemoryFunctionsFixture>>();
            logger.LogInformation("Creating in-memory metadata store at: {Path}", this.tempStorePath);
  
            // OpenOrCreate will initialize a new store if it doesn't exist
            var store = PackageStore.OpenOrCreate(this.tempStorePath);
            return store;
        });

        // Content store is optional - register as nullable
        services.AddSingleton<IContentStore>(sp => null!);

        // Create in-memory configuration with AppConfig settings  
        var configDictionary = new Dictionary<string, string>()
        {
            ["MetadataStorePath"] = this.tempStorePath,
            ["ContentStorePath"] = this.tempStorePath,
            ["ServiceUrl"] = "http://localhost:7071",
            ["HealthCheckIntervalMinutes"] = "5",
            ["SyncIntervalMinutes"] = "60",
            ["EmergencySyncIntervalMinutes"] = "15",
            ["MaintenanceIntervalHours"] = "6",
            ["DeepHealthCheckIntervalHours"] = "24",
            ["AnomalyDetectionCooldownMinutes"] = "30",
            ["ContentSyncBatchSize"] = "50",
            ["EnableScheduledSync"] = "true",
            ["EnableEmergencySync"] = "true",
            ["EnableMaintenance"] = "true",
            ["EnableDeepHealthCheck"] = "true",
            ["EnableAnomalyDetection"] = "false"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDictionary!)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Bind AppConfig from configuration
        var appConfig = new AppConfig();
        configuration.Bind(appConfig);
        services.AddSingleton(appConfig);

        // Add application services
        services.AddScoped<ISyncService, SyncService>();
        services.AddScoped<IQueryService, QueryService>();
        services.AddScoped<IHealthService, HealthService>();

        this.serviceProvider = services.BuildServiceProvider();

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (this.serviceProvider != null)
        {
            await this.serviceProvider.DisposeAsync();
        }

        // Clean up temporary storage
        if (!string.IsNullOrEmpty(this.tempStorePath) && Directory.Exists(this.tempStorePath))
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
    }

    /// <summary>
    /// Gets a service from the DI container
    /// </summary>
    public T GetService<T>() where T : notnull
    {
        return this.Services.GetRequiredService<T>();
    }

    /// <summary>
    /// Creates a new service scope for isolated test execution
    /// </summary>
    public IServiceScope CreateScope()
    {
        return this.Services.CreateScope();
    }

    /// <summary>
    /// Gets the metadata store for direct manipulation in tests
    /// </summary>
    public IMetadataStore GetMetadataStore()
    {
        return this.GetService<IMetadataStore>();
    }

    /// <summary>
    /// Creates an HTTP client (note: for in-memory tests, you'll need to mock HTTP calls)
    /// </summary>
    public HttpClient CreateClient()
    {
        // For true in-memory tests, you'd use WebApplicationFactory<T>
        // or mock the HTTP layer. This is a placeholder.
        return new HttpClient
        {
            BaseAddress = new Uri("http://localhost:7071/api/")
        };
    }
}

/// <summary>
/// Collection definition for in-memory integration tests
/// </summary>
[CollectionDefinition("InMemory")]
public class InMemoryCollection : ICollectionFixture<InMemoryFunctionsFixture>
{
}
