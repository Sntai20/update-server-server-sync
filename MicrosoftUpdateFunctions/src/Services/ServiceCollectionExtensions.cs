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

namespace MicrosoftUpdateFunctions.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMicrosoftUpdateServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register metadata store with dynamic storage selection
            services.AddSingleton<IMetadataStore>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<IMetadataStore>>();
                var metadataConnectionString = configuration.GetConnectionString("MetadataStorageConnection");
                var useAzureStorage = bool.Parse(configuration["UseAzureStorage"] ?? "false");
                
                logger.LogInformation("🔍 Metadata Store Init: UseAzure={UseAzure}, HasConnectionString={HasConnection}", 
                    useAzureStorage, !string.IsNullOrEmpty(metadataConnectionString));
                
                if (useAzureStorage && !string.IsNullOrEmpty(metadataConnectionString))
                {
                    logger.LogInformation("Using Azure Blob Storage for metadata store");
                    var blobServiceClient = new BlobServiceClient(metadataConnectionString);
                    var containerName = configuration["MetadataContainerName"] ?? "metadata";
                    
                    return Microsoft.PackageGraph.Storage.Azure.PackageStore.OpenOrCreate(blobServiceClient, containerName);
                }
                else
                {
                    var metadataPath = configuration["MetadataStorePath"] ?? "./store";
                    logger.LogInformation("Using local file system for metadata store: '{MetadataPath}'", metadataPath);
                    
                    // Ensure directory exists
                    if (!Directory.Exists(metadataPath))
                    {
                        Directory.CreateDirectory(metadataPath);
                    }
                    
                    return Microsoft.PackageGraph.Storage.Local.PackageStore.Open(metadataPath);
                }
            });

            // Register content store with dynamic storage selection (optional)
            services.AddSingleton<IContentStore?>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<IContentStore>>();
                var contentConnectionString = configuration.GetConnectionString("ContentStorageConnection");
                var useAzureStorage = bool.Parse(configuration["UseAzureStorage"] ?? "false");
                
                logger.LogInformation("🔍 Content Store Init: UseAzure={UseAzure}, HasConnectionString={HasConnection}", 
                    useAzureStorage, !string.IsNullOrEmpty(contentConnectionString));
                
                if (useAzureStorage && !string.IsNullOrEmpty(contentConnectionString))
                {
                    logger.LogInformation("Using Azure Blob Storage for content store");
                    var blobServiceClient = new BlobServiceClient(contentConnectionString);
                    var containerName = configuration["ContentContainerName"] ?? "content";
                    
                    return BlobContentStore.OpenOrCreate(blobServiceClient, containerName);
                }
                else
                {
                    var contentPath = configuration["ContentStorePath"];
                    
                    if (string.IsNullOrEmpty(contentPath))
                    {
                        logger.LogInformation("No content storage configured");
                        return null;
                    }
                    
                    logger.LogInformation("Using local file system for content store: '{ContentPath}'", contentPath);
                    
                    // Ensure directory exists
                    if (!Directory.Exists(contentPath))
                    {
                        Directory.CreateDirectory(contentPath);
                    }
                    
                    return new FileSystemContentStore(contentPath);
                }
            });

            // Register configuration objects
            services.AddSingleton<Config?>(provider =>
            {
                var serviceConfigJson = configuration["ServiceConfigurationJson"];
                if (!string.IsNullOrEmpty(serviceConfigJson))
                {
                    return JsonSerializer.Deserialize<Config>(serviceConfigJson);
                }
                return null;
            });

            services.AddSingleton<ServerSyncConfigData?>(provider =>
            {
                var serviceConfigJson = configuration["ServiceConfigurationJson"];
                if (!string.IsNullOrEmpty(serviceConfigJson))
                {
                    return JsonSerializer.Deserialize<ServerSyncConfigData>(serviceConfigJson);
                }
                return null;
            });

            // Register web services
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
                        logger.LogInformation($"Content URL base set to: {contentRoot}");
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
                        logger.LogInformation($"Server sync service configuration loaded (CatalogOnly: {config.CatalogOnlySync})");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error configuring ServerSyncWebService");
                    throw;
                }

                return service;
            });

            // Register other web services
            services.AddScoped<SimpleAuthenticationWebService>();
            services.AddScoped<AuthenticationWebService>();
            // Note: ReportingWebService has conflicts - register separately if needed

            return services;
        }
    }
}