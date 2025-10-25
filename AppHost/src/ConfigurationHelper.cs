namespace AppHost;

using Microsoft.Extensions.Configuration;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides helper methods for configuring the Microsoft Update Server-Server Sync application.
/// </summary>
public static class ConfigurationHelper
{
    /// <summary>
    /// Builds a <see cref="ServiceConfiguration"/> from application configuration settings.
    /// </summary>
    /// <param name="configuration">The application configuration containing service settings.</param>
    /// <returns>A configured <see cref="ServiceConfiguration"/> instance with all required settings.</returns>
    /// <remarks>
    /// Reads configuration from the following sections:
    /// <list type="bullet">
    /// <item><description>Storage: Metadata and content storage paths, Azure storage settings</description></item>
    /// <item><description>Service: Service URLs, update limits, and supported categories</description></item>
    /// <item><description>Sync: Synchronization intervals for updates and maintenance</description></item>
    /// <item><description>Features: Feature flag settings for optional capabilities</description></item>
    /// </list>
    /// </remarks>
    public static ServiceConfiguration BuildServiceConfiguration(IConfiguration configuration)
    {
        var storageConfig = configuration.GetSection("Storage");
        var serviceConfig = configuration.GetSection("Service");
        var syncConfig = configuration.GetSection("Sync");
        var featuresConfig = configuration.GetSection("Features");

        var useAzureStorage = storageConfig.GetValue<bool>("UseAzureStorage");
        var metadataStorePath = storageConfig["MetadataStorePath"] ?? "./store";
        var contentStorePath = storageConfig["ContentStorePath"] ?? "./content";
        var serviceUrl = serviceConfig["ServiceUrl"] ?? "http://localhost:7071";

        return new ServiceConfiguration
        {
            ServiceUrl = serviceUrl,
            ContentUrl = $"{serviceUrl}/api/content",
            MaxUpdateCount = serviceConfig.GetValue<int>("MaxUpdateCount", 1000),
            SupportedCategories = serviceConfig.GetSection("SupportedCategories").Get<string[]>()
                ?? new[] { "Security Updates", "Critical Updates", "Feature Packs", "Updates", "Drivers" },

            SyncConfiguration = new SyncConfiguration
            {
                CriticalUpdatesIntervalHours = syncConfig.GetValue<int>("CriticalUpdatesIntervalHours", 4),
                ComprehensiveUpdatesIntervalHours = syncConfig.GetValue<int>("ComprehensiveUpdatesIntervalHours", 24),
                ContentSyncIntervalHours = syncConfig.GetValue<int>("ContentSyncIntervalHours", 168),
                MaintenanceIntervalHours = syncConfig.GetValue<int>("MaintenanceIntervalHours", 168),
                HealthCheckIntervalMinutes = syncConfig.GetValue<int>("HealthCheckIntervalMinutes", 60)
            },

            StorageConfiguration = new StorageConfiguration
            {
                MetadataStorePath = metadataStorePath,
                ContentStorePath = contentStorePath,
                EnableContentStorage = !string.IsNullOrEmpty(contentStorePath),
                ReindexOnStartup = featuresConfig.GetValue<bool>("ReindexOnStartup", false),
                UseAzureStorage = useAzureStorage,
                MetadataContainerName = storageConfig["MetadataContainerName"] ?? "metadata",
                ContentContainerName = storageConfig["ContentContainerName"] ?? "content"
            },

            FeatureFlags = new FeatureFlags
            {
                UseAzureStorage = useAzureStorage,
                EnableScheduledSync = featuresConfig.GetValue<bool>("EnableScheduledSync", true),
                EnableContentSync = featuresConfig.GetValue<bool>("EnableContentSync", true),
                EnableHealthMonitoring = featuresConfig.GetValue<bool>("EnableHealthMonitoring", true),
                EnableMetadataExport = featuresConfig.GetValue<bool>("EnableMetadataExport", true),
                EnableDriverMatching = featuresConfig.GetValue<bool>("EnableDriverMatching", true)
            }
        };
    }

    /// <summary>
    /// Configures environment variables for the Azure Functions project resource.
    /// </summary>
    /// <param name="functions">The Azure Functions project resource builder to configure.</param>
    /// <param name="serviceConfiguration">The service configuration containing runtime settings.</param>
    /// <param name="storageConfig">The storage configuration section (currently unused but kept for compatibility).</param>
    /// <remarks>
    /// Sets up environment variables for:
    /// <list type="bullet">
    /// <item><description>Storage connection strings (when using Azure Storage)</description></item>
    /// <item><description>Storage paths and container names</description></item>
    /// <item><description>Service URLs and configuration JSON</description></item>
    /// </list>
    /// </remarks>
    public static void ConfigureUpdateFunctions(
        IResourceBuilder<AzureFunctionsProjectResource> functions,
        ServiceConfiguration serviceConfiguration,
        IConfiguration storageConfig)
    {
        var storageConf = serviceConfiguration.StorageConfiguration;

        if (storageConf.UseAzureStorage)
        {
            var connectionString = AzuriteDefaults.GetConnectionString();
            functions
                .WithEnvironment("ConnectionStrings__MetadataStorageConnection", connectionString)
                .WithEnvironment("ConnectionStrings__ContentStorageConnection", connectionString);
        }

        functions
            .WithEnvironment("UseAzureStorage", storageConf.UseAzureStorage.ToString())
            .WithEnvironment("MetadataContainerName", storageConf.MetadataContainerName)
            .WithEnvironment("ContentContainerName", storageConf.ContentContainerName)
            .WithEnvironment("MetadataStorePath", storageConf.MetadataStorePath)
            .WithEnvironment("ContentStorePath", storageConf.ContentStorePath)
            .WithEnvironment("ContentHttpRoot", serviceConfiguration.ContentUrl)
            .WithEnvironment("ServiceConfigurationJson", System.Text.Json.JsonSerializer.Serialize(serviceConfiguration));
    }

    /// <summary>
    /// Validates storage configuration and sets up storage resources for the application.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="serviceConfiguration">The service configuration containing storage settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown when MetadataStorePath is not configured.</exception>
    /// <remarks>
    /// <para>Ensures that required storage paths are configured and accessible.</para>
    /// <para>When using local storage in development mode:</para>
    /// <list type="bullet">
    /// <item><description>Creates local storage directories if they don't exist</description></item>
    /// <item><description>Registers health checks to monitor metadata store availability</description></item>
    /// </list>
    /// </remarks>
    public static void ValidateAndSetupStorage(
        IDistributedApplicationBuilder builder,
        ServiceConfiguration serviceConfiguration)
    {
        var storageConf = serviceConfiguration.StorageConfiguration;

        if (string.IsNullOrEmpty(storageConf.MetadataStorePath))
        {
            throw new InvalidOperationException("MetadataStorePath must be configured");
        }

        if (!storageConf.UseAzureStorage && builder.Environment.IsDevelopment())
        {
            StorageSetupHelper.EnsureLocalDirectories(storageConf.MetadataStorePath, storageConf.ContentStorePath);

            builder.Services.AddHealthChecks()
                .AddCheck("metadata-store", () =>
                    StorageSetupHelper.ValidateLocalStorage(storageConf.MetadataStorePath)
                        ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Metadata store directory exists")
                        : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Metadata store path not found"));
        }
    }
}