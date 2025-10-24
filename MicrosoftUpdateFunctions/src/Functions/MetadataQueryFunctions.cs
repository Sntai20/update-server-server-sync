// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MicrosoftUpdateFunctions.Functions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MicrosoftUpdateFunctions.Services;
using System.Net;
using System.Text.Json;

/// <summary>
/// Azure Functions for metadata query and analysis operations.
/// Provides read-only access to stored metadata using the service layer.
/// </summary>
public class MetadataQueryFunctions
{
    private readonly ILogger<MetadataQueryFunctions> logger;
    private readonly IQueryService queryService;

    public MetadataQueryFunctions(
        ILogger<MetadataQueryFunctions> logger,
        IQueryService queryService)
    {
        this.logger = logger;
        this.queryService = queryService;
    }

    /// <summary>
    /// Query metadata with flexible filtering options.
    /// POST /api/QueryMetadata
    /// </summary>
    [Function("QueryMetadata")]
    public async Task<HttpResponseData> QueryMetadata(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        this.logger.LogInformation("Metadata query requested");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var queryRequest = JsonSerializer.Deserialize<MetadataQueryRequest>(requestBody) ?? new MetadataQueryRequest();

            var results = await this.queryService.QueryMetadataAsync(queryRequest);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(results);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during metadata query");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }

    /// <summary>
    /// Get detailed metadata store status and statistics.
    /// GET /api/StoreStatus
    /// </summary>
    [Function("GetStoreStatus")]
    public async Task<HttpResponseData> GetStoreStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        try
        {
            var status = await this.queryService.GetStoreStatusAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(status);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error retrieving store status");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }

    /// <summary>
    /// Search for driver updates based on hardware information.
    /// POST /api/MatchDrivers
    /// </summary>
    [Function("MatchDrivers")]
    public async Task<HttpResponseData> MatchDrivers(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        this.logger.LogInformation("Driver matching requested");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var matchRequest = JsonSerializer.Deserialize<DriverMatchRequest>(requestBody) ?? new DriverMatchRequest();

            var matches = await this.queryService.MatchDriversAsync(matchRequest);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(matches);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during driver matching");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }

    /// <summary>
    /// Get available product and classification filters.
    /// GET /api/AvailableFilters
    /// </summary>
    [Function("GetAvailableFilters")]
    public async Task<HttpResponseData> GetAvailableFilters(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        try
        {
            var filters = await this.queryService.GetAvailableFiltersAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(filters);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error retrieving available filters");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }

    /// <summary>
    /// Export metadata in various formats.
    /// POST /api/ExportMetadata
    /// </summary>
    [Function("ExportMetadata")]
    public async Task<HttpResponseData> ExportMetadata(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        this.logger.LogInformation("Metadata export requested");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var exportRequest = JsonSerializer.Deserialize<MetadataExportRequest>(requestBody) ?? new MetadataExportRequest();

            var exportData = await this.queryService.ExportMetadataAsync(exportRequest);

            var response = req.CreateResponse(HttpStatusCode.OK);
            
            // Set appropriate content type based on export format
            response.Headers.Add("Content-Type", exportRequest.Format?.ToLower() switch
            {
                "csv" => "text/csv",
                "xml" => "application/xml",
                "json" => "application/json",
                _ => "application/json"
            });

            if (!string.IsNullOrEmpty(exportRequest.FileName))
            {
                response.Headers.Add("Content-Disposition", $"attachment; filename=\"{exportRequest.FileName}\"");
            }

            await response.WriteStringAsync(exportData);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during metadata export");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }
}

/// <summary>
/// Request model for metadata query operations
/// </summary>
public class MetadataQueryRequest
{
    [Required]
    public string PackageType { get; set; } = "microsoft-update";
    
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

/// <summary>
/// Request model for driver matching operations
/// </summary>
public class DriverMatchRequest
{
    [Required]
    public IEnumerable<string> HardwareIds { get; set; } = new List<string>();
    
    public IEnumerable<string>? ComputerHardwareIds { get; set; }
    
    public IEnumerable<string>? InstalledPrerequisites { get; set; }
}

/// <summary>
/// Result model for metadata query operations
/// </summary>
public class MetadataQueryResult
{
    public string PackageType { get; set; } = string.Empty;
    public int TotalMatches { get; set; }
    public List<PackageInfo> Packages { get; set; } = new();
}

/// <summary>
/// Package information model
/// </summary>
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

/// <summary>
/// Result model for driver matching operations
/// </summary>
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

/// <summary>
/// Detailed store status model
/// </summary>
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