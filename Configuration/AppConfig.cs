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
    public string ContentPathPrefix { get; set; } = "Content";
    public bool ReindexOnStartup { get; set; } = false;

    // Schedule settings (CRON expressions for Azure Functions TimerTrigger)
    public string SyncCriticalSchedule { get; set; } = "0 */2 * * * *";
    public string SyncComprehensiveSchedule { get; set; } = "0 0 */1 * * *";
    public string SyncContentSchedule { get; set; } = "0 0 0 */1 * *";
    public string ScheduledHealthCheckSchedule { get; set; } = "0 */15 * * * *";
    public string MaintenanceSchedule { get; set; } = "0 0 2 */7 * *";
    public string WeeklyMaintenanceSchedule { get; set; } = "0 0 3 */7 * *";
    public string AnomalyDetectionSchedule { get; set; } = "0 0 */1 * * *";

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