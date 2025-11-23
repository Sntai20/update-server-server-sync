// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using UpdateEngine.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UpdateEngine.Metadata.Storage;
using System.Text.Json;
using UpdateEngine.Core;

var hostBuilder = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((context, config) =>
    {
        // Add shared configuration from Configuration project
        config.AddSharedAppConfiguration();
    })
    .ConfigureServices((context, services) =>
    {
        ConfigureLogging(context);
        ConfigureJsonSerialization(services);
        
        // Register UpdateEngine core services (stores, orchestrators, health checks)
        services.AddUpdateEngineCore(context.Configuration);
    });

var host = hostBuilder.Build();

// Initialize storage services eagerly to create containers/directories
await InitializeStorageAsync(host);

await host.RunAsync();

static void ConfigureLogging(HostBuilderContext context)
{
    var tempLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("Startup");

    tempLogger.LogInformation("=== UpdateEngine Configuration ===");
    
    // Read from hierarchical UpdateEngine section
    var updateEngineSection = context.Configuration.GetSection("UpdateEngine");
    var serviceConfig = updateEngineSection.GetSection("ServiceConfiguration");
    var storageConfig = updateEngineSection.GetSection("StorageConfiguration");
    
    tempLogger.LogInformation("ServiceUrl: {ServiceUrl}", serviceConfig["ServiceUrl"]);
    tempLogger.LogInformation("UseAzureStorageForMetadata: {UseAzureStorageForMetadata}", storageConfig["UseAzureStorageForMetadata"]);
    tempLogger.LogInformation("UseAzureStorageForContent: {UseAzureStorageForContent}", storageConfig["UseAzureStorageForContent"]);

    var useAzureStorageForMetadata = storageConfig.GetValue<bool>("UseAzureStorageForMetadata");
    if (!useAzureStorageForMetadata)
    {
        tempLogger.LogInformation("MetadataPath: {MetadataPath}", storageConfig["MetadataPath"]);
    }
    else
    {
        tempLogger.LogInformation("MetadataContainerName: {ContainerName}", storageConfig["MetadataContainerName"]);
    }

    var useAzureStorageForContent = storageConfig.GetValue<bool>("UseAzureStorageForContent");
    if (!useAzureStorageForContent)
    {
        tempLogger.LogInformation("ContentPath: {ContentPath}", storageConfig["ContentPath"]);
    }
    else
    {
        tempLogger.LogInformation("ContentContainerName: {ContainerName}", storageConfig["ContentContainerName"]);
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