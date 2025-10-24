using Aspire.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Storage Emulator as a containerized resource
var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var blobs = storage.AddBlobs("blobs");

// Configure storage paths for development
var metadataStorePath = builder.Configuration["MetadataStorePath"] ?? "./store";
var contentStorePath = builder.Configuration["ContentStorePath"] ?? "./content";

// Enhanced service configuration for the new service layer architecture
var serviceConfiguration = new
{
    ServiceUrl = "http://localhost:7071",
    ContentUrl = "http://localhost:7071/api/content",
    MaxUpdateCount = 1000,
    SupportedCategories = new[] { "Security Updates", "Critical Updates", "Feature Packs", "Updates", "Drivers" },
    
    // New service layer configuration
    SyncConfiguration = new
    {
        CriticalUpdatesIntervalHours = 4,
        ComprehensiveUpdatesIntervalHours = 24,
        ContentSyncIntervalHours = 168, // Weekly
        MaintenanceIntervalHours = 168, // Weekly
        HealthCheckIntervalMinutes = 60
    },
    
    // Storage configuration
    StorageConfiguration = new
    {
        MetadataStorePath = metadataStorePath,
        ContentStorePath = contentStorePath,
        EnableContentStorage = !string.IsNullOrEmpty(contentStorePath),
        ReindexOnStartup = false
    },
    
    // Feature flags for the consolidated functions
    FeatureFlags = new
    {
        EnableScheduledSync = true,
        EnableContentSync = true,
        EnableHealthMonitoring = true,
        EnableMetadataExport = true,
        EnableDriverMatching = true
    }
};

// Add Azure Functions project using the project reference (better Aspire integration)
var updateFunctions = builder.AddAzureFunctionsProject<Projects.MicrosoftUpdateFunctions>("update-functions")
    .WithExternalHttpEndpoints()

    // Storage configuration for the service layer
    .WithEnvironment("MetadataStorePath", metadataStorePath)
    .WithEnvironment("ContentStorePath", contentStorePath)
    .WithEnvironment("MetadataStorageConnection", "") // Empty for local file system
    .WithEnvironment("ContentStorageConnection", "") // Empty for local file system
    
    // HTTP endpoints configuration
    .WithEnvironment("ContentHttpRoot", "http://localhost:7071/api/content")
    .WithEnvironment("ServiceConfigurationJson", System.Text.Json.JsonSerializer.Serialize(serviceConfiguration))
    
    // Reference to blob storage for Azure Functions
    .WithReference(blobs);

var app = builder.Build();

app.Run();
