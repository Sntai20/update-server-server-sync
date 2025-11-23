namespace UpdateEngine.Core.Models;

/// <summary>
/// Unified sync request model - works with any hosting model (Azure Functions, Worker Service, Console, etc.)
/// </summary>
public class UnifiedSyncRequest
{
    /// <summary>
    /// Type of sync to perform (required for Start action)
    /// </summary>
    public SyncType? SyncType { get; set; }

    /// <summary>
    /// Action to perform on the sync operation
    /// </summary>
    public SyncAction Action { get; set; } = SyncAction.Start;

    /// <summary>
    /// Filter for update sync (required for Updates sync type)
    /// </summary>
    public SyncFilter? Filter { get; set; }
}

/// <summary>
/// Type of synchronization to perform
/// </summary>
public enum SyncType
{
    /// <summary>Sync categories only (products, classifications, detectoids)</summary>
    Categories = 0,

    /// <summary>Sync updates only (requires filter)</summary>
    Updates = 1,

    /// <summary>Comprehensive sync (categories + updates)</summary>
    Comprehensive = 2
}

/// <summary>
/// Action to perform on sync operation
/// </summary>
public enum SyncAction
{
    /// <summary>Start a new sync operation</summary>
    Start = 0,

    /// <summary>Pause the current sync operation</summary>
    Pause = 1,

    /// <summary>Resume a paused sync operation</summary>
    Resume = 2,

    /// <summary>Cancel the current sync operation</summary>
    Cancel = 3
}

/// <summary>
/// Filter criteria for sync operations
/// </summary>
public class SyncFilter
{
    /// <summary>Product titles to sync (e.g., "Windows 10", "Windows 11")</summary>
    public List<string>? ProductTitles { get; set; }

    /// <summary>Classification GUIDs to sync</summary>
    public List<Guid>? ClassificationIds { get; set; }

    /// <summary>Sync updates from this date forward</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Sync updates up to this date</summary>
    public DateTime? ToDate { get; set; }

    /// <summary>Include superseded updates</summary>
    public bool IncludeSuperseded { get; set; } = false;
}

/// <summary>
/// Result of a sync operation
/// </summary>
public class SyncOperationResult
{
    /// <summary>Whether the operation succeeded</summary>
    public bool Success { get; set; }

    /// <summary>Success message</summary>
    public string? Message { get; set; }

    /// <summary>Error message (if Success is false)</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Operation timestamp</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Operation duration</summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>Number of items synced</summary>
    public int? ItemsSynced { get; set; }

    /// <summary>Detailed statistics (optional)</summary>
    public Dictionary<string, object>? Statistics { get; set; }
}

/// <summary>
/// Current status of sync operations
/// </summary>
public class SyncStatusResult
{
    /// <summary>Whether a sync is currently running</summary>
    public bool IsRunning { get; set; }

    /// <summary>Type of sync currently running (if any)</summary>
    public SyncType? CurrentSyncType { get; set; }

    /// <summary>When the current sync started</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>Progress percentage (0-100)</summary>
    public int? Progress { get; set; }

    /// <summary>Current phase of the sync operation</summary>
    public string? CurrentPhase { get; set; }

    /// <summary>Number of items processed so far</summary>
    public int? ItemsProcessed { get; set; }

    /// <summary>Total number of items to process</summary>
    public int? TotalItems { get; set; }

    /// <summary>Number of errors encountered</summary>
    public int? ErrorCount { get; set; }

    /// <summary>Estimated time remaining</summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }
}
