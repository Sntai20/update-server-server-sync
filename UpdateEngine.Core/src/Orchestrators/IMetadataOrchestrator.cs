// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core.Orchestrators;

using Microsoft.PackageGraph.ObjectModel;
using Microsoft.PackageGraph.Storage;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Host-agnostic orchestrator for metadata operations.
/// Provides high-level metadata querying, filtering, and export functionality.
/// Can be used in Azure Functions, Worker Service, CLI, or ASP.NET Core.
/// </summary>
public interface IMetadataOrchestrator
{
    /// <summary>
    /// Gets metadata statistics including total updates, categories, and classifications.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Metadata statistics</returns>
    Task<MetadataStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries updates matching the specified criteria.
    /// </summary>
    /// <param name="query">Query criteria for filtering updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Matching package identities</returns>
    Task<IReadOnlyList<IPackageIdentity>> QueryUpdatesAsync(
        MetadataQuery query, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information about a specific update.
    /// </summary>
    /// <param name="updateId">The update identity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Package details or null if not found</returns>
    Task<IPackage?> GetUpdateDetailsAsync(
        IPackageIdentity updateId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports metadata to a specified destination store.
    /// </summary>
    /// <param name="destinationStore">Target metadata store</param>
    /// <param name="filter">Optional filter for selective export</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Export result with statistics</returns>
    Task<ExportResult> ExportMetadataAsync(
        IMetadataSink destinationStore,
        IMetadataFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the metadata store requires reindexing and returns index status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Index status information</returns>
    Task<IndexStatus> GetIndexStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs reindexing of the metadata store.
    /// </summary>
    /// <param name="progress">Optional progress callback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Reindex result with statistics</returns>
    Task<ReindexResult> ReindexAsync(
        IProgress<ReindexProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about the metadata store.
/// </summary>
public record MetadataStatistics
{
    /// <summary>Total number of updates in the store</summary>
    public int TotalUpdates { get; init; }

    /// <summary>Number of categories</summary>
    public int TotalCategories { get; init; }

    /// <summary>Number of classifications</summary>
    public int TotalClassifications { get; init; }

    /// <summary>Number of products</summary>
    public int TotalProducts { get; init; }

    /// <summary>Store size in bytes (if available)</summary>
    public long? StoreSizeBytes { get; init; }

    /// <summary>Last update timestamp</summary>
    public DateTime? LastUpdated { get; init; }

    /// <summary>Whether reindexing is required</summary>
    public bool ReindexingRequired { get; init; }
}

/// <summary>
/// Query criteria for filtering metadata.
/// </summary>
public record MetadataQuery
{
    /// <summary>Filter by product names</summary>
    public IReadOnlyList<string>? Products { get; init; }

    /// <summary>Filter by classification names</summary>
    public IReadOnlyList<string>? Classifications { get; init; }

    /// <summary>Filter by update state (deployed, approved, etc.)</summary>
    public string? UpdateState { get; init; }

    /// <summary>Filter by date range - updates released after this date</summary>
    public DateTime? ReleasedAfter { get; init; }

    /// <summary>Filter by date range - updates released before this date</summary>
    public DateTime? ReleasedBefore { get; init; }

    /// <summary>Maximum number of results to return</summary>
    public int? MaxResults { get; init; }

    /// <summary>Skip this many results (for pagination)</summary>
    public int? Skip { get; init; }
}

/// <summary>
/// Result of metadata export operation.
/// </summary>
public record ExportResult
{
    /// <summary>Number of packages exported</summary>
    public int ExportedCount { get; init; }

    /// <summary>Number of packages skipped (already in destination)</summary>
    public int SkippedCount { get; init; }

    /// <summary>Duration of export operation</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Whether export completed successfully</summary>
    public bool Success { get; init; }

    /// <summary>Error message if export failed</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Status of metadata store index.
/// </summary>
public record IndexStatus
{
    /// <summary>Whether reindexing is required</summary>
    public bool ReindexingRequired { get; init; }

    /// <summary>Whether metadata indexing is supported</summary>
    public bool IndexingSupported { get; init; }

    /// <summary>Number of indexed packages</summary>
    public int IndexedPackageCount { get; init; }

    /// <summary>Available indexes</summary>
    public IReadOnlyList<string> AvailableIndexes { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Result of reindexing operation.
/// </summary>
public record ReindexResult
{
    /// <summary>Number of packages reindexed</summary>
    public int ReindexedCount { get; init; }

    /// <summary>Duration of reindex operation</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Whether reindex completed successfully</summary>
    public bool Success { get; init; }

    /// <summary>Error message if reindex failed</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Progress information for reindexing operation.
/// </summary>
public record ReindexProgress
{
    /// <summary>Current package being indexed</summary>
    public int Current { get; init; }

    /// <summary>Total packages to index</summary>
    public int Total { get; init; }

    /// <summary>Current package identity</summary>
    public string? CurrentPackage { get; init; }

    /// <summary>Progress percentage (0-100)</summary>
    public double PercentComplete => Total > 0 ? (Current * 100.0 / Total) : 0;
}
