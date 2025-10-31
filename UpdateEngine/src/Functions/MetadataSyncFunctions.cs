// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Functions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using UpdateEngine.Services;
using Configuration;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;

/// <summary>
/// Unified Azure Functions for metadata synchronization operations.
/// Combines HTTP endpoints, scheduled operations, and event-driven synchronization.
/// Uses service layer for testable, maintainable business logic.
/// </summary>
public class MetadataSyncFunctions
{
    private readonly ILogger<MetadataSyncFunctions> logger;
    private readonly ISyncService syncService;
    private readonly IHealthService healthService;
    private readonly ServiceConfigurationMutable serviceConfiguration;

    public MetadataSyncFunctions(
        ILogger<MetadataSyncFunctions> logger,
        ISyncService syncService,
        IHealthService healthService,
        IOptions<ServiceConfigurationMutable> serviceConfiguration)
    {
        this.logger = logger;
        this.syncService = syncService;
        this.healthService = healthService;
        this.serviceConfiguration = serviceConfiguration.Value;
    }

    /// <summary>
    /// HTTP endpoint for manual metadata synchronization.
    /// POST /api/SyncMetadata
    /// </summary>
    [Function("SyncMetadata")]
    public async Task<HttpResponseData> SyncMetadata(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        this.logger.LogInformation("Manual metadata sync requested");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<Services.SyncMetadataRequest>(requestBody) ?? new Services.SyncMetadataRequest();

            // Create filter based on request
            var filter = string.IsNullOrEmpty(request.FilterType)
                         || request.FilterType.ToLower() == "comprehensive" ? this.syncService.CreateComprehensiveUpdatesFilter() : this.syncService.CreateCriticalUpdatesFilter();

            if (request.CustomFilters?.ProductFilters?.Any() == true || request.CustomFilters?.ClassificationFilters?.Any() == true)
            {
                filter = this.syncService.CreateCustomFilter(
                    productFilters: request.CustomFilters.ProductFilters,
                    classificationFilters: request.CustomFilters.ClassificationFilters);
            }

            // Perform synchronization
            var result = new Services.SyncResult { StartTime = DateTime.UtcNow };

            if (request.SyncCategories)
            {
                await this.syncService.SyncCategoriesAsync();
                result.CategoriesSynced = true;
            }

            if (request.SyncUpdates)
            {
                await this.syncService.SyncUpdatesAsync(filter);
                result.UpdatesSynced = true;
            }

            result.EndTime = DateTime.UtcNow;
            result.Success = true;

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during metadata synchronization");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }

    /// <summary>
    /// Scheduled metadata synchronization - daily at 2 AM UTC.
    /// Performs comprehensive sync for regular maintenance.
    /// </summary>
    [Function("ScheduledMetadataSync")]
    public async Task ScheduledMetadataSync([TimerTrigger("%ScheduledMetadataSyncSchedule%")] TimerInfo timer)
    {
        this.logger.LogInformation("Starting scheduled comprehensive metadata sync at {Time}", DateTime.UtcNow);

        try
        {
            // Check if reindexing is required first
            if (await this.syncService.IsReindexingRequired())
            {
                this.logger.LogInformation("Store reindexing required, performing reindex");
                await this.syncService.ReindexStoreAsync();
            }

            // Sync categories and comprehensive updates
            await this.syncService.SyncCategoriesAsync();

            var filter = this.syncService.CreateComprehensiveUpdatesFilter();
            await this.syncService.SyncUpdatesAsync(filter);

            this.logger.LogInformation("Scheduled metadata sync completed successfully");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during scheduled metadata sync");
            throw;
        }
    }

    /// <summary>
    /// Critical updates synchronization - every 4 hours.
    /// Focuses on security and critical updates for faster sync.
    /// </summary>
    [Function("CriticalUpdatesSync")]
    public async Task CriticalUpdatesSync([TimerTrigger("%CriticalUpdatesSyncSchedule%")] TimerInfo timer)
    {
        this.logger.LogInformation("Starting critical updates sync at {Time}", DateTime.UtcNow);

        try
        {
            var filter = this.syncService.CreateCriticalUpdatesFilter();
            await this.syncService.SyncUpdatesAsync(filter);

            this.logger.LogInformation("Critical updates sync completed successfully");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during critical updates sync");
            throw;
        }
    }

    /// <summary>
    /// Health check for sync operations.
    /// GET /api/SyncHealth
    /// </summary>
    [Function("SyncHealth")]
    public async Task<HttpResponseData> GetSyncHealth(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        try
        {
            var health = await this.healthService.GetSyncHealthAsync();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(health);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error retrieving sync health");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }
}