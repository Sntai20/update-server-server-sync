// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MicrosoftUpdateFunctions.Services;

/// <summary>
/// Shared models for metadata query operations.
/// </summary>
public class MetadataQueryResult
{
    public string PackageType { get; set; } = string.Empty;
    public int TotalMatches { get; set; }
    public List<PackageInfo> Packages { get; set; } = new();
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

public class DetailedStoreStatus
{
    public int TotalPackageCount { get; set; }
    public int UpdateCount { get; set; }
    public int DriverCount { get; set; }
    public int ClassificationCount { get; set; }
    public int ProductCount { get; set; }
    public bool PackageIdIndexed { get; set; }
    public bool ReindexingRequired { get; set; }
    public DateTime Timestamp { get; set; }
}