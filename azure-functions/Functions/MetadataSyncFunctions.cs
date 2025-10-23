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
/// Azure Functions for metadata synchronization operations.
/// These functions provide the same capabilities as the upsync tool but in a serverless environment.
/// </summary>
public class MetadataSyncFunctions
{
    private readonly ILogger<MetadataSyncFunctions> logger;
    private readonly IMetadataStore? metadataStore;

    public MetadataSyncFunctions(ILogger<MetadataSyncFunctions> logger, IMetadataStore? metadataStore)
    {
        this.logger = logger;
        this.metadataStore = metadataStore;
    }

    /// <summary>
    /// Fetches server configuration data from upstream Microsoft Update endpoint.
    /// Equivalent to: upsync fetch-config
    /// </summary>
    [Function("FetchConfiguration")]
    public async Task<HttpResponseData> FetchConfiguration(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        this.logger.LogInformation("FetchConfiguration function started");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var options = JsonSerializer.Deserialize<FetchConfigurationRequest>(requestBody);

            if (options == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid request body");
                return badRequest;
            }

            // Set default upstream endpoint if not provided
            var upstreamEndpoint = string.IsNullOrEmpty(options.UpstreamEndpoint) 
                ? Microsoft.PackageGraph.MicrosoftUpdate.Source.Endpoint.Default 
                : new Microsoft.PackageGraph.MicrosoftUpdate.Source.Endpoint(options.UpstreamEndpoint);

            var server = new UpstreamServerClient(upstreamEndpoint);
            var configData = await server.GetServerConfigData();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            
            var configJson = JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true });
            await response.WriteStringAsync(configJson);

            this.logger.LogInformation("Configuration fetched successfully from {Endpoint}", upstreamEndpoint.URI);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error fetching configuration");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Fetches and stores product categories and classifications from upstream.
    /// Equivalent to: upsync fetch-categories
    /// </summary>
    [Function("FetchCategories")]
    public async Task<HttpResponseData> FetchCategories(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        this.logger.LogInformation("FetchCategories function started");

        if (this.metadataStore == null)
        {
            var configError = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            await configError.WriteStringAsync("Metadata store not configured");
            return configError;
        }

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var options = JsonSerializer.Deserialize<FetchCategoriesRequest>(requestBody);

            if (options == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid request body");
                return badRequest;
            }

            var upstreamEndpoint = string.IsNullOrEmpty(options.UpstreamEndpoint) 
                ? Microsoft.PackageGraph.MicrosoftUpdate.Source.Endpoint.Default 
                : new Microsoft.PackageGraph.MicrosoftUpdate.Source.Endpoint(options.UpstreamEndpoint);

            this.logger.LogInformation("Fetching categories from {Endpoint}", upstreamEndpoint.URI);

            var categoriesSource = new UpstreamCategoriesSource(upstreamEndpoint);
            var cancellationToken = new CancellationTokenSource();
            
            // Copy categories to metadata store
            categoriesSource.CopyTo(this.metadataStore, cancellationToken.Token);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Categories fetched and stored successfully");

            this.logger.LogInformation("Categories fetched successfully");
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error fetching categories");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Fetches update metadata from upstream based on filter criteria.
    /// Equivalent to: upsync fetch-updates
    /// </summary>
    [Function("FetchUpdates")]
    public async Task<HttpResponseData> FetchUpdates(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        this.logger.LogInformation("FetchUpdates function started");

        if (this.metadataStore == null)
        {
            var configError = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            await configError.WriteStringAsync("Metadata store not configured");
            return configError;
        }

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var options = JsonSerializer.Deserialize<FetchUpdatesRequest>(requestBody);

            if (options == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid request body");
                return badRequest;
            }

            var upstreamEndpoint = string.IsNullOrEmpty(options.UpstreamEndpoint) 
                ? Microsoft.PackageGraph.MicrosoftUpdate.Source.Endpoint.Default 
                : new Microsoft.PackageGraph.MicrosoftUpdate.Source.Endpoint(options.UpstreamEndpoint);

            this.logger.LogInformation("Fetching updates from {Endpoint}", upstreamEndpoint.URI);

            // First ensure categories are available
            var categoriesSource = new UpstreamCategoriesSource(upstreamEndpoint);
            var cancellationToken = new CancellationTokenSource();
            categoriesSource.CopyTo(this.metadataStore, cancellationToken.Token);

            // Handle specific update IDs if provided
            if (options.UpdateIds?.Any() == true)
            {
                var server = new UpstreamServerClient(upstreamEndpoint);
                
                foreach (var updateId in options.UpdateIds)
                {
                    if (Guid.TryParse(updateId, out var updateIdGuid))
                    {
                        this.logger.LogInformation("Searching for update {UpdateId}", updateId);
                        var foundPackage = await server.TryGetExpiredUpdate(updateIdGuid, 300, 100);
                        
                        if (foundPackage != null)
                        {
                            this.metadataStore.AddPackage(foundPackage);
                            this.logger.LogInformation("Added update {UpdateId}", updateId);
                        }
                        else
                        {
                            this.logger.LogWarning("Update {UpdateId} not found", updateId);
                        }
                    }
                    else
                    {
                        this.logger.LogError("Invalid GUID format: {UpdateId}", updateId);
                    }
                }
            }
            else
            {
                // Fetch updates based on filter criteria
                var sourceFilter = CreateFilterFromRequest(options, this.metadataStore);
                var updatesSource = new UpstreamUpdatesSource(upstreamEndpoint, sourceFilter);
                updatesSource.CopyTo(this.metadataStore, cancellationToken.Token);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Updates fetched and stored successfully");

            this.logger.LogInformation("Updates fetched successfully");
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error fetching updates");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Rebuilds the metadata store search index.
    /// Equivalent to: upsync reindex
    /// </summary>
    [Function("ReindexStore")]
    public async Task<HttpResponseData> ReindexStore(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        this.logger.LogInformation("ReindexStore function started");

        if (this.metadataStore == null)
        {
            var configError = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            await configError.WriteStringAsync("Metadata store not configured");
            return configError;
        }

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var options = JsonSerializer.Deserialize<ReindexRequest>(requestBody);

            if (!this.metadataStore.IsMetadataIndexingSupported)
            {
                var notSupported = req.CreateResponse(HttpStatusCode.BadRequest);
                await notSupported.WriteStringAsync("Metadata store does not support indexing");
                return notSupported;
            }

            bool forceReindex = options?.ForceReindex ?? false;

            if (this.metadataStore.IsReindexingRequired || forceReindex)
            {
                this.logger.LogInformation("Starting reindexing (force: {ForceReindex})", forceReindex);
                this.metadataStore.ReIndex();
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync("Store reindexed successfully");
                
                this.logger.LogInformation("Reindexing completed");
                return response;
            }
            else
            {
                var notRequired = req.CreateResponse(HttpStatusCode.OK);
                await notRequired.WriteStringAsync("Reindexing not required");
                return notRequired;
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during reindexing");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Gets the current status of the metadata store.
    /// </summary>
    [Function("GetStoreStatus")]
    public async Task<HttpResponseData> GetStoreStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        this.logger.LogInformation("GetStoreStatus function started");

        try
        {
            var status = new
            {
                IsConfigured = this.metadataStore != null,
                SupportsIndexing = this.metadataStore?.IsMetadataIndexingSupported ?? false,
                RequiresReindexing = this.metadataStore?.IsReindexingRequired ?? false,
                PackageCount = this.metadataStore?.Count() ?? 0,
                LastUpdated = DateTime.UtcNow
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            
            var statusJson = JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
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

    private static UpstreamSourceFilter CreateFilterFromRequest(FetchUpdatesRequest options, IMetadataStore metadataStore)
    {
        // Create product filter
        var productFilter = new List<Guid>();
        if (options.ProductFilters?.Any() == true)
        {
            foreach (var guidString in options.ProductFilters)
            {
                if (Guid.TryParse(guidString, out var guid))
                {
                    productFilter.Add(guid);
                }
            }
        }
        else
        {
            // Use all available products if no filter specified
            productFilter = metadataStore.OfType<Microsoft.PackageGraph.MicrosoftUpdate.Metadata.ProductCategory>()
                .Select(p => p.Id.ID)
                .ToList();
        }

        // Create classification filter  
        var classificationFilter = new List<Guid>();
        if (options.ClassificationFilters?.Any() == true)
        {
            foreach (var guidString in options.ClassificationFilters)
            {
                if (Guid.TryParse(guidString, out var guid))
                {
                    classificationFilter.Add(guid);
                }
            }
        }
        else
        {
            // Use all available classifications if no filter specified
            classificationFilter = metadataStore.OfType<Microsoft.PackageGraph.MicrosoftUpdate.Metadata.ClassificationCategory>()
                .Select(c => c.Id.ID)
                .ToList();
        }

        return new UpstreamSourceFilter(productFilter, classificationFilter);
    }
}

// Request/Response models for the metadata sync functions
public class FetchConfigurationRequest
{
    public string? UpstreamEndpoint { get; set; }
}

public class FetchCategoriesRequest
{
    public string? UpstreamEndpoint { get; set; }
    public string? AccountName { get; set; }
    public string? AccountGuid { get; set; }
}

public class FetchUpdatesRequest
{
    public string? UpstreamEndpoint { get; set; }
    public string? AccountName { get; set; }
    public string? AccountGuid { get; set; }
    public List<string>? UpdateIds { get; set; }
    public List<string>? ProductFilters { get; set; }
    public List<string>? ClassificationFilters { get; set; }
}

public class ReindexRequest
{
    public bool ForceReindex { get; set; }
}