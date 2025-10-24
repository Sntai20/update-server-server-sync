using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MicrosoftUpdateFunctions.Services;
using System.Text.Json; // Add this using directive for JsonSerializer
using Microsoft.Extensions.Options; // Add this using directive for IOptions
using MicrosoftUpdateFunctions.Models; // Add this using directive for ServiceConfiguration

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        // Bind configuration from ServiceConfigurationJson environment variable
        var configJson = context.Configuration["ServiceConfigurationJson"];
        if (!string.IsNullOrEmpty(configJson))
        {
            var config = JsonSerializer.Deserialize<ServiceConfiguration>(configJson);
            services.AddSingleton(config ?? new ServiceConfiguration());
        }
        
        // Or bind from individual settings
        services.Configure<ServiceConfiguration>(
            context.Configuration.GetSection("ServiceConfiguration"));
        
        // Register all Microsoft Update services using the extension method
        services.AddMicrosoftUpdateServices(context.Configuration);
        
        // Register the service layer
        services.AddScoped<ISyncService, SyncService>();
        services.AddScoped<IQueryService, QueryService>();
        services.AddScoped<IHealthService, HealthService>();
    })
    .Build();

host.Run();