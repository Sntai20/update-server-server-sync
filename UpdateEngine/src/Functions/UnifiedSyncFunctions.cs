// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Functions;

using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.MicrosoftUpdate.Source;
using UpdateEngine.Services;
using System.Net;

/// <summary>
/// Unified sync functions consolidating all synchronization operations.
/// Replaces 10+ separate functions with configurable sync type operations.
/// Supports HTTP triggers, scheduled timer triggers, and Service Bus queue triggers.
/// </summary>
public class UnifiedSyncFunctions
{
    private readonly ILogger<UnifiedSyncFunctions> logger;
    private readonly ISyncService syncService;
    private readonly IContentStore? contentStore;
    private readonly JsonSerializerOptions jsonOptions;

    public UnifiedSyncFunctions(
        ILogger<UnifiedSyncFunctions> logger,
        ISyncService syncService,
        JsonSerializerOptions jsonOptions,
        IContentStore? contentStore = null)
    {
        this.logger = logger;
        this.syncService = syncService;
        this.contentStore = contentStore;
        this.jsonOptions = jsonOptions;
    }

    /// <summary>
    /// Universal sync endpoint supporting all sync types via HTTP.
    /// POST /api/UniversalSync
    /// Replaces: SyncMetadata, SyncContent, EmergencySync
    /// </summary>
    [Function("UniversalSync")]
    public async Task<HttpResponseData> UniversalSync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<UniversalSyncRequest>(requestBody ?? "{}", this.jsonOptions);

            var result = await this.ExecuteSyncOperation(request);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(result, this.jsonOptions));
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during universal sync");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }, this.jsonOptions), System.Text.Encoding.UTF8);
            return errorResponse;
        }
    }

    /// <summary>
    /// Scheduled comprehensive metadata sync (categories + updates).
    /// Default: Every 24 hours.
    /// Configure with SyncComprehensiveSchedule app setting (TimeSpan format).
    /// Timer trigger requires AzureWebJobsStorage to be configured.
    /// </summary>
    [Function("SyncComprehensive")]
    public async Task SyncComprehensive(
        [TimerTrigger("%SyncComprehensiveSchedule%")] TimerInfo timer)
    {
        this.logger.LogInformation("Starting scheduled comprehensive sync at {Time}", DateTime.UtcNow);

        try
        {
            var request = new UniversalSyncRequest
            {
                SyncType = "comprehensive",
                SyncContent = false
            };

            await this.ExecuteSyncOperation(request);

            this.logger.LogInformation("Scheduled comprehensive sync completed. Next run: {NextRun}", timer.ScheduleStatus?.Next);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during scheduled comprehensive sync");
            throw;
        }
    }

    /// <summary>
    /// Critical updates sync (security/critical classification).
    /// Default: Every 4 hours.
    /// Configure with SyncCriticalSchedule app setting (TimeSpan format).
    /// </summary>
    [Function("SyncCritical")]
    public async Task SyncCritical(
        [TimerTrigger("%SyncCriticalSchedule%")] TimerInfo timer)
    {
        this.logger.LogInformation("Starting scheduled critical updates sync at {Time}", DateTime.UtcNow);

        try
        {
            var request = new UniversalSyncRequest
            {
                SyncType = "critical"
            };

            await this.ExecuteSyncOperation(request);

            this.logger.LogInformation("Scheduled critical sync completed. Next run: {NextRun}", timer.ScheduleStatus?.Next);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during scheduled critical sync");
            throw;
        }
    }

    /// <summary>
    /// Scheduled content sync for recently updated packages.
    /// Default: Every 24 hours.
    /// Configure with SyncContentSchedule app setting (TimeSpan format).
    /// Only runs if content store is configured.
    /// </summary>
    [Function("SyncContent")]
    public async Task SyncContent(
        [TimerTrigger("%SyncContentSchedule%")] TimerInfo timer)
    {
        if (this.contentStore == null)
        {
            this.logger.LogWarning("Content store not configured, skipping scheduled content sync");
            return;
        }

        this.logger.LogInformation("Starting scheduled content sync at {Time}", DateTime.UtcNow);

        try
        {
            var request = new UniversalSyncRequest
            {
                SyncType = "content",
                ContentDaysBack = 30  // Changed from 7 to 30 to match original
            };

            await this.ExecuteSyncOperation(request);

            this.logger.LogInformation("Scheduled content sync completed. Next run: {NextRun}", timer.ScheduleStatus?.Next);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during scheduled content sync");
            throw;
        }
    }

    /// <summary>
    /// Process priority sync requests from a Service Bus queue.
    /// Handles high-priority synchronization operations with minimal delay.
    /// Configure connection with ServiceBusConnection app setting.
    /// </summary>
    [Function("ProcessPrioritySyncRequest")]
    public async Task ProcessPrioritySyncRequest(
        [ServiceBusTrigger("priority-sync-requests", Connection = "ServiceBusConnection")]
        string queueItem)
    {
        this.logger.LogInformation("Processing priority sync request: {QueueItem}", queueItem);

        try
        {
            var request = JsonSerializer.Deserialize<Services.PrioritySyncRequest>(queueItem, this.jsonOptions);
            if (request == null)
            {
                this.logger.LogError("Invalid priority sync request format");
                return;
            }

            // Create filter based on request
            var filter = this.syncService.CreateCustomFilter(
                request.ProductFilters,
                request.ClassificationFilters);

            // Perform sync based on priority
            switch (request.Priority)
            {
                case 1: // Critical
                case 2: // High
                    await this.syncService.SyncUpdatesAsync(filter);
                    break;
                default: // Normal
                    // Include categories for normal priority
                    await this.syncService.SyncCategoriesAsync();
                    await this.syncService.SyncUpdatesAsync(filter);
                    break;
            }

            // Include content if requested
            if (request.IncludeContent && this.contentStore != null)
            {
                var contentFilter = new ServiceMetadataFilter
                {
                    ProductFilters = request.ProductFilters,
                    ClassificationFilters = request.ClassificationFilters
                };
                await this.syncService.SyncContentAsync(contentFilter, this.contentStore);
            }

            this.logger.LogInformation("Priority sync request completed successfully");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error processing priority sync request");
            throw;
        }
    }

    /// <summary>
    /// Process standard sync requests from a Service Bus queue.
    /// Handles regular synchronization operations with standard priority.
    /// Configure connection with ServiceBusConnection app setting.
    /// </summary>
    [Function("ProcessStandardSyncRequest")]
    public async Task ProcessStandardSyncRequest(
        [ServiceBusTrigger("standard-sync-requests", Connection = "ServiceBusConnection")]
        string queueItem)
    {
        this.logger.LogInformation("Processing standard sync request: {QueueItem}", queueItem);

        try
        {
            var request = JsonSerializer.Deserialize<Services.StandardSyncRequest>(queueItem, this.jsonOptions);
            if (request == null)
            {
                this.logger.LogError("Invalid standard sync request format");
                return;
            }

            // Perform sync based on request
            if (request.SyncCategories)
            {
                await this.syncService.SyncCategoriesAsync();
            }

            if (request.SyncUpdates)
            {
                var filter = this.syncService.CreateCustomFilter(
                    request.ProductFilters,
                    request.ClassificationFilters);
                await this.syncService.SyncUpdatesAsync(filter);
            }

            if (request.SyncContent && this.contentStore != null)
            {
                var contentFilter = new ServiceMetadataFilter
                {
                    ProductFilters = request.ProductFilters,
                    ClassificationFilters = request.ClassificationFilters
                };
                await this.syncService.SyncContentAsync(contentFilter, this.contentStore);
            }

            this.logger.LogInformation("Standard sync request completed successfully");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error processing standard sync request");
            throw;
        }
    }

    /// <summary>
    /// Process content sync requests from a Service Bus queue.
    /// Handles content-only synchronization operations.
    /// Configure connection with ServiceBusConnection app setting.
    /// </summary>
    [Function("ProcessContentSyncRequest")]
    public async Task ProcessContentSyncRequest(
        [ServiceBusTrigger("content-sync-requests", Connection = "ServiceBusConnection")]
        string queueItem)
    {
        this.logger.LogInformation("Processing content sync request: {QueueItem}", queueItem);

        if (this.contentStore == null)
        {
            this.logger.LogWarning("Content store not configured, skipping content sync request");
            return;
        }

        try
        {
            var request = JsonSerializer.Deserialize<Services.ContentSyncQueueRequest>(queueItem, this.jsonOptions);
            if (request == null)
            {
                this.logger.LogError("Invalid content sync request format");
                return;
            }

            var contentFilter = new ServiceMetadataFilter
            {
                ProductFilters = request.ProductFilters,
                ClassificationFilters = request.ClassificationFilters,
                UpdatedAfter = request.UpdatedAfter
            };

            await this.syncService.SyncContentAsync(contentFilter, this.contentStore);

            this.logger.LogInformation("Content sync request completed successfully");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error processing content sync request");
            throw;
        }
    }

    /// <summary>
    /// Query content synchronization status.
    /// GET /api/QueryContentStatus
    /// Replaces: QueryContentStatus from ContentSyncFunctions
    /// </summary>
    [Function("QueryContentStatus")]
    public async Task<HttpResponseData> QueryContentStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        try
        {
            if (this.contentStore == null)
            {
                var noStoreResponse = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
                noStoreResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await noStoreResponse.WriteStringAsync(JsonSerializer.Serialize(new { configured = false, message = "Content store not configured" }, this.jsonOptions), System.Text.Encoding.UTF8);
                return noStoreResponse;
            }

            // Get content store statistics using available properties
            var stats = new
            {
                configured = true,
                queuedCount = this.contentStore.QueuedCount,
                downloadedSize = this.contentStore.DownloadedSize
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(stats, this.jsonOptions), System.Text.Encoding.UTF8);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error querying content status");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }, this.jsonOptions), System.Text.Encoding.UTF8);
            return errorResponse;
        }
    }

    /// <summary>
    /// Core sync execution logic shared by HTTP and timer triggers.
    /// </summary>
    private async Task<SyncResult> ExecuteSyncOperation(UniversalSyncRequest? request)
    {
        var result = new SyncResult
        {
            StartTime = DateTime.UtcNow
        };

        try
        {
            switch (request?.SyncType?.ToLowerInvariant())
            {
                case "content":
                    await this.PerformContentSync(request.ContentDaysBack ?? 30);
                    result.ContentSynced = true;
                    break;

                case "emergency":
                case "critical":
                    await this.PerformCriticalSync();
                    result.UpdatesSynced = true;
                    break;

                case "comprehensive":
                    await this.PerformComprehensiveSync();
                    if (request.SyncContent == true) await this.PerformContentSync();
                    result.CategoriesSynced = true;
                    result.UpdatesSynced = true;
                    result.ContentSynced = request.SyncContent == true;
                    break;

                case "full":
                    await this.PerformFullSync();
                    result.CategoriesSynced = true;
                    result.UpdatesSynced = true;
                    result.ContentSynced = true;
                    break;

                default:
                    throw new ArgumentException($"Unknown sync type: {request?.SyncType}");
            }

            result.EndTime = DateTime.UtcNow;
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.EndTime = DateTime.UtcNow;
            result.Success = false;
            result.ErrorMessage = ex.Message;
            this.logger.LogError(ex, "Sync operation failed: {SyncType}", request?.SyncType);
        }

        return result;
    }

    private async Task PerformCriticalSync()
    {
        var filter = this.syncService.CreateCriticalUpdatesFilter();
        await this.syncService.SyncUpdatesAsync(filter);
    }

    private async Task PerformComprehensiveSync()
    {
        if (await this.syncService.IsReindexingRequired())
        {
            this.logger.LogInformation("Store reindexing required, performing reindex");
            await this.syncService.ReindexStoreAsync();
        }

        await this.syncService.SyncCategoriesAsync();
        var filter = this.syncService.CreateComprehensiveUpdatesFilter();
        await this.syncService.SyncUpdatesAsync(filter);
    }

    private async Task PerformContentSync(int daysBack = 30)
    {
        if (this.contentStore == null)
        {
            this.logger.LogWarning("Content store not configured, skipping content sync");
            return;
        }

        var filter = new ServiceMetadataFilter
        {
            UpdatedAfter = DateTime.UtcNow.AddDays(-daysBack)
        };
        await this.syncService.SyncContentAsync(filter, this.contentStore);
    }

    private async Task PerformFullSync()
    {
        await this.PerformComprehensiveSync();
        await this.PerformContentSync();
    }

    private async Task PerformEmergencySync(string? reason, IEnumerable<string>? specificUpdateIds)
    {
        this.logger.LogWarning("Emergency sync reason: {Reason}", reason ?? "Not specified");

        if (specificUpdateIds?.Any() == true)
        {
            this.logger.LogInformation("Emergency sync for specific updates: {UpdateIds}",
                string.Join(", ", specificUpdateIds));
        }

        // Always perform critical updates in emergency
        var filter = this.syncService.CreateCriticalUpdatesFilter();
        await this.syncService.SyncUpdatesAsync(filter);
    }
}