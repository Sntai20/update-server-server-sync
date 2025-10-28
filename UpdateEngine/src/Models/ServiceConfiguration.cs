// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Models;

public class ServiceConfiguration
{
    public string ServiceUrl { get; set; } = string.Empty;
    public string ContentUrl { get; set; } = string.Empty;
    public int MaxUpdateCount { get; set; }
    public string[] SupportedCategories { get; set; } = Array.Empty<string>();
    public SyncConfig SyncConfiguration { get; set; } = new();
    public StorageConfig StorageConfiguration { get; set; } = new();
    public FeatureConfig FeatureFlags { get; set; } = new();
}

public class SyncConfig
{
    public int CriticalUpdatesIntervalHours { get; set; }
    public int ComprehensiveUpdatesIntervalHours { get; set; }
    public int ContentSyncIntervalHours { get; set; }
    public int MaintenanceIntervalHours { get; set; }
    public int HealthCheckIntervalMinutes { get; set; }

    // TimeSpan-based schedules for Azure Functions
    public TimeSpan HourlyHealthCheckSchedule { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan DailyCriticalSyncSchedule { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan WeeklyComprehensiveSyncSchedule { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan MonthlyMaintenanceSchedule { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan ScheduledHealthCheckSchedule { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan WeeklyMaintenanceSchedule { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan ScheduledMetadataSyncSchedule { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan CriticalUpdatesSyncSchedule { get; set; } = TimeSpan.FromHours(4);
    public TimeSpan ScheduledContentSyncSchedule { get; set; } = TimeSpan.FromDays(7);
}

public class StorageConfig
{
    public string MetadataStorePath { get; set; } = string.Empty;
    public string ContentStorePath { get; set; } = string.Empty;
    public bool EnableContentStorage { get; set; }
    public bool ReindexOnStartup { get; set; }
}

public class FeatureConfig
{
    public bool EnableScheduledSync { get; set; }
    public bool EnableContentSync { get; set; }
    public bool EnableHealthMonitoring { get; set; }
    public bool EnableMetadataExport { get; set; }
    public bool EnableDriverMatching { get; set; }
}