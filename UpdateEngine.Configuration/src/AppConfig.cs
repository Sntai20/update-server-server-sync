// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Configuration;

/// <summary>
/// Root configuration class for the Microsoft Update Server-Server Sync application.
/// Uses .NET Options Pattern for hot-reload support with IOptionsMonitor.
/// </summary>
public class AppConfig
{
    public const string SectionName = "UpdateEngine";

    public ServiceConfiguration ServiceConfiguration { get; set; } = new();
    public SyncConfiguration SyncConfiguration { get; set; } = new();
    public StorageConfiguration StorageConfiguration { get; set; } = new();
    public FeatureFlags FeatureFlags { get; set; } = new();
    public CacheConfiguration CacheConfiguration { get; set; } = new();

    /// <summary>
    /// Validates the configuration. Throws InvalidOperationException if invalid.
    /// </summary>
    public void Validate()
    {
        this.ServiceConfiguration.Validate();
        this.SyncConfiguration.Validate();
        this.StorageConfiguration.Validate();
    }
}