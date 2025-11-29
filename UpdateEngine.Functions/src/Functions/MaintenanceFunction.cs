// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Functions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using UpdateEngine.Core.Maintenance;

/// <summary>
/// Azure Function for cleaning up corrupted metadata stores.
/// Provides HTTP endpoint for maintenance operations.
/// </summary>
public class MaintenanceFunction
{
    private readonly ILogger<MaintenanceFunction> logger;
    private readonly MetadataStoreCleanup cleanup;
    private readonly JsonSerializerOptions jsonOptions;

    public MaintenanceFunction(
        ILogger<MaintenanceFunction> logger,
        MetadataStoreCleanup cleanup,
        JsonSerializerOptions jsonOptions)
    {
        this.logger = logger;
        this.cleanup = cleanup;
        this.jsonOptions = jsonOptions;
    }

    /// <summary>
    /// HTTP endpoint to clean up corrupted metadata stores.
    /// POST /api/maintenance/cleanup
    /// </summary>
    [Function("CleanupCorruptedStores")]
    public async Task<HttpResponseData> CleanupCorruptedStores(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "maintenance/cleanup")]
        HttpRequestData req,
        CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Cleanup corrupted stores request received");

        try
        {
            // Parse request body for options
            CleanupRequest? request = null;
            
            if (req.Body.Length > 0)
            {
                request = await JsonSerializer.DeserializeAsync<CleanupRequest>(
                    req.Body,
                    this.jsonOptions,
                    cancellationToken);
            }

            request ??= new CleanupRequest();

            // Execute cleanup
            var options = new MetadataStoreCleanup.CleanupOptions
            {
                DryRun = request.DryRun,
                IncludeAzurite = request.IncludeAzurite,
                BaseDirectory = request.BaseDirectory
            };

            var result = await this.cleanup.CleanupCorruptedStoresAsync(options, cancellationToken);

            // Create response
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result, cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during cleanup operation");

            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteAsJsonAsync(
                new { error = ex.Message },
                cancellationToken);

            return response;
        }
    }

    /// <summary>
    /// HTTP endpoint to get cleanup status (scan without deleting).
    /// GET /api/maintenance/cleanup/status
    /// </summary>
    [Function("GetCleanupStatus")]
    public async Task<HttpResponseData> GetCleanupStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "maintenance/cleanup/status")]
        HttpRequestData req,
        CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Cleanup status request received");

        try
        {
            // Always dry run for status endpoint
            var options = new MetadataStoreCleanup.CleanupOptions
            {
                DryRun = true,
                IncludeAzurite = false
            };

            var result = await this.cleanup.CleanupCorruptedStoresAsync(options, cancellationToken);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(
                new
                {
                    hasCorruptedStores = result.DirectoriesFound > 0,
                    directoriesFound = result.DirectoriesFound,
                    totalSizeBytes = result.TotalSizeBytes,
                    totalSizeFormatted = this.FormatBytes(result.TotalSizeBytes),
                    lockingProcesses = result.LockingProcesses,
                    timestamp = DateTime.UtcNow
                },
                cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error getting cleanup status");

            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteAsJsonAsync(
                new { error = ex.Message },
                cancellationToken);

            return response;
        }
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "bytes", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Request model for cleanup operation.
    /// </summary>
    public class CleanupRequest
    {
        public bool DryRun { get; set; }
        public bool IncludeAzurite { get; set; }
        public string? BaseDirectory { get; set; }
    }
}
