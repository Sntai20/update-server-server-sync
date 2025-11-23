// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Functions.Core;

using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UpdateEngine.Metadata;
using UpdateEngine.Metadata.Metadata;
using UpdateEngine.Metadata.ObjectModel;
using UpdateEngine.Metadata.Storage;
using Microsoft.UpdateServices.WebServices.ServerSync;
using System.Collections;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using UpdateEngine.Core.Models;
using UpdateEngine.Core.Services;
using UpdateEngine.Functions.Shared;
using UpdateEngine.Helpers;

/// <summary>
/// Consolidated functions for metadata access, querying, and export operations.
/// Combines functionality from MetadataQueryFunctions and MetadataExportFunctions.
/// </summary>
public class MetadataAccessFunctions
{
    private readonly ILogger<MetadataAccessFunctions> logger;
    private readonly IQueryService queryService;
    private readonly IAnomalyDetectionService? anomalyDetectionService;
    private readonly IMetadataStore metadataStore;
    private readonly BlobServiceClient? blobServiceClient;
    private readonly IConfiguration configuration;
    private readonly JsonSerializerOptions jsonOptions;

    public MetadataAccessFunctions(
        ILogger<MetadataAccessFunctions> logger,
        IQueryService queryService,
        IMetadataStore metadataStore,
        JsonSerializerOptions jsonOptions,
        IConfiguration configuration,
        IAnomalyDetectionService? anomalyDetectionService = null,
        BlobServiceClient? blobServiceClient = null)
    {
        this.logger = logger;
        this.queryService = queryService;
        this.anomalyDetectionService = anomalyDetectionService;
        this.metadataStore = metadataStore;
        this.blobServiceClient = blobServiceClient;
        this.configuration = configuration;
        this.jsonOptions = jsonOptions;
    }

    /// <summary>
    /// Query metadata with flexible filtering options.
    /// POST /api/metadata/query
    /// </summary>
    [Function("QueryMetadata")]
    public async Task<HttpResponseData> QueryMetadata(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "metadata/query")] HttpRequestData req)
    {
        return await FunctionHelpers.ExecuteWithErrorHandlingAsync(req, this.logger, async () =>
        {
            var queryRequest = await FunctionHelpers.ParseJsonRequestAsync<MetadataQueryRequest>(req, this.jsonOptions);
            queryRequest ??= new MetadataQueryRequest();

            this.logger.LogInformation("Metadata query requested with {MaxResults} max results", queryRequest.MaxResults);

            var results = await this.queryService.QueryMetadataAsync(queryRequest);
            return results;
        }, this.jsonOptions);
    }

    /// <summary>
    /// Get detailed metadata store status and statistics.
    /// GET /api/metadata/status
    /// </summary>
    [Function("QueryMetadataStoreStatus")]
    public async Task<HttpResponseData> QueryMetadataStoreStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "metadata/status")] HttpRequestData req)
    {
        return await FunctionHelpers.ExecuteWithErrorHandlingAsync(req, this.logger, async () =>
        {
            this.logger.LogInformation("Metadata store status requested");

            var status = await this.queryService.GetStoreStatusAsync();
            return status;
        }, this.jsonOptions);
    }

    /// <summary>
    /// Match drivers based on hardware IDs and prerequisites.
    /// POST /api/metadata/drivers/match
    /// </summary>
    [Function("MatchDrivers")]
    public async Task<HttpResponseData> MatchDrivers(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "metadata/drivers/match")] HttpRequestData req)
    {
        return await FunctionHelpers.ExecuteWithErrorHandlingAsync(req, this.logger, async () =>
        {
            var matchRequest = await FunctionHelpers.ParseJsonRequestAsync<DriverMatchRequest>(req, this.jsonOptions);
            FunctionHelpers.ValidateRequiredParameters(
                ("matchRequest", matchRequest),
                ("hardwareIds", matchRequest?.HardwareIds)
            );

            this.logger.LogInformation("Driver match requested for {HardwareIdCount} hardware IDs", 
                matchRequest!.HardwareIds?.Count() ?? 0);

            var result = await this.queryService.MatchDriversAsync(matchRequest);
            return result;
        }, this.jsonOptions);
    }

    /// <summary>
    /// Get available filter options for metadata queries.
    /// GET /api/metadata/filters
    /// </summary>
    [Function("QueryAvailableFilters")]
    public async Task<HttpResponseData> QueryAvailableFilters(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "metadata/filters")] HttpRequestData req)
    {
        return await FunctionHelpers.ExecuteWithErrorHandlingAsync(req, this.logger, async () =>
        {
            this.logger.LogInformation("Available filters requested");

            var filters = await this.queryService.GetAvailableFiltersAsync();
            return filters;
        }, this.jsonOptions);
    }

    /// <summary>
    /// Export metadata with basic filtering.
    /// POST /api/metadata/export
    /// </summary>
    [Function("ExportMetadata")]
    public async Task<HttpResponseData> ExportMetadata(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "metadata/export")] HttpRequestData req)
    {
        return await FunctionHelpers.ExecuteWithErrorHandlingAsync(req, this.logger, async () =>
        {
            var exportRequest = await FunctionHelpers.ParseJsonRequestAsync<UpdateEngine.Core.Models.MetadataExportRequest>(req, this.jsonOptions);
            FunctionHelpers.ValidateRequiredParameters(
                ("exportRequest", exportRequest)
            );

            this.logger.LogInformation("Basic metadata export requested");

            // Convert UpdateEngine.Core.Models.MetadataExportRequest to UpdateEngine.Core.Services.MetadataExportRequest
            var serviceRequest = new UpdateEngine.Core.Services.MetadataExportRequest
            {
                Format = exportRequest!.Format,
                ProductsFilter = exportRequest.ProductsFilter?.ToList(),
                ClassificationsFilter = exportRequest.ClassificationsFilter?.ToList(),
                IncludeSuperseded = !exportRequest.SkipSuperseded,
                IncludeContent = false,
                FileName = null
            };

            var result = await this.queryService.ExportMetadataAsync(serviceRequest);
            return result;
        }, this.jsonOptions);
    }

    /// <summary>
    /// Export filtered metadata using comprehensive filtering options.
    /// POST /api/metadata/export/advanced
    /// Equivalent to: upsync export
    /// </summary>
    [Function("ExportAdvanced")]
    public async Task<HttpResponseData> ExportMetadataAdvanced(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "metadata/export/advanced")] HttpRequestData req)
    {
        return await FunctionHelpers.ExecuteWithErrorHandlingAsync(req, this.logger, async () =>
        {
            var exportRequest = await FunctionHelpers.ParseJsonRequestAsync<UpdateEngine.Core.Models.MetadataExportRequest>(req, this.jsonOptions);
            FunctionHelpers.ValidateRequiredParameters(
                ("exportRequest", exportRequest)
            );

            this.logger.LogInformation("Advanced metadata export requested with format: {Format}", exportRequest!.Format);

            // Build filter from request
            var filter = this.BuildFilterFromRequest(exportRequest);
            if (filter == null)
            {
                throw new ArgumentException("Invalid filter parameters");
            }

            // Parse server configuration if provided
            ServerSyncConfigData? serverConfig = null;
            if (!string.IsNullOrEmpty(exportRequest.ServerConfigJson))
            {
                try
                {
                    serverConfig = JsonSerializer.Deserialize<ServerSyncConfigData>(exportRequest.ServerConfigJson);
                }
                catch (JsonException ex)
                {
                    throw new ArgumentException($"Invalid server configuration JSON: {ex.Message}");
                }
            }

            // Export based on format
            return exportRequest.Format.ToLowerInvariant() switch
            {
                "wsus" => await this.ExportToWsusAsync(filter, serverConfig),
                "csv" => await this.ExportToCsvAsync(filter),
                "json" => await this.ExportToJsonAsync(filter),
                _ => throw new ArgumentException($"Unsupported export format: {exportRequest.Format}")
            };
        });
    }

    /// <summary>
    /// Export sync summary to CSV format.
    /// POST /api/metadata/export/csv
    /// </summary>
    [Function("ExportToCsv")]
    public async Task<HttpResponseData> ExportSyncSummaryToCsv(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "metadata/export/csv")] HttpRequestData req)
    {
        return await FunctionHelpers.ExecuteWithErrorHandlingAsync(req, this.logger, async () =>
        {
            var exportRequest = await FunctionHelpers.ParseJsonRequestAsync<UpdateEngine.Core.Models.MetadataExportRequest>(req, this.jsonOptions);
            exportRequest ??= new UpdateEngine.Core.Models.MetadataExportRequest();

            this.logger.LogInformation("CSV export requested");

            var filter = this.BuildFilterFromRequest(exportRequest);
            var csvContent = await this.GenerateCsvAsync(filter);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/csv; charset=utf-8");
            response.Headers.Add("Content-Disposition", $"attachment; filename=\"metadata-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv\"");
            
            await response.WriteStringAsync(csvContent, Encoding.UTF8);
            return response;
        });
    }

    /// <summary>
    /// Analyze metadata for anomalies using ML detection.
    /// POST /api/metadata/anomalies
    /// Requires Features:EnableAnomalyDetection=true
    /// </summary>
    [Function("AnalyzeAnomalies")]
    public async Task<HttpResponseData> AnalyzeMetadataAnomalies(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "metadata/anomalies")] HttpRequestData req)
    {
        return await FunctionHelpers.ExecuteWithErrorHandlingAsync(req, this.logger, async () =>
        {
            if (this.anomalyDetectionService == null)
            {
                throw new NotSupportedException("Anomaly detection not enabled. Set Features:EnableAnomalyDetection=true");
            }

            var request = await FunctionHelpers.ParseJsonRequestAsync<AnomalyDetectionRequest>(req, this.jsonOptions);
            request ??= new AnomalyDetectionRequest();

            this.logger.LogInformation("Anomaly detection requested");

            // TODO: Implement proper anomaly detection logic
            // var result = await this.anomalyDetectionService.DetectAnomaliesAsync(request);
            var result = new
            {
                Anomalies = new List<object>(),
                TotalChecked = 0,
                AnomaliesFound = 0,
                Timestamp = DateTime.UtcNow
            };
            return result;
        }, this.jsonOptions);
    }

    #region Private Helper Methods

    private MetadataFilter? BuildFilterFromRequest(IMetadataFilterRequest request)
    {
        return this.queryService.BuildFilterFromRequest(request);
    }

    private async Task<object> ExportToWsusAsync(MetadataFilter? filter, ServerSyncConfigData? serverConfig)
    {
        // TODO: Implement proper query using IQueryService
        // Get filtered packages 
        var mockPackages = new List<object>();

        // Generate WSUS export format
        var wsusExport = new
        {
            Configuration = serverConfig,
            UpdateCount = mockPackages.Count,
            ExportTime = DateTime.UtcNow,
            Updates = new List<object>() // Empty for now.ToList()
        };

        return wsusExport;
    }

    private async Task<object> ExportToCsvAsync(MetadataFilter? filter)
    {
        var csvContent = await this.GenerateCsvAsync(filter);
        return new { Content = csvContent, Format = "CSV" };
    }

    private async Task<object> ExportToJsonAsync(MetadataFilter? filter)
    {
        // TODO: Implement proper query using IQueryService
        var mockUpdates = new List<object>();
        
        var jsonExport = new
        {
            ExportTime = DateTime.UtcNow,
            UpdateCount = mockUpdates.Count,
            Updates = mockUpdates
        };

        return jsonExport;
    }

    private async Task<string> GenerateCsvAsync(MetadataFilter? filter)
    {
        // TODO: Implement proper query using IQueryService
        var csvBuilder = new StringBuilder();

        // CSV header
        csvBuilder.AppendLine("ID,Title,CreationDate,Size,IsSuperseded,Products,Classifications");

        // CSV data rows - empty for now
        // TODO: Implement actual data retrieval

        return csvBuilder.ToString();
    }

    private static string EscapeCsvValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
            
        return value.Replace("\"", "\"\"");
    }

    #endregion

    #region Models for Internal Use

    private class AnomalyDetectionRequest
    {
        public string? AnalysisType { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public double? SensitivityThreshold { get; set; }
    }

    #endregion
}
