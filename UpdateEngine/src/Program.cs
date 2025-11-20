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
        
        // Configure shared configuration from Configuration project
        services.AddSharedAppConfiguration(
            context.HostingEnvironment.EnvironmentName,
            context.Configuration);

        // Storage services are now configured using the shared AppConfig
        // that was registered via AddSharedAppConfiguration
        services.AddMicrosoftUpdateServices(context.Configuration);
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
    tempLogger.LogInformation("UseAzureStorageForMetadata: {UseAzureStorageForMetadata}", context.Configuration["UseAzureStorageForMetadata"]);
    tempLogger.LogInformation("UseAzureStorageForContent: {UseAzureStorageForContent}", context.Configuration["UseAzureStorageForContent"]);

    var useAzureStorageForMetadata = context.Configuration.GetValue<bool>("UseAzureStorageForMetadata");
    if (!useAzureStorageForMetadata)
    {
        tempLogger.LogInformation("MetadataPath: {MetadataPath}", context.Configuration["MetadataPath"]);
    }

    var useAzureStorageForContent = context.Configuration.GetValue<bool>("UseAzureStorageForContent");
    if (!useAzureStorageForContent)
    {
        tempLogger.LogInformation("ContentPath: {ContentPath}", context.Configuration["ContentPath"]);
    }
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
    
    // Register as singleton for dependency injection
    services.AddSingleton(jsonOptions);
    
    // Also configure for IOptions<JsonSerializerOptions> if needed
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