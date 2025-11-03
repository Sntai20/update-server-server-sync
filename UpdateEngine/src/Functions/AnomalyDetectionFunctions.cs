// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Functions;

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UpdateEngine.Models;
using UpdateEngine.Services;

/// <summary>
/// Azure Functions for ML.NET-based anomaly detection in Windows Update metadata
/// Controlled by Features:EnableAnomalyDetection configuration flag
/// </summary>
public class AnomalyDetectionFunctions
{
    private readonly ILogger<AnomalyDetectionFunctions> logger;
    private readonly IAnomalyDetectionService anomalyService;
    private readonly IQueueService queueService;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly bool enabled;

    public AnomalyDetectionFunctions(
        ILogger<AnomalyDetectionFunctions> logger,
        IAnomalyDetectionService anomalyService,
        IQueueService queueService,
        JsonSerializerOptions jsonOptions,
        IConfiguration configuration)
    {
        this.logger = logger;
        this.anomalyService = anomalyService;
        this.queueService = queueService;
        this.jsonOptions = jsonOptions;
        this.enabled = configuration.GetValue<bool>("Features:EnableAnomalyDetection", false);
    }

    /// <summary>
    /// HTTP endpoint for ingesting anomaly detection requests
    /// POST /api/ingest-anomaly
    /// Requires Features:EnableAnomalyDetection=true
    /// </summary>
    [Function("IngestAnomaly")]
    public async Task<HttpResponseData> IngestAnomaly(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        if (!this.enabled)
        {
            var disabled = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            await disabled.WriteStringAsync("Anomaly detection is disabled");
            return disabled;
        }

        try
        {
            var body = await req.ReadAsStringAsync();
            var evt = JsonSerializer.Deserialize<AnomalyEvent>(body ?? "{}", this.jsonOptions);

            if (evt == null || string.IsNullOrEmpty(evt.KB_ID))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid payload: KB_ID is required");
                return badRequest;
            }

            // Log quarantine if hash mismatch
            if (!evt.HashMatch)
            {
                this.logger.LogWarning("Quarantined {KB} due to hash mismatch", evt.KB_ID);
            }

            // Enqueue event for async processing
            await this.queueService.EnqueueAnomalyEventAsync(evt);

            this.logger.LogInformation(
                "Anomaly ingested: KB={KB} score={Score:F2} signed={Signed} reputation={Rep:F2}",
                evt.KB_ID, evt.Score, evt.IsSigned, evt.DomainReputation);

            var response = req.CreateResponse(HttpStatusCode.Accepted);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(new 
            { 
                status = "accepted", 
                kbId = evt.KB_ID,
                timestamp = DateTime.UtcNow
            }, this.jsonOptions));
            return response;
        }
        catch (JsonException ex)
        {
            this.logger.LogError(ex, "Failed to deserialize anomaly event");
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync($"Invalid JSON: {ex.Message}");
            return badRequest;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error processing anomaly event");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Internal error: {ex.Message}");
            return errorResponse;
        }
    }
}