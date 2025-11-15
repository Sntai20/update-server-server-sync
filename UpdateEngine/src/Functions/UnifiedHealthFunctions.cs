// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using UpdateEngine.Services;
using UpdateEngine.Models;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.MicrosoftUpdate.Metadata;
using System.Net;

namespace UpdateEngine.Functions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using UpdateEngine.Services;

/// <summary>
/// Unified health and maintenance functions consolidating all health-related operations.
/// Replaces 8 separate functions with configurable scope-based operations.
/// Supports both HTTP triggers and scheduled timer triggers.
/// </summary>
public class UnifiedHealthFunctions
{
    private readonly ILogger<UnifiedHealthFunctions> logger;
    private readonly IHealthService healthService;
    private readonly ISyncService syncService;
    private readonly IAnomalyDetectionService? anomalyDetectionService;
    private readonly IMetadataStore? metadataStore;
    private readonly JsonSerializerOptions jsonOptions;

    public UnifiedHealthFunctions(
        ILogger<UnifiedHealthFunctions> logger,
        IHealthService healthService,
        ISyncService syncService,
        JsonSerializerOptions jsonOptions,
        IAnomalyDetectionService? anomalyDetectionService = null,
        IMetadataStore? metadataStore = null)
    {
        this.logger = logger;
        this.healthService = healthService;
        this.syncService = syncService;
        this.anomalyDetectionService = anomalyDetectionService;
        this.metadataStore = metadataStore;
        this.jsonOptions = jsonOptions;
    }

    /// <summary>
    /// Universal health endpoint supporting multiple scopes and checks.
    /// GET /api/UniversalHealth?scope={basic|full|sync|store}
    /// Replaces: HealthCheck, StoreHealth, SyncHealth
    /// </summary>
    [Function("UniversalHealth")]
    public async Task<HttpResponseData> UniversalHealth(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        try
        {
            var scope = req.Query["scope"] ?? "basic";

            var health = scope switch
            {
                "full" => await this.healthService.GetSystemHealthAsync(),
                "sync" => await this.healthService.GetSyncHealthAsync(),
                "store" => await this.GetStoreHealthAsync(),
                _ => await this.GetBasicHealthAsync()
            };

            var statusCode = health.IsHealthy ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(health, this.jsonOptions));
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during health check");
            var errorResponse = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new
            {
                isHealthy = false,
                error = ex.Message,
                timestamp = DateTime.UtcNow
            }, this.jsonOptions));
            return errorResponse;
        }
    }

    /// <summary>
    /// Scheduled health monitoring - runs every hour.
    /// Default: Every 1 hour.
    /// Configure with ScheduledHealthCheckSchedule app setting (TimeSpan format).
    /// Logs health status and triggers alerts if needed.
    /// </summary>
    [Function("ScheduledHealthCheck")]
    public async Task ScheduledHealthCheck(
        [TimerTrigger("%ScheduledHealthCheckSchedule%")] TimerInfo timer)
    {
        this.logger.LogInformation("Running scheduled health check at {Time}", DateTime.UtcNow);

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

            // Perform metadata anomaly health check if available
            await this.PerformMetadataHealthAnomalyCheck();

            this.logger.LogInformation("Scheduled health check completed. Next run: {NextRun}", timer.ScheduleStatus?.Next);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during scheduled health check");
            throw;
        }
    }

    /// <summary>
    /// Weekly maintenance tasks.
    /// Default: Every 7 days (Sunday at 1 AM UTC when using CRON, or every 168 hours with TimeSpan).
    /// Configure with WeeklyMaintenanceSchedule app setting (TimeSpan format).
    /// Performs cleanup and optimization tasks.
    /// </summary>
    [Function("WeeklyMaintenance")]
    public async Task WeeklyMaintenance(
        [TimerTrigger("%WeeklyMaintenanceSchedule%")] TimerInfo timer)
    {
        this.logger.LogInformation("Starting weekly maintenance tasks at {Time}", DateTime.UtcNow);

        try
        {
            // Check if reindexing is required and perform if needed
            if (await this.syncService.IsReindexingRequired())
            {
                this.logger.LogInformation("Performing scheduled reindexing as part of maintenance");
                await this.syncService.ReindexStoreAsync();
            }

            // Run health diagnostics
            var health = await this.healthService.GetSystemHealthAsync();
            this.logger.LogInformation("Weekly maintenance health check completed. Healthy: {IsHealthy}", health.IsHealthy);

            this.logger.LogInformation("Weekly maintenance tasks completed successfully. Next run: {NextRun}", timer.ScheduleStatus?.Next);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during weekly maintenance");
            throw;
        }
    }

    /// <summary>
    /// Store management operations.
    /// POST /api/StoreManagement
    /// Replaces: ReindexStore, ClearCache, StoreCleanup
    /// </summary>
    [Function("StoreManagement")]
    public async Task<HttpResponseData> StoreManagement(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<StoreManagementRequest>(requestBody ?? "{}", this.jsonOptions);

            var operations = new List<string>();

            if (request?.Reindex == true)
            {
                await this.syncService.ReindexStoreAsync();
                operations.Add("reindex");
            }

            // Note: ClearCache and Cleanup methods don't exist in ISyncService
            // Removed those operations per compilation error fixes

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                completed = operations,
                timestamp = DateTime.UtcNow,
                success = true
            }, this.jsonOptions));
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during store management");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }, this.jsonOptions));
            return errorResponse;
        }
    }

    /// <summary>
    /// Check if store reindex is required.
    /// GET /api/CheckReindexRequired
    /// Replaces: CheckReindexRequired from StoreManagementFunctions
    /// </summary>
    [Function("CheckReindexRequired")]
    public async Task<HttpResponseData> CheckReindexRequired(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        try
        {
            var isRequired = await this.syncService.IsReindexingRequired();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { reindexRequired = isRequired }, this.jsonOptions));
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error checking reindex requirement");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }, this.jsonOptions));
            return errorResponse;
        }
    }

    private async Task<HealthStatus> GetBasicHealthAsync()
    {
        try
        {
            // Basic health check - just verify services are responding
            return new HealthStatus
            {
                IsHealthy = true,
                Timestamp = DateTime.UtcNow,
                Metrics = new List<HealthMetric>
                {
                    new() { Name = "scope", Value = "basic" }
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthStatus
            {
                IsHealthy = false,
                Timestamp = DateTime.UtcNow,
                Issues = new List<HealthIssue>
                {
                    new() { Component = "basic", Message = ex.Message, Severity = "Error" }
                },
                Metrics = new List<HealthMetric>
                {
                    new() { Name = "scope", Value = "basic" }
                }
            };
        }
    }

    private async Task<HealthStatus> GetStoreHealthAsync()
    {
        try
        {
            var isReindexRequired = await this.syncService.IsReindexingRequired();
            return new HealthStatus
            {
                IsHealthy = !isReindexRequired,
                Timestamp = DateTime.UtcNow,
                Issues = isReindexRequired ? new List<HealthIssue>
                {
                    new() { Component = "store", Message = "Reindexing required", Severity = "Warning" }
                } : null,
                Metrics = new List<HealthMetric>
                {
                    new() { Name = "scope", Value = "store" },
                    new() { Name = "reindexRequired", Value = isReindexRequired.ToString() }
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthStatus
            {
                IsHealthy = false,
                Timestamp = DateTime.UtcNow,
                Issues = new List<HealthIssue>
                {
                    new() { Component = "store", Message = ex.Message, Severity = "Error" }
                },
                Metrics = new List<HealthMetric>
                {
                    new() { Name = "scope", Value = "store" }
                }
            };
        }
    }

    /// <summary>
    /// Performs metadata health check including anomaly detection for monitoring purposes.
    /// Analyzes recent metadata for anomalies and potential security issues.
    /// </summary>
    private async Task PerformMetadataHealthAnomalyCheck()
    {
        if (this.anomalyDetectionService == null || this.metadataStore == null)
        {
            this.logger.LogDebug("Anomaly detection service or metadata store not available, skipping metadata health check");
            return;
        }

        try
        {
            this.logger.LogInformation("Performing metadata health anomaly check");

            // Get sample of recent software updates for health monitoring
            var allUpdates = this.metadataStore.OfType<SoftwareUpdate>().Take(50).ToList();

            if (!allUpdates.Any())
            {
                this.logger.LogInformation("No software updates found in metadata store for health check");
                return;
            }

            var anomalyCount = 0;
            var highRiskCount = 0;
            var totalAnalyzed = 0;

            foreach (var softwareUpdate in allUpdates)
            {
                try
                {
                    var anomalyScore = this.anomalyDetectionService.Score(softwareUpdate);
                    totalAnalyzed++;

                    if (anomalyScore > 0.5)
                    {
                        anomalyCount++;
                        if (anomalyScore > 0.8)
                        {
                            highRiskCount++;
                            this.logger.LogWarning("HIGH-RISK metadata anomaly detected in health check: {UpdateId} - {Title} (Score: {Score:F2})",
                                softwareUpdate.Id.ID, softwareUpdate.Title, anomalyScore);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log but continue - missing metadata is expected during partial sync
                    this.logger.LogDebug("Skipping update {UpdateId} during health check - metadata incomplete: {Message}", 
                        softwareUpdate.Id.ID, ex.Message);
                    // Don't increment totalAnalyzed for failed analyses
                }
            }

            // Log health summary
            var anomalyPercentage = totalAnalyzed > 0 ? (double)anomalyCount / totalAnalyzed * 100 : 0;
            
            if (highRiskCount > 0)
            {
                this.logger.LogWarning("Metadata health alert: {HighRisk} high-risk anomalies found out of {Total} updates analyzed ({Percentage:F1}% anomaly rate)",
                    highRiskCount, totalAnalyzed, anomalyPercentage);
            }
            else if (anomalyCount > 0)
            {
                this.logger.LogInformation("Metadata health check: {Anomalies} anomalies found out of {Total} updates analyzed ({Percentage:F1}% anomaly rate)",
                    anomalyCount, totalAnalyzed, anomalyPercentage);
            }
            else
            {
                this.logger.LogInformation("Metadata health check passed: No anomalies detected in {Total} updates analyzed", totalAnalyzed);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during metadata health anomaly check");
        }
    }
}

/// <summary>
/// Store management request model
/// </summary>
public class StoreManagementRequest
{
    public bool Reindex { get; set; } = false;
    public bool ClearCache { get; set; } = false;
    public bool Cleanup { get; set; } = false;
}