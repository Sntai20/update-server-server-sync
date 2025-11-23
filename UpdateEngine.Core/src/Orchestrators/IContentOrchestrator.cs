// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core.Orchestrators;

using UpdateEngine.Metadata.ObjectModel;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Host-agnostic orchestrator for content download and management operations.
/// Provides high-level content downloading, verification, and cleanup functionality.
/// Can be used in Azure Functions, Worker Service, CLI, or ASP.NET Core.
/// </summary>
public interface IContentOrchestrator
{
    /// <summary>
    /// Gets statistics about the content store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Content store statistics</returns>
    Task<ContentStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads content files for the specified updates.
    /// </summary>
    /// <param name="updateIds">List of update identities to download content for</param>
    /// <param name="progress">Optional progress callback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Download result with statistics</returns>
    Task<DownloadResult> DownloadContentAsync(
        IReadOnlyList<IPackageIdentity> updateIds,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies integrity of downloaded content files.
    /// </summary>
    /// <param name="updateIds">Optional list of updates to verify (null = verify all)</param>
    /// <param name="progress">Optional progress callback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Verification result with statistics</returns>
    Task<VerificationResult> VerifyContentAsync(
        IReadOnlyList<IPackageIdentity>? updateIds = null,
        IProgress<VerificationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets list of content files for a specific update.
    /// </summary>
    /// <param name="updateId">The update identity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of content files or empty list if none</returns>
    Task<IReadOnlyList<IContentFile>> GetContentFilesAsync(
        IPackageIdentity updateId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if content for specific updates is available in the store.
    /// </summary>
    /// <param name="updateIds">List of update identities to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping update IDs to availability status</returns>
    Task<IReadOnlyDictionary<IPackageIdentity, bool>> CheckContentAvailabilityAsync(
        IReadOnlyList<IPackageIdentity> updateIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up orphaned or unused content files.
    /// </summary>
    /// <param name="dryRun">If true, only reports what would be deleted without actually deleting</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cleanup result with statistics</returns>
    Task<CleanupResult> CleanupContentAsync(
        bool dryRun = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about the content store.
/// </summary>
public record ContentStatistics
{
    /// <summary>Total number of content files stored</summary>
    public int TotalFiles { get; init; }

    /// <summary>Total size of all content in bytes</summary>
    public long TotalSizeBytes { get; init; }

    /// <summary>Number of updates with complete content</summary>
    public int UpdatesWithContent { get; init; }

    /// <summary>Number of pending downloads</summary>
    public int PendingDownloads { get; init; }

    /// <summary>Total bytes queued for download</summary>
    public long QueuedSizeBytes { get; init; }

    /// <summary>Number of orphaned files (content without metadata)</summary>
    public int OrphanedFiles { get; init; }
}

/// <summary>
/// Result of content download operation.
/// </summary>
public record DownloadResult
{
    /// <summary>Number of files successfully downloaded</summary>
    public int DownloadedCount { get; init; }

    /// <summary>Number of files that failed to download</summary>
    public int FailedCount { get; init; }

    /// <summary>Number of files that were already present (skipped)</summary>
    public int SkippedCount { get; init; }

    /// <summary>Total bytes downloaded</summary>
    public long TotalBytesDownloaded { get; init; }

    /// <summary>Duration of download operation</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Whether download completed successfully</summary>
    public bool Success { get; init; }

    /// <summary>List of errors encountered</summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Progress information for download operation.
/// </summary>
public record DownloadProgress
{
    /// <summary>Current file being downloaded</summary>
    public int CurrentFile { get; init; }

    /// <summary>Total files to download</summary>
    public int TotalFiles { get; init; }

    /// <summary>Bytes downloaded so far</summary>
    public long BytesDownloaded { get; init; }

    /// <summary>Total bytes to download</summary>
    public long TotalBytes { get; init; }

    /// <summary>Current file name</summary>
    public string? CurrentFileName { get; init; }

    /// <summary>Progress percentage (0-100)</summary>
    public double PercentComplete => TotalBytes > 0 ? (BytesDownloaded * 100.0 / TotalBytes) : 0;
}

/// <summary>
/// Result of content verification operation.
/// </summary>
public record VerificationResult
{
    /// <summary>Number of files verified successfully</summary>
    public int VerifiedCount { get; init; }

    /// <summary>Number of files that failed verification</summary>
    public int FailedCount { get; init; }

    /// <summary>Number of files that are missing</summary>
    public int MissingCount { get; init; }

    /// <summary>Duration of verification operation</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Whether all files verified successfully</summary>
    public bool Success { get; init; }

    /// <summary>List of failed file paths</summary>
    public IReadOnlyList<string> FailedFiles { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Progress information for verification operation.
/// </summary>
public record VerificationProgress
{
    /// <summary>Current file being verified</summary>
    public int CurrentFile { get; init; }

    /// <summary>Total files to verify</summary>
    public int TotalFiles { get; init; }

    /// <summary>Current file name</summary>
    public string? CurrentFileName { get; init; }

    /// <summary>Progress percentage (0-100)</summary>
    public double PercentComplete => TotalFiles > 0 ? (CurrentFile * 100.0 / TotalFiles) : 0;
}

/// <summary>
/// Result of content cleanup operation.
/// </summary>
public record CleanupResult
{
    /// <summary>Number of files deleted</summary>
    public int DeletedCount { get; init; }

    /// <summary>Bytes freed by cleanup</summary>
    public long BytesFreed { get; init; }

    /// <summary>Duration of cleanup operation</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Whether cleanup completed successfully</summary>
    public bool Success { get; init; }

    /// <summary>List of deleted file paths (for dry-run or reporting)</summary>
    public IReadOnlyList<string> DeletedFiles { get; init; } = Array.Empty<string>();

    /// <summary>Error message if cleanup failed</summary>
    public string? ErrorMessage { get; init; }
}
