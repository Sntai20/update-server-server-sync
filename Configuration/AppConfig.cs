// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Configuration;

/// <summary>
/// Simple configuration class for the Microsoft Update Server-Server Sync application.
/// All settings in one place with sensible defaults.
/// </summary>
public class AppConfig
{
    // Server settings
    public string ServiceUrl { get; set; } = "http://localhost:7071";
    public string ContentUrl { get; set; } = "http://localhost:7071/api/content";
    public int MaxUpdateCount { get; set; } = 1000;
    public string[] SupportedCategories { get; set; } = ["Security Updates", "Critical Updates", "Updates"];
    public string[] SupportedLanguages { get; set; } = ["en", "en-US", "neutral", ""];

    // Storage settings
    public string MetadataPath { get; set; } = "./store";
    public string ContentPath { get; set; } = "./content";
    public bool UseAzureStorageForMetadata { get; set; } = false;
    public bool UseAzureStorageForContent { get; set; } = false;
    public string MetadataContainerName { get; set; } = "metadata";
    public string ContentContainerName { get; set; } = "content";
    public bool ReindexOnStartup { get; set; } = false;

    // Schedule settings (simple strings, convert to TimeSpan in code if needed)
    public string SyncCriticalSchedule { get; set; } = "02:00:00";
    public string SyncComprehensiveSchedule { get; set; } = "1.00:00:00";
    public string SyncContentSchedule { get; set; } = "7.00:00:00";
    public string HealthCheckSchedule { get; set; } = "00:15:00";
    public string MaintenanceSchedule { get; set; } = "7.00:00:00";
    public string WeeklyMaintenanceSchedule { get; set; } = "7.00:00:00";
    public string AnomalyDetectionSchedule { get; set; } = "01:00:00";

    // Feature flags
    public bool EnableScheduledSync { get; set; } = true;
    public bool EnableDetailedLogging { get; set; } = false;
    public bool EnableMetrics { get; set; } = true;
    public bool EnableCaching { get; set; } = true;

    // Simple validation (only if really needed)
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServiceUrl))
            throw new InvalidOperationException("ServiceUrl is required");
            
        if (string.IsNullOrWhiteSpace(MetadataPath))
            throw new InvalidOperationException("MetadataPath is required");
    }
}