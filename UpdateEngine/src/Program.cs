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

    tempLogger.LogInformation("=== Storage Configuration ===");
    tempLogger.LogInformation("UseAzureStorageForMetadata: {UseAzureStorageForMetadata}", context.Configuration["UseAzureStorageForMetadata"]);
    tempLogger.LogInformation("UseAzureStorageForContent: {UseAzureStorageForContent}", context.Configuration["UseAzureStorageForContent"]);
    tempLogger.LogInformation("MetadataStorePath: {MetadataStorePath}", context.Configuration["MetadataStorePath"]);
    tempLogger.LogInformation("ContentStorePath: {ContentStorePath}", context.Configuration["ContentStorePath"]);
    tempLogger.LogInformation("MetadataContainerName: {ContainerName}", context.Configuration["MetadataContainerName"]);
    tempLogger.LogInformation("ContentContainerName: {ContainerName}", context.Configuration["ContentContainerName"]);

    tempLogger.LogInformation("=== Connection Strings ===");
    foreach (var connStr in context.Configuration.GetSection("ConnectionStrings").GetChildren())
    {
        var value = connStr.Value ?? "";
        var displayValue = value.Length > 50 ? value.Substring(0, 50) + "..." : value;
        tempLogger.LogInformation("  {Key} = {Value}", connStr.Key, displayValue);
    }
}

static void ConfigureJsonSerialization(IServiceCollection services)
{
    // Configure global JSON serialization options for Azure Functions
    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    services.AddSingleton(jsonOptions);
}

static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    var serviceProvider = services.BuildServiceProvider();
    var jsonOptions = serviceProvider.GetRequiredService<JsonSerializerOptions>();

    var configJson = configuration["ServiceConfigurationJson"];
    if (!string.IsNullOrEmpty(configJson))
    {
        var config = JsonSerializer.Deserialize<ServiceConfigurationMutable>(configJson, jsonOptions);
        if (config != null)
        {
            services.AddSingleton(config);
        }
    }

    services.Configure<ServiceConfigurationMutable>(configuration.GetSection("ServiceConfiguration"));
    
    // Register Microsoft Update services - this handles ALL storage setup
    services.AddMicrosoftUpdateServices(configuration);
}

static async Task InitializeStorageAsync(IHost host)
{
    using var scope = host.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        // Initialize metadata store (creates directories/containers automatically via DI)
        var metadataStore = scope.ServiceProvider.GetRequiredService<IMetadataStore>();
        logger.LogInformation("Metadata store initialized successfully");

        // Initialize content store if configured
        var contentStore = scope.ServiceProvider.GetService<IContentStore>();
        if (contentStore != null)
        {
            logger.LogInformation("Content store initialized successfully");
        }
        else
        {
            logger.LogInformation("Content store not configured (running in catalog-only mode)");
        }
    }
    catch (DirectoryNotFoundException ex)
    {
        logger.LogError(ex, "Directory not found: {Message}", ex.Message);
        throw;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize storage services: {Message}", ex.Message);
        throw;
    }

    await Task.CompletedTask;
}