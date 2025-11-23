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
/// Implementation of content orchestrator.
/// Uses IOptionsMonitor for hot-reload configuration support.
/// Supports distributed caching for improved performance.
/// </summary>
public class ContentOrchestrator : IContentOrchestrator
{
    private readonly IMetadataStore metadataStore;
    private readonly IContentStore? contentStore;
    private readonly IOptionsMonitor<AppConfig> configMonitor;
    private readonly ILogger<ContentOrchestrator> logger;
    private readonly CacheService? cacheService;

    public ContentOrchestrator(
        IMetadataStore metadataStore,
        IContentStore? contentStore,
        IOptionsMonitor<AppConfig> configMonitor,
        ILogger<ContentOrchestrator> logger,
        CacheService? cacheService = null)
    {
        this.metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
        this.contentStore = contentStore; // Can be null if content operations not configured
        this.configMonitor = configMonitor ?? throw new ArgumentNullException(nameof(configMonitor));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.cacheService = cacheService; // Optional for backward compatibility
    }

    /// <inheritdoc />
    public async Task<ContentStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        // Check if caching is enabled
        var config = this.configMonitor.CurrentValue;
        if (this.cacheService != null && config.CacheConfiguration.EnableDistributedCache)
        {
            this.logger.LogDebug("Attempting to retrieve content statistics from cache");
            
            return await this.cacheService.GetOrSetAsync(
                "content:stats",
                async () => await this.ComputeContentStatisticsAsync(cancellationToken),
                this.cacheService.GetStatisticsExpiration(),
                cancellationToken);
        }

        // No caching, compute directly
        return await this.ComputeContentStatisticsAsync(cancellationToken);
    }

    /// <summary>
    /// Computes content statistics without caching.
    /// </summary>
    private Task<ContentStatistics> ComputeContentStatisticsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            this.logger.LogInformation("Gathering content statistics");

            if (this.contentStore == null)
            {
                this.logger.LogWarning("Content store not configured");
                return new ContentStatistics
                {
                    TotalFiles = 0,
                    TotalSizeBytes = 0,
                    UpdatesWithContent = 0,
                    PendingDownloads = 0,
                    QueuedSizeBytes = 0,
                    OrphanedFiles = 0
                };
            }

            try
            {
                // Get all updates and check which have content
                var identities = this.metadataStore.GetPackageIdentities();
                int updatesWithContent = 0;
                int totalFiles = 0;

                foreach (var identity in identities)
                {
                    try
                    {
                        var contentFiles = this.metadataStore.GetFiles<IContentFile>(identity);
                        if (contentFiles.Count > 0)
                        {
                            // Check if content is actually downloaded
                            var hasContent = contentFiles.All(f => this.contentStore.Contains(f));
                            if (hasContent)
                            {
                                updatesWithContent++;
                                totalFiles += contentFiles.Count;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogWarning(ex, "Could not check content for update {UpdateId}", identity);
                    }
                }

                // Try to get queued stats, but handle if not implemented (FileSystemContentStore)
                int queuedCount = 0;
                long queuedSize = 0;
                try
                {
                    queuedCount = this.contentStore.QueuedCount;
                    queuedSize = this.contentStore.QueuedSize;
                }
                catch (NotImplementedException)
                {
                    // FileSystemContentStore doesn't implement queuing
                    this.logger.LogDebug("Content store does not support queuing statistics");
                }

                var stats = new ContentStatistics
                {
                    TotalFiles = totalFiles,
                    TotalSizeBytes = 0, // Would need to calculate from file sizes
                    UpdatesWithContent = updatesWithContent,
                    PendingDownloads = queuedCount,
                    QueuedSizeBytes = queuedSize,
                    OrphanedFiles = 0 // Would need separate check for orphaned files
                };

                this.logger.LogInformation(
                    "Content statistics: {TotalFiles} files, {UpdatesWithContent} updates with content",
                    stats.TotalFiles, stats.UpdatesWithContent);

                return stats;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to gather content statistics");
                throw;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<DownloadResult> DownloadContentAsync(
        IReadOnlyList<IPackageIdentity> updateIds,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            this.logger.LogInformation("Starting content download for {Count} updates", updateIds.Count);
            var stopwatch = Stopwatch.StartNew();

            if (this.contentStore == null)
            {
                this.logger.LogError("Content store not configured");
                return new DownloadResult
                {
                    DownloadedCount = 0,
                    FailedCount = updateIds.Count,
                    SkippedCount = 0,
                    TotalBytesDownloaded = 0,
                    Duration = stopwatch.Elapsed,
                    Success = false,
                    Errors = new[] { "Content store not configured" }
                };
            }

            try
            {
                var allContentFiles = new List<IContentFile>();
                int downloadedCount = 0;
                int skippedCount = 0;
                int failedCount = 0;
                var errors = new List<string>();
                long totalBytes = 0;

                // Collect all content files
                foreach (var updateId in updateIds)
                {
                    try
                    {
                        var contentFiles = this.metadataStore.GetFiles<IContentFile>(updateId);
                        allContentFiles.AddRange(contentFiles);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, "Failed to get content files for {UpdateId}", updateId);
                        errors.Add($"Failed to get content files for {updateId}: {ex.Message}");
                        failedCount++;
                    }
                }

                // Set up progress reporting
                if (progress != null && this.contentStore != null)
                {
                    EventHandler<ContentOperationProgress>? progressHandler = (sender, args) =>
                    {
                        var downloadProgress = new DownloadProgress
                        {
                            CurrentFile = 0, // Would need to track this
                            TotalFiles = allContentFiles.Count,
                            BytesDownloaded = args.Current,
                            TotalBytes = args.Maximum,
                            CurrentFileName = null
                        };
                        progress.Report(downloadProgress);
                    };

                    this.contentStore.Progress += progressHandler;

                    try
                    {
                        // Perform download
                        this.contentStore.Download(allContentFiles, cancellationToken);

                        // Count downloaded vs skipped
                        foreach (var file in allContentFiles)
                        {
                            if (this.contentStore.Contains(file))
                            {
                                downloadedCount++;
                                totalBytes += (long)file.Size;
                            }
                            else
                            {
                                skippedCount++;
                            }
                        }
                    }
                    finally
                    {
                        this.contentStore.Progress -= progressHandler;
                    }
                }

                stopwatch.Stop();

                var result = new DownloadResult
                {
                    DownloadedCount = downloadedCount,
                    FailedCount = failedCount,
                    SkippedCount = skippedCount,
                    TotalBytesDownloaded = totalBytes,
                    Duration = stopwatch.Elapsed,
                    Success = failedCount == 0,
                    Errors = errors
                };

                this.logger.LogInformation(
                    "Download completed: {Downloaded} downloaded, {Skipped} skipped, {Failed} failed in {Duration}",
                    result.DownloadedCount, result.SkippedCount, result.FailedCount, result.Duration);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                this.logger.LogError(ex, "Download failed after {Duration}", stopwatch.Elapsed);

                return new DownloadResult
                {
                    DownloadedCount = 0,
                    FailedCount = updateIds.Count,
                    SkippedCount = 0,
                    TotalBytesDownloaded = 0,
                    Duration = stopwatch.Elapsed,
                    Success = false,
                    Errors = new[] { ex.Message }
                };
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<VerificationResult> VerifyContentAsync(
        IReadOnlyList<IPackageIdentity>? updateIds = null,
        IProgress<VerificationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            this.logger.LogInformation("Starting content verification");
            var stopwatch = Stopwatch.StartNew();

            if (this.contentStore == null)
            {
                this.logger.LogError("Content store not configured");
                return new VerificationResult
                {
                    VerifiedCount = 0,
                    FailedCount = 0,
                    MissingCount = 0,
                    Duration = stopwatch.Elapsed,
                    Success = false,
                    FailedFiles = Array.Empty<string>()
                };
            }

            try
            {
                var identities = updateIds ?? this.metadataStore.GetPackageIdentities();
                int verifiedCount = 0;
                int failedCount = 0;
                int missingCount = 0;
                var failedFiles = new List<string>();
                int currentFile = 0;
                int totalFiles = 0;

                // Count total files first
                foreach (var identity in identities)
                {
                    try
                    {
                        var contentFiles = this.metadataStore.GetFiles<IContentFile>(identity);
                        totalFiles += contentFiles.Count;
                    }
                    catch { }
                }

                // Verify each file
                foreach (var identity in identities)
                {
                    try
                    {
                        var contentFiles = this.metadataStore.GetFiles<IContentFile>(identity);

                        foreach (var file in contentFiles)
                        {
                            currentFile++;

                            // Report progress
                            progress?.Report(new VerificationProgress
                            {
                                CurrentFile = currentFile,
                                TotalFiles = totalFiles,
                                CurrentFileName = file.Source
                            });

                            if (this.contentStore.Contains(file))
                            {
                                // File exists - could add hash verification here
                                verifiedCount++;
                            }
                            else
                            {
                                missingCount++;
                                failedFiles.Add(file.Source);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogWarning(ex, "Could not verify content for {UpdateId}", identity);
                        failedCount++;
                    }
                }

                stopwatch.Stop();

                var result = new VerificationResult
                {
                    VerifiedCount = verifiedCount,
                    FailedCount = failedCount,
                    MissingCount = missingCount,
                    Duration = stopwatch.Elapsed,
                    Success = failedCount == 0 && missingCount == 0,
                    FailedFiles = failedFiles
                };

                this.logger.LogInformation(
                    "Verification completed: {Verified} verified, {Missing} missing, {Failed} failed in {Duration}",
                    result.VerifiedCount, result.MissingCount, result.FailedCount, result.Duration);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                this.logger.LogError(ex, "Verification failed after {Duration}", stopwatch.Elapsed);
                throw;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IContentFile>> GetContentFilesAsync(
        IPackageIdentity updateId,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            this.logger.LogInformation("Getting content files for {UpdateId}", updateId);

            try
            {
                var contentFiles = this.metadataStore.GetFiles<IContentFile>(updateId);
                this.logger.LogInformation("Found {Count} content files for {UpdateId}", contentFiles.Count, updateId);
                return (IReadOnlyList<IContentFile>)contentFiles;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get content files for {UpdateId}", updateId);
                throw;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<IPackageIdentity, bool>> CheckContentAvailabilityAsync(
        IReadOnlyList<IPackageIdentity> updateIds,
        CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Checking content availability for {Count} updates", updateIds.Count);

        if (this.contentStore == null)
        {
            this.logger.LogWarning("Content store not configured");
            return updateIds.ToDictionary(id => id, id => false);
        }

        try
        {
            var availability = new Dictionary<IPackageIdentity, bool>();
            var config = this.configMonitor.CurrentValue;
            var cachingEnabled = this.cacheService != null && config.CacheConfiguration.EnableDistributedCache;

            foreach (var updateId in updateIds)
            {
                bool hasContent;

                if (cachingEnabled)
                {
                    // Use cache for each update's availability
                    var cacheKey = $"content:availability:{updateId.OpenIdHex}";
                    this.logger.LogDebug("Checking cache for content availability: {UpdateId}", updateId.OpenIdHex);

                    hasContent = await this.cacheService!.GetOrSetAsync(
                        cacheKey,
                        async () => await this.CheckSingleContentAvailabilityAsync(updateId, cancellationToken),
                        this.cacheService.GetContentAvailabilityExpiration(),
                        cancellationToken);
                }
                else
                {
                    // No caching, check directly
                    hasContent = await this.CheckSingleContentAvailabilityAsync(updateId, cancellationToken);
                }

                availability[updateId] = hasContent;
            }

            var availableCount = availability.Values.Count(v => v);
            this.logger.LogInformation(
                "Content availability: {Available}/{Total} updates have content",
                availableCount, updateIds.Count);

            return availability;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to check content availability");
            throw;
        }
    }

    /// <summary>
    /// Checks content availability for a single update without caching.
    /// </summary>
    private Task<bool> CheckSingleContentAvailabilityAsync(
        IPackageIdentity updateId,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var contentFiles = this.metadataStore.GetFiles<IContentFile>(updateId);
                var hasContent = contentFiles.Count > 0 && contentFiles.All(f => this.contentStore!.Contains(f));
                return hasContent;
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Could not check content for {UpdateId}", updateId);
                return false;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<CleanupResult> CleanupContentAsync(
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            this.logger.LogInformation("Starting content cleanup (dry run: {DryRun})", dryRun);
            var stopwatch = Stopwatch.StartNew();

            if (this.contentStore == null)
            {
                this.logger.LogError("Content store not configured");
                return new CleanupResult
                {
                    DeletedCount = 0,
                    BytesFreed = 0,
                    Duration = stopwatch.Elapsed,
                    Success = false,
                    DeletedFiles = Array.Empty<string>(),
                    ErrorMessage = "Content store not configured"
                };
            }

            try
            {
                // Cleanup logic would go here
                // For now, return a placeholder result
                stopwatch.Stop();

                var result = new CleanupResult
                {
                    DeletedCount = 0,
                    BytesFreed = 0,
                    Duration = stopwatch.Elapsed,
                    Success = true,
                    DeletedFiles = Array.Empty<string>(),
                    ErrorMessage = null
                };

                this.logger.LogInformation(
                    "Cleanup completed: {Deleted} files deleted, {Freed} bytes freed in {Duration}",
                    result.DeletedCount, result.BytesFreed, result.Duration);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                this.logger.LogError(ex, "Cleanup failed after {Duration}", stopwatch.Elapsed);

                return new CleanupResult
                {
                    DeletedCount = 0,
                    BytesFreed = 0,
                    Duration = stopwatch.Elapsed,
                    Success = false,
                    DeletedFiles = Array.Empty<string>(),
                    ErrorMessage = ex.Message
                };
            }
        }, cancellationToken);
    }
}
