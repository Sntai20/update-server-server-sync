namespace AppHost;

public record ServiceConfiguration
{
    public required string ServiceUrl { get; init; }
    public required string ContentUrl { get; init; }
    public int MaxUpdateCount { get; init; }
    public required string[] SupportedCategories { get; init; }
    public required SyncConfiguration SyncConfiguration { get; init; }
    public required StorageConfiguration StorageConfiguration { get; init; }
    public required FeatureFlags FeatureFlags { get; init; }
}

public record SyncConfiguration
{
    public int CriticalUpdatesIntervalHours { get; init; }
    public int ComprehensiveUpdatesIntervalHours { get; init; }
    public int ContentSyncIntervalHours { get; init; }
    public int MaintenanceIntervalHours { get; init; }
    public int HealthCheckIntervalMinutes { get; init; }
}

public record StorageConfiguration
{
    public required string MetadataStorePath { get; init; }
    public required string ContentStorePath { get; init; }
    public bool EnableContentStorage { get; init; }
    public bool ReindexOnStartup { get; init; }
    public bool UseAzureStorage { get; init; }
    public required string MetadataContainerName { get; init; }
    public required string ContentContainerName { get; init; }
}

public record FeatureFlags
{
    public bool UseAzureStorage { get; init; }
    public bool EnableScheduledSync { get; init; }
    public bool EnableContentSync { get; init; }
    public bool EnableHealthMonitoring { get; init; }
    public bool EnableMetadataExport { get; init; }
    public bool EnableDriverMatching { get; init; }
}