// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MicrosoftUpdateFunctions.Services;

using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.MicrosoftUpdate.Source;
using Microsoft.PackageGraph.MicrosoftUpdate.Metadata;

/// <summary>
/// Service interface for metadata and content synchronization operations.
/// Provides testable, reusable sync logic shared across all Azure Functions.
/// </summary>
public interface ISyncService
{
    Task SyncCategoriesAsync(CancellationToken cancellationToken = default);
    Task SyncUpdatesAsync(UpstreamSourceFilter filter, CancellationToken cancellationToken = default);
    Task SyncContentAsync(MetadataFilter filter, IContentStore contentStore, CancellationToken cancellationToken = default);
    Task<bool> IsReindexingRequired();
    Task ReindexStoreAsync(CancellationToken cancellationToken = default);
    UpstreamSourceFilter CreateCriticalUpdatesFilter();
    UpstreamSourceFilter CreateComprehensiveUpdatesFilter();
    UpstreamSourceFilter CreateCustomFilter(List<string>? productFilters, List<string>? classificationFilters);
}