// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core.Services;

using Microsoft.Extensions.Logging;
using UpdateEngine.Metadata.Metadata;
using UpdateEngine.Metadata.Source;
using UpdateEngine.Metadata.Storage;
using System.Linq;
using System.Threading;

/// <summary>
/// Implementation of sync service providing core synchronization logic.
/// This service is shared across all Azure Functions for consistency and testability.
/// </summary>
public class SyncService : ISyncService
{
    private readonly ILogger<SyncService> logger;
    private readonly IMetadataStore metadataStore;
    
    // Sync locking to prevent concurrent operations
    private readonly SemaphoreSlim syncLock = new SemaphoreSlim(1, 1);
    private readonly SemaphoreSlim contentLock = new SemaphoreSlim(1, 1);
    
    // Simple in-memory state tracking (in production, use distributed cache like Redis)
    private bool isRunning = false;
    private bool isPaused = false;
    private DateTime? syncStartTime = null;
    private CancellationTokenSource? currentSyncCancellation = null;

    public SyncService(ILogger<SyncService> logger, IMetadataStore metadataStore)
    {
        this.logger = logger;
        this.metadataStore = metadataStore;
    }

    public async Task SyncCategoriesAsync(CancellationToken cancellationToken = default)
    {
        if (!await this.syncLock.WaitAsync(0, cancellationToken))
        {
            this.logger.LogWarning("Categories sync skipped - another sync operation is in progress");
            return;
        }

        try
        {
            this.logger.LogInformation("Starting categories synchronization");

            var upstreamEndpoint = Endpoint.Default;
            var categoriesSource = new UpstreamCategoriesSource(upstreamEndpoint);
            
            categoriesSource.CopyTo(this.metadataStore, cancellationToken);
            
            // Flush to persist changes to Azure Blob Storage
            this.logger.LogInformation("Flushing metadata store to persist categories");
            this.metadataStore.Flush();
            
            this.logger.LogInformation("Categories synchronization completed");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Categories synchronization failed: {Message}", ex.Message);
            throw;
        }
        finally
        {
            this.syncLock.Release();
        }
    }

    public async Task SyncUpdatesAsync(UpstreamSourceFilter filter, CancellationToken cancellationToken = default)
    {
        if (!await this.syncLock.WaitAsync(0, cancellationToken))
        {
            this.logger.LogWarning("Updates sync skipped - another sync operation is in progress");
            return;
        }

        try
        {
            this.logger.LogInformation("Starting updates synchronization with filter: {@Filter}", filter);

            var upstreamEndpoint = Endpoint.Default;
            var updatesSource = new UpstreamUpdatesSource(upstreamEndpoint, filter);
            
            updatesSource.CopyTo(this.metadataStore, cancellationToken);
            
            // Flush to persist changes to Azure Blob Storage
            this.logger.LogInformation("Flushing metadata store to persist updates");
            this.metadataStore.Flush();
            
            this.logger.LogInformation("Updates synchronization completed");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Updates synchronization failed: {Message}", ex.Message);
            throw;
        }
        finally
        {
            this.syncLock.Release();
        }
    }

    public async Task SyncContentAsync(ServiceMetadataFilter filter, IContentStore contentStore, CancellationToken cancellationToken = default)
    {
        if (!await this.contentLock.WaitAsync(0, cancellationToken))
        {
            this.logger.LogWarning("Content sync skipped - another content download is in progress");
            return;
        }

        try
        {
            this.logger.LogInformation("Starting content synchronization");

            // Convert ServiceMetadataFilter to library MetadataFilter
            var metadataFilter = this.ConvertToMetadataFilter(filter);
            
            var filteredPackages = metadataFilter.Apply(this.metadataStore);
            var filesToDownload = filteredPackages
                .Where(p => p.Files != null)
                .SelectMany(p => p.Files)
                .ToList();

            // Add bundled update files for Microsoft Update packages
            foreach (var microsoftUpdatePackage in filteredPackages.OfType<MicrosoftUpdatePackage>())
            {
                filesToDownload.AddRange(this.GetAllUpdateFiles(microsoftUpdatePackage));
            }

            filesToDownload = filesToDownload.Distinct().ToList();

            if (filesToDownload.Any())
            {
                this.logger.LogInformation("Content sync: {FileCount} files to download", filesToDownload.Count);
                
                // Run the synchronous Download() method on a background thread to avoid blocking
                await Task.Run(() => contentStore.Download(filesToDownload, cancellationToken), cancellationToken);
                
                this.logger.LogInformation("Content synchronization completed: {FileCount} files", filesToDownload.Count);
            }
            else
            {
                this.logger.LogInformation("No files to download");
            }
        }
        finally
        {
            this.contentLock.Release();
        }
    }

    public async Task<bool> IsReindexingRequired()
    {
        return this.metadataStore.IsReindexingRequired;
    }

    public async Task ReindexStoreAsync(CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Starting store reindexing");
        // Implementation for reindexing would go here
        this.logger.LogInformation("Store reindexing completed");
    }

    public UpstreamSourceFilter CreateCriticalUpdatesFilter()
    {
        // Windows 11
        var criticalProductIds = new List<Guid>
        {
            Guid.Parse("72e7624a-5b00-45d2-b92f-e561c0a6a160")
        };

        var criticalClassificationIds = new List<Guid>
        {
            // Critical Updates, Security Updates
            Guid.Parse("E6CF1350-C01B-414D-A61F-263D14D133B4"),
            Guid.Parse("0FA1201D-4330-4FA8-8AE9-B877473B6441")
        };

        return new UpstreamSourceFilter(criticalProductIds, criticalClassificationIds);
    }

    public UpstreamSourceFilter CreateComprehensiveUpdatesFilter()
    {
        var classifications = new List<Guid>();
        var products = new List<Guid>();

        var classificationGuids = new[]
        {
            "E6CF1350-C01B-414D-A61F-263D14D133B4", // Critical Updates
            "0FA1201D-4330-4FA8-8AE9-B877473B6441", // Security Updates
            "28BC880E-0592-4CBF-8F95-C79B17911D5F", // Update Rollups
            "CD5FFD1E-E932-4E3A-BF74-18BF0B1BBD83"  // Updates
        };

        foreach (var guidString in classificationGuids)
        {
            if (Guid.TryParse(guidString, out var guid))
                classifications.Add(guid);
        }

        return new UpstreamSourceFilter(products, classifications);
    }

    public UpstreamSourceFilter CreateCustomFilter(List<string>? productFilters, List<string>? classificationFilters)
    {
        var products = new List<Guid>();
        var classifications = new List<Guid>();

        if (productFilters != null)
        {
            foreach (var product in productFilters)
            {
                if (Guid.TryParse(product, out var productGuid))
                    products.Add(productGuid);
            }
        }

        if (classificationFilters != null)
        {
            foreach (var classification in classificationFilters)
            {
                if (Guid.TryParse(classification, out var classificationGuid))
                    classifications.Add(classificationGuid);
            }
        }

        // If no filters specified, use critical updates as default
        if (!products.Any() && !classifications.Any())
        {
            return this.CreateCriticalUpdatesFilter();
        }

        return new UpstreamSourceFilter(products, classifications);
    }

    /// <summary>
    /// Converts ServiceMetadataFilter to library MetadataFilter for internal use.
    /// </summary>
    private MetadataFilter ConvertToMetadataFilter(ServiceMetadataFilter serviceFilter)
    {
        var metadataFilter = new MetadataFilter();

        // Convert product filters to GUIDs
        if (serviceFilter.ProductFilters?.Any() == true)
        {
            var productGuids = new List<Guid>();
            foreach (var product in serviceFilter.ProductFilters)
            {
                if (Guid.TryParse(product, out var productGuid))
                {
                    productGuids.Add(productGuid);
                }
            }
            metadataFilter.CategoryFilter = productGuids;
        }

        // Convert classification filters to GUIDs and add to category filter
        if (serviceFilter.ClassificationFilters?.Any() == true)
        {
            var classificationGuids = new List<Guid>();
            foreach (var classification in serviceFilter.ClassificationFilters)
            {
                if (Guid.TryParse(classification, out var classificationGuid))
                {
                    classificationGuids.Add(classificationGuid);
                }
            }
            
            // Combine with existing category filter
            if (metadataFilter.CategoryFilter?.Any() == true)
            {
                metadataFilter.CategoryFilter = metadataFilter.CategoryFilter.Concat(classificationGuids).ToList();
            }
            else
            {
                metadataFilter.CategoryFilter = classificationGuids;
            }
        }

        // Note: MetadataFilter doesn't have UpdatedAfter/UpdatedBefore properties
        // Those would need to be handled differently based on the library's capabilities

        return metadataFilter;
    }

    /// <summary>
    /// Gets all files for an update, including files in bundled updates (recursive)
    /// </summary>
    private List<UpdateEngine.Metadata.ObjectModel.IContentFile> GetAllUpdateFiles(MicrosoftUpdatePackage update)
    {
        var filesList = new List<UpdateEngine.Metadata.ObjectModel.IContentFile>();
        
        if (update.Files != null)
        {
            filesList.AddRange(update.Files);
        }

        if (update is SoftwareUpdate softwareUpdate && softwareUpdate.BundledUpdates != null)
        {
            foreach (var bundledUpdate in softwareUpdate.BundledUpdates)
            {
                // Skip null GUID packages - these are placeholders or invalid references
                if (bundledUpdate.ToString().Contains("00000000-0000-0000-0000-000000000000"))
                {
                    // Log at debug level - null GUIDs are common placeholders in metadata
                    this.logger.LogDebug("Skipping bundled update with null GUID: {BundledUpdate}", bundledUpdate);
                    continue;
                }

                var bundledPackage = this.metadataStore.GetPackage(bundledUpdate) as MicrosoftUpdatePackage;
                if (bundledPackage != null)
                {
                    filesList.AddRange(this.GetAllUpdateFiles(bundledPackage));
                }
            }
        }

        return filesList;
    }

    // New methods required by ISyncOrchestrator
    public Task<SyncStatus> GetSyncStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = new SyncStatus
        {
            IsRunning = this.isRunning,
            StartTime = this.syncStartTime,
            ProgressPercentage = 0, // TODO: Implement progress tracking
            CurrentPhase = this.isRunning ? "Syncing" : "Idle",
            ItemsProcessed = 0, // TODO: Implement item counting
            TotalItems = 0,
            ErrorCount = 0
        };

        return Task.FromResult(status);
    }

    public Task PauseSyncAsync(CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Pausing sync operation");
        this.isPaused = true;
        // TODO: Implement actual pause logic
        return Task.CompletedTask;
    }

    public Task ResumeSyncAsync(CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Resuming sync operation");
        this.isPaused = false;
        // TODO: Implement actual resume logic
        return Task.CompletedTask;
    }

    public Task CancelSyncAsync(CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Cancelling sync operation");
        this.currentSyncCancellation?.Cancel();
        this.isRunning = false;
        this.isPaused = false;
        this.syncStartTime = null;
        return Task.CompletedTask;
    }
}