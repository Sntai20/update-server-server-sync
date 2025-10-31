// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Functions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.ObjectModel;
using UpdateEngine.Services;
using Configuration;
using System.Net;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

/// <summary>
/// Azure Functions for content synchronization operations.
/// Handles downloading and managing update content files using the service layer.
/// </summary>
public class ContentSyncFunctions
{
    private readonly ILogger<ContentSyncFunctions> logger;
    private readonly ISyncService syncService;
    private readonly IContentStore? contentStore;
    private readonly ServiceConfigurationMutable serviceConfiguration;

    public ContentSyncFunctions(
        ILogger<ContentSyncFunctions> logger,
        ISyncService syncService,
        IContentStore? contentStore,
        IOptions<ServiceConfigurationMutable> serviceConfiguration)
    {
        this.logger = logger;
        this.syncService = syncService;
        this.contentStore = contentStore;
        this.serviceConfiguration = serviceConfiguration.Value;
    }

    /// <summary>
    /// HTTP endpoint for content synchronization.
    /// POST /api/SyncContent
    /// </summary>
    [Function("SyncContent")]
    public async Task<HttpResponseData> SyncContent(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        this.logger.LogInformation("Content sync requested");

        if (this.contentStore == null)
        {
            var noStoreResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await noStoreResponse.WriteAsJsonAsync(new { error = "Content store not configured" });
            return noStoreResponse;
        }

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<Services.SyncContentRequest>(requestBody) ?? new Services.SyncContentRequest();

            // Create service metadata filter
            var filter = request.ToServiceFilter();

            var result = new Services.SyncResult { StartTime = DateTime.UtcNow };

            // Perform content synchronization using service layer
            await this.syncService.SyncContentAsync(filter, this.contentStore);

            result.EndTime = DateTime.UtcNow;
            result.Success = true;
            result.ContentSynced = true;

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during content synchronization");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }

    /// <summary>
    /// Scheduled weekly content synchronization.
    /// Runs every Sunday at 3 AM UTC to sync content for recent updates.
    /// </summary>
    [Function("SyncContentScheduled")]
    public async Task SyncContentScheduled([TimerTrigger("%SyncContentSchedule%")] TimerInfo timer)
    {
        this.logger.LogInformation("Starting scheduled content sync at {Time}", DateTime.UtcNow);

        if (this.contentStore == null)
        {
            this.logger.LogWarning("Content store not configured, skipping scheduled content sync");
            return;
        }

        try
        {
            // Sync content for recent updates (last 30 days)
            var filter = new ServiceMetadataFilter
            {
                UpdatedAfter = DateTime.UtcNow.AddDays(-30)
            };

            await this.syncService.SyncContentAsync(filter, this.contentStore);

            this.logger.LogInformation("Scheduled content sync completed successfully");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during scheduled content sync");
            throw;
        }
    }

    /// <summary>
    /// Get content storage status and statistics.
    /// GET /api/ContentStatus
    /// </summary>
    [Function("QueryContentStatus")]
    public async Task<HttpResponseData> QueryContentStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        try
        {
            if (this.contentStore == null)
            {
                var noStoreResponse = req.CreateResponse(HttpStatusCode.OK);
                await noStoreResponse.WriteAsJsonAsync(new { configured = false, message = "Content store not configured" });
                return noStoreResponse;
            }

            // Get content store statistics
            var status = new
            {
                configured = true,
                type = this.contentStore.GetType().Name,
                timestamp = DateTime.UtcNow
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(status);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error retrieving content status");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }
}

/// <summary>
/// Request model for content synchronization operations
/// </summary>
public class ContentSyncRequest
{
    [Required]
    public string ContentStorePath { get; set; } = string.Empty;

    public string ContentStoreType { get; set; } = "local";

    public string? ContentStoreConnectionString { get; set; }

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
/// Result model for content synchronization operations
/// </summary>
public class ContentSyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int FilesProcessed { get; set; }
    public long TotalBytes { get; set; }
}

/// <summary>
/// Progress tracker for content sync operations
/// </summary>
internal class ContentSyncProgressTracker
{
    private readonly ILogger logger;
    private string lastFileDigest = string.Empty;

    public ContentSyncProgressTracker(ILogger logger)
    {
        this.logger = logger;
    }

    public void OnProgress(object? sender, ContentOperationProgress e)
    {
        if (e.File.Digest.DigestBase64 != this.lastFileDigest)
        {
            this.logger.LogInformation($"Starting download of file: {e.File.Digest.DigestBase64}");
            this.lastFileDigest = e.File.Digest.DigestBase64;
        }

        switch (e.CurrentOperation)
        {
            case PackagesOperationType.DownloadFileProgress:
                this.logger.LogDebug($"Download progress [{e.Maximum}]: {e.PercentDone:F2}%");
                break;
        }
    }
}