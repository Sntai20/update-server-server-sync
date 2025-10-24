using Aspire.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using System.Net.Sockets;

var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Storage Emulator as a containerized resource with FIXED ports
var storage = builder
    .AddAzureStorage("Storage")
    .RunAsEmulator(configure =>
    {
        configure.WithApiVersionCheck(false);
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

// Get the blob endpoints for manual configuration
var blobEndpoint = storage.Resource.BlobEndpoint;

// Add Azure Service Bus for queue-triggered functions
var serviceBus = builder
    .AddAzureServiceBus("ServiceBusConnection")
    .RunAsEmulator();

// Add queues for the sync operations
var contentSyncQueue = serviceBus.AddQueue("content-sync-requests");
var prioritySyncQueue = serviceBus.AddQueue("priority-sync-requests");
var standardSyncQueue = serviceBus.AddQueue("standard-sync-requests");

// 2. Use configuration with fallback values
var metadataStorePath = builder.Configuration["MetadataStorePath"] ?? "./store";
var contentStorePath = builder.Configuration["ContentStorePath"] ?? "./content";
var serviceUrl = builder.Configuration["ServiceUrl"] ?? "http://localhost:7071";
var useAzureStorage = bool.Parse(builder.Configuration["UseAzureStorage"] ?? "false");

// 3. Create typed configuration objects
var serviceConfiguration = new
{
    ServiceUrl = serviceUrl,
    ContentUrl = $"{serviceUrl}/api/content",
    MaxUpdateCount = int.Parse(builder.Configuration["MaxUpdateCount"] ?? "1000"),
    SupportedCategories = new[] { "Security Updates", "Critical Updates", "Feature Packs", "Updates", "Drivers" },

    SyncConfiguration = new
    {
        CriticalUpdatesIntervalHours = int.Parse(builder.Configuration["CriticalUpdatesIntervalHours"] ?? "4"),
        ComprehensiveUpdatesIntervalHours = int.Parse(builder.Configuration["ComprehensiveUpdatesIntervalHours"] ?? "24"),
        ContentSyncIntervalHours = int.Parse(builder.Configuration["ContentSyncIntervalHours"] ?? "168"),
        MaintenanceIntervalHours = int.Parse(builder.Configuration["MaintenanceIntervalHours"] ?? "168"),
        HealthCheckIntervalMinutes = int.Parse(builder.Configuration["HealthCheckIntervalMinutes"] ?? "60")
    },

    StorageConfiguration = new
    {
        MetadataStorePath = metadataStorePath,
        ContentStorePath = contentStorePath,
        EnableContentStorage = !string.IsNullOrEmpty(contentStorePath),
        ReindexOnStartup = bool.Parse(builder.Configuration["ReindexOnStartup"] ?? "false"),
        UseAzureStorage = useAzureStorage,
        MetadataContainerName = builder.Configuration["MetadataContainerName"] ?? "metadata",
        ContentContainerName = builder.Configuration["ContentContainerName"] ?? "content"
    },

    FeatureFlags = new
    {
        UseAzureStorage = useAzureStorage,
        EnableScheduledSync = bool.Parse(builder.Configuration["EnableScheduledSync"] ?? "true"),
        EnableContentSync = bool.Parse(builder.Configuration["EnableContentSync"] ?? "true"),
        EnableHealthMonitoring = bool.Parse(builder.Configuration["EnableHealthMonitoring"] ?? "true"),
        EnableMetadataExport = bool.Parse(builder.Configuration["EnableMetadataExport"] ?? "true"),
        EnableDriverMatching = bool.Parse(builder.Configuration["EnableDriverMatching"] ?? "true")
    }
};

// 4. Configure Azure Functions with manual connection strings
var updateFunctions = builder.AddAzureFunctionsProject<Projects.MicrosoftUpdateFunctions>("update-functions")
    .WithExternalHttpEndpoints()
    .WithHostStorage(storage)  // Functions runtime storage
    .WithReference(serviceBus)
    .WithReference(contentSyncQueue)
    .WithReference(prioritySyncQueue)
    .WithReference(standardSyncQueue);

// Manually pass storage connection strings for Azure Functions
if (useAzureStorage)
{
    // Azure Functions run as a LOCAL PROCESS (not in Docker), so they need to connect to
    // Azurite via localhost, not the container name. Azurite ports are forwarded to localhost.
    var storageConnectionString = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;";
    
    updateFunctions
        .WithEnvironment("ConnectionStrings__MetadataStorageConnection", storageConnectionString)
        .WithEnvironment("ConnectionStrings__ContentStorageConnection", storageConnectionString);
}

// Pass other configuration
updateFunctions
    .WithEnvironment("UseAzureStorage", useAzureStorage.ToString())
    .WithEnvironment("MetadataContainerName", builder.Configuration["MetadataContainerName"] ?? "metadata")
    .WithEnvironment("ContentContainerName", builder.Configuration["ContentContainerName"] ?? "content")
    .WithEnvironment("MetadataStorePath", metadataStorePath)
    .WithEnvironment("ContentStorePath", contentStorePath)
    .WithEnvironment("ContentHttpRoot", $"{serviceUrl}/api/content")
    .WithEnvironment("ServiceConfigurationJson", System.Text.Json.JsonSerializer.Serialize(serviceConfiguration));

if (string.IsNullOrEmpty(metadataStorePath))
{
    throw new InvalidOperationException("MetadataStorePath must be configured");
}

if (!Directory.Exists(metadataStorePath) && builder.Environment.IsDevelopment())
{
    Directory.CreateDirectory(metadataStorePath);
    builder.Services.AddHealthChecks()
        .AddCheck("metadata-store", () =>
            Directory.Exists(metadataStorePath)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Metadata store path not found"));
}

var app = builder.Build();

app.Run();
