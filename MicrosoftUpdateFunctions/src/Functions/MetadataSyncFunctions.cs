namespace MicrosoftUpdateFunctions.Functions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MicrosoftUpdateFunctions.Services;
using System.Net;
using System.Text.Json;

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

    public MetadataSyncFunctions(
        ILogger<MetadataSyncFunctions> logger,
        ISyncService syncService,
        IHealthService healthService)
    {
        this.logger = logger;
        this.syncService = syncService;
        this.healthService = healthService;
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
            var request = JsonSerializer.Deserialize<SyncMetadataRequest>(requestBody) ?? new SyncMetadataRequest();

            // Create filter based on request
            var filter = string.IsNullOrEmpty(request.FilterType) || request.FilterType.ToLower() == "comprehensive"
                ? this.syncService.CreateComprehensiveUpdatesFilter()
                : this.syncService.CreateCriticalUpdatesFilter();

            if (request.CustomFilters?.Any() == true)
            {
                filter = this.syncService.CreateCustomFilter(
                    request.CustomFilters.ProductFilters,
                    request.CustomFilters.ClassificationFilters);
            }

            // Perform synchronization
            var result = new SyncResult { StartTime = DateTime.UtcNow };

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
    public async Task ScheduledMetadataSync([TimerTrigger("0 0 2 * * *")] TimerInfo timer)
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
    public async Task CriticalUpdatesSync([TimerTrigger("0 0 */4 * * *")] TimerInfo timer)
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

// Request/Response models for the metadata sync functions
public class SyncMetadataRequest
{
    public bool SyncCategories { get; set; } = true;
    public bool SyncUpdates { get; set; } = true;
    public string? FilterType { get; set; }
    public CustomFilter? CustomFilters { get; set; }
}

public class CustomFilter
{
    public List<string>? ProductFilters { get; set; }
    public List<string>? ClassificationFilters { get; set; }
}

public class SyncResult
{
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool CategoriesSynced { get; set; }
    public bool UpdatesSynced { get; set; }
}