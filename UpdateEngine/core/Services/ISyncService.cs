// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core.Services;

using Microsoft.PackageGraph.MicrosoftUpdate.Source;
using Microsoft.PackageGraph.Storage;
using UpdateEngine.Core.Models;

/// <summary>
/// Service interface for metadata and content synchronization operations.
/// Provides testable, reusable sync logic shared across all Azure Functions.
/// </summary>
public interface ISyncService
{
    // Existing methods
    Task SyncCategoriesAsync(CancellationToken cancellationToken = default);
    Task SyncUpdatesAsync(UpstreamSourceFilter filter, CancellationToken cancellationToken = default);
    Task SyncContentAsync(ServiceMetadataFilter filter, IContentStore contentStore, CancellationToken cancellationToken = default);
    Task<bool> IsReindexingRequired();
    Task ReindexStoreAsync(CancellationToken cancellationToken = default);
    UpstreamSourceFilter CreateCriticalUpdatesFilter();
    UpstreamSourceFilter CreateComprehensiveUpdatesFilter();
    UpstreamSourceFilter CreateCustomFilter(List<string>? productFilters, List<string>? classificationFilters);

    // New methods required by SyncOrchestrator
    /// <summary>
    /// Gets the current status of sync operations.
    /// </summary>
    Task<SyncStatus> GetSyncStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses an ongoing sync operation.
    /// </summary>
    Task PauseSyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused sync operation.
    /// </summary>
    Task ResumeSyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an ongoing sync operation.
    /// </summary>
    Task CancelSyncAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Current sync operation status.
/// </summary>
public class SyncStatus
{
    /// <summary>Indicates if a sync operation is currently running</summary>
    public bool IsRunning { get; set; }

    /// <summary>Type of sync operation currently running (if any)</summary>
    public int? SyncType { get; set; }

    /// <summary>Time when the current sync operation started</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>Progress percentage (0-100)</summary>
    public int ProgressPercentage { get; set; }

    /// <summary>Current phase of the sync operation</summary>
    public string? CurrentPhase { get; set; }

    /// <summary>Number of items processed so far</summary>
    public int ItemsProcessed { get; set; }

    /// <summary>Total number of items to process</summary>
    public int TotalItems { get; set; }

    /// <summary>Number of errors encountered</summary>
    public int ErrorCount { get; set; }
}

/// <summary>
/// Result of a category synchronization operation.
/// </summary>
public class CategorySyncResult
{
    /// <summary>Number of product categories synced</summary>
    public int ProductCategories { get; set; }

    /// <summary>Number of classification categories synced</summary>
    public int ClassificationCategories { get; set; }

    /// <summary>Number of detectoid categories synced</summary>
    public int DetectoidCategories { get; set; }

    /// <summary>Total time taken for the operation</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>List of errors encountered (if any)</summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Result of an update synchronization operation.
/// </summary>
public class UpdateSyncResult
{
    /// <summary>Number of software updates synced</summary>
    public int SoftwareUpdates { get; set; }

    /// <summary>Number of driver updates synced</summary>
    public int DriverUpdates { get; set; }

    /// <summary>Total time taken for the operation</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>List of errors encountered (if any)</summary>
    public List<string> Errors { get; set; } = new();
}