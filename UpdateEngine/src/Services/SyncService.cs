// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Services;

using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.MicrosoftUpdate.Source;
using Microsoft.PackageGraph.MicrosoftUpdate.Metadata;

/// <summary>
/// Implementation of sync service providing core synchronization logic.
/// This service is shared across all Azure Functions for consistency and testability.
/// </summary>
public class SyncService : ISyncService
{
    private readonly ILogger<SyncService> logger;
    private readonly IMetadataStore metadataStore;

    public SyncService(ILogger<SyncService> logger, IMetadataStore metadataStore)
    {
        this.logger = logger;
        this.metadataStore = metadataStore;
    }

    public async Task SyncCategoriesAsync(CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Starting categories synchronization");

        var upstreamEndpoint = Endpoint.Default;
        var categoriesSource = new UpstreamCategoriesSource(upstreamEndpoint);
        
        categoriesSource.CopyTo(this.metadataStore, cancellationToken);
        
        this.logger.LogInformation("Categories synchronization completed");
    }

    public async Task SyncUpdatesAsync(UpstreamSourceFilter filter, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Starting updates synchronization with filter");

        var upstreamEndpoint = Endpoint.Default;
        var updatesSource = new UpstreamUpdatesSource(upstreamEndpoint, filter);
        
        updatesSource.CopyTo(this.metadataStore, cancellationToken);
        
        this.logger.LogInformation("Updates synchronization completed");
    }

    public async Task SyncContentAsync(ServiceMetadataFilter filter, IContentStore contentStore, CancellationToken cancellationToken = default)
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
            contentStore.Download(filesToDownload, cancellationToken);
            this.logger.LogInformation("Content synchronization completed: {FileCount} files", filesToDownload.Count);
        }
        else
        {
            this.logger.LogInformation("No files to download");
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
    private List<Microsoft.PackageGraph.ObjectModel.IContentFile> GetAllUpdateFiles(MicrosoftUpdatePackage update)
    {
        var filesList = new List<Microsoft.PackageGraph.ObjectModel.IContentFile>();
        
        if (update.Files != null)
        {
            filesList.AddRange(update.Files);
        }

        if (update is SoftwareUpdate softwareUpdate && softwareUpdate.BundledUpdates != null)
        {
            foreach (var bundledUpdate in softwareUpdate.BundledUpdates)
            {
                var bundledPackage = this.metadataStore.GetPackage(bundledUpdate) as MicrosoftUpdatePackage;
                if (bundledPackage != null)
                {
                    filesList.AddRange(this.GetAllUpdateFiles(bundledPackage));
                }
            }
        }

        return filesList;
    }
}