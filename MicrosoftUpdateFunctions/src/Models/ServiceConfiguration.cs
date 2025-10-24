// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MicrosoftUpdateFunctions.Models;

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