// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using Configuration;
using System.Text.Json;
using UpdateEngine.Services;

var hostBuilder = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        ConfigureLogging(context);
        ConfigureJsonSerialization(services);
        ConfigureServices(services, context.Configuration);
    });

var host = hostBuilder.Build();

// Initialize storage services eagerly to create containers/directories
await InitializeStorageAsync(host);

await host.RunAsync();

static void ConfigureLogging(HostBuilderContext context)
{
    var tempLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("Startup");

    tempLogger.LogInformation("=== UpdateEngine Configuration ===");
    tempLogger.LogInformation("ServiceUrl: {ServiceUrl}", context.Configuration["ServiceUrl"]);
    tempLogger.LogInformation("MetadataPath: {MetadataPath}", context.Configuration["MetadataPath"]);
    tempLogger.LogInformation("ContentPath: {ContentPath}", context.Configuration["ContentPath"]);
    tempLogger.LogInformation("UseAzureStorageForMetadata: {UseAzureStorageForMetadata}", context.Configuration["UseAzureStorageForMetadata"]);
}

static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    // Simple configuration setup - no complex validation
    var appConfig = new AppConfig();
    configuration.Bind(appConfig);
    services.AddSingleton(appConfig);
    
    // Optionally validate if needed
    appConfig.Validate();

    // Register storage and content services using simplified config
    services.AddMicrosoftUpdateServices(configuration);
    
    // Register additional services
    services.AddSingleton<IConfiguration>(configuration);
}

static void ConfigureJsonSerialization(IServiceCollection services)
{
    // Configure global JSON serialization options for Azure Functions
    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
    
    services.Configure<JsonSerializerOptions>(opts =>
    {
        opts.PropertyNamingPolicy = jsonOptions.PropertyNamingPolicy;
        opts.WriteIndented = jsonOptions.WriteIndented;
        opts.DefaultIgnoreCondition = jsonOptions.DefaultIgnoreCondition;
    });
}

static async Task InitializeStorageAsync(IHost host)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Initializing storage services...");
    
    try
    {
        // Initialize metadata store
        var metadataStore = host.Services.GetService<IMetadataStore>();
        if (metadataStore != null)
        {
            logger.LogInformation("Metadata store initialized successfully");
        }
        
        // Initialize content store  
        var contentStore = host.Services.GetService<IContentStore>();
        if (contentStore != null)
        {
            logger.LogInformation("Content store initialized successfully");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize storage services");
        throw;
    }
}