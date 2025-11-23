// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AppHost;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
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
    /// Works for any project type (Azure Functions, Worker Service, etc.).
    /// </summary>
    /// <param name="project">The project resource builder to configure.</param>
    /// <param name="configuration">The application configuration.</param>
    public static void ConfigureUpdateEngine(
        IResourceBuilder<ProjectResource> project,
        IConfiguration configuration)
    {
        // Bind simplified configuration
        var appConfig = new Configuration.AppConfig();
        configuration.Bind(appConfig);

        // Set storage environment variables from StorageConfiguration
        project
            .WithEnvironment("UpdateEngine__ServiceConfiguration__MaxUpdateCount", appConfig.ServiceConfiguration.MaxUpdateCount.ToString())
            .WithEnvironment("UpdateEngine__StorageConfiguration__UseAzureStorageForMetadata", appConfig.StorageConfiguration.UseAzureStorageForMetadata.ToString())
            .WithEnvironment("UpdateEngine__StorageConfiguration__UseAzureStorageForContent", appConfig.StorageConfiguration.UseAzureStorageForContent.ToString())
            .WithEnvironment("UpdateEngine__StorageConfiguration__MetadataContainerName", appConfig.StorageConfiguration.MetadataContainerName)
            .WithEnvironment("UpdateEngine__StorageConfiguration__ContentContainerName", appConfig.StorageConfiguration.ContentContainerName)
            .WithEnvironment("UpdateEngine__StorageConfiguration__ContentPathPrefix", appConfig.StorageConfiguration.ContentPathPrefix ?? "Content")
            .WithEnvironment("UpdateEngine__StorageConfiguration__MetadataPath", appConfig.StorageConfiguration.MetadataPath)
            .WithEnvironment("UpdateEngine__StorageConfiguration__ContentPath", appConfig.StorageConfiguration.ContentPath)
            .WithEnvironment("UpdateEngine__StorageConfiguration__ReindexOnStartup", appConfig.StorageConfiguration.ReindexOnStartup.ToString())
            .WithEnvironment("UpdateEngine__SyncConfiguration__EnableScheduledSync", appConfig.SyncConfiguration.EnableScheduledSync.ToString())
            .WithEnvironment("UpdateEngine__FeatureFlags__EnableDetailedLogging", appConfig.FeatureFlags.EnableDetailedLogging.ToString())
            .WithEnvironment("UpdateEngine__FeatureFlags__EnableMetrics", appConfig.FeatureFlags.EnableMetrics.ToString())
            .WithEnvironment("UpdateEngine__FeatureFlags__EnableCaching", appConfig.FeatureFlags.EnableCaching.ToString());

        // Note: Azure Storage connection string is automatically injected by Aspire via .WithReference(data, "MetadataStorageConnection")
        // It will be available at runtime as ConnectionStrings:MetadataStorageConnection
        // ServiceCollectionExtensions reads it from there

        // Set cache configuration from CacheConfiguration
        project
            .WithEnvironment("UpdateEngine__CacheConfiguration__EnableDistributedCache", appConfig.CacheConfiguration.EnableDistributedCache.ToString())
            .WithEnvironment("UpdateEngine__CacheConfiguration__KeyPrefix", appConfig.CacheConfiguration.KeyPrefix)
            .WithEnvironment("UpdateEngine__CacheConfiguration__DefaultExpirationMinutes", appConfig.CacheConfiguration.DefaultExpirationMinutes.ToString())
            .WithEnvironment("UpdateEngine__CacheConfiguration__StatisticsCacheMinutes", appConfig.CacheConfiguration.StatisticsCacheMinutes.ToString())
            .WithEnvironment("UpdateEngine__CacheConfiguration__UpdateDetailsCacheMinutes", appConfig.CacheConfiguration.UpdateDetailsCacheMinutes.ToString())
            .WithEnvironment("UpdateEngine__CacheConfiguration__ContentAvailabilityCacheMinutes", appConfig.CacheConfiguration.ContentAvailabilityCacheMinutes.ToString())
            .WithEnvironment("UpdateEngine__CacheConfiguration__InvalidateOnSync", appConfig.CacheConfiguration.InvalidateOnSync.ToString());

        // Set service configuration
        project
            .WithEnvironment("UpdateEngine__ServiceConfiguration__ServiceUrl", appConfig.ServiceConfiguration.ServiceUrl)
            .WithEnvironment("UpdateEngine__ServiceConfiguration__ContentUrl", appConfig.ServiceConfiguration.ContentUrl);

        // Set sync intervals for Worker Service background workers
        project
            .WithEnvironment("UpdateEngine__SyncConfiguration__SyncIntervalMinutes", appConfig.SyncConfiguration.SyncIntervalMinutes.ToString())
            .WithEnvironment("UpdateEngine__SyncConfiguration__HealthCheckIntervalMinutes", appConfig.SyncConfiguration.HealthCheckIntervalMinutes.ToString());
    }

    /// <summary>
    /// Maps configuration sections directly to environment variables without complex transformations.
    /// Azure Functions specific version that includes timer trigger schedules.
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

        // Set storage environment variables from StorageConfiguration
        functions
            .WithEnvironment("MaxUpdateCount", appConfig.ServiceConfiguration.MaxUpdateCount.ToString())
            .WithEnvironment("UseAzureStorageForMetadata", appConfig.StorageConfiguration.UseAzureStorageForMetadata.ToString())
            .WithEnvironment("UseAzureStorageForContent", appConfig.StorageConfiguration.UseAzureStorageForContent.ToString())
            .WithEnvironment("MetadataContainerName", appConfig.StorageConfiguration.MetadataContainerName)
            .WithEnvironment("ContentContainerName", appConfig.StorageConfiguration.ContentContainerName)
            .WithEnvironment("ContentPathPrefix", appConfig.StorageConfiguration.ContentPathPrefix ?? "Content")
            .WithEnvironment("MetadataPath", appConfig.StorageConfiguration.MetadataPath)
            .WithEnvironment("ContentPath", appConfig.StorageConfiguration.ContentPath)
            .WithEnvironment("ReindexOnStartup", appConfig.StorageConfiguration.ReindexOnStartup.ToString())
            .WithEnvironment("EnableScheduledSync", appConfig.SyncConfiguration.EnableScheduledSync.ToString())
            .WithEnvironment("EnableDetailedLogging", appConfig.FeatureFlags.EnableDetailedLogging.ToString())
            .WithEnvironment("EnableMetrics", appConfig.FeatureFlags.EnableMetrics.ToString())
            .WithEnvironment("EnableCaching", appConfig.FeatureFlags.EnableCaching.ToString());

        // Set cache configuration from CacheConfiguration
        functions
            .WithEnvironment("EnableDistributedCache", appConfig.CacheConfiguration.EnableDistributedCache.ToString())
            .WithEnvironment("KeyPrefix", appConfig.CacheConfiguration.KeyPrefix)
            .WithEnvironment("DefaultExpirationMinutes", appConfig.CacheConfiguration.DefaultExpirationMinutes.ToString())
            .WithEnvironment("StatisticsCacheMinutes", appConfig.CacheConfiguration.StatisticsCacheMinutes.ToString())
            .WithEnvironment("UpdateDetailsCacheMinutes", appConfig.CacheConfiguration.UpdateDetailsCacheMinutes.ToString())
            .WithEnvironment("ContentAvailabilityCacheMinutes", appConfig.CacheConfiguration.ContentAvailabilityCacheMinutes.ToString())
            .WithEnvironment("InvalidateOnSync", appConfig.CacheConfiguration.InvalidateOnSync.ToString());

        // Set service configuration as JSON from ServiceConfiguration
        // Note: ServiceUrl and ContentUrl will be dynamically resolved by Aspire at runtime
        // For local development, these will use the dynamically assigned ports
        var serviceConfig = new
        {
            ServiceUrl = appConfig.ServiceConfiguration.ServiceUrl, // Fallback for non-Aspire scenarios
            ContentUrl = appConfig.ServiceConfiguration.ContentUrl,   // Fallback for non-Aspire scenarios  
            MaxUpdateCount = appConfig.ServiceConfiguration.MaxUpdateCount
        };

        functions.WithEnvironment("ServiceConfigurationJson", JsonSerializer.Serialize(serviceConfig));

        // Set function schedules for timer triggers (with defaults) from SyncConfiguration
        functions
            .WithEnvironment("SyncCriticalSchedule", appConfig.SyncConfiguration.SyncCriticalSchedule)
            .WithEnvironment("SyncComprehensiveSchedule", appConfig.SyncConfiguration.SyncComprehensiveSchedule)
            .WithEnvironment("SyncContentSchedule", appConfig.SyncConfiguration.SyncContentSchedule)
            .WithEnvironment("ScheduledHealthCheckSchedule", appConfig.SyncConfiguration.ScheduledHealthCheckSchedule)
            .WithEnvironment("MaintenanceSchedule", appConfig.SyncConfiguration.MaintenanceSchedule)
            .WithEnvironment("AnomalyDetectionSchedule", appConfig.SyncConfiguration.AnomalyDetectionSchedule);

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