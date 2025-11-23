// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Configuration;

/// <summary>
/// Runtime feature flags that can be toggled via configuration.
/// Supports hot-reload via IOptionsMonitor for enabling/disabling features at runtime.
/// </summary>
public class FeatureFlags
{
    // Sync features
    public bool EnableEmergencySync { get; set; } = true;
    public bool EnableComprehensiveSync { get; set; } = true;
    public bool EnableContentSync { get; set; } = true;

    // Monitoring features
    public bool EnableAnomalyDetection { get; set; }
    public bool EnableDeepHealthCheck { get; set; } = true;
    public bool EnableMaintenance { get; set; } = true;

    // Logging and telemetry
    public bool EnableDetailedLogging { get; set; }
    public bool EnableMetrics { get; set; } = true;
    public bool EnableOpenTelemetry { get; set; } = true;

    // Performance features
    public bool EnableCaching { get; set; } = true;
    public bool EnableCompression { get; set; } = true;

    // Experimental features
    public bool EnableExperimentalFeatures { get; set; }
}
