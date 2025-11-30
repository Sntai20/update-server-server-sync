// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Functions.Intelligence;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UpdateEngine.Metadata.Metadata;
using System.Net;
using System.Text.Json;
using UpdateEngine.Core.Models;
using UpdateEngine.Core.Services;

/// <summary>
/// Azure Functions for ML.NET-based anomaly detection in Windows Update metadata
/// Controlled by Features:EnableAnomalyDetection configuration flag
/// </summary>
public class AnomalyDetectionFunctions
{
    private readonly ILogger<AnomalyDetectionFunctions> logger;
    private readonly IAnomalyDetectionService anomalyService;
    private readonly IQueueService queueService;
    private readonly IQueryService queryService;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly bool enabled;

    public AnomalyDetectionFunctions(
        ILogger<AnomalyDetectionFunctions> logger,
        IAnomalyDetectionService anomalyService,
        IQueueService queueService,
        IQueryService queryService,
        JsonSerializerOptions jsonOptions,
        IConfiguration configuration)
    {
        this.logger = logger;
        this.anomalyService = anomalyService;
        this.queueService = queueService;
        this.queryService = queryService;
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
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        UpdateEngine.Core.Metrics.AnomalyIngestionMetrics.IngestRequestsReceived.Add(1,
            new KeyValuePair<string, object?>("enabled", this.enabled.ToString()));

        if (!this.enabled)
        {
            var disabled = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            await disabled.WriteStringAsync("Anomaly detection is disabled");
            
            stopwatch.Stop();
            UpdateEngine.Core.Metrics.AnomalyIngestionMetrics.IngestRequestsRejected.Add(1,
                new KeyValuePair<string, object?>("status_code", "503"),
                new KeyValuePair<string, object?>("reason", "disabled"));
            UpdateEngine.Core.Metrics.AnomalyIngestionMetrics.IngestRequestDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("status_code", "503"));
            
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
                
                stopwatch.Stop();
                UpdateEngine.Core.Metrics.AnomalyIngestionMetrics.IngestRequestsRejected.Add(1,
                    new KeyValuePair<string, object?>("status_code", "400"),
                    new KeyValuePair<string, object?>("reason", "invalid_payload"));
                UpdateEngine.Core.Metrics.AnomalyIngestionMetrics.IngestRequestDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("status_code", "400"));
                
                return badRequest;
            }

            // Log quarantine if hash mismatch
            if (!evt.HashMatch)
            {
                this.logger.LogWarning("Quarantined {KB} due to hash mismatch", evt.KB_ID);
                UpdateEngine.Core.Metrics.AnomalyIngestionMetrics.QuarantinedUpdates.Add(1,
                    new KeyValuePair<string, object?>("kb_id", evt.KB_ID));
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
            
            stopwatch.Stop();
            UpdateEngine.Core.Metrics.AnomalyIngestionMetrics.IngestRequestsAccepted.Add(1,
                new KeyValuePair<string, object?>("kb_id", evt.KB_ID));
            UpdateEngine.Core.Metrics.AnomalyIngestionMetrics.IngestRequestDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("status_code", "202"));
            
            return response;
        }
        catch (JsonException ex)
        {
            this.logger.LogError(ex, "Failed to deserialize anomaly event");
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync($"Invalid JSON: {ex.Message}");
            
            stopwatch.Stop();
            UpdateEngine.Core.Metrics.AnomalyIngestionMetrics.IngestRequestsRejected.Add(1,
                new KeyValuePair<string, object?>("status_code", "400"),
                new KeyValuePair<string, object?>("reason", "invalid_json"));
            UpdateEngine.Core.Metrics.AnomalyIngestionMetrics.IngestRequestDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("status_code", "400"));
            
            return badRequest;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error processing anomaly event");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Internal error: {ex.Message}");
            
            stopwatch.Stop();
            UpdateEngine.Core.Metrics.AnomalyIngestionMetrics.IngestRequestsRejected.Add(1,
                new KeyValuePair<string, object?>("status_code", "500"),
                new KeyValuePair<string, object?>("reason", "error"));
            UpdateEngine.Core.Metrics.AnomalyIngestionMetrics.IngestRequestDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("status_code", "500"));
            
            return errorResponse;
        }
    }

    /// <summary>
    /// Scheduled anomaly detection for sync patterns and metadata health
    /// Default: Every 30 minutes for production monitoring
    /// Configure with AnomalyDetectionSchedule app setting (TimeSpan format)
    /// </summary>
    [Function("ScheduledAnomalyDetection")]
    public async Task RunScheduledAnomalyDetection([TimerTrigger("%AnomalyDetectionSchedule%")] TimerInfo timer)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var anomaliesFound = 0;
        var updatesAnalyzed = 0;
        
        UpdateEngine.Core.Metrics.ScheduledDetectionMetrics.ScheduledRunsStarted.Add(1,
            new KeyValuePair<string, object?>("enabled", this.enabled.ToString()));
        
        this.logger.LogInformation("Scheduled anomaly detection triggered at {time}", DateTime.UtcNow);

        if (!this.enabled)
        {
            this.logger.LogInformation("Anomaly detection is disabled, skipping scheduled analysis");
            return;
        }

        try
        {
            // Analyze recent sync patterns and metadata health
            var queryRequest = new MetadataQueryRequest
            {
                IncludeSuperseded = false, // Exclude superseded updates
                MaxResults = 100 // Limit for demo
            };

            var queryResult = await this.queryService.QueryMetadataAsync(queryRequest);
            
            UpdateEngine.Core.Metrics.ScheduledDetectionMetrics.UpdatesBatchSize.Record(queryResult.Packages.Count);
            
            // Convert PackageInfo to SoftwareUpdate for anomaly detection
            // Note: This is a simplification - in production, you'd want direct access to SoftwareUpdate objects
            var updates = new List<SoftwareUpdate>();
            
            // For demo purposes, create fake anomalous metadata to demonstrate detection
            var fakeAnomalousMetadata = new UpdateMetadata
            {
                KB_ID = "DemoBad_999",
                Publisher = "UnknownPublisher",
                HashMatch = false,
                IsSigned = false,
                FileSize = 1234567,
                DomainReputation = "Suspicious",
                SupersededCount = 0,
                SupersededByCount = 0,
                BundledUpdatesCount = 0,
                IsSecurityUpdate = false,
                IsCriticalUpdate = false,
                IsCumulativeUpdate = false,
                ApplicabilityRulesCount = 0,
                HasComplexApplicability = false
            };

            // Score the fake anomaly first for demo
            double fakeScore = this.anomalyService.Score(fakeAnomalousMetadata);
            updatesAnalyzed++;
            
            if (fakeScore > 0.8)
            {
                anomaliesFound++;
                this.logger.LogWarning("ALERT: Anomaly detected for KB_ID={KB_ID} (score={score:F2}) Metadata: {metadata}",
                    fakeAnomalousMetadata.KB_ID, fakeScore,
                    JsonSerializer.Serialize(fakeAnomalousMetadata, this.jsonOptions)
                );
            }

            // Process real updates from query results
            foreach (var packageInfo in queryResult.Packages)
            {
                // Convert PackageInfo to UpdateMetadata for anomaly detection
                var metadata = new UpdateMetadata
                {
                    KB_ID = packageInfo.KbArticle ?? packageInfo.Title ?? "Unknown",
                    Publisher = "Microsoft",
                    HashMatch = true,
                    IsSigned = true,
                    FileSize = packageInfo.Size,
                    DomainReputation = "Trusted",
                    SupersededCount = 0, // Would need SoftwareUpdate object for actual values
                    SupersededByCount = 0,
                    BundledUpdatesCount = 0,
                    IsSecurityUpdate = packageInfo.Title?.ToLowerInvariant().Contains("security") ?? false,
                    IsCriticalUpdate = packageInfo.Title?.ToLowerInvariant().Contains("critical") ?? false,
                    IsCumulativeUpdate = packageInfo.Title?.ToLowerInvariant().Contains("cumulative") ?? false,
                    ApplicabilityRulesCount = 0, // Would need SoftwareUpdate object for actual values
                    HasComplexApplicability = false
                };

                double score = this.anomalyService.Score(metadata);
                updatesAnalyzed++;

                if (score > 0.8)
                {
                    anomaliesFound++;
                    this.logger.LogWarning("ALERT: Anomaly detected for update={kbId} (score={score:F2}) Title: {title}",
                        metadata.KB_ID, score, packageInfo.Title);
                }
                else
                {
                    this.logger.LogInformation("Update {kbId} scored normal: {score:F2}", metadata.KB_ID, score);
                }
            }
            
            stopwatch.Stop();
            
            UpdateEngine.Core.Metrics.ScheduledDetectionMetrics.ScheduledRunsCompleted.Add(1,
                new KeyValuePair<string, object?>("updates_analyzed", updatesAnalyzed),
                new KeyValuePair<string, object?>("anomalies_found", anomaliesFound));
            
            UpdateEngine.Core.Metrics.ScheduledDetectionMetrics.ScheduledRunDuration.Record(stopwatch.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("updates_analyzed", updatesAnalyzed));
            
            UpdateEngine.Core.Metrics.ScheduledDetectionMetrics.AnomaliesPerRun.Record(anomaliesFound);
            
            this.logger.LogInformation("Anomaly detection completed for {count} updates ({anomalies} anomalies found) in {duration:F2}s", 
                queryResult.Packages.Count, anomaliesFound, stopwatch.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            UpdateEngine.Core.Metrics.ScheduledDetectionMetrics.ScheduledRunsFailed.Add(1,
                new KeyValuePair<string, object?>("error_type", ex.GetType().Name));
            
            this.logger.LogError(ex, "Error during anomaly detection demo");
        }
    }
}
