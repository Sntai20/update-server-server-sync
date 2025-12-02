// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AppHost;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using UpdateEngine.Configuration;

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
        // Bind simplified configuration from UpdateEngine section
        var appConfig = new AppConfig();
        configuration.GetSection(AppConfig.SectionName).Bind(appConfig);

        // Set storage environment variables from StorageConfiguration
        project
            .WithEnvironment("UpdateEngine__ServiceConfiguration__MaxUpdateCount", appConfig.ServiceConfiguration.MaxUpdateCount.ToString())
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
        // Bind simplified configuration from UpdateEngine section
        var appConfig = new AppConfig();
        configuration.GetSection(AppConfig.SectionName).Bind(appConfig);

        // Set storage environment variables from StorageConfiguration
        // IMPORTANT: Do NOT set UseAzureStorageForMetadata/UseAzureStorageForContent as environment variables
        // They should come from shared appsettings.Development.json (already set to true)
        // Setting them here would override the JSON configuration
        functions
            .WithEnvironment("UpdateEngine__ServiceConfiguration__MaxUpdateCount", appConfig.ServiceConfiguration.MaxUpdateCount.ToString())
            .WithEnvironment("UpdateEngine__StorageConfiguration__MetadataContainerName", appConfig.StorageConfiguration.MetadataContainerName)
            .WithEnvironment("UpdateEngine__StorageConfiguration__ContentContainerName", appConfig.StorageConfiguration.ContentContainerName)
            .WithEnvironment("UpdateEngine__StorageConfiguration__ContentPathPrefix", appConfig.StorageConfiguration.ContentPathPrefix ?? "Content")
            .WithEnvironment("UpdateEngine__StorageConfiguration__ReindexOnStartup", appConfig.StorageConfiguration.ReindexOnStartup.ToString())
            .WithEnvironment("UpdateEngine__SyncConfiguration__EnableScheduledSync", appConfig.SyncConfiguration.EnableScheduledSync.ToString())
            .WithEnvironment("UpdateEngine__FeatureFlags__EnableDetailedLogging", appConfig.FeatureFlags.EnableDetailedLogging.ToString())
            .WithEnvironment("UpdateEngine__FeatureFlags__EnableMetrics", appConfig.FeatureFlags.EnableMetrics.ToString())
            .WithEnvironment("UpdateEngine__FeatureFlags__EnableCaching", appConfig.FeatureFlags.EnableCaching.ToString())
            .WithEnvironment("Features__EnableAnomalyDetection", appConfig.FeatureFlags.EnableAnomalyDetection.ToString());

        // IMPORTANT: Only set local paths when NOT using Azure Storage
        // When using Azurite, Aspire connection strings (ConnectionStrings__MetadataStorageConnection) take precedence
        if (!appConfig.StorageConfiguration.UseAzureStorageForMetadata)
        {
            functions.WithEnvironment("UpdateEngine__StorageConfiguration__MetadataPath", appConfig.StorageConfiguration.MetadataPath);
        }
        
        if (!appConfig.StorageConfiguration.UseAzureStorageForContent)
        {
            functions.WithEnvironment("UpdateEngine__StorageConfiguration__ContentPath", appConfig.StorageConfiguration.ContentPath);
        }

        // Set cache configuration from CacheConfiguration
        functions
            .WithEnvironment("UpdateEngine__CacheConfiguration__EnableDistributedCache", appConfig.CacheConfiguration.EnableDistributedCache.ToString())
            .WithEnvironment("UpdateEngine__CacheConfiguration__KeyPrefix", appConfig.CacheConfiguration.KeyPrefix)
            .WithEnvironment("UpdateEngine__CacheConfiguration__DefaultExpirationMinutes", appConfig.CacheConfiguration.DefaultExpirationMinutes.ToString())
            .WithEnvironment("UpdateEngine__CacheConfiguration__StatisticsCacheMinutes", appConfig.CacheConfiguration.StatisticsCacheMinutes.ToString())
            .WithEnvironment("UpdateEngine__CacheConfiguration__UpdateDetailsCacheMinutes", appConfig.CacheConfiguration.UpdateDetailsCacheMinutes.ToString())
            .WithEnvironment("UpdateEngine__CacheConfiguration__ContentAvailabilityCacheMinutes", appConfig.CacheConfiguration.ContentAvailabilityCacheMinutes.ToString())
            .WithEnvironment("UpdateEngine__CacheConfiguration__InvalidateOnSync", appConfig.CacheConfiguration.InvalidateOnSync.ToString());

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
        // IMPORTANT: Timer triggers need BOTH:
        // 1. Hierarchical names for configuration binding (UpdateEngine__SyncConfiguration__*)
        // 2. Flat names for TimerTrigger attribute resolution (%ScheduleName%)
        functions
            .WithEnvironment("UpdateEngine__SyncConfiguration__SyncCriticalSchedule", appConfig.SyncConfiguration.SyncCriticalSchedule)
            .WithEnvironment("UpdateEngine__SyncConfiguration__SyncComprehensiveSchedule", appConfig.SyncConfiguration.SyncComprehensiveSchedule)
            .WithEnvironment("UpdateEngine__SyncConfiguration__SyncContentSchedule", appConfig.SyncConfiguration.SyncContentSchedule)
            .WithEnvironment("UpdateEngine__SyncConfiguration__ScheduledHealthCheckSchedule", appConfig.SyncConfiguration.ScheduledHealthCheckSchedule)
            .WithEnvironment("UpdateEngine__SyncConfiguration__MaintenanceSchedule", appConfig.SyncConfiguration.MaintenanceSchedule)
            .WithEnvironment("UpdateEngine__SyncConfiguration__AnomalyDetectionSchedule", appConfig.SyncConfiguration.AnomalyDetectionSchedule)
            // Flat names for TimerTrigger attributes
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
            functions.WithEnvironment($"AzureWebJobs__{job.Key}__Disabled", disabledValue.ToString().ToLower());
        }
        
        // NOTE: Connection strings are automatically injected by Aspire via .WithReference()
        // They will be available as:
        // - ConnectionStrings__MetadataStorageConnection (from .WithReference(data, "MetadataStorageConnection"))
        // - ConnectionStrings__ContentStorageConnection (from .WithReference(data, "ContentStorageConnection"))
        // - ConnectionStrings__Redis (from .WithReference(redis))
        // Azure Functions configuration system will read these automatically
        //
        // Storage configuration (UseAzureStorageForMetadata/UseAzureStorageForContent) comes from
        // shared/appsettings.Development.json and should NOT be overridden here
    }
}