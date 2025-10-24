using Aspire.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Storage Emulator as a containerized resource
var storage = builder.AddAzureStorage("storage").RunAsEmulator();

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

// Add the Microsoft Update Functions with comprehensive configuration
var updateFunctions = builder.AddExecutable("update-functions", "func", "../MicrosoftUpdateFunctions/src", "start", "--port", "7071")
    .WithEnvironment("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated")
    .WithEnvironment("AzureWebJobsSecretStorageType", "files")
    .WithEnvironment("AZURE_FUNCTIONS_ENVIRONMENT", "Development")
    .WithEnvironment("AzureWebJobsStorage", "UseDevelopmentStorage=true")
    
    // Storage configuration for the service layer
    .WithEnvironment("MetadataStorePath", metadataStorePath)
    .WithEnvironment("ContentStorePath", contentStorePath)
    .WithEnvironment("MetadataStorageConnection", "") // Empty for local file system
    .WithEnvironment("ContentStorageConnection", "") // Empty for local file system
    
    // HTTP endpoints configuration
    .WithEnvironment("ContentHttpRoot", "http://localhost:7071/api/content")
    .WithEnvironment("ServiceConfigurationJson", System.Text.Json.JsonSerializer.Serialize(serviceConfiguration))
    
    // Logging configuration for better debugging
    .WithEnvironment("FUNCTIONS_WORKER_RUNTIME_VERSION", "~4")
    .WithEnvironment("WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED", "1")
    
    // Health check configuration
    .WithEnvironment("HealthCheck__Enabled", "true")
    .WithEnvironment("HealthCheck__Timeout", "30")
    
    // Configure HTTP endpoint with health check
    .WithHttpEndpoint(port: 7071, name: "http")
    .WithHttpHealthCheck("/api/HealthCheck");

// Add reference to storage for dependency tracking
updateFunctions.WithReference(storage);

// Configure the application with enhanced monitoring
var app = builder.Build();

// Add startup logging
app.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(() =>
{
    Console.WriteLine("=== Microsoft Update Functions AppHost Started ===");
    Console.WriteLine($"Functions URL: http://localhost:7071");
    Console.WriteLine($"Aspire Dashboard: http://localhost:15888");
    Console.WriteLine($"Health Check: http://localhost:7071/api/HealthCheck");
    Console.WriteLine($"Store Status: http://localhost:7071/api/StoreStatus");
    Console.WriteLine($"Metadata Store Path: {metadataStorePath}");
    Console.WriteLine($"Content Store Path: {contentStorePath}");
    Console.WriteLine("=== Available Endpoints ===");
    Console.WriteLine("Sync Operations:");
    Console.WriteLine("  POST /api/SyncMetadata - Manual metadata sync");
    Console.WriteLine("  POST /api/SyncContent - Manual content sync");
    Console.WriteLine("Query Operations:");
    Console.WriteLine("  GET  /api/StoreStatus - Store status and statistics");
    Console.WriteLine("  POST /api/QueryMetadata - Query stored metadata");
    Console.WriteLine("  POST /api/MatchDrivers - Driver matching");
    Console.WriteLine("Administrative:");
    Console.WriteLine("  GET  /api/HealthCheck - System health check");
    Console.WriteLine("  POST /api/ReindexStore - Force store reindexing");
    Console.WriteLine("SOAP Endpoints:");
    Console.WriteLine("  POST /api/ClientWebService/client.asmx - Client sync");
    Console.WriteLine("  POST /api/ServerWebService/server.asmx - Server sync");
    Console.WriteLine("Content Serving:");
    Console.WriteLine("  GET  /api/content/{hash} - Download content files");
    Console.WriteLine("==============================");
});

app.Run();
