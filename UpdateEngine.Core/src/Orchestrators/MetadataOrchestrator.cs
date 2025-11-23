// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core.Orchestrators;

using UpdateEngine.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpdateEngine.Metadata.ObjectModel;
using UpdateEngine.Metadata.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UpdateEngine.Core.Services;

/// <summary>
/// Implementation of metadata orchestrator.
/// Uses IOptionsMonitor for hot-reload configuration support.
/// Supports distributed caching for improved performance.
/// </summary>
public class MetadataOrchestrator : IMetadataOrchestrator
{
    private readonly IMetadataStore metadataStore;
    private readonly IOptionsMonitor<AppConfig> configMonitor;
    private readonly ILogger<MetadataOrchestrator> logger;
    private readonly CacheService? cacheService;

    public MetadataOrchestrator(
        IMetadataStore metadataStore,
        IOptionsMonitor<AppConfig> configMonitor,
        ILogger<MetadataOrchestrator> logger,
        CacheService? cacheService = null)
    {
        this.metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
        this.configMonitor = configMonitor ?? throw new ArgumentNullException(nameof(configMonitor));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.cacheService = cacheService; // Optional for backward compatibility
    }

    /// <inheritdoc />
    public async Task<MetadataStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        // Check if caching is enabled
        var config = this.configMonitor.CurrentValue;
        if (this.cacheService != null && config.CacheConfiguration.EnableDistributedCache)
        {
            this.logger.LogDebug("Attempting to retrieve metadata statistics from cache");
            
            return await this.cacheService.GetOrSetAsync(
                "metadata:stats",
                async () => await this.ComputeMetadataStatisticsAsync(cancellationToken),
                this.cacheService.GetStatisticsExpiration(),
                cancellationToken);
        }

        // No caching, compute directly
        return await this.ComputeMetadataStatisticsAsync(cancellationToken);
    }

    /// <summary>
    /// Computes metadata statistics without caching.
    /// </summary>
    private Task<MetadataStatistics> ComputeMetadataStatisticsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            this.logger.LogInformation("Gathering metadata statistics");

            try
            {
                var identities = this.metadataStore.GetPackageIdentities();
                var totalUpdates = identities.Count;

                // Count categories, classifications, products
                int categories = 0;
                int classifications = 0;
                int products = 0;

                try
                {
                    // These are examples - actual implementation depends on package type
                    categories = identities.Count(id => id.ToString().Contains("Category"));
                    classifications = identities.Count(id => id.ToString().Contains("Classification"));
                    products = identities.Count(id => id.ToString().Contains("Product"));
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Could not determine detailed statistics");
                }

                var stats = new MetadataStatistics
                {
                    TotalUpdates = totalUpdates,
                    TotalCategories = categories,
                    TotalClassifications = classifications,
                    TotalProducts = products,
                    StoreSizeBytes = null, // Could be calculated if needed
                    LastUpdated = DateTime.UtcNow,
                    ReindexingRequired = this.metadataStore.IsReindexingRequired
                };

                this.logger.LogInformation(
                    "Statistics: {TotalUpdates} updates, {Categories} categories, {Classifications} classifications",
                    stats.TotalUpdates, stats.TotalCategories, stats.TotalClassifications);

                return stats;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to gather metadata statistics");
                throw;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IPackageIdentity>> QueryUpdatesAsync(
        MetadataQuery query,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            this.logger.LogInformation("Querying updates with criteria: {@Query}", query);

            try
            {
                var allIdentities = this.metadataStore.GetPackageIdentities();
                IEnumerable<IPackageIdentity> results = allIdentities;

                // Apply filters (example implementation - actual filtering logic may differ)
                if (query.Products?.Count > 0)
                {
                    results = results.Where(id =>
                        query.Products.Any(p => id.ToString().Contains(p, StringComparison.OrdinalIgnoreCase)));
                }

                if (query.Classifications?.Count > 0)
                {
                    results = results.Where(id =>
                        query.Classifications.Any(c => id.ToString().Contains(c, StringComparison.OrdinalIgnoreCase)));
                }

                // Apply skip and max results for pagination
                if (query.Skip.HasValue)
                {
                    results = results.Skip(query.Skip.Value);
                }

                if (query.MaxResults.HasValue)
                {
                    results = results.Take(query.MaxResults.Value);
                }

                var resultList = results.ToList();

                this.logger.LogInformation("Query returned {Count} results", resultList.Count);

                return (IReadOnlyList<IPackageIdentity>)resultList;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to query updates");
                throw;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IPackage?> GetUpdateDetailsAsync(
        IPackageIdentity updateId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"metadata:update:{updateId.OpenIdHex}";
        var config = this.configMonitor.CurrentValue;

        // Check if caching is enabled
        if (this.cacheService != null && config.CacheConfiguration.EnableDistributedCache)
        {
            this.logger.LogDebug("Attempting to retrieve update details from cache: {UpdateId}", updateId.OpenIdHex);

            return await this.cacheService.GetOrSetAsync(
                cacheKey,
                async () => await this.FetchUpdateDetailsAsync(updateId, cancellationToken),
                this.cacheService.GetUpdateDetailsExpiration(),
                cancellationToken);
        }

        // No caching, fetch directly
        return await this.FetchUpdateDetailsAsync(updateId, cancellationToken);
    }

    /// <summary>
    /// Fetches update details without caching.
    /// </summary>
    private Task<IPackage?> FetchUpdateDetailsAsync(
        IPackageIdentity updateId,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            this.logger.LogInformation("Getting details for update: {UpdateId}", updateId);

            try
            {
                if (!this.metadataStore.ContainsPackage(updateId))
                {
                    this.logger.LogWarning("Update not found: {UpdateId}", updateId);
                    return (IPackage?)null;
                }

                var package = this.metadataStore.GetPackage(updateId);
                this.logger.LogInformation("Retrieved update details: {UpdateId}", updateId);
                return (IPackage?)package;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get update details for {UpdateId}", updateId);
                throw;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ExportResult> ExportMetadataAsync(
        IMetadataSink destinationStore,
        IMetadataFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            this.logger.LogInformation("Starting metadata export");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var initialDestinationCount = 0;
                if (destinationStore is IMetadataStore destStore)
                {
                    initialDestinationCount = destStore.GetPackageIdentities().Count;
                }

                // Perform export using CopyTo
                if (filter != null)
                {
                    this.metadataStore.CopyTo(destinationStore, filter, cancellationToken);
                }
                else
                {
                    this.metadataStore.CopyTo(destinationStore, cancellationToken);
                }

                var finalDestinationCount = 0;
                if (destinationStore is IMetadataStore destStore2)
                {
                    finalDestinationCount = destStore2.GetPackageIdentities().Count;
                }

                var exportedCount = finalDestinationCount - initialDestinationCount;

                stopwatch.Stop();

                var result = new ExportResult
                {
                    ExportedCount = exportedCount,
                    SkippedCount = 0, // Would need to track this during copy
                    Duration = stopwatch.Elapsed,
                    Success = true,
                    ErrorMessage = null
                };

                this.logger.LogInformation(
                    "Export completed successfully: {ExportedCount} packages in {Duration}",
                    result.ExportedCount, result.Duration);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                this.logger.LogError(ex, "Export failed after {Duration}", stopwatch.Elapsed);

                return new ExportResult
                {
                    ExportedCount = 0,
                    SkippedCount = 0,
                    Duration = stopwatch.Elapsed,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IndexStatus> GetIndexStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            this.logger.LogInformation("Getting index status");

            try
            {
                var identities = this.metadataStore.GetPackageIdentities();
                var availableIndexes = new List<string>();

                // Note: IMetadataLookup is internal, so we can't check available indexes directly
                // This could be enhanced if the interface is made public

                var status = new IndexStatus
                {
                    ReindexingRequired = this.metadataStore.IsReindexingRequired,
                    IndexingSupported = this.metadataStore.IsMetadataIndexingSupported,
                    IndexedPackageCount = identities.Count,
                    AvailableIndexes = availableIndexes
                };

                this.logger.LogInformation(
                    "Index status: Reindexing required={ReindexRequired}, Indexed packages={Count}",
                    status.ReindexingRequired, status.IndexedPackageCount);

                return status;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get index status");
                throw;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ReindexResult> ReindexAsync(
        IProgress<ReindexProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            this.logger.LogInformation("Starting reindex operation");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var identities = this.metadataStore.GetPackageIdentities();
                var totalCount = identities.Count;

                // Set up progress reporting
                int currentCount = 0;
                if (this.metadataStore is IMetadataStore store)
                {
                    // Subscribe to indexing progress if available
                    EventHandler<PackageStoreEventArgs>? progressHandler = null;
                    if (progress != null)
                    {
                        progressHandler = (sender, args) =>
                        {
                            var reindexProgress = new ReindexProgress
                            {
                                Current = (int)args.Current, // Cast from long to int
                                Total = (int)args.Total, // Cast from long to int
                                CurrentPackage = null // Could be enhanced with package info
                            };
                            progress.Report(reindexProgress);
                        };

                        store.PackageIndexingProgress += progressHandler;
                    }

                    try
                    {
                        // Perform reindex
                        store.ReIndex();
                        currentCount = totalCount;
                    }
                    finally
                    {
                        if (progressHandler != null)
                        {
                            store.PackageIndexingProgress -= progressHandler;
                        }
                    }
                }

                stopwatch.Stop();

                var result = new ReindexResult
                {
                    ReindexedCount = currentCount,
                    Duration = stopwatch.Elapsed,
                    Success = true,
                    ErrorMessage = null
                };

                this.logger.LogInformation(
                    "Reindex completed successfully: {Count} packages in {Duration}",
                    result.ReindexedCount, result.Duration);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                this.logger.LogError(ex, "Reindex failed after {Duration}", stopwatch.Elapsed);

                return new ReindexResult
                {
                    ReindexedCount = 0,
                    Duration = stopwatch.Elapsed,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }, cancellationToken);
    }
}
