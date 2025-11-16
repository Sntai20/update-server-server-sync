// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.Storage.Local;
using Microsoft.PackageGraph.Storage.Azure;
using Azure.Storage.Blobs;
using Microsoft.PackageGraph.MicrosoftUpdate.Endpoints.ClientSync;
using Microsoft.PackageGraph.MicrosoftUpdate.Endpoints.ServerSync;
using Microsoft.UpdateServices.WebServices.ClientSync;
using Microsoft.UpdateServices.WebServices.ServerSync;
using System.Text.Json;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMicrosoftUpdateServices(this IServiceCollection services, IConfiguration configuration)
    {
        RegisterJsonSerialization(services);
        RegisterMetadataStore(services, configuration);
        RegisterContentStore(services, configuration);
        RegisterBlobServiceClient(services, configuration);
        RegisterConfigurations(services, configuration);
        RegisterWebServices(services, configuration);
        RegisterAnomalyDetectionServices(services, configuration);

        return services;
    }

    private static void RegisterMetadataStore(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMetadataStore>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<IMetadataStore>>();
            
            // Production-first: Default to Azure Storage unless explicitly disabled
            var useLocalStorage = bool.Parse(configuration["UseLocalStorageForMetadata"] ?? "false");

            IMetadataStore store;

            if (!useLocalStorage)
            {
                // Azure Blob Storage for metadata (production default)
                var connectionString = configuration.GetConnectionString("MetadataStorageConnection")
                    ?? configuration["AzureWebJobsStorage"]
                    ?? configuration["AZURE_STORAGE_CONNECTION_STRING"]
                    ?? throw new InvalidOperationException(
                        "Azure storage connection string not found. Set 'MetadataStorageConnection' connection string, " +
                        "'AzureWebJobsStorage', or 'AZURE_STORAGE_CONNECTION_STRING' configuration value.");

                var containerName = configuration["MetadataContainerName"] ?? "metadata";

                logger.LogInformation(
                    "Initializing Azure Blob metadata store in container: {ContainerName}",
                    containerName);

                try
                {
                    var blobServiceClient = new BlobServiceClient(connectionString);
                    
                    // Create container if it doesn't exist
                    var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                    containerClient.CreateIfNotExists();

                    // Use library's native method
                    store = Microsoft.PackageGraph.Storage.Azure.PackageStore.OpenOrCreate(blobServiceClient, containerName);

                    logger.LogInformation(
                        "Azure Blob metadata store initialized - Account: {AccountName}, Container: {Container}",
                        blobServiceClient.AccountName,
                        containerName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to initialize Azure Blob metadata store");
                    throw;
                }
            }
            else
            {
                // Local file system storage for metadata (development fallback)
                var storePath = configuration["MetadataStorePath"] ?? "./store";

                logger.LogInformation(
                    "Using local file system metadata store for development at: {Path}",
                    storePath);

                try
                {
                    // Create directory if it doesn't exist
                    if (!Directory.Exists(storePath))
                    {
                        Directory.CreateDirectory(storePath);
                        logger.LogInformation("Created metadata directory: {Path}", storePath);
                    }

                    // Use library's native method
                    store = Microsoft.PackageGraph.Storage.Local.PackageStore.OpenOrCreate(storePath);

                    logger.LogInformation(
                        "Local metadata store initialized at: {Path}",
                        Path.GetFullPath(storePath));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to initialize local metadata store at: {Path}", storePath);
                    throw;
                }
            }

            // Check if reindexing is required
            if (store.IsMetadataIndexingSupported && store.IsReindexingRequired)
            {
                var pendingPackages = store.GetPendingPackages();
                logger.LogWarning(
                    "Metadata store requires reindexing. {PendingCount} packages pending indexing.",
                    pendingPackages.Count);
            }
            else
            {
                var packageCount = store.Count();
                logger.LogInformation(
                    "Metadata store ready with {PackageCount} packages",
                    packageCount);
            }

            return store;
        });
    }

    private static void RegisterContentStore(IServiceCollection services, IConfiguration configuration)
    {
        // Register as a factory that returns IContentStore (can be null)
        services.AddSingleton(typeof(IContentStore), provider =>
        {
            var logger = provider.GetRequiredService<ILogger<IContentStore>>();
            
            // Check if content store is explicitly disabled (catalog-only mode)
            var disableContentStore = bool.Parse(configuration["DisableContentStore"] ?? "false");
            if (disableContentStore)
            {
                logger.LogInformation("Content store explicitly disabled - running in catalog-only mode");
                return null!;
            }

            // Production-first: Default to Azure Storage unless explicitly using local storage
            var useLocalStorage = bool.Parse(configuration["UseLocalStorageForContent"] ?? "false");

            if (!useLocalStorage)
            {
                // Azure Blob Storage for content (production default)
                var connectionString = configuration.GetConnectionString("ContentStorageConnection")
                    ?? configuration.GetConnectionString("MetadataStorageConnection")
                    ?? configuration["AzureWebJobsStorage"]
                    ?? configuration["AZURE_STORAGE_CONNECTION_STRING"];
                    
                if (string.IsNullOrEmpty(connectionString))
                {
                    // Fallback to local storage if no Azure connection is available
                    logger.LogWarning("No Azure storage connection found - falling back to local content storage");
                    var localStorePath = configuration["ContentStorePath"] ?? "./content";
                    if (!string.IsNullOrEmpty(localStorePath))
                    {
                        var store = new FileSystemContentStore(localStorePath);
                        logger.LogInformation("Using local content store fallback at: {Path}", localStorePath);
                        return store;
                    }
                    else
                    {
                        logger.LogInformation("No content storage configured - running in catalog-only mode");
                        return null!;
                    }
                }

                var containerName = configuration["ContentContainerName"] ?? "content";
                var pathPrefix = configuration["ContentPathPrefix"] ?? "";

                logger.LogInformation(
                    "Initializing Azure Blob content store in container: {ContainerName}, path prefix: {PathPrefix}",
                    containerName, pathPrefix);

                try
                {
                    var blobServiceClient = new BlobServiceClient(connectionString);
                    
                    // Create container if it doesn't exist
                    var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                    containerClient.CreateIfNotExists();

                    // Use library's native method with path prefix
                    var store = BlobContentStore.OpenOrCreate(blobServiceClient, containerName, pathPrefix);

                    logger.LogInformation(
                        "Azure Blob content store initialized - Account: {AccountName}, Container: {Container}, PathPrefix: {PathPrefix}",
                        blobServiceClient.AccountName,
                        containerName, 
                        pathPrefix);
                        
                    return store;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to initialize Azure Blob content store");
                    throw;
                }
            }
            else
            {
                // Local file system storage for content (development fallback)
                var localStorePath = configuration["ContentStorePath"] ?? "./content";
                logger.LogInformation(
                    "Using local file system content store for development at: {Path}",
                    localStorePath);

                try
                {
                    // Create directory if it doesn't exist
                    if (!Directory.Exists(localStorePath))
                    {
                        Directory.CreateDirectory(localStorePath);
                        logger.LogInformation("Created content directory: {Path}", localStorePath);
                    }

                    // Use library's native constructor
                    var store = new FileSystemContentStore(localStorePath);

                    logger.LogInformation(
                        "Local content store initialized at: {Path}",
                        Path.GetFullPath(localStorePath));
                        
                    return store;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to initialize local content store at: {Path}", localStorePath);
                    throw;
                }
            }
        });
    }

    private static void RegisterBlobServiceClient(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<BlobServiceClient>>();
            
            try
            {
                var connectionString = configuration.GetConnectionString("MetadataStorageConnection")
                    ?? configuration["AzureWebJobsStorage"]
                    ?? configuration["AZURE_STORAGE_CONNECTION_STRING"];

                if (string.IsNullOrEmpty(connectionString))
                {
                    logger.LogInformation("No Azure Storage connection string configured - BlobServiceClient will be null");
                    return null as BlobServiceClient;
                }

                var blobServiceClient = new BlobServiceClient(connectionString);
                logger.LogInformation("BlobServiceClient registered successfully for account: {Account}", 
                    blobServiceClient.AccountName);
                return blobServiceClient;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to initialize BlobServiceClient - CSV export will be disabled");
                return null as BlobServiceClient;
            }
        });
    }

    private static void RegisterConfigurations(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(typeof(Microsoft.UpdateServices.WebServices.ClientSync.Config), provider =>
        {
            var serviceConfigJson = configuration["ServiceConfigurationJson"];
            if (string.IsNullOrEmpty(serviceConfigJson))
            {
                // Return default configuration instead of null to avoid null reference issues
                return new Microsoft.UpdateServices.WebServices.ClientSync.Config();
            }
            
            try
            {
                return JsonSerializer.Deserialize<Microsoft.UpdateServices.WebServices.ClientSync.Config>(serviceConfigJson) 
                    ?? new Microsoft.UpdateServices.WebServices.ClientSync.Config();
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Failed to deserialize Config from ServiceConfigurationJson", ex);
            }
        });

        services.AddSingleton(typeof(Microsoft.UpdateServices.WebServices.ServerSync.ServerSyncConfigData), provider =>
        {
            var serviceConfigJson = configuration["ServiceConfigurationJson"];
            if (string.IsNullOrEmpty(serviceConfigJson))
            {
                // Return default configuration instead of null to avoid null reference issues
                return new Microsoft.UpdateServices.WebServices.ServerSync.ServerSyncConfigData();
            }
            
            try
            {
                return JsonSerializer.Deserialize<Microsoft.UpdateServices.WebServices.ServerSync.ServerSyncConfigData>(serviceConfigJson) 
                    ?? new Microsoft.UpdateServices.WebServices.ServerSync.ServerSyncConfigData();
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Failed to deserialize ServerSyncConfigData from ServiceConfigurationJson", ex);
            }
        });
    }

    private static void RegisterWebServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ClientSyncWebService>(provider =>
        {
            var service = new ClientSyncWebService();
            var metadataStore = provider.GetRequiredService<IMetadataStore>();
            var contentStore = provider.GetService<IContentStore>();
            var config = provider.GetService(typeof(Microsoft.UpdateServices.WebServices.ClientSync.Config)) as Microsoft.UpdateServices.WebServices.ClientSync.Config;
            var contentRoot = configuration["ContentHttpRoot"];
            var logger = provider.GetRequiredService<ILogger<ClientSyncWebService>>();

            try
            {
                service.SetPackageStore(metadataStore);

                // Validate store readiness
                var packageCount = metadataStore.Count();
                if (packageCount == 0)
                {
                    logger.LogWarning(
                        "Metadata store is empty. Service will not be able to serve updates. " +
                        "Run metadata sync first.");
                }
                else
                {
                    logger.LogInformation(
                        "ClientSyncWebService initialized with {PackageCount} packages",
                        packageCount);
                }

                // Check for pending packages requiring indexing
                if (metadataStore.IsReindexingRequired)
                {
                    var pendingPackages = metadataStore.GetPendingPackages();
                    logger.LogWarning(
                        "{PendingCount} packages pending indexing. Some queries may be slow.",
                        pendingPackages.Count);
                }

                if (config != null)
                {
                    service.SetServiceConfiguration(config);
                    logger.LogInformation("Client sync service configuration loaded");
                }

                if (contentStore != null && !string.IsNullOrEmpty(contentRoot))
                {
                    service.SetContentURLBase(contentRoot);
                    logger.LogInformation("Content URL base set to: {ContentRoot}", contentRoot);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error configuring ClientSyncWebService");
                throw;
            }

            return service;
        });

        services.AddScoped<ServerSyncWebService>(provider =>
        {
            var service = new ServerSyncWebService();
            var metadataStore = provider.GetRequiredService<IMetadataStore>();
            var config = provider.GetService(typeof(Microsoft.UpdateServices.WebServices.ServerSync.ServerSyncConfigData)) as Microsoft.UpdateServices.WebServices.ServerSync.ServerSyncConfigData;
            var contentStore = provider.GetService<IContentStore>();
            var logger = provider.GetRequiredService<ILogger<ServerSyncWebService>>();

            try
            {
                service.SetPackageStore(metadataStore);

                // Validate store readiness
                var packageCount = metadataStore.Count();
                logger.LogInformation(
                    "ServerSyncWebService initialized with {PackageCount} packages",
                    packageCount);

                if (config != null)
                {
                    // Set catalog-only mode based on content store availability
                    config.CatalogOnlySync = contentStore == null;
                    service.SetServerConfiguration(config);
                    logger.LogInformation(
                        "Server sync service configuration loaded (CatalogOnly: {CatalogOnly})",
                        config.CatalogOnlySync);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error configuring ServerSyncWebService");
                throw;
            }

            return service;
        });

        services.AddScoped<SimpleAuthenticationWebService>();
        services.AddScoped<AuthenticationWebService>();
    }

    
    private static void RegisterJsonSerialization(IServiceCollection services)
    {
        // Configure global JSON serialization options for Azure Functions
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        
        // Register as singleton for dependency injection
        services.AddSingleton(jsonOptions);
        
        // Also configure for IOptions<JsonSerializerOptions> if needed
        services.Configure<JsonSerializerOptions>(opts =>
        {
            opts.PropertyNamingPolicy = jsonOptions.PropertyNamingPolicy;
            opts.WriteIndented = jsonOptions.WriteIndented;
            opts.DefaultIgnoreCondition = jsonOptions.DefaultIgnoreCondition;
        });
    }

    private static void RegisterAnomalyDetectionServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register sync and query services (needed for anomaly detection data sources)
        services.AddScoped<ISyncService, SyncService>();
        services.AddScoped<IQueryService, QueryService>();
        services.AddScoped<IHealthService, HealthService>();

        // Register anomaly detection and queue services as singletons for performance
        services.AddSingleton<IAnomalyDetectionService, AnomalyDetectionService>();
        services.AddSingleton<IQueueService, QueueService>();
    }
}