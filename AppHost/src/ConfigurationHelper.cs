namespace AppHost;

using Microsoft.Extensions.Configuration;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Configuration;

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
        return configuration.BuildServiceConfiguration();
    }

    /// <summary>
    /// Configures environment variables for the Azure Functions project resource.
    /// </summary>
    /// <param name="functions">The Azure Functions project resource builder to configure.</param>
    /// <param name="serviceConfiguration">The service configuration containing runtime settings.</param>
    /// <param name="storageConfig">The storage configuration section (currently unused but kept for compatibility).</param>
    /// <param name="azureWebJobsConfig">The Azure WebJobs configuration section.</param>
    /// <remarks>
    /// Sets up environment variables for:
    /// <list type="bullet">
    /// <item><description>Storage connection strings (when using Azure Storage)</description></item>
    /// <item><description>Storage paths and container names</description></item>
    /// <item><description>Service URLs and configuration JSON</description></item>
    /// </list>
    /// Storage directories and containers are created automatically during Functions startup via DI.
    /// </remarks>
    public static void ConfigureUpdateFunctions(
        IResourceBuilder<AzureFunctionsProjectResource> functions,
        ServiceConfiguration serviceConfiguration,
        IConfigurationSection storageConfig,
        IConfigurationSection azureWebJobsConfig)
    {
        var storageConf = serviceConfiguration.StorageConfiguration;

        functions
            .WithEnvironment("UseAzureStorageForMetadata", storageConf.UseAzureStorageForMetadata.ToString())
            .WithEnvironment("UseAzureStorageForContent", storageConf.UseAzureStorageForContent.ToString())
            .WithEnvironment("MetadataContainerName", storageConf.MetadataContainerName)
            .WithEnvironment("ContentContainerName", storageConf.ContentContainerName)
            .WithEnvironment("MetadataStorePath", storageConf.MetadataStorePath)
            .WithEnvironment("ContentStorePath", storageConf.ContentStorePath)
            .WithEnvironment("ContentHttpRoot", serviceConfiguration.ContentUrl)
            .WithEnvironment("ServiceConfigurationJson", System.Text.Json.JsonSerializer.Serialize(serviceConfiguration));

        // In production, set explicit connection strings if provided in configuration
        var metadataConnectionString = storageConfig.GetConnectionString("MetadataStorageConnection");
        var contentConnectionString = storageConfig.GetConnectionString("ContentStorageConnection");

        if (!string.IsNullOrEmpty(metadataConnectionString))
        {
            functions.WithEnvironment("ConnectionStrings__MetadataStorageConnection", metadataConnectionString);
        }

        if (!string.IsNullOrEmpty(contentConnectionString))
        {
            functions.WithEnvironment("ConnectionStrings__ContentStorageConnection", contentConnectionString);
        }

        // Pass function schedules as environment variables for timer triggers
        var schedules = serviceConfiguration.FunctionSchedules;
        functions
            .WithEnvironment("HourlyHealthCheckSchedule", schedules.HourlyHealthCheckSchedule)
            .WithEnvironment("DailyCriticalSyncSchedule", schedules.DailyCriticalSyncSchedule)
            .WithEnvironment("WeeklyComprehensiveSyncSchedule", schedules.WeeklyComprehensiveSyncSchedule)
            .WithEnvironment("MonthlyMaintenanceSchedule", schedules.MonthlyMaintenanceSchedule)
            .WithEnvironment("ScheduledHealthCheckSchedule", schedules.ScheduledHealthCheckSchedule)
            .WithEnvironment("WeeklyMaintenanceSchedule", schedules.WeeklyMaintenanceSchedule)
            .WithEnvironment("SyncMetadataComprehensiveSchedule", schedules.SyncMetadataComprehensiveSchedule)
            .WithEnvironment("SyncMetadataCriticalSchedule", schedules.SyncMetadataCriticalSchedule)
            .WithEnvironment("SyncContentSchedule", schedules.SyncContentSchedule)

            // Map to the expected timer trigger parameter names
            .WithEnvironment("SyncComprehensiveSchedule", schedules.SyncMetadataComprehensiveSchedule)
            .WithEnvironment("SyncCriticalSchedule", schedules.SyncMetadataCriticalSchedule)
            .WithEnvironment("AnomalyDetectionSchedule", schedules.AnomalyDetectionSchedule);

        // Pass Azure Functions disable configuration
        foreach (var job in azureWebJobsConfig.GetChildren())
        {
            var disabledValue = job.GetValue<bool>("Disabled");
            functions.WithEnvironment($"AzureWebJobs.{job.Key}.Disabled", disabledValue.ToString().ToLower());
        }
    }
}