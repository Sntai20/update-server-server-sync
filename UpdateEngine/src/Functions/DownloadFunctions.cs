// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Functions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.ObjectModel;
using Microsoft.PackageGraph.Storage;
using System.Net;
using System.Text.Json;
using UpdateEngine.Services;

/// <summary>
/// Functions for downloading updates and metadata from the UpdateEngine.
/// </summary>
public class DownloadFunctions
{
    private const string ContentTypeJson = "application/json";
    private const string ContentTypeOctetStream = "application/octet-stream";
    private const string ContentTypeZip = "application/zip";
    private const string ContentDisposition = "Content-Disposition";

    private readonly ILogger logger;
    private readonly IQueryService queryService;
    private readonly IContentStore? contentStore;

    public DownloadFunctions(ILoggerFactory loggerFactory, IQueryService queryService, IContentStore? contentStore = null)
    {
        this.logger = loggerFactory.CreateLogger<DownloadFunctions>();
        this.queryService = queryService;
        this.contentStore = contentStore;
    }

    /// <summary>
    /// Downloads an update package by ID in JSON format.
    /// </summary>
    [Function("DownloadUpdateMetadata")]
    public async Task<HttpResponseData> DownloadUpdateMetadata(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "download/metadata/{updateId}")] HttpRequestData req,
        string updateId)
    {
        this.logger.LogInformation("Download metadata requested for update: {UpdateId}", updateId);

        try
        {
            // Search for the update by ID in the title or description (as a fallback approach)
            var queryRequest = new MetadataQueryRequest
            {
                SearchTerm = updateId,
                MaxResults = 10,
                IncludeSuperseded = true
            };

            var queryResult = await this.queryService.QueryMetadataAsync(queryRequest);
            
            if (queryResult?.Packages?.Any() != true)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Update not found: {updateId}");
                return notFoundResponse;
            }

            // Try to find exact match by ID first, or take the first result
            var update = queryResult.Packages.FirstOrDefault(p => p.Id.ToString().Equals(updateId, StringComparison.OrdinalIgnoreCase))
                        ?? queryResult.Packages.First();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", ContentTypeJson);
            response.Headers.Add(ContentDisposition, $"attachment; filename=\"{updateId}_metadata.json\"");
            
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(update, jsonOptions);
            await response.WriteStringAsync(json);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error downloading metadata for update: {UpdateId}", updateId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Internal server error: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Downloads update content (files) by update ID.
    /// </summary>
    [Function("DownloadUpdateContent")]
    public async Task<HttpResponseData> DownloadUpdateContent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "download/content/{updateId}")] HttpRequestData req,
        string updateId)
    {
        this.logger.LogInformation("Download content requested for update: {UpdateId}", updateId);

        if (this.contentStore == null)
        {
            var noContentResponse = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            await noContentResponse.WriteStringAsync("Content store not configured");
            return noContentResponse;
        }

        try
        {
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotImplemented);
            await notFoundResponse.WriteStringAsync("Content download not yet implemented - requires access to detailed update information with file metadata");
            return notFoundResponse;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error downloading content for update: {UpdateId}", updateId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Internal server error: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Lists available downloads for an update.
    /// </summary>
    [Function("ListUpdateDownloads")]
    public async Task<HttpResponseData> ListUpdateDownloads(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "download/list/{updateId}")] HttpRequestData req,
        string updateId)
    {
        this.logger.LogInformation("List downloads requested for update: {UpdateId}", updateId);

        try
        {
            // Search for the update by ID
            var queryRequest = new MetadataQueryRequest
            {
                SearchTerm = updateId,
                MaxResults = 10,
                IncludeSuperseded = true
            };

            var queryResult = await this.queryService.QueryMetadataAsync(queryRequest);
            
            if (queryResult?.Packages?.Any() != true)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Update not found: {updateId}");
                return notFoundResponse;
            }

            // Try to find exact match by ID first, or take the first result
            var update = queryResult.Packages.FirstOrDefault(p => p.Id.ToString().Equals(updateId, StringComparison.OrdinalIgnoreCase))
                        ?? queryResult.Packages.First();
            
            var downloadInfo = new
            {
                UpdateId = updateId,
                Title = update.Title,
                MetadataAvailable = true,
                ContentAvailable = this.contentStore != null,
                Note = "Content download functionality requires access to detailed file metadata which is not available through the current query service"
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", ContentTypeJson);
            
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(downloadInfo, jsonOptions);
            await response.WriteStringAsync(json);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error listing downloads for update: {UpdateId}", updateId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Internal server error: {ex.Message}");
            return errorResponse;
        }
    }
}