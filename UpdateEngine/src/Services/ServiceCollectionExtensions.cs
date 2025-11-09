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
            
            // Try multiple connection string names - AzureWebJobsStorage is provided by WithHostStorage()
            var connectionString = configuration.GetConnectionString("MetadataStorageConnection")
                ?? configuration["AzureWebJobsStorage"];  // Fallback to the Functions host storage
            
            var useAzureStorageForMetadata = bool.Parse(configuration["UseAzureStorageForMetadata"] ?? "false");
            var storePath = configuration["MetadataStorePath"] ?? "./store";
            var containerName = configuration["MetadataContainerName"] ?? "metadata";
            var storeType = useAzureStorageForMetadata ? "azure" : "local";

            return StorageFactory.CreateMetadataStore(
                storePath,
                storeType,
                connectionString,
                containerName,
                createIfNotExists: true,
                logger);
        });
    }

    private static void RegisterContentStore(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IContentStore?>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<IContentStore>>();
            
            // Try multiple connection string names - AzureWebJobsStorage is provided by WithHostStorage()
            var connectionString = configuration.GetConnectionString("ContentStorageConnection")
                ?? configuration["AzureWebJobsStorage"];  // Fallback to the Functions host storage
            
            var useAzureStorageForContent = bool.Parse(configuration["UseAzureStorageForContent"] ?? "false");
            var storePath = configuration["ContentStorePath"];
            var containerName = configuration["ContentContainerName"] ?? "content";
            var storeType = useAzureStorageForContent ? "azureblob" : "local";

            return StorageFactory.CreateContentStore(
                storePath,
                storeType,
                connectionString,
                containerName,
                createIfNotExists: true,
                logger);
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