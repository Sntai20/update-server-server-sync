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

namespace UpdateEngine.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMicrosoftUpdateServices(this IServiceCollection services, IConfiguration configuration)
        {
            RegisterMetadataStore(services, configuration);
            RegisterContentStore(services, configuration);
            RegisterConfigurations(services, configuration);
            RegisterWebServices(services, configuration);

            return services;
        }

        private static void RegisterMetadataStore(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IMetadataStore>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<IMetadataStore>>();
                var metadataConnectionString = configuration.GetConnectionString("MetadataStorageConnection");
                var useAzureStorage = bool.Parse(configuration["UseAzureStorage"] ?? "false");

                logger.LogInformation(
                    "Initializing metadata store - UseAzure: {UseAzure}, HasConnection: {HasConnection}",
                    useAzureStorage,
                    !string.IsNullOrEmpty(metadataConnectionString));

                if (useAzureStorage && !string.IsNullOrEmpty(metadataConnectionString))
                {
                    logger.LogInformation("Using Azure Blob Storage for metadata store");
                    
                    try
                    {
                        var blobServiceClient = new BlobServiceClient(metadataConnectionString);
                        var containerName = configuration["MetadataContainerName"] ?? "metadata";
 
                        // Verify connection and create container
                        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                        var createResponse = containerClient.CreateIfNotExists();
                        
                        if (createResponse != null)
                        {
                            logger.LogInformation("Created Azure Blob container: {ContainerName}", containerName);
                        }
                        else
                        {
                            logger.LogInformation("Azure Blob container already exists: {ContainerName}", containerName);
                        }
  
                        logger.LogInformation("Successfully connected to Azure Storage account: {AccountName}", 
                            blobServiceClient.AccountName);

                        return Microsoft.PackageGraph.Storage.Azure.PackageStore.OpenOrCreate(blobServiceClient, containerName);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to initialize Azure Blob Storage for metadata store. Connection string: {ConnectionString}", 
                            metadataConnectionString?.Substring(0, Math.Min(100, metadataConnectionString.Length)));
                        throw;
                    }
                }

                var metadataPath = configuration["MetadataStorePath"] ?? "./store";
                logger.LogInformation("Using local file system for metadata store: '{MetadataPath}'", metadataPath);

                // Ensure directory exists
                if (!Directory.Exists(metadataPath))
                {
                    Directory.CreateDirectory(metadataPath);
                    logger.LogInformation("Created metadata directory: {Path}", metadataPath);
                }

                return Microsoft.PackageGraph.Storage.Local.PackageStore.OpenOrCreate(metadataPath);
            });
        }

        private static void RegisterContentStore(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IContentStore?>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<IContentStore>>();
                var contentConnectionString = configuration.GetConnectionString("ContentStorageConnection");
                var useAzureStorage = bool.Parse(configuration["UseAzureStorage"] ?? "false");

                logger.LogInformation(
                    "Initializing content store - UseAzure: {UseAzure}, HasConnection: {HasConnection}",
                    useAzureStorage,
                    !string.IsNullOrEmpty(contentConnectionString));

                if (useAzureStorage && !string.IsNullOrEmpty(contentConnectionString))
                {
                    logger.LogInformation("Using Azure Blob Storage for content store");
        
                    try
                    {
                        var blobServiceClient = new BlobServiceClient(contentConnectionString);
                        var containerName = configuration["ContentContainerName"] ?? "content";
         
                        // Verify connection and create container
                        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                        var createResponse = containerClient.CreateIfNotExists();
     
                        if (createResponse != null)
                        {
                            logger.LogInformation("Created Azure Blob container: {ContainerName}", containerName);
                        }
                        else
                        {
                            logger.LogInformation("Azure Blob container already exists: {ContainerName}", containerName);
                        }
        
                        logger.LogInformation("Successfully connected to Azure Storage account: {AccountName}", 
                            blobServiceClient.AccountName);

                        return BlobContentStore.OpenOrCreate(blobServiceClient, containerName);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to initialize Azure Blob Storage for content store");
                        throw;
                    }
                }

                var contentPath = configuration["ContentStorePath"];

                if (string.IsNullOrEmpty(contentPath))
                {
                    logger.LogInformation("Content storage not configured");
                    return null;
                }

                logger.LogInformation("Using local file system for content store: '{ContentPath}'", contentPath);

                // Ensure directory exists
                if (!Directory.Exists(contentPath))
                {
                    Directory.CreateDirectory(contentPath);
                    logger.LogInformation("Created content directory: {Path}", contentPath);
                }

                return new FileSystemContentStore(contentPath);
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
    }
}