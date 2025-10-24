// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MicrosoftUpdateFunctions.Services;

using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.ObjectModel;
using Microsoft.PackageGraph.MicrosoftUpdate.Metadata;
using Microsoft.PackageGraph.MicrosoftUpdate.Metadata.Drivers;

/// <summary>
/// Implementation of query service providing metadata query and analysis operations.
/// </summary>
public class QueryService : IQueryService
{
    private readonly ILogger<QueryService> logger;
    private readonly IMetadataStore metadataStore;

    public QueryService(ILogger<QueryService> logger, IMetadataStore metadataStore)
    {
        this.logger = logger;
        this.metadataStore = metadataStore;
    }

    public MetadataQueryResult QueryPackages(string packageType, MetadataFilter filter)
    {
        var filteredPackages = filter.Apply(this.metadataStore);
        var result = new MetadataQueryResult
        {
            PackageType = packageType,
            TotalMatches = filteredPackages.Count(),
            Packages = new List<PackageInfo>()
        };

        foreach (var package in filteredPackages.Take(100)) // Limit to first 100 for performance
        {
            var packageInfo = new PackageInfo
            {
                Id = package.Id.ID,
                Title = package.Title,
                PackageType = package.GetType().Name
            };

            if (package is MicrosoftUpdatePackage updatePackage)
            {
                packageInfo.Description = updatePackage.Description;
                packageInfo.Size = updatePackage.Files?.Sum(f => (long)f.Size) ?? 0;
                packageInfo.Classification = updatePackage.Classification?.Title;
                packageInfo.Product = updatePackage.ProductNames?.FirstOrDefault();
                packageInfo.KbArticle = updatePackage.KBArticleId;
                packageInfo.IsSuperseded = updatePackage.IsSuperseded;
            }

            result.Packages.Add(packageInfo);
        }

        return result;
    }

    public DriverMatchResult MatchDriver(IEnumerable<string> hardwareIds, List<Guid> computerHardwareIds, List<Guid> prerequisites)
    {
        var driverMatching = DriverUpdateMatching.FromPackageSource(this.metadataStore);
        var driverMatch = driverMatching.MatchDriver(hardwareIds, computerHardwareIds, prerequisites);

        if (driverMatch != null)
        {
            return new DriverMatchResult
            {
                MatchFound = true,
                DriverId = driverMatch.Driver.Id.ID,
                DriverTitle = driverMatch.Driver.Title,
                MatchedHardwareId = driverMatch.MatchedHardwareId,
                DriverVersion = driverMatch.MatchedVersion?.VersionString,
                DriverDate = driverMatch.MatchedVersion?.Date,
                MatchedComputerHardwareId = driverMatch.MatchedComputerHardwareId,
                FeatureScore = driverMatch.MatchedFeatureScore?.Score,
                OperatingSystem = driverMatch.MatchedFeatureScore?.OperatingSystem
            };
        }

        return new DriverMatchResult { MatchFound = false };
    }

    public DetailedStoreStatus GetDetailedStoreStatus()
    {
        var packageCount = this.metadataStore.Cast<IPackage>().Count();
        var updateCount = this.metadataStore.OfType<MicrosoftUpdatePackage>().Count();
        var driverCount = this.metadataStore.OfType<DriverUpdate>().Count();
        var classificationCount = this.metadataStore.OfType<ClassificationCategory>().Count();
        var productCount = this.metadataStore.OfType<ProductCategory>().Count();

        return new DetailedStoreStatus
        {
            TotalPackageCount = packageCount,
            UpdateCount = updateCount,
            DriverCount = driverCount,
            ClassificationCount = classificationCount,
            ProductCount = productCount,
            PackageIdIndexed = this.metadataStore is IMetadataStore,
            ReindexingRequired = this.metadataStore.IsReindexingRequired,
            Timestamp = DateTime.UtcNow
        };
    }

    public MetadataFilter? BuildFilterFromRequest(IMetadataFilterRequest request)
    {
        try
        {
            var filter = new MetadataFilter
            {
                TitleFilter = request.TitleFilter,
                HardwareIdFilter = request.HardwareIdFilter,
                SkipSuperseded = request.SkipSuperseded,
                FirstX = request.FirstX
            };

            // Parse KB article filter
            if (request.KbArticleFilter != null && request.KbArticleFilter.Any())
            {
                filter.KbArticleFilter = request.KbArticleFilter.ToList();
            }

            // Parse computer hardware ID filter
            if (!string.IsNullOrEmpty(request.ComputerHardwareIdFilter))
            {
                if (!Guid.TryParse(request.ComputerHardwareIdFilter, out Guid computerHardwareIdFilterGuid))
                {
                    this.logger.LogError($"Invalid computer hardware ID GUID: {request.ComputerHardwareIdFilter}");
                    return null;
                }
                filter.ComputerHardwareIdFilter = computerHardwareIdFilterGuid;
            }

            // Parse classification and product filters
            var categoryGuids = new List<Guid>();
            
            if (request.ClassificationsFilter != null)
            {
                foreach (var classification in request.ClassificationsFilter)
                {
                    if (!Guid.TryParse(classification, out Guid classificationGuid))
                    {
                        this.logger.LogError($"Invalid classification GUID: {classification}");
                        return null;
                    }
                    categoryGuids.Add(classificationGuid);
                }
            }

            if (request.ProductsFilter != null)
            {
                foreach (var product in request.ProductsFilter)
                {
                    if (!Guid.TryParse(product, out Guid productGuid))
                    {
                        this.logger.LogError($"Invalid product GUID: {product}");
                        return null;
                    }
                    categoryGuids.Add(productGuid);
                }
            }

            filter.CategoryFilter = categoryGuids;

            // Parse ID filter
            if (request.IdFilter != null)
            {
                var idGuids = new List<Guid>();
                foreach (var id in request.IdFilter)
                {
                    if (!Guid.TryParse(id, out Guid idGuid))
                    {
                        this.logger.LogError($"Invalid ID GUID: {id}");
                        return null;
                    }
                    idGuids.Add(idGuid);
                }
                filter.IdFilter = idGuids;
            }

            return filter;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to build filter from request");
            return null;
        }
    }
}