using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using MicrosoftUpdateFunctions.Models;
using MicrosoftUpdateFunctions.Services;
using System.Text.Json;

var hostBuilder = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        // 🔍 ADD DIAGNOSTIC LOGGING FIRST - Before anything can fail
        var tempLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("Startup");
        
        tempLogger.LogInformation("=== Storage Configuration Diagnostics ===");
        tempLogger.LogInformation("UseAzureStorage: {UseAzureStorage}", context.Configuration["UseAzureStorage"]);
        tempLogger.LogInformation("MetadataStorePath: {MetadataStorePath}", context.Configuration["MetadataStorePath"]);
        tempLogger.LogInformation("ContentStorePath: {ContentStorePath}", context.Configuration["ContentStorePath"]);
        tempLogger.LogInformation("MetadataStorageConnection: {HasConnection}", 
            !string.IsNullOrEmpty(context.Configuration.GetConnectionString("MetadataStorageConnection")));
        tempLogger.LogInformation("ContentStorageConnection: {HasConnection}", 
            !string.IsNullOrEmpty(context.Configuration.GetConnectionString("ContentStorageConnection")));
        tempLogger.LogInformation("MetadataContainerName: {ContainerName}", context.Configuration["MetadataContainerName"]);
        tempLogger.LogInformation("ContentContainerName: {ContainerName}", context.Configuration["ContentContainerName"]);
        
        // 🔍 ADD THIS: Show all connection strings
        tempLogger.LogInformation("=== All Connection Strings ===");
        foreach (var connStr in context.Configuration.GetSection("ConnectionStrings").GetChildren())
        {
            var value = connStr.Value ?? "";
            tempLogger.LogInformation("  {Key} = {Value}", connStr.Key, value.Substring(0, Math.Min(50, value.Length)));
        }
        
        // Create directory if using file system and it doesn't exist
        var useAzureStorage = bool.Parse(context.Configuration["UseAzureStorage"] ?? "false");
        if (!useAzureStorage)
        {
            var metadataPath = context.Configuration["MetadataStorePath"] ?? "./store";
            var contentPath = context.Configuration["ContentStorePath"] ?? "./content";
            
            if (!Directory.Exists(metadataPath))
            {
                tempLogger.LogInformation("Creating metadata directory: {Path}", metadataPath);
                Directory.CreateDirectory(metadataPath);
            }
            
            if (!string.IsNullOrEmpty(contentPath) && !Directory.Exists(contentPath))
            {
                tempLogger.LogInformation("Creating content directory: {Path}", contentPath);
                Directory.CreateDirectory(contentPath);
            }
        }

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
    });

var host = hostBuilder.Build();

// Force eager initialization of storage services to create containers
using (var scope = host.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        // This will trigger container creation in Azure Storage
        var metadataStore = scope.ServiceProvider.GetRequiredService<IMetadataStore>();
        logger.LogInformation("✅ Metadata store initialized successfully");

        var contentStore = scope.ServiceProvider.GetService<IContentStore>();
        if (contentStore != null)
        {
            logger.LogInformation("✅ Content store initialized successfully");
        }
        else
        {
            logger.LogInformation("ℹ️ Content store not configured");
        }
    }
    catch (DirectoryNotFoundException ex)
    {
        logger.LogError(ex, "❌ Directory not found - likely using file system storage but directory doesn't exist: {Path}", ex.Message);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Failed to initialize storage services: {Message}", ex.Message);
    }
}

host.Run();