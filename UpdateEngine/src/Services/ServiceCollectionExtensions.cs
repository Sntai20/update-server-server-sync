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
        RegisterMetadataStore(services, configuration);
        RegisterContentStore(services, configuration);
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
            var useAzure = bool.Parse(configuration["UseAzureStorageForMetadata"] ?? "false");

            IMetadataStore store;

            if (useAzure)
            {
                // Azure Blob Storage for metadata
                var connectionString = configuration.GetConnectionString("MetadataStorageConnection")
                    ?? configuration["AzureWebJobsStorage"]
                    ?? throw new InvalidOperationException(
                        "Azure storage connection string not found. Set either 'MetadataStorageConnection' " +
                        "connection string or 'AzureWebJobsStorage' configuration value.");

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
                // Local file system storage for metadata
                var storePath = configuration["MetadataStorePath"] ?? "./store";

                logger.LogInformation(
                    "Initializing local file system metadata store at: {Path}",
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
        services.AddSingleton<IContentStore?>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<IContentStore>>();
            var storePath = configuration["ContentStorePath"];

            // Content store is optional - return null for catalog-only mode
            if (string.IsNullOrEmpty(storePath))
            {
                logger.LogInformation("Content store path not configured - running in catalog-only mode");
                return null;
            }

            var useAzure = bool.Parse(configuration["UseAzureStorageForContent"] ?? "false");
            IContentStore store;

            if (useAzure)
            {
                // Azure Blob Storage for content
                var connectionString = configuration.GetConnectionString("ContentStorageConnection")
                    ?? configuration["AzureWebJobsStorage"]
                    ?? throw new InvalidOperationException(
                        "Azure storage connection string not found. Set either 'ContentStorageConnection' " +
                        "connection string or 'AzureWebJobsStorage' configuration value.");

                var containerName = configuration["ContentContainerName"] ?? "content";

                logger.LogInformation(
                    "Initializing Azure Blob content store in container: {ContainerName}",
                    containerName);

                try
                {
                    var blobServiceClient = new BlobServiceClient(connectionString);
                    
                    // Create container if it doesn't exist
                    var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                    containerClient.CreateIfNotExists();

                    // Use library's native method
                    store = BlobContentStore.OpenOrCreate(blobServiceClient, containerName);

                    logger.LogInformation(
                        "Azure Blob content store initialized - Account: {AccountName}, Container: {Container}",
                        blobServiceClient.AccountName,
                        containerName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to initialize Azure Blob content store");
                    throw;
                }
            }
            else
            {
                // Local file system storage for content
                logger.LogInformation(
                    "Initializing local file system content store at: {Path}",
                    storePath);

                try
                {
                    // Create directory if it doesn't exist
                    if (!Directory.Exists(storePath))
                    {
                        Directory.CreateDirectory(storePath);
                        logger.LogInformation("Created content directory: {Path}", storePath);
                    }

                    // Use library's native constructor
                    store = new FileSystemContentStore(storePath);

                    logger.LogInformation(
                        "Local content store initialized at: {Path}",
                        Path.GetFullPath(storePath));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to initialize local content store at: {Path}", storePath);
                    throw;
                }
            }

            return store;
        });
    }

    private static void RegisterConfigurations(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(provider =>
        {
            var serviceConfigJson = configuration["ServiceConfigurationJson"];
            if (!string.IsNullOrEmpty(serviceConfigJson))
            {
                return JsonSerializer.Deserialize<Config>(serviceConfigJson);
            }
            return null as Config;
        });

        services.AddSingleton(provider =>
        {
            var serviceConfigJson = configuration["ServiceConfigurationJson"];
            if (!string.IsNullOrEmpty(serviceConfigJson))
            {
                return JsonSerializer.Deserialize<ServerSyncConfigData>(serviceConfigJson);
            }
            return null as ServerSyncConfigData;
        });
    }

    private static void RegisterWebServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ClientSyncWebService>(provider =>
        {
            var service = new ClientSyncWebService();
            var metadataStore = provider.GetRequiredService<IMetadataStore>();
            var contentStore = provider.GetService<IContentStore?>();
            var config = provider.GetService<Config?>();
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
            var config = provider.GetService<ServerSyncConfigData?>();
            var contentStore = provider.GetService<IContentStore?>();
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