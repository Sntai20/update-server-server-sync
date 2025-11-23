// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core.Services;

using Microsoft.PackageGraph.Storage;

/// <summary>
/// Service interface for health check and maintenance operations.
/// </summary>
public interface IHealthService
{
    Task<HealthCheckResult> PerformHealthCheckAsync();
    Task CleanupTemporaryFilesAsync();
    Task<MaintenanceResult> PerformMaintenanceAsync(MaintenanceLevel level);
    Task<bool> CheckReindexingNeeded();
    Task<HealthStatus> GetSystemHealthAsync();
    Task<HealthStatus> GetSyncHealthAsync();
}

/// <summary>
/// Health check result model.
/// </summary>
public class HealthCheckResult
{
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? PackageCount { get; set; }
    public bool ContentStoreAvailable { get; set; }
    public bool ReindexingRequired { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Maintenance result model.
/// </summary>
public class MaintenanceResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public MaintenanceLevel Level { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Maintenance operation levels.
/// </summary>
public enum MaintenanceLevel
{
    Light,
    Weekly,
    Monthly
}