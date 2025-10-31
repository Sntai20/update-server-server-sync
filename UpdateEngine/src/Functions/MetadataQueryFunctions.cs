// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Functions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using UpdateEngine.Services;
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
}