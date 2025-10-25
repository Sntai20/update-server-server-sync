using Aspire.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using System.Net.Sockets;
using System.Text.Json;

var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Storage Emulator as a containerized resource with FIXED ports AND PERSISTENT STORAGE
var storage = builder
    .AddAzureStorage("Storage")
    .RunAsEmulator(configure =>
    {
        configure.WithApiVersionCheck(false);
        
        // Add persistent data volume for Azurite
     configure.WithDataVolume("azurite-data");
        
        // Fix the ports so UseDevelopmentStorage=true works
    configure.WithEndpoint("blob", endpoint =>
            {
      endpoint.Protocol = ProtocolType.Tcp;
       endpoint.Port = 10000;
  });
  configure.WithEndpoint("queue", endpoint =>
     {
         endpoint.Protocol = ProtocolType.Tcp;
         endpoint.Port = 10001;
  });
     configure.WithEndpoint("table", endpoint =>
 {
     endpoint.Protocol = ProtocolType.Tcp;
     endpoint.Port = 10002;
 });
    });

// Add Azure Service Bus for queue-triggered functions
var serviceBus = builder
    .AddAzureServiceBus("ServiceBusConnection")
  .RunAsEmulator();

// Add queues for the sync operations
var contentSyncQueue = serviceBus.AddQueue("content-sync-requests");
var prioritySyncQueue = serviceBus.AddQueue("priority-sync-requests");
var standardSyncQueue = serviceBus.AddQueue("standard-sync-requests");

// Read configuration from structured settings
var storageConfig = builder.Configuration.GetSection("Storage");
var serviceConfig = builder.Configuration.GetSection("Service");
var syncConfig = builder.Configuration.GetSection("Sync");
var featuresConfig = builder.Configuration.GetSection("Features");

var useAzureStorage = storageConfig.GetValue<bool>("UseAzureStorage");
var metadataStorePath = storageConfig["MetadataStorePath"] ?? "./store";
var contentStorePath = storageConfig["ContentStorePath"] ?? "./content";
var serviceUrl = serviceConfig["ServiceUrl"] ?? "http://localhost:7071";

// Create typed configuration object for Azure Functions
var serviceConfiguration = new
{
    ServiceUrl = serviceUrl,
    ContentUrl = $"{serviceUrl}/api/content",
    MaxUpdateCount = serviceConfig.GetValue<int>("MaxUpdateCount", 1000),
    SupportedCategories = serviceConfig.GetSection("SupportedCategories").Get<string[]>()
        ?? new[] { "Security Updates", "Critical Updates", "Feature Packs", "Updates", "Drivers" },

    SyncConfiguration = new
    {
        CriticalUpdatesIntervalHours = syncConfig.GetValue<int>("CriticalUpdatesIntervalHours", 4),
        ComprehensiveUpdatesIntervalHours = syncConfig.GetValue<int>("ComprehensiveUpdatesIntervalHours", 24),
        ContentSyncIntervalHours = syncConfig.GetValue<int>("ContentSyncIntervalHours", 168),
        MaintenanceIntervalHours = syncConfig.GetValue<int>("MaintenanceIntervalHours", 168),
        HealthCheckIntervalMinutes = syncConfig.GetValue<int>("HealthCheckIntervalMinutes", 60)
    },

    StorageConfiguration = new
    {
        MetadataStorePath = metadataStorePath,
        ContentStorePath = contentStorePath,
        EnableContentStorage = !string.IsNullOrEmpty(contentStorePath),
        ReindexOnStartup = featuresConfig.GetValue<bool>("ReindexOnStartup", false),
        UseAzureStorage = useAzureStorage,
        MetadataContainerName = storageConfig["MetadataContainerName"] ?? "metadata",
        ContentContainerName = storageConfig["ContentContainerName"] ?? "content"
    },

    FeatureFlags = new
    {
        UseAzureStorage = useAzureStorage,
        EnableScheduledSync = featuresConfig.GetValue<bool>("EnableScheduledSync", true),
        EnableContentSync = featuresConfig.GetValue<bool>("EnableContentSync", true),
        EnableHealthMonitoring = featuresConfig.GetValue<bool>("EnableHealthMonitoring", true),
        EnableMetadataExport = featuresConfig.GetValue<bool>("EnableMetadataExport", true),
        EnableDriverMatching = featuresConfig.GetValue<bool>("EnableDriverMatching", true)
    }
};

// Configure Azure Functions with proper connection strings
var updateFunctions = builder.AddAzureFunctionsProject<Projects.UpdateEngine>("update-functions")
    .WithExternalHttpEndpoints()
    .WithHostStorage(storage)  // Functions runtime storage
    .WithReference(serviceBus)
    .WithReference(contentSyncQueue)
    .WithReference(prioritySyncQueue)
    .WithReference(standardSyncQueue);

// Pass storage connection strings when using Azure Storage
if (useAzureStorage)
{
    // Azure Functions run as a LOCAL PROCESS (not in Docker), so they need to connect to
    // Azurite via localhost, not the container name. Azurite ports are forwarded to localhost.
    var storageConnectionString = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;";

    updateFunctions
   .WithEnvironment("ConnectionStrings__MetadataStorageConnection", storageConnectionString)
        .WithEnvironment("ConnectionStrings__ContentStorageConnection", storageConnectionString);
}

// Pass configuration to Azure Functions
updateFunctions
    .WithEnvironment("UseAzureStorage", useAzureStorage.ToString())
    .WithEnvironment("MetadataContainerName", storageConfig["MetadataContainerName"] ?? "metadata")
    .WithEnvironment("ContentContainerName", storageConfig["ContentContainerName"] ?? "content")
    .WithEnvironment("MetadataStorePath", metadataStorePath)
    .WithEnvironment("ContentStorePath", contentStorePath)
    .WithEnvironment("ContentHttpRoot", $"{serviceUrl}/api/content")
    .WithEnvironment("ServiceConfigurationJson", JsonSerializer.Serialize(serviceConfiguration));

// Validate and create directories for file system storage
if (string.IsNullOrEmpty(metadataStorePath))
{
    throw new InvalidOperationException("MetadataStorePath must be configured");
}

if (!useAzureStorage && builder.Environment.IsDevelopment())
{
    // Create local directories if using file system storage
    if (!Directory.Exists(metadataStorePath))
    {
        Directory.CreateDirectory(metadataStorePath);
    }

    if (!string.IsNullOrEmpty(contentStorePath) && !Directory.Exists(contentStorePath))
    {
        Directory.CreateDirectory(contentStorePath);
    }

    // Add health check for metadata store
    builder.Services.AddHealthChecks()
        .AddCheck("metadata-store", () =>
            Directory.Exists(metadataStorePath)
           ? HealthCheckResult.Healthy("Metadata store directory exists")
 : HealthCheckResult.Unhealthy("Metadata store path not found"));
}

var app = builder.Build();

app.Run();
