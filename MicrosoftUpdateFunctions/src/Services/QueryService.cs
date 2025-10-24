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

    public async Task<MetadataQueryResult> QueryMetadataAsync(MetadataQueryRequest request)
    {
        this.logger.LogInformation("Executing metadata query with {MaxResults} max results", request.MaxResults);
        
        var startTime = DateTime.UtcNow;
        var result = new MetadataQueryResult
        {
            PackageType = "Updates",
            RequestTimestamp = request.RequestTimestamp
        };

        try
        {
            // Build filter from request
            var filterRequest = new MetadataFilterRequest
            {
                ProductsFilter = request.ProductFilters,
                ClassificationsFilter = request.ClassificationFilters,
                TitleFilter = request.SearchTerm,
                SkipSuperseded = !request.IncludeSuperseded,
                FirstX = request.MaxResults
            };

            var filter = this.BuildFilterFromRequest(filterRequest);
            if (filter == null)
            {
                result.TotalMatches = 0;
                return result;
            }

            var filteredPackages = filter.Apply(this.metadataStore);
            result.TotalMatches = filteredPackages.Count();
            
            foreach (var package in filteredPackages.Take(request.MaxResults))
            {
                var packageInfo = new PackageInfo
                {
                    Id = new Guid(package.Id.OpenId),
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
                    packageInfo.CreationDate = updatePackage.CreationDate;
                }

                result.Packages.Add(packageInfo);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during metadata query");
            throw;
        }

        result.QueryDuration = DateTime.UtcNow - startTime;
        return result;
    }

    public async Task<DetailedStoreStatus> GetStoreStatusAsync()
    {
        this.logger.LogInformation("Getting detailed store status");
        
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
            LastUpdated = DateTime.UtcNow,
            Timestamp = DateTime.UtcNow
        };
    }

    public async Task<DriverMatchResult> MatchDriversAsync(DriverMatchRequest request)
    {
        this.logger.LogInformation("Matching drivers for {HardwareIdCount} hardware IDs", request.HardwareIds.Count);
        
        try
        {
            var driverMatching = DriverUpdateMatching.FromPackageSource(this.metadataStore);
            var computerHardwareIds = new List<Guid>(); // Could be parsed from request if needed
            var prerequisites = new List<Guid>(); // Could be parsed from request if needed
            
            var driverMatch = driverMatching.MatchDriver(request.HardwareIds, computerHardwareIds, prerequisites);

            if (driverMatch != null)
            {
                return new DriverMatchResult
                {
                    MatchFound = true,
                    DriverId = driverMatch.Driver.Id.OpenId,
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
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during driver matching");
            throw;
        }
    }

    public async Task<AvailableFilters> GetAvailableFiltersAsync()
    {
        this.logger.LogInformation("Getting available filter options");
        
        var products = this.metadataStore.OfType<ProductCategory>()
            .Select(p => p.Title)
            .Where(title => !string.IsNullOrEmpty(title))
            .Distinct()
            .ToList();

        var classifications = this.metadataStore.OfType<ClassificationCategory>()
            .Select(c => c.Title)
            .Where(title => !string.IsNullOrEmpty(title))
            .Distinct()
            .ToList();

        return new AvailableFilters
        {
            Products = products,
            Classifications = classifications,
            LastUpdated = DateTime.UtcNow
        };
    }

    public async Task<MetadataExportResult> ExportMetadataAsync(MetadataExportRequest request)
    {
        this.logger.LogInformation("Exporting metadata in {Format} format", request.Format);
        
        var startTime = DateTime.UtcNow;
        var result = new MetadataExportResult
        {
            Format = request.Format,
            ExportTimestamp = startTime
        };

        try
        {
            // Build filter from request
            var filterRequest = new MetadataFilterRequest
            {
                ProductsFilter = request.ProductsFilter,
                ClassificationsFilter = request.ClassificationsFilter,
                SkipSuperseded = !request.IncludeSuperseded,
                FirstX = int.MaxValue // Export all matching items
            };

            var filter = this.BuildFilterFromRequest(filterRequest);
            if (filter == null)
            {
                result.Success = false;
                result.ErrorMessage = "Invalid filter parameters";
                return result;
            }

            var filteredPackages = filter.Apply(this.metadataStore);
            var packages = filteredPackages.ToList();
            
            result.ItemsExported = packages.Count;

            // Convert to export format based on request.Format
            switch (request.Format.ToLowerInvariant())
            {
                case "json":
                    result.ExportData = System.Text.Json.JsonSerializer.Serialize(packages.Select(p => new
                    {
                        Id = p.Id.OpenId,
                        Title = p.Title,
                        Type = p.GetType().Name
                    }), new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    break;
                    
                case "csv":
                    result.ExportData = "Id,Title,Type\n" + 
                        string.Join("\n", packages.Select(p => $"{p.Id.OpenId},{p.Title},{p.GetType().Name}"));
                    break;
                    
                default:
                    result.Success = false;
                    result.ErrorMessage = $"Unsupported export format: {request.Format}";
                    return result;
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during metadata export");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
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
                Id = package.Id.OpenId,
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
                DriverId = driverMatch.Driver.Id.OpenId,
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
            LastUpdated = DateTime.UtcNow,
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

/// <summary>
/// Implementation of IMetadataFilterRequest for internal use.
/// </summary>
public class MetadataFilterRequest : IMetadataFilterRequest
{
    public IEnumerable<string>? ProductsFilter { get; set; }
    public IEnumerable<string>? ClassificationsFilter { get; set; }
    public IEnumerable<string>? IdFilter { get; set; }
    public string? TitleFilter { get; set; }
    public string? HardwareIdFilter { get; set; }
    public string? ComputerHardwareIdFilter { get; set; }
    public IEnumerable<string>? KbArticleFilter { get; set; }
    public bool SkipSuperseded { get; set; }
    public int FirstX { get; set; }
}