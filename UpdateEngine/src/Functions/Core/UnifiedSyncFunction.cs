using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using UpdateEngine.Core.Orchestrators;
using UpdateEngine.Core.Models;

namespace UpdateEngine.Functions.Core;

/// <summary>
/// Unified sync function that handles all sync operations through a single endpoint.
/// Consolidates: SyncCategories, SyncUpdates, ComprehensiveSync, PauseSync, ResumeSync, CancelSync
/// </summary>
public class UnifiedSyncFunction
{
    private readonly ISyncOrchestrator syncOrchestrator;
    private readonly ILogger<UnifiedSyncFunction> logger;
    private readonly JsonSerializerOptions jsonOptions;

    public UnifiedSyncFunction(
        ISyncOrchestrator syncOrchestrator,
        ILogger<UnifiedSyncFunction> logger,
        JsonSerializerOptions jsonOptions)
    {
        this.syncOrchestrator = syncOrchestrator ?? throw new ArgumentNullException(nameof(syncOrchestrator));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
    }

    /// <summary>
    /// Unified sync endpoint - handles all sync operations based on request parameters
    /// </summary>
    /// <remarks>
    /// POST /api/sync
    /// Body: {
    ///   "syncType": "categories" | "updates" | "comprehensive",
    ///   "action": "start" | "pause" | "resume" | "cancel",
    ///   "filter": {
    ///     "productTitles": ["Windows 10"],
    ///     "classificationIds": ["guid"],
    ///     "fromDate": "2024-01-01"
    ///   }
    /// }
    /// </remarks>
    [Function("Sync")]
    public async Task<HttpResponseData> Sync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sync")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            this.logger.LogInformation("Unified sync request received");

            // Parse request
            var request = await JsonSerializer.DeserializeAsync<UnifiedSyncRequest>(
                req.Body, 
                this.jsonOptions, 
                cancellationToken);

            if (request == null)
            {
                return await this.CreateErrorResponse(req, "Invalid request body", HttpStatusCode.BadRequest, cancellationToken);
            }

            this.logger.LogInformation(
                "Processing sync request: Type={SyncType}, Action={Action}", 
                request.SyncType, 
                request.Action);

            // Execute sync via orchestrator
            var result = await this.syncOrchestrator.ExecuteSyncAsync(request, cancellationToken);

            // Create response
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result, cancellationToken);
            return response;
        }
        catch (JsonException ex)
        {
            this.logger.LogError(ex, "Failed to parse sync request");
            return await this.CreateErrorResponse(req, "Invalid JSON format", HttpStatusCode.BadRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Sync operation failed");
            return await this.CreateErrorResponse(req, $"Sync operation failed: {ex.Message}", HttpStatusCode.InternalServerError, cancellationToken);
        }
    }

    /// <summary>
    /// Get current sync status
    /// </summary>
    [Function("SyncStatus")]
    public async Task<HttpResponseData> GetSyncStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sync/status")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await this.syncOrchestrator.GetStatusAsync(cancellationToken);
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(status, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to get sync status");
            return await this.CreateErrorResponse(req, $"Failed to get sync status: {ex.Message}", HttpStatusCode.InternalServerError, cancellationToken);
        }
    }

    private async Task<HttpResponseData> CreateErrorResponse(
        HttpRequestData req, 
        string message, 
        HttpStatusCode statusCode,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(new
        {
            Error = message,
            Timestamp = DateTime.UtcNow
        }, cancellationToken);
        return response;
    }
}
