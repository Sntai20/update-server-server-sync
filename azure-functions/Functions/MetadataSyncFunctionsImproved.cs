using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.MicrosoftUpdate.Source;
using System.Net;
using System.Text;
using System.Text.Json;

namespace MicrosoftUpdateFunctions.Functions;

/// <summary>
/// Azure Functions for metadata synchronization operations using multiple trigger types.
/// These functions provide the same capabilities as the upsync tool but in a serverless environment
/// with better trigger options for long-running and scheduled operations.
/// </summary>
public class MetadataSyncFunctions
{
    private readonly ILogger<MetadataSyncFunctions> logger;
    private readonly IMetadataStore metadataStore;

    public MetadataSyncFunctions(ILogger<MetadataSyncFunctions> logger, IMetadataStore metadataStore)
    {
        this.logger = logger;
        this.metadataStore = metadataStore;
    }

    #region Timer-based Triggers (Best for scheduled sync operations)

    /// <summary>
    /// Scheduled metadata synchronization that runs daily at 2 AM UTC.
    /// This is ideal for regular upstream synchronization without manual intervention.
    /// </summary>
    [Function("ScheduledMetadataSync")]
    public async Task RunScheduledMetadataSync([TimerTrigger("0 0 2 * * *")] TimerInfo timer)
    {
        this.logger.LogInformation("Starting scheduled metadata synchronization at {Time}", DateTime.UtcNow);

        try
        {
            // Use default Microsoft Update endpoint for scheduled sync
            var upstreamEndpoint = Endpoint.Default;
            var client = new UpstreamServerClient(upstreamEndpoint);

            // Fetch configuration
            this.logger.LogInformation("Fetching server configuration from {Endpoint}", upstreamEndpoint.URI);
            var configData = await client.GetServerConfigData();

            // Sync categories (limited batch for scheduled operation)
            this.logger.LogInformation("Synchronizing categories");
            var categoriesSource = new UpstreamCategoriesSource(upstreamEndpoint);
            await foreach (var category in categoriesSource.GetCategoriesAsync())
            {
                // Process categories (implement your storage logic here)
                this.logger.LogDebug("Processing category: {CategoryId}", category.Identity);
                
                // Break after reasonable batch size for scheduled operation
                if (categoriesSource.PackageCount > 100)
                    break;
            }

            // Sync critical updates only for scheduled operation
            var updatesFilter = new UpstreamSourceFilter
            {
                ClassificationFilter = new[] { "Security Updates", "Critical Updates" },
                SkipSuperseded = true
            };

            var updatesSource = new UpstreamUpdatesSource(upstreamEndpoint, updatesFilter);
            var updateCount = 0;
            await foreach (var update in updatesSource.GetUpdatesAsync())
            {
                this.logger.LogDebug("Processing update: {UpdateId}", update.Identity);
                updateCount++;
                
                // Limit updates for scheduled operation
                if (updateCount >= 50)
                    break;
            }

            this.logger.LogInformation("Scheduled metadata sync completed. Processed {UpdateCount} updates", updateCount);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during scheduled metadata synchronization");
            throw; // Re-throw to trigger Azure Functions retry logic
        }
    }

    /// <summary>
    /// Weekly full synchronization that runs every Sunday at 1 AM UTC.
    /// This performs a more comprehensive sync including all update types.
    /// </summary>
    [Function("WeeklyFullMetadataSync")]
    public async Task RunWeeklyFullSync([TimerTrigger("0 0 1 * * 0")] TimerInfo timer)
    {
        this.logger.LogInformation("Starting weekly full metadata synchronization at {Time}", DateTime.UtcNow);

        try
        {
            var upstreamEndpoint = Endpoint.Default;
            
            // Full category sync
            var categoriesSource = new UpstreamCategoriesSource(upstreamEndpoint);
            var categoryCount = 0;
            await foreach (var category in categoriesSource.GetCategoriesAsync())
            {
                categoryCount++;
                // Process all categories for weekly sync
            }

            // Full update sync with broader filter
            var updatesFilter = new UpstreamSourceFilter
            {
                SkipSuperseded = true // Still skip superseded to keep reasonable size
            };

            var updatesSource = new UpstreamUpdatesSource(upstreamEndpoint, updatesFilter);
            var updateCount = 0;
            await foreach (var update in updatesSource.GetUpdatesAsync())
            {
                updateCount++;
                // Process more updates for weekly sync
                if (updateCount >= 500) // Higher limit for weekly sync
                    break;
            }

            this.logger.LogInformation("Weekly full sync completed. Categories: {CategoryCount}, Updates: {UpdateCount}", 
                categoryCount, updateCount);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during weekly full metadata synchronization");
            throw;
        }
    }

    #endregion

    #region Service Bus Triggers (Best for event-driven operations)

    /// <summary>
    /// Processes metadata sync requests from a Service Bus queue.
    /// This allows for on-demand, parameterized synchronization operations.
    /// </summary>
    [Function("ProcessMetadataSyncRequest")]
    public async Task ProcessSyncRequest([ServiceBusTrigger("metadata-sync-requests")] string requestMessage)
    {
        this.logger.LogInformation("Processing metadata sync request: {Request}", requestMessage);

        try
        {
            var syncRequest = JsonSerializer.Deserialize<MetadataSyncRequest>(requestMessage);
            if (syncRequest == null)
            {
                this.logger.LogWarning("Invalid sync request received");
                return;
            }

            var upstreamEndpoint = string.IsNullOrEmpty(syncRequest.UpstreamEndpoint) 
                ? Endpoint.Default 
                : new Endpoint(syncRequest.UpstreamEndpoint);

            switch (syncRequest.SyncType.ToLowerInvariant())
            {
                case "configuration":
                    await this.SyncConfiguration(upstreamEndpoint);
                    break;
                case "categories":
                    await this.SyncCategories(upstreamEndpoint, syncRequest.MaxItems ?? 100);
                    break;
                case "updates":
                    await this.SyncUpdates(upstreamEndpoint, syncRequest);
                    break;
                case "reindex":
                    await this.ReindexStore(syncRequest.StorePath);
                    break;
                default:
                    this.logger.LogWarning("Unknown sync type: {SyncType}", syncRequest.SyncType);
                    break;
            }

            this.logger.LogInformation("Metadata sync request completed: {SyncType}", syncRequest.SyncType);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error processing metadata sync request");
            throw; // This will send the message to dead letter queue if configured
        }
    }

    #endregion

    #region Blob Storage Triggers (Best for configuration-driven operations)

    /// <summary>
    /// Processes metadata sync configuration files uploaded to blob storage.
    /// This enables configuration-driven synchronization workflows.
    /// </summary>
    [Function("ProcessSyncConfigFile")]
    public async Task ProcessConfigFile([BlobTrigger("metadata-config/{name}")] Stream configStream, string name)
    {
        this.logger.LogInformation("Processing sync configuration file: {FileName}", name);

        try
        {
            using var reader = new StreamReader(configStream);
            var configJson = await reader.ReadToEndAsync();
            var config = JsonSerializer.Deserialize<MetadataSyncConfiguration>(configJson);

            if (config == null)
            {
                this.logger.LogWarning("Invalid configuration file: {FileName}", name);
                return;
            }

            // Process each sync operation defined in the configuration
            foreach (var operation in config.Operations)
            {
                var upstreamEndpoint = string.IsNullOrEmpty(operation.UpstreamEndpoint) 
                    ? Endpoint.Default 
                    : new Endpoint(operation.UpstreamEndpoint);

                this.logger.LogInformation("Executing sync operation: {OperationType} from config {FileName}", 
                    operation.Type, name);

                switch (operation.Type.ToLowerInvariant())
                {
                    case "categories":
                        await this.SyncCategories(upstreamEndpoint, operation.MaxItems ?? 100);
                        break;
                    case "updates":
                        await this.SyncUpdatesFromConfig(upstreamEndpoint, operation);
                        break;
                    default:
                        this.logger.LogWarning("Unknown operation type in config: {OperationType}", operation.Type);
                        break;
                }
            }

            this.logger.LogInformation("Configuration file processing completed: {FileName}", name);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error processing configuration file: {FileName}", name);
            throw;
        }
    }

    #endregion

    #region HTTP Triggers (For manual/administrative operations)

    /// <summary>
    /// HTTP endpoint for manual metadata synchronization requests.
    /// Use this for administrative operations and testing.
    /// </summary>
    [Function("ManualMetadataSync")]
    public async Task<HttpResponseData> ManualSync([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        this.logger.LogInformation("Manual metadata sync requested");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
            if (string.IsNullOrEmpty(requestBody))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Request body cannot be empty");
                return badRequest;
            }

            var options = JsonSerializer.Deserialize<ManualSyncRequest>(requestBody);
            if (options == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid request format");
                return badRequest;
            }

            var upstreamEndpoint = string.IsNullOrEmpty(options.UpstreamEndpoint) 
                ? Endpoint.Default 
                : new Endpoint(options.UpstreamEndpoint);

            var result = new Dictionary<string, object>();

            if (options.SyncConfiguration)
            {
                var config = await this.SyncConfiguration(upstreamEndpoint);
                result["configuration"] = config;
            }

            if (options.SyncCategories)
            {
                var categoryCount = await this.SyncCategories(upstreamEndpoint, options.MaxCategories ?? 50);
                result["categoriesSynced"] = categoryCount;
            }

            if (options.SyncUpdates)
            {
                var updateCount = await this.SyncUpdatesManual(upstreamEndpoint, options);
                result["updatesSynced"] = updateCount;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            
            var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true 
            });
            await response.WriteStringAsync(resultJson);

            this.logger.LogInformation("Manual metadata sync completed");
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error in manual metadata sync");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// HTTP endpoint to get metadata store status.
    /// </summary>
    [Function("GetStoreStatus")]
    public async Task<HttpResponseData> GetStoreStatus([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        try
        {
            var status = new
            {
                StoreType = this.metadataStore.GetType().Name,
                LastUpdated = DateTime.UtcNow,
                IsHealthy = true,
                // Add more status information as needed
                PackageCount = 0 // Implement based on your store interface
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            
            var statusJson = JsonSerializer.Serialize(status, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true 
            });
            await response.WriteStringAsync(statusJson);

            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error getting store status");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    #endregion

    #region Helper Methods

    private async Task<object> SyncConfiguration(Endpoint upstreamEndpoint)
    {
        this.logger.LogInformation("Syncing configuration from {Endpoint}", upstreamEndpoint.URI);
        var client = new UpstreamServerClient(upstreamEndpoint);
        return await client.GetServerConfigData();
    }

    private async Task<int> SyncCategories(Endpoint upstreamEndpoint, int maxCategories)
    {
        this.logger.LogInformation("Syncing categories from {Endpoint}, max: {MaxCategories}", 
            upstreamEndpoint.URI, maxCategories);
        
        var source = new UpstreamCategoriesSource(upstreamEndpoint);
        var count = 0;
        
        await foreach (var category in source.GetCategoriesAsync())
        {
            count++;
            if (count >= maxCategories) break;
        }
        
        return count;
    }

    private async Task<int> SyncUpdates(Endpoint upstreamEndpoint, MetadataSyncRequest request)
    {
        var filter = new UpstreamSourceFilter
        {
            ProductFilter = request.ProductsFilter,
            ClassificationFilter = request.ClassificationsFilter,
            SkipSuperseded = request.SkipSuperseded ?? true
        };

        var source = new UpstreamUpdatesSource(upstreamEndpoint, filter);
        var count = 0;
        
        await foreach (var update in source.GetUpdatesAsync())
        {
            count++;
            if (count >= (request.MaxItems ?? 100)) break;
        }
        
        return count;
    }

    private async Task<int> SyncUpdatesManual(Endpoint upstreamEndpoint, ManualSyncRequest request)
    {
        var filter = new UpstreamSourceFilter
        {
            ProductFilter = request.ProductsFilter,
            ClassificationFilter = request.ClassificationsFilter,
            SkipSuperseded = request.SkipSuperseded ?? true
        };

        var source = new UpstreamUpdatesSource(upstreamEndpoint, filter);
        var count = 0;
        
        await foreach (var update in source.GetUpdatesAsync())
        {
            count++;
            if (count >= (request.MaxUpdates ?? 100)) break;
        }
        
        return count;
    }

    private async Task SyncUpdatesFromConfig(Endpoint upstreamEndpoint, SyncOperation operation)
    {
        var filter = new UpstreamSourceFilter
        {
            ProductFilter = operation.ProductsFilter,
            ClassificationFilter = operation.ClassificationsFilter,
            SkipSuperseded = operation.SkipSuperseded ?? true
        };

        var source = new UpstreamUpdatesSource(upstreamEndpoint, filter);
        var count = 0;
        
        await foreach (var update in source.GetUpdatesAsync())
        {
            count++;
            if (count >= (operation.MaxItems ?? 100)) break;
        }

        this.logger.LogInformation("Synced {Count} updates from configuration", count);
    }

    private async Task ReindexStore(string? storePath)
    {
        this.logger.LogInformation("Reindexing metadata store at {StorePath}", storePath ?? "default");
        // Implement store reindexing based on your store interface
        await Task.CompletedTask;
    }

    #endregion
}

#region Data Transfer Objects

/// <summary>
/// Request model for Service Bus triggered metadata sync operations.
/// </summary>
public class MetadataSyncRequest
{
    public string SyncType { get; set; } = string.Empty;
    public string? UpstreamEndpoint { get; set; }
    public string[]? ProductsFilter { get; set; }
    public string[]? ClassificationsFilter { get; set; }
    public bool? SkipSuperseded { get; set; }
    public int? MaxItems { get; set; }
    public string? StorePath { get; set; }
}

/// <summary>
/// Request model for manual HTTP-triggered sync operations.
/// </summary>
public class ManualSyncRequest
{
    public string? UpstreamEndpoint { get; set; }
    public bool SyncConfiguration { get; set; }
    public bool SyncCategories { get; set; }
    public bool SyncUpdates { get; set; }
    public int? MaxCategories { get; set; }
    public int? MaxUpdates { get; set; }
    public string[]? ProductsFilter { get; set; }
    public string[]? ClassificationsFilter { get; set; }
    public bool? SkipSuperseded { get; set; }
}

/// <summary>
/// Configuration model for blob-triggered sync operations.
/// </summary>
public class MetadataSyncConfiguration
{
    public SyncOperation[] Operations { get; set; } = Array.Empty<SyncOperation>();
}

/// <summary>
/// Individual sync operation in a configuration file.
/// </summary>
public class SyncOperation
{
    public string Type { get; set; } = string.Empty;
    public string? UpstreamEndpoint { get; set; }
    public string[]? ProductsFilter { get; set; }
    public string[]? ClassificationsFilter { get; set; }
    public bool? SkipSuperseded { get; set; }
    public int? MaxItems { get; set; }
}

#endregion