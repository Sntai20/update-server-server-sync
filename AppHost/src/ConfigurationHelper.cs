// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AppHost;

using Aspire.Hosting;
using Aspire.Hosting.Azure;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

/// <summary>
/// Simplified configuration helper for mapping AppConfig to Azure Functions environment variables.
/// </summary>
public static class ConfigurationHelper
{
    /// <summary>
    /// Maps configuration sections directly to environment variables without complex transformations.
    /// </summary>
    /// <param name="functions">The Azure Functions project resource builder to configure.</param>
    /// <param name="configuration">The application configuration.</param>
    public static void ConfigureUpdateFunctions(
        IResourceBuilder<AzureFunctionsProjectResource> functions,
        IConfiguration configuration)
    {
        // Bind simplified configuration
        var appConfig = new Configuration.AppConfig();
        configuration.Bind(appConfig);

        // Set storage environment variables
        functions
            .WithEnvironment("MaxUpdateCount", appConfig.MaxUpdateCount.ToString())
            .WithEnvironment("UseAzureStorageForMetadata", appConfig.UseAzureStorageForMetadata.ToString())
            .WithEnvironment("UseAzureStorageForContent", appConfig.UseAzureStorageForContent.ToString())
            .WithEnvironment("MetadataContainerName", appConfig.MetadataContainerName)
            .WithEnvironment("ContentContainerName", appConfig.ContentContainerName)
            .WithEnvironment("ContentPathPrefix", appConfig.ContentPathPrefix ?? "Content")
            .WithEnvironment("MetadataPath", appConfig.MetadataPath)
            .WithEnvironment("ContentPath", appConfig.ContentPath)
            .WithEnvironment("ReindexOnStartup", appConfig.ReindexOnStartup.ToString())
            .WithEnvironment("EnableScheduledSync", appConfig.EnableScheduledSync.ToString())
            .WithEnvironment("EnableDetailedLogging", appConfig.EnableDetailedLogging.ToString())
            .WithEnvironment("EnableMetrics", appConfig.EnableMetrics.ToString())
            .WithEnvironment("EnableCaching", appConfig.EnableCaching.ToString());

        // Set service configuration as JSON 
        // Note: ServiceUrl and ContentUrl will be dynamically resolved by Aspire at runtime
        // For local development, these will use the dynamically assigned ports
        var serviceConfig = new
        {
            ServiceUrl = appConfig.ServiceUrl, // Fallback for non-Aspire scenarios
            ContentUrl = appConfig.ContentUrl,   // Fallback for non-Aspire scenarios  
            MaxUpdateCount = appConfig.MaxUpdateCount
        };

        functions.WithEnvironment("ServiceConfigurationJson", JsonSerializer.Serialize(serviceConfig));

        // Set function schedules for timer triggers (with defaults)
        functions
            .WithEnvironment("SyncCriticalSchedule", appConfig.SyncCriticalSchedule)
            .WithEnvironment("SyncComprehensiveSchedule", appConfig.SyncComprehensiveSchedule)
            .WithEnvironment("SyncContentSchedule", appConfig.SyncContentSchedule)
            .WithEnvironment("ScheduledHealthCheckSchedule", appConfig.ScheduledHealthCheckSchedule)
            .WithEnvironment("WeeklyMaintenanceSchedule", appConfig.WeeklyMaintenanceSchedule)
            .WithEnvironment("AnomalyDetectionSchedule", appConfig.AnomalyDetectionSchedule);

        // Pass Azure Functions disable configuration
        var azureWebJobsSection = configuration.GetSection("AzureWebJobs");
        foreach (var job in azureWebJobsSection.GetChildren())
        {
            var disabledValue = job.GetValue<bool>("Disabled");
            functions.WithEnvironment($"AzureWebJobs.{job.Key}.Disabled", disabledValue.ToString().ToLower());
        }
    }

    /// <summary>
    /// Sets a connection string environment variable if it exists in configuration.
    /// </summary>
    private static void SetConnectionStringIfExists(
        IResourceBuilder<AzureFunctionsProjectResource> functions,
        IConfiguration configuration,
        string connectionStringName)
    {
        var connectionString = configuration.GetConnectionString(connectionStringName);
        if (!string.IsNullOrEmpty(connectionString))
        {
            functions.WithEnvironment($"ConnectionStrings__{connectionStringName}", connectionString);
        }
    }
}