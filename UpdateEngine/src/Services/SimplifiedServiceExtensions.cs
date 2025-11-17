// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.Storage.Local;
using Microsoft.PackageGraph.Storage.Azure;
using Azure.Storage.Blobs;
using Configuration;
using System.Text.Json;
using Microsoft.PackageGraph.MicrosoftUpdate.Endpoints.ClientSync;
using Microsoft.PackageGraph.MicrosoftUpdate.Endpoints.ServerSync;

namespace UpdateEngine.Services;

public static class SimplifiedServiceExtensions
{
    public static IServiceCollection AddUpdateEngineServices(this IServiceCollection services, IConfiguration configuration, AppConfig appConfig)
    {
        RegisterMetadataStore(services, configuration, appConfig);
        RegisterContentStore(services, configuration, appConfig);
        RegisterWebServices(services, configuration, appConfig);
        
        return services;
    }

    private static void RegisterMetadataStore(IServiceCollection services, IConfiguration configuration, AppConfig appConfig)
    {
        services.AddSingleton<IMetadataStore>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<IMetadataStore>>();
            
            if (appConfig.UseAzureStorageForMetadata)
            {
                // Azure Blob Storage for metadata
                var connectionString = configuration.GetConnectionString("MetadataStorage") 
                    ?? configuration["AzureWebJobsStorage"]
                    ?? throw new InvalidOperationException("No storage connection string found for metadata");

                var containerName = appConfig.MetadataContainerName;
                logger.LogInformation("Using Azure Blob Storage for metadata: container '{ContainerName}'", containerName);
                
                return Microsoft.PackageGraph.Storage.Azure.PackageStore.Open(new BlobServiceClient(connectionString), containerName);
            }
            else
            {
                // Local file system for metadata
                logger.LogInformation("Using local file system for metadata: '{Path}'", appConfig.MetadataPath);
                return Microsoft.PackageGraph.Storage.Local.PackageStore.Open(appConfig.MetadataPath);
            }
        });
    }

    private static void RegisterContentStore(IServiceCollection services, IConfiguration configuration, AppConfig appConfig)
    {
        services.AddSingleton<IContentStore?>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<IContentStore>>();
            
            if (appConfig.UseAzureStorageForContent)
            {
                // Azure Blob Storage for content
                var connectionString = configuration.GetConnectionString("ContentStorage")
                    ?? configuration["AzureWebJobsStorage"]
                    ?? throw new InvalidOperationException("No storage connection string found for content");

                var containerName = appConfig.ContentContainerName;
                logger.LogInformation("Using Azure Blob Storage for content: container '{ContainerName}'", containerName);
                
                var blobClient = new BlobServiceClient(connectionString);
                return Microsoft.PackageGraph.Storage.Azure.BlobContentStore.OpenOrCreate(blobClient, containerName);
            }
            else if (!string.IsNullOrEmpty(appConfig.ContentPath))
            {
                // Local file system for content
                logger.LogInformation("Using local file system for content: '{Path}'", appConfig.ContentPath);
                return new FileSystemContentStore(appConfig.ContentPath);
            }
            else
            {
                // No content store configured
                logger.LogInformation("No content store configured");
                return null;
            }
        });
    }

    private static void RegisterWebServices(IServiceCollection services, IConfiguration configuration, AppConfig appConfig)
    {
        // Register service configuration as JSON for compatibility
        var serviceConfig = new
        {
            ServiceUrl = appConfig.ServiceUrl,
            ContentUrl = appConfig.ContentUrl
        };
        
        var serviceConfigJson = JsonSerializer.Serialize(serviceConfig);
        services.AddSingleton<string>(provider => serviceConfigJson);
        
        // Register web services (if needed by existing code)
        services.AddScoped<ClientSyncWebService>();
        services.AddScoped<ServerSyncWebService>();
    }
}