// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MicrosoftUpdateFunctions.Services;

using Microsoft.PackageGraph.MicrosoftUpdate.Metadata;
using Microsoft.PackageGraph.MicrosoftUpdate.Metadata.Drivers;

/// <summary>
/// Service interface for metadata querying operations.
/// </summary>
public interface IQueryService
{
    MetadataQueryResult QueryPackages(string packageType, MetadataFilter filter);
    DriverMatchResult MatchDriver(IEnumerable<string> hardwareIds, List<Guid> computerHardwareIds, List<Guid> prerequisites);
    DetailedStoreStatus GetDetailedStoreStatus();
    MetadataFilter? BuildFilterFromRequest(IMetadataFilterRequest request);
    Task<MetadataQueryResult> QueryMetadataAsync(MetadataQueryRequest request);
    Task<DetailedStoreStatus> GetStoreStatusAsync();
    Task<DriverMatchResult> MatchDriversAsync(DriverMatchRequest request);
    Task<AvailableFilters> GetAvailableFiltersAsync();
    Task<MetadataExportResult> ExportMetadataAsync(MetadataExportRequest request);
}

/// <summary>
/// Interface for objects that can provide metadata filter parameters.
/// </summary>
public interface IMetadataFilterRequest
{
    IEnumerable<string>? ProductsFilter { get; }
    IEnumerable<string>? ClassificationsFilter { get; }
    IEnumerable<string>? IdFilter { get; }
    string? TitleFilter { get; }
    string? HardwareIdFilter { get; }
    string? ComputerHardwareIdFilter { get; }
    IEnumerable<string>? KbArticleFilter { get; }
    bool SkipSuperseded { get; }
    int FirstX { get; }
}