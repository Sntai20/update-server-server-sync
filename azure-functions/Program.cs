using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.Storage.Local;
using Microsoft.PackageGraph.MicrosoftUpdate.Endpoints.ClientSync;
using Microsoft.PackageGraph.MicrosoftUpdate.Endpoints.ServerSync;
using Microsoft.UpdateServices.WebServices.ClientSync;
using Microsoft.UpdateServices.WebServices.ServerSync;
using Newtonsoft.Json;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // Register metadata store
        services.AddSingleton<IMetadataStore>(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var metadataPath = configuration["MetadataStorePath"] ?? "./store";
            return PackageStore.Open(metadataPath);
        });

        // Register content store (optional)
        services.AddSingleton(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var contentPath = configuration["ContentStorePath"];
            if (!string.IsNullOrEmpty(contentPath))
            {
                return new FileSystemContentStore(contentPath);
            }
            return (IContentStore?)null;
        });

        // Register configuration objects
        services.AddSingleton(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var serviceConfigJson = configuration["ServiceConfigurationJson"];
            if (!string.IsNullOrEmpty(serviceConfigJson))
            {
                try 
                {
                    return JsonConvert.DeserializeObject<Config>(serviceConfigJson) ?? new Config();
                }
                catch
                {
                    return new Config();
                }
            }
            return new Config();
        });

        services.AddSingleton(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var serviceConfigJson = configuration["ServiceConfigurationJson"];
            if (!string.IsNullOrEmpty(serviceConfigJson))
            {
                try
                {
                    return JsonConvert.DeserializeObject<ServerSyncConfigData>(serviceConfigJson) ?? new ServerSyncConfigData();
                }
                catch 
                {
                    return new ServerSyncConfigData();
                }
            }
            return new ServerSyncConfigData();
        });

        // Register web services
        services.AddScoped<ClientSyncWebService>();
        services.AddScoped<ServerSyncWebService>();
        services.AddScoped<SimpleAuthenticationWebService>();
        services.AddScoped<AuthenticationWebService>();
        // Note: ReportingWebService has conflicts between client and server - register separately if needed
    })
    .Build();

host.Run();