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
/// Azure Functions for metadata store management operations.
/// Provides administrative functions for store maintenance and health monitoring.
/// </summary>
public class StoreManagementFunctions
{
    private readonly ILogger<StoreManagementFunctions> logger;
    private readonly ISyncService syncService;
    private readonly IHealthService healthService;
    private readonly ServiceConfigurationMutable serviceConfiguration;

    public StoreManagementFunctions(
        ILogger<StoreManagementFunctions> logger,
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
    /// Reindex the metadata store.
    /// POST /api/ReindexStore
    /// </summary>
    [Function("ReindexStore")]
    public async Task<HttpResponseData> ReindexStore(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        this.logger.LogInformation("Store reindex requested");

        try
        {
            var result = new { startTime = DateTime.UtcNow };

            await this.syncService.ReindexStoreAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new 
            { 
                startTime = result.startTime,
                endTime = DateTime.UtcNow,
                success = true,
                message = "Store reindexing completed successfully"
            });
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during store reindexing");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }

    /// <summary>
    /// Check if store reindexing is required.
    /// GET /api/CheckReindexRequired
    /// </summary>
    [Function("CheckReindexRequired")]
    public async Task<HttpResponseData> CheckReindexRequired(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        try
        {
            var isRequired = await this.syncService.IsReindexingRequired();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { reindexRequired = isRequired });
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error checking reindex status");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }

    /// <summary>
    /// Get comprehensive health status of the entire system.
    /// GET /api/HealthCheck
    /// </summary>
    [Function("HealthCheck")]
    public async Task<HttpResponseData> HealthCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        try
        {
            var health = await this.healthService.GetSystemHealthAsync();

            var statusCode = health.IsHealthy ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;
            var response = req.CreateResponse(statusCode);
            await response.WriteAsJsonAsync(health);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during health check");
            var errorResponse = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            await errorResponse.WriteAsJsonAsync(new { 
                isHealthy = false,
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
            return errorResponse;
        }
    }

    /// <summary>
    /// Scheduled health monitoring - runs every hour.
    /// Logs health status and triggers alerts if needed.
    /// </summary>
    [Function("ScheduledHealthCheck")]
    public async Task ScheduledHealthCheck([TimerTrigger("%ScheduledHealthCheckSchedule%")] TimerInfo timer)
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
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during scheduled health check");
        }
    }

    /// <summary>
    /// Weekly maintenance tasks - runs every Sunday at 1 AM UTC.
    /// Performs cleanup and optimization tasks.
    /// </summary>
    [Function("WeeklyMaintenance")]
    public async Task WeeklyMaintenance([TimerTrigger("%WeeklyMaintenanceSchedule%")] TimerInfo timer)
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

            this.logger.LogInformation("Weekly maintenance tasks completed successfully");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during weekly maintenance");
            throw;
        }
    }
}