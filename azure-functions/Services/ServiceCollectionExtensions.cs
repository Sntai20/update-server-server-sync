using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.Storage.Local;
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
            // Register metadata store
            services.AddSingleton<IMetadataStore>(provider =>
            {
                var metadataPath = configuration["MetadataStorePath"] ?? "./store";
                return PackageStore.Open(metadataPath);
            });

            // Register content store (optional)
            services.AddSingleton<IContentStore?>(provider =>
            {
                var contentPath = configuration["ContentStorePath"];
                if (!string.IsNullOrEmpty(contentPath))
                {
                    return new FileSystemContentStore(contentPath);
                }
                return null;
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