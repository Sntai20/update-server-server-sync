// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;

namespace Configuration;

/// <summary>
/// Extension methods for converting between immutable and mutable configuration types.
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Converts an immutable ServiceConfiguration record to a mutable ServiceConfigurationMutable class.
    /// </summary>
    /// <param name="config">The immutable service configuration.</param>
    /// <returns>A mutable service configuration with the same values.</returns>
    public static ServiceConfigurationMutable ToMutable(this ServiceConfiguration config)
    {
        return new ServiceConfigurationMutable
        {
            ServiceUrl = config.ServiceUrl,
            ContentUrl = config.ContentUrl,
            MaxUpdateCount = config.MaxUpdateCount,
            SupportedCategories = config.SupportedCategories.ToArray(), // Create a copy of the array
            SyncConfiguration = config.SyncConfiguration.ToMutable(config.FunctionSchedules),
            StorageConfiguration = config.StorageConfiguration.ToMutable(),
            FeatureFlags = config.FeatureFlags.ToMutable()
        };
    }

    /// <summary>
    /// Converts a mutable ServiceConfigurationMutable class to an immutable ServiceConfiguration record.
    /// </summary>
    /// <param name="config">The mutable service configuration.</param>
    /// <param name="functionSchedules">The function schedules to include in the immutable configuration.</param>
    /// <returns>An immutable service configuration with the same values.</returns>
    public static ServiceConfiguration ToImmutable(this ServiceConfigurationMutable config, FunctionSchedules functionSchedules)
    {
        return new ServiceConfiguration
        {
            ServiceUrl = config.ServiceUrl,
            ContentUrl = config.ContentUrl,
            MaxUpdateCount = config.MaxUpdateCount,
            SupportedCategories = config.SupportedCategories.ToArray(), // Create a copy of the array
            SupportedLanguages = config.SupportedLanguages.ToArray(), // Create a copy of the array
            SyncConfiguration = config.SyncConfiguration.ToImmutable(),
            StorageConfiguration = config.StorageConfiguration.ToImmutable(),
            FeatureFlags = config.FeatureFlags.ToImmutable(),
            FunctionSchedules = functionSchedules
        };
    }

    /// <summary>
    /// Converts an immutable SyncConfiguration record to a mutable SyncConfigMutable class.
    /// </summary>
    /// <param name="config">The immutable sync configuration.</param>
    /// <param name="functionSchedules">The function schedules to parse into TimeSpan values.</param>
    /// <returns>A mutable sync configuration with the same values and parsed TimeSpan schedules.</returns>
    public static SyncConfigMutable ToMutable(this SyncConfiguration config, FunctionSchedules functionSchedules)
    {
        return new SyncConfigMutable
        {
            CriticalUpdatesIntervalHours = config.CriticalUpdatesIntervalHours,
            ComprehensiveUpdatesIntervalHours = config.ComprehensiveUpdatesIntervalHours,
            ContentSyncIntervalHours = config.ContentSyncIntervalHours,
            MaintenanceIntervalHours = config.MaintenanceIntervalHours,
            HealthCheckIntervalMinutes = config.HealthCheckIntervalMinutes,
            
            // Parse TimeSpan schedules from string values
            HourlyHealthCheckSchedule = ParseTimeSpan(functionSchedules.HourlyHealthCheckSchedule, TimeSpan.FromHours(1)),
            DailyCriticalSyncSchedule = ParseTimeSpan(functionSchedules.DailyCriticalSyncSchedule, TimeSpan.FromHours(24)),
            WeeklyComprehensiveSyncSchedule = ParseTimeSpan(functionSchedules.WeeklyComprehensiveSyncSchedule, TimeSpan.FromDays(7)),
            MonthlyMaintenanceSchedule = ParseTimeSpan(functionSchedules.MonthlyMaintenanceSchedule, TimeSpan.FromDays(30)),
            ScheduledHealthCheckSchedule = ParseTimeSpan(functionSchedules.ScheduledHealthCheckSchedule, TimeSpan.FromHours(1)),
            WeeklyMaintenanceSchedule = ParseTimeSpan(functionSchedules.WeeklyMaintenanceSchedule, TimeSpan.FromDays(7)),
            SyncMetadataComprehensiveSchedule = ParseTimeSpan(functionSchedules.SyncMetadataComprehensiveSchedule, TimeSpan.FromHours(24)),
            SyncMetadataCriticalSchedule = ParseTimeSpan(functionSchedules.SyncMetadataCriticalSchedule, TimeSpan.FromHours(4)),
            SyncContentSchedule = ParseTimeSpan(functionSchedules.SyncContentSchedule, TimeSpan.FromDays(7)),
            AnomalyDetectionIntervalMinutes = ParseTimeSpan(functionSchedules.AnomalyDetectionSchedule, TimeSpan.FromMinutes(30)).TotalMinutes
        };
    }

    /// <summary>
    /// Converts a mutable SyncConfigMutable class to an immutable SyncConfiguration record.
    /// </summary>
    /// <param name="config">The mutable sync configuration.</param>
    /// <returns>An immutable sync configuration with the same values.</returns>
    public static SyncConfiguration ToImmutable(this SyncConfigMutable config)
    {
        return new SyncConfiguration
        {
            CriticalUpdatesIntervalHours = config.CriticalUpdatesIntervalHours,
            ComprehensiveUpdatesIntervalHours = config.ComprehensiveUpdatesIntervalHours,
            ContentSyncIntervalHours = config.ContentSyncIntervalHours,
            MaintenanceIntervalHours = config.MaintenanceIntervalHours,
            HealthCheckIntervalMinutes = config.HealthCheckIntervalMinutes
        };
    }

    /// <summary>
    /// Converts an immutable StorageConfiguration record to a mutable StorageConfigMutable class.
    /// </summary>
    /// <param name="config">The immutable storage configuration.</param>
    /// <returns>A mutable storage configuration with the same values.</returns>
    public static StorageConfigMutable ToMutable(this StorageConfiguration config)
    {
        return new StorageConfigMutable
        {
            MetadataStorePath = config.MetadataStorePath,
            ContentStorePath = config.ContentStorePath,
            EnableContentStorage = config.EnableContentStorage,
            ReindexOnStartup = config.ReindexOnStartup
        };
    }

    /// <summary>
    /// Converts a mutable StorageConfigMutable class to an immutable StorageConfiguration record.
    /// </summary>
    /// <param name="config">The mutable storage configuration.</param>
    /// <param name="useAzureStorageForMetadata">Whether Azure Storage is being used.</param>
    /// <param name="useAzureStorageForContent">Whether Azure Storage is being used.</param>
    /// <param name="metadataContainerName">The metadata container name for Azure Storage.</param>
    /// <param name="contentContainerName">The content container name for Azure Storage.</param>
    /// <param name="contentPathPrefix">The content path prefix for Azure Storage blob paths.</param>
    /// <returns>An immutable storage configuration with the same values.</returns>
    public static StorageConfiguration ToImmutable(this StorageConfigMutable config, 
        bool useAzureStorageForMetadata = false,
        bool useAzureStorageForContent = false,
        string metadataContainerName = "metadata", 
        string contentContainerName = "content",
        string contentPathPrefix = "")
    {
        return new StorageConfiguration
        {
            MetadataStorePath = config.MetadataStorePath,
            ContentStorePath = config.ContentStorePath,
            EnableContentStorage = config.EnableContentStorage,
            ReindexOnStartup = config.ReindexOnStartup,
            UseAzureStorageForMetadata = useAzureStorageForMetadata,
            UseAzureStorageForContent = useAzureStorageForContent,
            MetadataContainerName = metadataContainerName,
            ContentContainerName = contentContainerName,
            ContentPathPrefix = contentPathPrefix
        };
    }

    /// <summary>
    /// Converts an immutable FeatureFlags record to a mutable FeatureConfigMutable class.
    /// </summary>
    /// <param name="config">The immutable feature flags.</param>
    /// <returns>A mutable feature configuration with the same values.</returns>
    public static FeatureConfigMutable ToMutable(this FeatureFlags config)
    {
        return new FeatureConfigMutable
        {
            EnableScheduledSync = config.EnableScheduledSync,
            EnableContentSync = config.EnableContentSync,
            EnableHealthMonitoring = config.EnableHealthMonitoring,
            EnableMetadataExport = config.EnableMetadataExport,
            EnableDriverMatching = config.EnableDriverMatching
        };
    }

    /// <summary>
    /// Converts a mutable FeatureConfigMutable class to an immutable FeatureFlags record.
    /// </summary>
    /// <param name="config">The mutable feature configuration.</param>
    /// <param name="useAzureStorageForMetadata">Whether Azure Storage is being used.</param>
    /// <param name="useAzureStorageForContent">Whether Azure Storage is being used.</param>
    /// <returns>An immutable feature flags with the same values.</returns>
    public static FeatureFlags ToImmutable(this FeatureConfigMutable config, bool useAzureStorageForMetadata = false, bool useAzureStorageForContent = false)
    {
        return new FeatureFlags
        {
            UseAzureStorageForMetadata = useAzureStorageForMetadata,
            UseAzureStorageForContent = useAzureStorageForContent,
            EnableScheduledSync = config.EnableScheduledSync,
            EnableContentSync = config.EnableContentSync,
            EnableHealthMonitoring = config.EnableHealthMonitoring,
            EnableMetadataExport = config.EnableMetadataExport,
            EnableDriverMatching = config.EnableDriverMatching
        };
    }

    /// <summary>
    /// Creates a FunctionSchedules record from TimeSpan values in a mutable sync configuration.
    /// </summary>
    /// <param name="config">The mutable sync configuration containing TimeSpan schedules.</param>
    /// <returns>A FunctionSchedules record with string representations of the TimeSpan values.</returns>
    public static FunctionSchedules ToFunctionSchedules(this SyncConfigMutable config)
    {
        return new FunctionSchedules
        {
            HourlyHealthCheckSchedule = config.HourlyHealthCheckSchedule.ToString(@"hh\:mm\:ss"),
            DailyCriticalSyncSchedule = config.DailyCriticalSyncSchedule.ToString(@"d\.hh\:mm\:ss"),
            WeeklyComprehensiveSyncSchedule = config.WeeklyComprehensiveSyncSchedule.ToString(@"d\.hh\:mm\:ss"),
            MonthlyMaintenanceSchedule = config.MonthlyMaintenanceSchedule.ToString(@"d\.hh\:mm\:ss"),
            ScheduledHealthCheckSchedule = config.ScheduledHealthCheckSchedule.ToString(@"hh\:mm\:ss"),
            WeeklyMaintenanceSchedule = config.WeeklyMaintenanceSchedule.ToString(@"d\.hh\:mm\:ss"),
            SyncMetadataComprehensiveSchedule = config.SyncMetadataComprehensiveSchedule.ToString(@"d\.hh\:mm\:ss"),
            SyncMetadataCriticalSchedule = config.SyncMetadataCriticalSchedule.ToString(@"hh\:mm\:ss"),
            SyncContentSchedule = config.SyncContentSchedule.ToString(@"d\.hh\:mm\:ss"),
            AnomalyDetectionSchedule = TimeSpan.FromMinutes(config.AnomalyDetectionIntervalMinutes).ToString(@"hh\:mm\:ss")
        };
    }

    /// <summary>
    /// Parses a TimeSpan from a string value with a fallback default value.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    /// <param name="defaultValue">The default value to use if parsing fails.</param>
    /// <returns>The parsed TimeSpan or the default value.</returns>
    private static TimeSpan ParseTimeSpan(string value, TimeSpan defaultValue)
    {
        if (TimeSpan.TryParse(value, out var result))
        {
            return result;
        }
        return defaultValue;
    }
}

/// <summary>
/// Extension methods for building configuration from IConfiguration sources.
/// </summary>
public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Builds a ServiceConfiguration from IConfiguration settings.
    /// </summary>
    /// <param name="configuration">The configuration source.</param>
    /// <returns>A fully populated ServiceConfiguration.</returns>
    public static ServiceConfiguration BuildServiceConfiguration(this IConfiguration configuration)
    {
        var storageConfig = configuration.GetSection("Storage");
        var serviceConfig = configuration.GetSection("Service");
        var syncConfig = configuration.GetSection("Sync");
        var featuresConfig = configuration.GetSection("Features");
        var functionSchedulesConfig = configuration.GetSection("FunctionSchedules");

        var useAzureStorageForMetadata = storageConfig.GetValue<bool>("UseAzureStorageForMetadata");
        var useAzureStorageForContent = storageConfig.GetValue<bool>("UseAzureStorageForContent");
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
            SupportedLanguages = serviceConfig.GetSection("SupportedLanguages").Get<string[]>()
                ?? new[] { "en", "en-US", "neutral", "" },

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
                UseAzureStorageForMetadata = useAzureStorageForMetadata,
                UseAzureStorageForContent = useAzureStorageForContent,
                MetadataContainerName = storageConfig["MetadataContainerName"] ?? "metadata",
                ContentContainerName = storageConfig["ContentContainerName"] ?? "content",
                ContentPathPrefix = storageConfig["ContentPathPrefix"] ?? ""
            },

            FeatureFlags = new FeatureFlags
            {
                UseAzureStorageForMetadata = useAzureStorageForMetadata,
                UseAzureStorageForContent = useAzureStorageForContent,
                EnableScheduledSync = featuresConfig.GetValue<bool>("EnableScheduledSync", true),
                EnableContentSync = featuresConfig.GetValue<bool>("EnableContentSync", true),
                EnableHealthMonitoring = featuresConfig.GetValue<bool>("EnableHealthMonitoring", true),
                EnableMetadataExport = featuresConfig.GetValue<bool>("EnableMetadataExport", true),
                EnableDriverMatching = featuresConfig.GetValue<bool>("EnableDriverMatching", true)
            },

            FunctionSchedules = new FunctionSchedules
            {
                HourlyHealthCheckSchedule = functionSchedulesConfig["HourlyHealthCheckSchedule"] ?? "01:00:00",
                DailyCriticalSyncSchedule = functionSchedulesConfig["DailyCriticalSyncSchedule"] ?? "1.00:00:00",
                WeeklyComprehensiveSyncSchedule = functionSchedulesConfig["WeeklyComprehensiveSyncSchedule"] ?? "7.00:00:00",
                MonthlyMaintenanceSchedule = functionSchedulesConfig["MonthlyMaintenanceSchedule"] ?? "30.00:00:00",
                ScheduledHealthCheckSchedule = functionSchedulesConfig["ScheduledHealthCheckSchedule"] ?? "01:00:00",
                WeeklyMaintenanceSchedule = functionSchedulesConfig["WeeklyMaintenanceSchedule"] ?? "7.00:00:00",
                SyncMetadataComprehensiveSchedule = functionSchedulesConfig["SyncMetadataComprehensiveSchedule"] ?? "1.00:00:00",
                SyncMetadataCriticalSchedule = functionSchedulesConfig["SyncMetadataCriticalSchedule"] ?? "04:00:00",
                SyncContentSchedule = functionSchedulesConfig["SyncContentSchedule"] ?? "7.00:00:00",
                AnomalyDetectionSchedule = functionSchedulesConfig["AnomalyDetectionSchedule"] ?? "00:30:00"
            }
        };
    }
}