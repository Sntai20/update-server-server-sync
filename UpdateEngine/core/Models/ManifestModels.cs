// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core.Models;

using UpdateEngine.Core.Services;

public class MetadataExportRequest : IMetadataFilterRequest
{
    public string? ServerConfigJson { get; set; }
    public string Format { get; set; } = "wsus";
    public IEnumerable<string>? ProductsFilter { get; set; }
    public IEnumerable<string>? ClassificationsFilter { get; set; }
    public IEnumerable<string>? IdFilter { get; set; }
    public string? TitleFilter { get; set; }
    public string? HardwareIdFilter { get; set; }
    public string? ComputerHardwareIdFilter { get; set; }
    public IEnumerable<string>? KbArticleFilter { get; set; }
    public bool SkipSuperseded { get; set; } = false;
    public int FirstX { get; set; } = 0;
}

public class MetadataExportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int PackagesExported { get; set; }
    public string Format { get; set; } = string.Empty;
    public DateTime ExportTimestamp { get; set; }
}

public class ManifestVerifyRequest
{
    public string? ManifestBlobPath { get; set; }
}
