// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MicrosoftUpdateFunctions.Services;

using Microsoft.PackageGraph.MicrosoftUpdate.Source;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// Shared models for all Azure Functions and services.
/// </summary>

public class SyncMetadataRequest
{
    public bool SyncCategories { get; set; } = true;
    public bool SyncUpdates { get; set; } = true;
    public string? FilterType { get; set; }
    public CustomFilter? CustomFilters { get; set; }
}

public class SyncContentRequest
{
    public List<string>? ProductFilters { get; set; }
    public List<string>? ClassificationFilters { get; set; }
    public DateTime? UpdatedAfter { get; set; }
    public int MaxItems { get; set; } = 1000;
}

public class CustomFilter
{
    public List<string>? ProductFilters { get; set; }
    public List<string>? ClassificationFilters { get; set; }
}

public class SyncResult
{
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool CategoriesSynced { get; set; }
    public bool UpdatesSynced { get; set; }
    public bool ContentSynced { get; set; }
    public string? ErrorMessage { get; set; }
    public int ItemsProcessed { get; set; }
}

public class MetadataQueryRequest
{
    public List<string>? ProductFilters { get; set; }
    public List<string>? ClassificationFilters { get; set; }
    public string? SearchTerm { get; set; }
    public DateTime? UpdatedAfter { get; set; }
    public DateTime? UpdatedBefore { get; set; }
    public bool IncludeSuperseded { get; set; } = false;
    public int MaxResults { get; set; } = 100;
    public DateTime RequestTimestamp { get; set; } = DateTime.UtcNow;
}

public class MetadataQueryResult
{
    public string PackageType { get; set; } = string.Empty;
    public int TotalMatches { get; set; }
    public List<PackageInfo> Packages { get; set; } = new();
    public DateTime RequestTimestamp { get; set; } = DateTime.UtcNow;
    public TimeSpan QueryDuration { get; set; }
}

public class PackageInfo
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string PackageType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long Size { get; set; }
    public string? Classification { get; set; }
    public string? Product { get; set; }
    public string? KbArticle { get; set; }
    public bool IsSuperseded { get; set; }
    public DateTime CreationDate { get; set; }
}

public class DriverMatchRequest
{
    [Required]
    public List<string> HardwareIds { get; set; } = new();
    public string? OperatingSystem { get; set; }
    public string? Architecture { get; set; }
    public DateTime? MinDriverDate { get; set; }
}

public class DriverMatchResult
{
    public bool MatchFound { get; set; }
    public Guid? DriverId { get; set; }
    public string? DriverTitle { get; set; }
    public string? MatchedHardwareId { get; set; }
    public string? DriverVersion { get; set; }
    public DateTime? DriverDate { get; set; }
    public Guid? MatchedComputerHardwareId { get; set; }
    public byte? FeatureScore { get; set; }
    public int? OperatingSystem { get; set; }
}

public class HealthStatus
{
    public bool IsHealthy { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<HealthIssue>? Issues { get; set; }
    public List<HealthMetric>? Metrics { get; set; }
}

public class HealthIssue
{
    public string Component { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
}

public class HealthMetric
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Unit { get; set; }
}

public class DetailedStoreStatus
{
    public int TotalPackageCount { get; set; }
    public int UpdateCount { get; set; }
    public int DriverCount { get; set; }
    public int ClassificationCount { get; set; }
    public int ProductCount { get; set; }
    public bool PackageIdIndexed { get; set; }
    public bool ReindexingRequired { get; set; }
    public DateTime LastUpdated { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class AvailableFilters
{
    public List<string> Products { get; set; } = new();
    public List<string> Classifications { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class PrioritySyncRequest
{
    public int Priority { get; set; } = 1;  // Changed from string to int
    public List<string>? ProductFilters { get; set; }
    public List<string>? ClassificationFilters { get; set; }
    public bool IncludeContent { get; set; } = false;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string? RequestedBy { get; set; }
    public string SyncType { get; set; } = string.Empty;
    public string? UpstreamEndpoint { get; set; }
    public int? MaxItems { get; set; }
    public string? Reason { get; set; }

    public StandardSyncRequest ToStandardRequest()
    {
        return new StandardSyncRequest
        {
            SyncType = this.SyncType,
            UpstreamEndpoint = this.UpstreamEndpoint,
            ProductFilters = this.ProductFilters,
            ClassificationFilters = this.ClassificationFilters,
            MaxItems = this.MaxItems
        };
    }
}

public class StandardSyncRequest
{
    public List<string>? ProductFilters { get; set; }
    public List<string>? ClassificationFilters { get; set; }
    public bool SyncCategories { get; set; } = true;
    public bool SyncUpdates { get; set; } = true;
    public bool SyncContent { get; set; } = false;
    public string SyncType { get; set; } = string.Empty;
    public string? UpstreamEndpoint { get; set; }
    public int? MaxItems { get; set; }
    public string? RequestId { get; set; }
}

public class ContentSyncQueueRequest
{
    public List<string>? ProductFilters { get; set; }
    public List<string>? ClassificationFilters { get; set; }
    public DateTime? UpdatedAfter { get; set; }
    public int BatchSize { get; set; } = 100;
    public int Priority { get; set; } = 1;  // Changed from string to int
    public string ContentStorePath { get; set; } = string.Empty;
    public string ContentStoreType { get; set; } = "local";
    public string? ContentStoreConnectionString { get; set; }
    public bool SkipSuperseded { get; set; } = true;
    public int? MaxFiles { get; set; }
}

public class EmergencySyncRequest
{
    public string Reason { get; set; } = string.Empty;
    public List<string>? SpecificUpdateIds { get; set; }
    public bool ForceSync { get; set; } = true;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string? RequestedBy { get; set; }
    [Required]
    public string SyncType { get; set; } = string.Empty;
    public string? UpstreamEndpoint { get; set; }
    public List<string>? ProductFilters { get; set; }
    public List<string>? ClassificationFilters { get; set; }
    public bool IncludeContent { get; set; } = false;
}

public class QueuedSyncRequest : StandardSyncRequest
{
    public string QueueId { get; set; } = Guid.NewGuid().ToString();
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; } = 0;
    public int Priority { get; set; } = 1;  // Changed from string to int
    public DateTime? ScheduledTime { get; set; }
    public string? CallbackUrl { get; set; }
}

public class AutomationStatus
{
    public bool MetadataStoreConfigured { get; set; }
    public bool ContentStoreConfigured { get; set; }
    public DateTime? LastDailySync { get; set; }
    public DateTime? LastWeeklySync { get; set; }
    public DateTime? NextScheduledSync { get; set; }
    public Dictionary<string, int> QueueDepths { get; set; } = new();
    public string SystemHealth { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Simple metadata filter for internal service use (avoids conflicts with library MetadataFilter).
/// </summary>
public class ServiceMetadataFilter
{
    public List<string>? ProductFilters { get; set; }
    public List<string>? ClassificationFilters { get; set; }
    public DateTime? UpdatedAfter { get; set; }
    public DateTime? UpdatedBefore { get; set; }
}

/// <summary>
/// Extension methods to convert between model types.
/// </summary>
public static class FilterExtensions
{
    public static ServiceMetadataFilter ToServiceFilter(this MetadataQueryRequest request)
    {
        return new ServiceMetadataFilter
        {
            ProductFilters = request.ProductFilters,
            ClassificationFilters = request.ClassificationFilters,
            UpdatedAfter = request.UpdatedAfter,
            UpdatedBefore = request.UpdatedBefore
        };
    }
    
    public static ServiceMetadataFilter ToServiceFilter(this SyncContentRequest request)
    {
        return new ServiceMetadataFilter
        {
            ProductFilters = request.ProductFilters,
            ClassificationFilters = request.ClassificationFilters,
            UpdatedAfter = request.UpdatedAfter
        };
    }
}

public class MetadataExportRequest
{
    public List<string>? ProductsFilter { get; set; }
    public List<string>? ClassificationsFilter { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public string Format { get; set; } = "json";
    public bool IncludeSuperseded { get; set; } = false;
    public bool IncludeContent { get; set; } = false;
    public string? FileName { get; set; }
}

public class MetadataExportResult
{
    public bool Success { get; set; }
    public string? ExportData { get; set; }
    public int ItemsExported { get; set; }
    public string Format { get; set; } = string.Empty;
    public DateTime ExportTimestamp { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}