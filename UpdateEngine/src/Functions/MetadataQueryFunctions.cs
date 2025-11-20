// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Functions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.MicrosoftUpdate.Metadata;
using Microsoft.PackageGraph.Storage;
using System.Net;
using System.Text.Json;
using UpdateEngine.Models;
using UpdateEngine.Services;

/// <summary>
/// Azure Functions for metadata query and analysis operations.
/// Provides read-only access to stored metadata using the service layer.
/// </summary>
public class MetadataQueryFunctions
{
    private readonly ILogger<MetadataQueryFunctions> logger;
    private readonly IQueryService queryService;
    private readonly IAnomalyDetectionService? anomalyDetectionService;
    private readonly IMetadataStore? metadataStore;

    public MetadataQueryFunctions(
        ILogger<MetadataQueryFunctions> logger,
        IQueryService queryService,
        IAnomalyDetectionService? anomalyDetectionService = null,
        IMetadataStore? metadataStore = null)
    {
        this.logger = logger;
        this.queryService = queryService;
        this.anomalyDetectionService = anomalyDetectionService;
        this.metadataStore = metadataStore;
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
            var queryRequest = JsonSerializer.Deserialize<Services.MetadataQueryRequest>(requestBody) ?? new Services.MetadataQueryRequest();

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
    /// GET /api/QueryMetadataStoreStatus
    /// </summary>
    [Function("QueryMetadataStoreStatus")]
    public async Task<HttpResponseData> QueryMetadataStoreStatus(
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
            var matchRequest = JsonSerializer.Deserialize<Services.DriverMatchRequest>(requestBody) ?? new Services.DriverMatchRequest();

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
    [Function("QueryAvailableFilters")]
    public async Task<HttpResponseData> QueryAvailableFilters(
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
            var exportRequest = JsonSerializer.Deserialize<Services.MetadataExportRequest>(requestBody) ?? new Services.MetadataExportRequest();

            var exportResult = await this.queryService.ExportMetadataAsync(exportRequest);

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

            await response.WriteStringAsync(exportResult.ExportData ?? string.Empty);
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

    /// <summary>
    /// Analyze metadata for anomalies using Microsoft Update library capabilities.
    /// POST /api/AnalyzeMetadataAnomalies
    /// </summary>
    [Function("AnalyzeMetadataAnomalies")]
    public async Task<HttpResponseData> AnalyzeMetadataAnomalies(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        this.logger.LogInformation("Metadata anomaly analysis requested");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var analysisRequest = JsonSerializer.Deserialize<MetadataAnomalyAnalysisRequest>(requestBody) 
                ?? new MetadataAnomalyAnalysisRequest();

            if (this.anomalyDetectionService == null || this.metadataStore == null)
            {
                var notAvailableResponse = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
                await notAvailableResponse.WriteAsJsonAsync(new { 
                    error = "Anomaly detection service or metadata store not available",
                    message = "Anomaly detection capabilities are not configured"
                });
                return notAvailableResponse;
            }

            // Get software updates for analysis
            var allUpdates = this.metadataStore.OfType<SoftwareUpdate>().ToList();
            var updatesToAnalyze = analysisRequest.MaxUpdates.HasValue 
                ? allUpdates.Take(analysisRequest.MaxUpdates.Value).ToList()
                : allUpdates.Take(100).ToList(); // Default limit

            if (!updatesToAnalyze.Any())
            {
                var noDataResponse = req.CreateResponse(HttpStatusCode.OK);
                await noDataResponse.WriteAsJsonAsync(new MetadataAnomalyAnalysisResult
                {
                    TotalAnalyzed = 0,
                    AnomaliesDetected = 0,
                    HighRiskCount = 0,
                    Anomalies = new List<AnomalyDetails>(),
                    AnalysisTimestamp = DateTime.UtcNow
                });
                return noDataResponse;
            }

            this.logger.LogInformation("Analyzing {Count} software updates for anomalies", updatesToAnalyze.Count);

            var anomalies = new List<AnomalyDetails>();
            var startTime = DateTime.UtcNow;

            foreach (var softwareUpdate in updatesToAnalyze)
            {
                try
                {
                    var anomalyScore = this.anomalyDetectionService.Score(softwareUpdate);
                    
                    if (anomalyScore > (analysisRequest.AnomalyThreshold ?? 0.5))
                    {
                        anomalies.Add(new AnomalyDetails
                        {
                            UpdateId = softwareUpdate.Id.ID,
                            Title = softwareUpdate.Title,
                            Score = anomalyScore,
                            RiskLevel = anomalyScore > 0.8 ? "High" : anomalyScore > 0.5 ? "Medium" : "Low",
                            KBArticleId = softwareUpdate.KBArticleId,
                            Categories = softwareUpdate.Categories?.Select(c => c.ToString()).ToList() ?? new List<string>()
                        });
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Error analyzing update {UpdateId} for anomalies", softwareUpdate.Id.ID);
                }
            }

            var result = new MetadataAnomalyAnalysisResult
            {
                TotalAnalyzed = updatesToAnalyze.Count,
                AnomaliesDetected = anomalies.Count,
                HighRiskCount = anomalies.Count(a => a.RiskLevel == "High"),
                Anomalies = anomalies.OrderByDescending(a => a.Score).ToList(),
                AnalysisTimestamp = DateTime.UtcNow,
                AnalysisDuration = DateTime.UtcNow - startTime,
                AnomalyThreshold = analysisRequest.AnomalyThreshold ?? 0.5
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during metadata anomaly analysis");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }
}

/// <summary>
/// Request model for metadata anomaly analysis
/// </summary>
public class MetadataAnomalyAnalysisRequest
{
    public int? MaxUpdates { get; set; } = 100;
    public double? AnomalyThreshold { get; set; } = 0.5;
}

/// <summary>
/// Result model for metadata anomaly analysis
/// </summary>
public class MetadataAnomalyAnalysisResult
{
    public int TotalAnalyzed { get; set; }
    public int AnomaliesDetected { get; set; }
    public int HighRiskCount { get; set; }
    public List<AnomalyDetails> Anomalies { get; set; } = new();
    public DateTime AnalysisTimestamp { get; set; }
    public TimeSpan AnalysisDuration { get; set; }
    public double AnomalyThreshold { get; set; }
}

/// <summary>
/// Details of a detected anomaly
/// </summary>
public class AnomalyDetails
{
    public Guid UpdateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public double Score { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string? KBArticleId { get; set; }
    public List<string> Categories { get; set; } = new();
}