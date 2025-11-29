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
        
        // IMPORTANT: Explicitly add environment variables to ensure Aspire-injected
        // connection strings (ConnectionStrings__MetadataStorageConnection) are available
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        ConfigureLogging(context);
        ConfigureJsonSerialization(services);
        
        // Register UpdateEngine core services (stores, orchestrators, health checks)
        services.AddUpdateEngineCore(context.Configuration);
        
        // Configure OpenTelemetry when enabled (uses ServiceDefaults via Aspire)
        ConfigureOpenTelemetry(context, services);
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
        
        // DIAGNOSTIC: Check for Aspire connection strings
        var metadataConnection = context.Configuration.GetConnectionString("MetadataStorageConnection");
        var contentConnection = context.Configuration.GetConnectionString("ContentStorageConnection");
        
        tempLogger.LogInformation("=== Connection String Diagnostics ===");
        tempLogger.LogInformation("MetadataStorageConnection available: {HasMetadata}", !string.IsNullOrEmpty(metadataConnection));
        tempLogger.LogInformation("ContentStorageConnection available: {HasContent}", !string.IsNullOrEmpty(contentConnection));
        
        if (!string.IsNullOrEmpty(metadataConnection))
        {
            // Log first 50 chars to verify it's correct (don't log secrets)
            var preview = metadataConnection.Length > 50 ? metadataConnection.Substring(0, 50) + "..." : metadataConnection;
            tempLogger.LogInformation("MetadataStorageConnection preview: {Preview}", preview);
        }
        else
        {
            tempLogger.LogWarning("MetadataStorageConnection NOT FOUND - will fall back to local filesystem!");
            
            // Check raw environment variable
            var envVar = Environment.GetEnvironmentVariable("ConnectionStrings__MetadataStorageConnection");
            tempLogger.LogInformation("Environment variable ConnectionStrings__MetadataStorageConnection: {HasEnvVar}", !string.IsNullOrEmpty(envVar));
            
            // COMPREHENSIVE DIAGNOSTICS: List ALL connection string environment variables
            tempLogger.LogInformation("=== All Connection String Environment Variables ===");
            var allEnvVars = Environment.GetEnvironmentVariables();
            foreach (var key in allEnvVars.Keys)
            {
                var keyStr = key.ToString() ?? "";
                if (keyStr.Contains("Connection", StringComparison.OrdinalIgnoreCase) || 
                    keyStr.Contains("Storage", StringComparison.OrdinalIgnoreCase) ||
                    keyStr.Contains("UpdateEngine", StringComparison.OrdinalIgnoreCase))
                {
                    var value = allEnvVars[key]?.ToString() ?? "";
                    var preview = value.Length > 100 ? value.Substring(0, 100) + "..." : value;
                    tempLogger.LogInformation("  {Key} = {Value}", keyStr, preview);
                }
            }
        }
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

static void ConfigureOpenTelemetry(HostBuilderContext context, IServiceCollection services)
{
    var appConfig = new AppConfig();
    context.Configuration.GetSection(AppConfig.SectionName).Bind(appConfig);
    
    if (appConfig.FeatureFlags.EnableOpenTelemetry)
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("Startup");
        logger.LogInformation("OpenTelemetry ENABLED - Metrics and tracing will be collected via ServiceDefaults");
        logger.LogInformation("  Note: ServiceDefaults integration is provided by .NET Aspire when running via AppHost");
        logger.LogInformation("  OTEL_EXPORTER_OTLP_ENDPOINT: {Endpoint}", 
            context.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "(not set - using Aspire defaults)");
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