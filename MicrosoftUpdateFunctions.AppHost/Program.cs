using Aspire.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// 1. Define configuration sources with proper precedence
// Values come from: appsettings.json -> appsettings.{Environment}.json -> User Secrets -> Environment Variables -> Command Line

// Add Azure Storage Emulator as a containerized resource
var storage = builder
    .AddAzureStorage("Storage")
    .RunAsEmulator(configure => configure.WithApiVersionCheck(false));

var blobs = storage.AddBlobs("DataContainerConnection");

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

// 3. Create typed configuration objects instead of anonymous types for better maintainability
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
        ReindexOnStartup = bool.Parse(builder.Configuration["ReindexOnStartup"] ?? "false")
    },

    FeatureFlags = new
    {
        EnableScheduledSync = bool.Parse(builder.Configuration["EnableScheduledSync"] ?? "true"),
        EnableContentSync = bool.Parse(builder.Configuration["EnableContentSync"] ?? "true"),
        EnableHealthMonitoring = bool.Parse(builder.Configuration["EnableHealthMonitoring"] ?? "true"),
        EnableMetadataExport = bool.Parse(builder.Configuration["EnableMetadataExport"] ?? "true"),
        EnableDriverMatching = bool.Parse(builder.Configuration["EnableDriverMatching"] ?? "true")
    }
};

// 4. Use resource references for infrastructure dependencies
var updateFunctions = builder.AddAzureFunctionsProject<Projects.MicrosoftUpdateFunctions>("update-functions")
    .WithExternalHttpEndpoints()
    .WithHostStorage(storage)  // Aspire manages connection string automatically
    .WithReference(blobs)      // Creates environment variable: ConnectionStrings__DataContainerConnection
    .WithReference(serviceBus) // Creates environment variable: ConnectionStrings__ServiceBusConnection
    .WithReference(contentSyncQueue)
    .WithReference(prioritySyncQueue)
    .WithReference(standardSyncQueue)

    // 5. Only set application-specific variables (not infrastructure)
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
