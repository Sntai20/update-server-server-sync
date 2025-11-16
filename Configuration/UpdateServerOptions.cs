// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;

namespace Configuration;

/// <summary>
/// Main configuration options for the Microsoft Update Server-Server Sync application.
/// Bound directly from appsettings.json using IOptions pattern.
/// </summary>
public class UpdateServerOptions
{
    public const string SectionName = "UpdateServer";
    
    /// <summary>Gets or sets the base service URL for the update server.</summary>
    [Required]
    [Url]
    public string ServiceUrl { get; set; } = "http://localhost:7071";

    /// <summary>Gets or sets the content URL for serving update files.</summary>
    [Required]
    [Url]
    public string ContentUrl { get; set; } = "http://localhost:7071/api/content";

    /// <summary>Gets or sets the maximum number of updates to process in a single operation.</summary>
    [Range(1, 100000)]
    public int MaxUpdateCount { get; set; } = 1000;

    /// <summary>Gets or sets the supported update categories.</summary>
    public string[] SupportedCategories { get; set; } = 
    [
        "Security Updates",
        "Critical Updates", 
        "Updates"
    ];

    /// <summary>Gets or sets the supported languages for updates.</summary>
    public string[] SupportedLanguages { get; set; } = 
    [
        "en",
        "en-US", 
        "neutral",
        ""
    ];
}

/// <summary>
/// Storage configuration options.
/// </summary>
public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Gets or sets the metadata store path (local) or container name (Azure).</summary>
    [Required]
    public string MetadataPath { get; set; } = "./store";

    /// <summary>Gets or sets the content store path (local) or container name (Azure).</summary>
    [Required]
    public string ContentPath { get; set; } = "./content";

    /// <summary>Gets or sets whether to use Azure Storage for metadata.</summary>
    public bool UseAzureStorageForMetadata { get; set; } = false;

    /// <summary>Gets or sets whether to use Azure Storage for content.</summary>
    public bool UseAzureStorageForContent { get; set; } = false;

    /// <summary>Gets or sets the metadata container name for Azure Storage.</summary>
    public string MetadataContainerName { get; set; } = "metadata";

    /// <summary>Gets or sets the content container name for Azure Storage.</summary>
    public string ContentContainerName { get; set; } = "content";

    /// <summary>Gets or sets whether to reindex the metadata store on startup.</summary>
    public bool ReindexOnStartup { get; set; } = false;
}

/// <summary>
/// Function schedule options for Azure Functions timer triggers.
/// Uses TimeSpan string format (e.g., "01:30:00" for 1.5 hours).
/// </summary>
public class FunctionScheduleOptions
{
    public const string SectionName = "FunctionSchedules";

    /// <summary>Gets or sets the schedule for critical updates sync (default: every 2 hours).</summary>
    public string SyncCritical { get; set; } = "02:00:00";

    /// <summary>Gets or sets the schedule for comprehensive metadata sync (default: daily).</summary>
    public string SyncComprehensive { get; set; } = "1.00:00:00";

    /// <summary>Gets or sets the schedule for content sync (default: weekly).</summary>
    public string SyncContent { get; set; } = "7.00:00:00";

    /// <summary>Gets or sets the schedule for health checks (default: every 15 minutes).</summary>
    public string HealthCheck { get; set; } = "00:15:00";

    /// <summary>Gets or sets the schedule for weekly maintenance (default: weekly).</summary>
    public string WeeklyMaintenance { get; set; } = "7.00:00:00";

    /// <summary>Gets or sets the schedule for anomaly detection (default: every 30 minutes).</summary>
    public string AnomalyDetection { get; set; } = "00:30:00";
}

/// <summary>
/// Feature flag options for enabling/disabling functionality.
/// </summary>
public class FeatureOptions
{
    public const string SectionName = "Features";

    /// <summary>Gets or sets whether scheduled synchronization is enabled.</summary>
    public bool EnableScheduledSync { get; set; } = true;

    /// <summary>Gets or sets whether content synchronization is enabled.</summary>
    public bool EnableContentSync { get; set; } = true;

    /// <summary>Gets or sets whether health monitoring is enabled.</summary>
    public bool EnableHealthMonitoring { get; set; } = true;

    /// <summary>Gets or sets whether metadata export is enabled.</summary>
    public bool EnableMetadataExport { get; set; } = false;

    /// <summary>Gets or sets whether driver matching is enabled.</summary>
    public bool EnableDriverMatching { get; set; } = false;

    /// <summary>Gets or sets whether anomaly detection is enabled.</summary>
    public bool EnableAnomalyDetection { get; set; } = false;
}

/// <summary>
/// Sync interval configuration options.
/// </summary>
public class SyncOptions
{
    public const string SectionName = "Sync";

    /// <summary>Gets or sets the interval in hours for critical updates synchronization.</summary>
    [Range(1, 168)]
    public int CriticalUpdatesIntervalHours { get; set; } = 2;

    /// <summary>Gets or sets the interval in hours for comprehensive updates synchronization.</summary>
    [Range(1, 720)]
    public int ComprehensiveUpdatesIntervalHours { get; set; } = 24;

    /// <summary>Gets or sets the interval in hours for content synchronization.</summary>
    [Range(1, 720)]
    public int ContentSyncIntervalHours { get; set; } = 168;

    /// <summary>Gets or sets the interval in minutes for health checks.</summary>
    [Range(1, 1440)]
    public int HealthCheckIntervalMinutes { get; set; } = 15;

    /// <summary>Gets or sets the interval in hours for maintenance operations.</summary>
    [Range(1, 720)]
    public int MaintenanceIntervalHours { get; set; } = 168;
}