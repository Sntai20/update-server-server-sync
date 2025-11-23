// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Configuration;

using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Reflection;

/// <summary>
/// Extension methods for working with AppConfig instances.
/// </summary>
public static class AppConfigExtensions
{
    /// <summary>
    /// Binds configuration to an AppConfig instance.
    /// </summary>
    /// <param name="configuration">The configuration source</param>
    /// <returns>A bound AppConfig instance</returns>
    public static AppConfig BindToAppConfig(this IConfiguration configuration)
    {
        var appConfig = new AppConfig();
        configuration.GetSection(AppConfig.SectionName).Bind(appConfig);
        appConfig.Validate();
        return appConfig;
    }

    /// <summary>
    /// Gets the effective metadata storage type based on configuration.
    /// </summary>
    /// <param name="config">The AppConfig instance</param>
    /// <returns>The storage type description</returns>
    public static string GetMetadataStorageType(this AppConfig config)
    {
        return config.StorageConfiguration.UseAzureStorageForMetadata ? "Azure Blob Storage" : "Local File System";
    }

    /// <summary>
    /// Gets the effective content storage type based on configuration.
    /// </summary>
    /// <param name="config">The AppConfig instance</param>
    /// <returns>The storage type description</returns>
    public static string GetContentStorageType(this AppConfig config)
    {
        return config.StorageConfiguration.UseAzureStorageForContent ? "Azure Blob Storage" : "Local File System";
    }

    /// <summary>
    /// Creates a summary of the current configuration for logging/debugging.
    /// </summary>
    /// <param name="config">The AppConfig instance</param>
    /// <returns>A formatted configuration summary</returns>
    public static string GetConfigurationSummary(this AppConfig config)
    {
        return $@"Microsoft Update Server Configuration:
  Service URL: {config.ServiceConfiguration.ServiceUrl}
  Content URL: {config.ServiceConfiguration.ContentUrl}
  Max Updates: {config.ServiceConfiguration.MaxUpdateCount}
  Categories: {string.Join(", ", config.ServiceConfiguration.SupportedCategories)}
  Languages: {string.Join(", ", config.ServiceConfiguration.SupportedLanguages)}
  
  Storage:
    Metadata: {config.GetMetadataStorageType()} ({config.StorageConfiguration.MetadataPath})
    Content: {config.GetContentStorageType()} ({config.StorageConfiguration.ContentPath})
    Metadata Container: {config.StorageConfiguration.MetadataContainerName}
    Content Container: {config.StorageConfiguration.ContentContainerName}
    Reindex on Startup: {config.StorageConfiguration.ReindexOnStartup}
  
  Schedules:
    Critical Sync: {config.SyncConfiguration.SyncCriticalSchedule}
    Comprehensive Sync: {config.SyncConfiguration.SyncComprehensiveSchedule}
    Content Sync: {config.SyncConfiguration.SyncContentSchedule}
    Health Check: {config.SyncConfiguration.ScheduledHealthCheckSchedule}
    Maintenance: {config.SyncConfiguration.MaintenanceSchedule}
    Anomaly Detection: {config.SyncConfiguration.AnomalyDetectionSchedule}
  
  Features:
    Emergency Sync: {config.FeatureFlags.EnableEmergencySync}
    Comprehensive Sync: {config.FeatureFlags.EnableComprehensiveSync}
    Content Sync: {config.FeatureFlags.EnableContentSync}
    Detailed Logging: {config.FeatureFlags.EnableDetailedLogging}
    Metrics: {config.FeatureFlags.EnableMetrics}
    Caching: {config.FeatureFlags.EnableCaching}";
    }
}

/// <summary>
/// Extension methods for IConfigurationBuilder to add shared configuration files.
/// </summary>
public static class SharedConfigurationExtensions
{
    /// <summary>
    /// Adds shared configuration files from the Configuration project to the configuration builder.
    /// Loads: defaults -> shared overrides -> environment-specific overrides.
    /// </summary>
    /// <param name="builder">The configuration builder</param>
    /// <returns>The configuration builder for chaining</returns>
    public static IConfigurationBuilder AddSharedAppConfiguration(this IConfigurationBuilder builder)
    {
        // Get the Configuration assembly location
        var configAssembly = typeof(AppConfig).Assembly;
        var baseDirectory = Path.GetDirectoryName(configAssembly.Location)
            ?? throw new InvalidOperationException("Could not determine assembly directory");

        var sharedPath = Path.Combine(baseDirectory, "shared");

        // Load configuration files in order: defaults -> shared overrides -> environment overrides  
        var defaultsPath = Path.Combine(sharedPath, "appsettings.defaults.json");
        if (File.Exists(defaultsPath))
        {
            builder.AddJsonFile(defaultsPath, optional: false, reloadOnChange: true);
        }

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var environmentFile = Path.Combine(sharedPath, $"appsettings.{environment}.json");
        if (File.Exists(environmentFile))
        {
            builder.AddJsonFile(environmentFile, optional: true, reloadOnChange: true);
        }

        return builder;
    }
}