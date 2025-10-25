// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Functions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using UpdateEngine.Services;
using System.Net;
using System.Text.Json;
using Microsoft.PackageGraph.Storage;

/// <summary>
/// Azure Functions for automated synchronization operations with multiple trigger types.
/// Provides comprehensive automation including scheduled operations, queue processing, and emergency sync.
/// Uses service layer for testable, maintainable business logic.
/// </summary>
public class AutomatedSyncFunctions
{
    private readonly ILogger<AutomatedSyncFunctions> logger;
    private readonly ISyncService syncService;
    private readonly IHealthService healthService;
    private readonly IContentStore? contentStore;

    public AutomatedSyncFunctions(
        ILogger<AutomatedSyncFunctions> logger,
        ISyncService syncService,
        IHealthService healthService,
        IContentStore? contentStore)
    {
        this.logger = logger;
        this.syncService = syncService;
        this.healthService = healthService;
        this.contentStore = contentStore;
    }

    /// <summary>
    /// Hourly health monitoring and system checks.
    /// Ensures the system is healthy and logs any issues.
    /// </summary>
    [Function("HourlyHealthCheck")]
    public async Task HourlyHealthCheck([TimerTrigger("0 0 * * * *")] TimerInfo timer)
    {
        this.logger.LogInformation("Starting hourly health check at {Time}", DateTime.UtcNow);

        try
        {
            var health = await this.healthService.GetSystemHealthAsync();

            if (health.IsHealthy)
            {
                this.logger.LogInformation("System health check passed");
            }
            else
            {
                this.logger.LogWarning("System health issues detected: {Issues}",
                    string.Join(", ", health.Issues?.Select(i => i.Message) ?? Array.Empty<string>()));
            }

            // Log detailed metrics
            if (health.Metrics?.Any() == true)
            {
                foreach (var metric in health.Metrics)
                {
                    this.logger.LogInformation("Health metric - {Name}: {Value}", metric.Name, metric.Value);
                }
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during hourly health check");
        }
    }

    /// <summary>
    /// Daily critical updates synchronization.
    /// Focuses on security and critical updates for faster processing.
    /// </summary>
    [Function("DailyCriticalSync")]
    public async Task DailyCriticalSync([TimerTrigger("0 30 1 * * *")] TimerInfo timer)
    {
        this.logger.LogInformation("Starting daily critical sync at {Time}", DateTime.UtcNow);

        try
        {
            // Check system health first
            var health = await this.healthService.GetSystemHealthAsync();
            if (!health.IsHealthy)
            {
                this.logger.LogWarning("System health issues detected, proceeding with caution");
            }

            // Perform critical updates sync
            var filter = this.syncService.CreateCriticalUpdatesFilter();
            await this.syncService.SyncUpdatesAsync(filter);

            this.logger.LogInformation("Daily critical sync completed successfully");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during daily critical sync");
            throw;
        }
    }

    /// <summary>
    /// Weekly comprehensive synchronization.
    /// Performs full metadata and content synchronization.
    /// </summary>
    [Function("WeeklyComprehensiveSync")]
    public async Task WeeklyComprehensiveSync([TimerTrigger("0 0 2 * * 0")] TimerInfo timer)
    {
        this.logger.LogInformation("Starting weekly comprehensive sync at {Time}", DateTime.UtcNow);

        try
        {
            // Check if reindexing is required first
            if (await this.syncService.IsReindexingRequired())
            {
                this.logger.LogInformation("Store reindexing required, performing reindex");
                await this.syncService.ReindexStoreAsync();
            }

            // Sync categories first
            await this.syncService.SyncCategoriesAsync();

            // Sync comprehensive updates
            var filter = this.syncService.CreateComprehensiveUpdatesFilter();
            await this.syncService.SyncUpdatesAsync(filter);

            // Sync content if content store is available
            if (this.contentStore != null)
            {
                this.logger.LogInformation("Syncing content for recent updates");
                var contentFilter = new ServiceMetadataFilter
                {
                    UpdatedAfter = DateTime.UtcNow.AddDays(-7)
                };
                await this.syncService.SyncContentAsync(contentFilter, this.contentStore);
            }

            this.logger.LogInformation("Weekly comprehensive sync completed successfully");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during weekly comprehensive sync");
            throw;
        }
    }

    /// <summary>
    /// Monthly maintenance operations.
    /// Performs cleanup, optimization, and deep health checks.
    /// </summary>
    [Function("MonthlyMaintenance")]
    public async Task MonthlyMaintenance([TimerTrigger("0 0 3 1 * *")] TimerInfo timer)
    {
        this.logger.LogInformation("Starting monthly maintenance at {Time}", DateTime.UtcNow);

        try
        {
            // Perform comprehensive health check
            var health = await this.healthService.GetSystemHealthAsync();
            this.logger.LogInformation("Monthly health check completed. Healthy: {IsHealthy}", health.IsHealthy);

            // Force reindex if needed
            if (await this.syncService.IsReindexingRequired())
            {
                this.logger.LogInformation("Performing monthly reindexing");
                await this.syncService.ReindexStoreAsync();
            }

            this.logger.LogInformation("Monthly maintenance completed successfully");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during monthly maintenance");
            throw;
        }
    }

    /// <summary>
    /// Process priority sync requests from a Service Bus queue.
    /// Handles high-priority synchronization operations.
    /// </summary>
    [Function("ProcessPrioritySyncRequest")]
    public async Task ProcessPrioritySyncRequest(
        [ServiceBusTrigger("priority-sync-requests", Connection = "ServiceBusConnection")]
        string queueItem)
    {
        this.logger.LogInformation("Processing priority sync request: {QueueItem}", queueItem);

        try
        {
            var request = JsonSerializer.Deserialize<Services.PrioritySyncRequest>(queueItem);
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
    /// Handles regular synchronization operations.
    /// </summary>
    [Function("ProcessStandardSyncRequest")]
    public async Task ProcessStandardSyncRequest(
        [ServiceBusTrigger("standard-sync-requests", Connection = "ServiceBusConnection")]
        string queueItem)
    {
        this.logger.LogInformation("Processing standard sync request: {QueueItem}", queueItem);

        try
        {
            var request = JsonSerializer.Deserialize<Services.StandardSyncRequest>(queueItem);
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
            var request = JsonSerializer.Deserialize<Services.ContentSyncQueueRequest>(queueItem);
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
    /// Emergency sync endpoint for critical situations.
    /// POST /api/EmergencySync
    /// </summary>
    [Function("EmergencySync")]
    public async Task<HttpResponseData> EmergencySync(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        this.logger.LogWarning("Emergency sync requested");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<Services.EmergencySyncRequest>(requestBody) ?? new Services.EmergencySyncRequest();

            this.logger.LogWarning("Emergency sync reason: {Reason}", request.Reason);

            var result = new Services.SyncResult { StartTime = DateTime.UtcNow };

            // Perform emergency sync
            if (request.SpecificUpdateIds?.Any() == true)
            {
                // Handle specific update IDs if the service supports it
                this.logger.LogInformation("Emergency sync for specific updates: {UpdateIds}",
                    string.Join(", ", request.SpecificUpdateIds));
            }

            // Always perform critical updates in emergency
            var filter = this.syncService.CreateCriticalUpdatesFilter();
            await this.syncService.SyncUpdatesAsync(filter);

            result.EndTime = DateTime.UtcNow;
            result.Success = true;
            result.UpdatesSynced = true;

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during emergency sync");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }
}