// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Configuration;

/// <summary>
/// Immutable service configuration record for build-time configuration (AppHost).
/// Contains all configuration needed for the Microsoft Update Server-Server Sync application.
/// </summary>
public record ServiceConfiguration
{
    /// <summary>Gets the base service URL for the update server.</summary>
    public required string ServiceUrl { get; init; }

    /// <summary>Gets the content URL for serving update files.</summary>
    public required string ContentUrl { get; init; }

    /// <summary>Gets the maximum number of updates to process in a single operation.</summary>
    public int MaxUpdateCount { get; init; }

    /// <summary>Gets the supported update categories.</summary>
    public required string[] SupportedCategories { get; init; }

    /// <summary>Gets the synchronization configuration settings.</summary>
    public required SyncConfiguration SyncConfiguration { get; init; }

    /// <summary>Gets the storage configuration settings.</summary>
    public required StorageConfiguration StorageConfiguration { get; init; }

    /// <summary>Gets the feature flag settings.</summary>
    public required FeatureFlags FeatureFlags { get; init; }

    /// <summary>Gets the function schedule settings for timer triggers.</summary>
    public required FunctionSchedules FunctionSchedules { get; init; }
}

/// <summary>
/// Mutable service configuration class for runtime configuration (UpdateEngine).
/// Contains all configuration needed for the Microsoft Update Server-Server Sync application.
/// </summary>
public class ServiceConfigurationMutable
{
    /// <summary>Gets or sets the base service URL for the update server.</summary>
    public string ServiceUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the content URL for serving update files.</summary>
    public string ContentUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the maximum number of updates to process in a single operation.</summary>
    public int MaxUpdateCount { get; set; }

    /// <summary>Gets or sets the supported update categories.</summary>
    public string[] SupportedCategories { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets the synchronization configuration settings.</summary>
    public SyncConfigMutable SyncConfiguration { get; set; } = new();

    /// <summary>Gets or sets the storage configuration settings.</summary>
    public StorageConfigMutable StorageConfiguration { get; set; } = new();

    /// <summary>Gets or sets the feature flag settings.</summary>
    public FeatureConfigMutable FeatureFlags { get; set; } = new();
}

/// <summary>
/// Immutable synchronization configuration record.
/// </summary>
public record SyncConfiguration
{
    /// <summary>Gets the interval in hours for critical updates synchronization.</summary>
    public int CriticalUpdatesIntervalHours { get; init; }

    /// <summary>Gets the interval in hours for comprehensive updates synchronization.</summary>
    public int ComprehensiveUpdatesIntervalHours { get; init; }

    /// <summary>Gets the interval in hours for content synchronization.</summary>
    public int ContentSyncIntervalHours { get; init; }

    /// <summary>Gets the interval in hours for maintenance operations.</summary>
    public int MaintenanceIntervalHours { get; init; }

    /// <summary>Gets the interval in minutes for health checks.</summary>
    public int HealthCheckIntervalMinutes { get; init; }
}

/// <summary>
/// Mutable synchronization configuration class with TimeSpan-based schedules.
/// </summary>
public class SyncConfigMutable
{
    /// <summary>Gets or sets the interval in hours for critical updates synchronization.</summary>
    public int CriticalUpdatesIntervalHours { get; set; }

    /// <summary>Gets or sets the interval in hours for comprehensive updates synchronization.</summary>
    public int ComprehensiveUpdatesIntervalHours { get; set; }

    /// <summary>Gets or sets the interval in hours for content synchronization.</summary>
    public int ContentSyncIntervalHours { get; set; }

    /// <summary>Gets or sets the interval in hours for maintenance operations.</summary>
    public int MaintenanceIntervalHours { get; set; }

    /// <summary>Gets or sets the interval in minutes for health checks.</summary>
    public int HealthCheckIntervalMinutes { get; set; }

    // TimeSpan-based schedules for Azure Functions
    /// <summary>Gets or sets the schedule for hourly health checks.</summary>
    public TimeSpan HourlyHealthCheckSchedule { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Gets or sets the schedule for daily critical sync operations.</summary>
    public TimeSpan DailyCriticalSyncSchedule { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Gets or sets the schedule for weekly comprehensive sync operations.</summary>
    public TimeSpan WeeklyComprehensiveSyncSchedule { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Gets or sets the schedule for monthly maintenance operations.</summary>
    public TimeSpan MonthlyMaintenanceSchedule { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Gets or sets the schedule for scheduled health checks.</summary>
    public TimeSpan ScheduledHealthCheckSchedule { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Gets or sets the schedule for weekly maintenance operations.</summary>
    public TimeSpan WeeklyMaintenanceSchedule { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Gets or sets the schedule for scheduled metadata sync operations.</summary>
    public TimeSpan ScheduledMetadataSyncSchedule { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Gets or sets the schedule for critical updates sync operations.</summary>
    public TimeSpan CriticalMetadataSyncSchedule { get; set; } = TimeSpan.FromHours(4);

    /// <summary>Gets or sets the schedule for scheduled content sync operations.</summary>
    public TimeSpan SyncContentSchedule { get; set; } = TimeSpan.FromDays(7);
}

/// <summary>
/// Immutable storage configuration record.
/// </summary>
public record StorageConfiguration
{
    /// <summary>Gets the metadata store path.</summary>
    public required string MetadataStorePath { get; init; }

    /// <summary>Gets the content store path.</summary>
    public required string ContentStorePath { get; init; }

    /// <summary>Gets a value indicating whether content storage is enabled.</summary>
    public bool EnableContentStorage { get; init; }

    /// <summary>Gets a value indicating whether to reindex on startup.</summary>
    public bool ReindexOnStartup { get; init; }

    /// <summary>Gets a value indicating whether to use Azure Storage for the metadata store.</summary>
    public bool UseAzureStorageForMetadata { get; init; }

    /// <summary>Gets a value indicating whether to use Azure Storage for the content store.</summary>
    public bool UseAzureStorageForContent { get; init; }

    /// <summary>Gets the metadata container name for Azure Storage.</summary>
    public required string MetadataContainerName { get; init; }

    /// <summary>Gets the content container name for Azure Storage.</summary>
    public required string ContentContainerName { get; init; }
}

/// <summary>
/// Mutable storage configuration class.
/// </summary>
public class StorageConfigMutable
{
    /// <summary>Gets or sets the metadata store path.</summary>
    public string MetadataStorePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the content store path.</summary>
    public string ContentStorePath { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether content storage is enabled.</summary>
    public bool EnableContentStorage { get; set; }

    /// <summary>Gets or sets a value indicating whether to reindex on startup.</summary>
    public bool ReindexOnStartup { get; set; }
}

/// <summary>
/// Immutable feature flags record.
/// </summary>
public record FeatureFlags
{
    /// <summary>Gets a value indicating whether to use Azure Storage for the metadata store.</summary>
    public bool UseAzureStorageForMetadata { get; init; }

    /// <summary>Gets a value indicating whether to use Azure Storage for the content store.</summary>
    public bool UseAzureStorageForContent{ get; init; }

    /// <summary>Gets a value indicating whether scheduled sync is enabled.</summary>
    public bool EnableScheduledSync { get; init; }

    /// <summary>Gets a value indicating whether content sync is enabled.</summary>
    public bool EnableContentSync { get; init; }

    /// <summary>Gets a value indicating whether health monitoring is enabled.</summary>
    public bool EnableHealthMonitoring { get; init; }

    /// <summary>Gets a value indicating whether metadata export is enabled.</summary>
    public bool EnableMetadataExport { get; init; }

    /// <summary>Gets a value indicating whether driver matching is enabled.</summary>
    public bool EnableDriverMatching { get; init; }
}

/// <summary>
/// Mutable feature flags class.
/// </summary>
public class FeatureConfigMutable
{
    /// <summary>Gets or sets a value indicating whether scheduled sync is enabled.</summary>
    public bool EnableScheduledSync { get; set; }

    /// <summary>Gets or sets a value indicating whether content sync is enabled.</summary>
    public bool EnableContentSync { get; set; }

    /// <summary>Gets or sets a value indicating whether health monitoring is enabled.</summary>
    public bool EnableHealthMonitoring { get; set; }

    /// <summary>Gets or sets a value indicating whether metadata export is enabled.</summary>
    public bool EnableMetadataExport { get; set; }

    /// <summary>Gets or sets a value indicating whether driver matching is enabled.</summary>
    public bool EnableDriverMatching { get; set; }
}

/// <summary>
/// Function schedules configuration for Azure Functions timer triggers.
/// Contains TimeSpan schedule values as strings in TimeSpan format.
/// </summary>
public record FunctionSchedules
{
    /// <summary>Gets the schedule for hourly health check function.</summary>
    public required string HourlyHealthCheckSchedule { get; init; }

    /// <summary>Gets the schedule for daily critical sync function.</summary>
    public required string DailyCriticalSyncSchedule { get; init; }

    /// <summary>Gets the schedule for weekly comprehensive sync function.</summary>
    public required string WeeklyComprehensiveSyncSchedule { get; init; }

    /// <summary>Gets the schedule for monthly maintenance function.</summary>
    public required string MonthlyMaintenanceSchedule { get; init; }

    /// <summary>Gets the schedule for scheduled health check function.</summary>
    public required string ScheduledHealthCheckSchedule { get; init; }

    /// <summary>Gets the schedule for weekly maintenance function.</summary>
    public required string WeeklyMaintenanceSchedule { get; init; }

    /// <summary>Gets the schedule for scheduled metadata sync function.</summary>
    public required string ScheduledMetadataSyncSchedule { get; init; }

    /// <summary>Gets the schedule for critical updates sync function.</summary>
    public required string CriticalMetadataSyncSchedule { get; init; }

    /// <summary>Gets the schedule for scheduled content sync function.</summary>
    public required string SyncContentSchedule { get; init; }
}