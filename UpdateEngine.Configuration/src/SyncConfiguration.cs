// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Configuration;

/// <summary>
/// Sync operation configuration settings.
/// Supports hot-reload via IOptionsMonitor for runtime adjustments.
/// </summary>
public class SyncConfiguration
{
    // Interval settings (in minutes)
    public int SyncIntervalMinutes { get; set; } = 60;
    public int EmergencySyncIntervalMinutes { get; set; } = 15;
    public int HealthCheckIntervalMinutes { get; set; } = 5;
    public int MaintenanceIntervalHours { get; set; } = 168; // Weekly
    public int DeepHealthCheckIntervalHours { get; set; } = 24;
    public int AnomalyDetectionCooldownMinutes { get; set; } = 30;

    // Batch settings
    public int ContentSyncBatchSize { get; set; } = 50;
    public int MetadataQueryBatchSize { get; set; } = 100;

    // Schedule settings (CRON expressions for Azure Functions TimerTrigger)
    public string SyncCriticalSchedule { get; set; } = "0 */2 * * * *"; // Every 2 hours
    public string SyncComprehensiveSchedule { get; set; } = "0 0 */1 * * *"; // Hourly
    public string SyncContentSchedule { get; set; } = "0 0 0 */1 * *"; // Daily
    public string ScheduledHealthCheckSchedule { get; set; } = "0 */15 * * * *"; // Every 15 minutes
    public string MaintenanceSchedule { get; set; } = "0 0 2 */7 * *"; // Weekly at 2 AM
    public string AnomalyDetectionSchedule { get; set; } = "0 0 */1 * * *"; // Hourly

    // Enable/disable settings
    public bool EnableScheduledSync { get; set; } = true;
    public bool EnableEmergencySync { get; set; } = true;

    public void Validate()
    {
        if (this.SyncIntervalMinutes < 1)
        {
            throw new InvalidOperationException("SyncConfiguration.SyncIntervalMinutes must be at least 1");
        }

        if (this.EmergencySyncIntervalMinutes < 1)
        {
            throw new InvalidOperationException("SyncConfiguration.EmergencySyncIntervalMinutes must be at least 1");
        }

        if (this.ContentSyncBatchSize < 1)
        {
            throw new InvalidOperationException("SyncConfiguration.ContentSyncBatchSize must be at least 1");
        }
    }
}
