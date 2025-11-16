using AppHost;
using Aspire.Hosting;
using Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Entry point for the .NET Aspire application host that orchestrates the distributed
/// Microsoft Update Server-Server Sync application, including Azure Functions, storage emulators,
/// and service bus infrastructure.
/// 
/// Configuration Best Practices Implementation:
/// - Uses strongly typed configuration classes with validation
/// - Supports multiple configuration sources (appsettings, environment, user secrets)
/// - Environment-specific configuration files
/// - Secure handling of sensitive data
/// </summary>
var builder = DistributedApplication.CreateBuilder(args);

// Configure shared configuration loading from Configuration project
// This loads the shared base settings plus environment-specific overrides
builder.Services.AddSharedAppConfiguration(builder.Environment.EnvironmentName, builder.Configuration);

// Configure additional configuration sources following best practices
builder.Configuration.AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true);  // For local development secrets

// Validate configuration at startup
ValidateConfiguration(builder.Configuration);

/// <summary>
/// Configures Azure Storage for the appropriate environment.
/// - Development: Uses Azurite storage emulator with dynamic ports
/// - Production: Uses real Azure Storage with connection strings from configuration
/// </summary>
var storage = builder.Environment.EnvironmentName == Environments.Development
    ? builder.AddAzureStorage("Storage").RunAsEmulator()
    : builder.AddAzureStorage("Storage");

// Check if Service Bus should be enabled (disable for minimal testing)
var enableServiceBus = builder.Configuration.GetValue<bool>("Features:EnableScheduledSync", false);

/// <summary>
/// Configures the UpdateEngine Azure Functions project with dependencies.
/// Service Bus and queues are conditionally included based on configuration.
/// Waits for storage to be ready to prevent worker process crashes during startup.
/// Uses simplified configuration approach with direct property binding.
/// </summary>
var updateFunctions = builder.AddAzureFunctionsProject<Projects.UpdateEngine>("UpdateEngine")
    .WithExternalHttpEndpoints()
    .WithHostStorage(storage)
    .WaitFor(storage);

// Conditionally add Service Bus if enabled
if (enableServiceBus)
{
    /// <summary>
    /// Configures Azure Service Bus emulator with message queues for sync operations.
    /// Includes content sync, priority sync, and standard sync queues for managing
    /// update synchronization workflows and message-based communication between components.
    /// </summary>
    var serviceBus = builder
        .AddAzureServiceBus("ServiceBusConnection")
        .RunAsEmulator();

    var contentSyncQueue = serviceBus.AddServiceBusQueue("content-sync-requests");
    var prioritySyncQueue = serviceBus.AddServiceBusQueue("priority-sync-requests");
    var standardSyncQueue = serviceBus.AddServiceBusQueue("standard-sync-requests");

    updateFunctions
        .WithReference(serviceBus)
        .WithReference(contentSyncQueue)
        .WithReference(prioritySyncQueue)
        .WithReference(standardSyncQueue)
        .WaitFor(serviceBus);
}

/// <summary>
/// Applies service configuration and storage settings to the Azure Functions environment,
/// including metadata store paths, content store paths, and service endpoint URLs.
/// Storage directories/containers are created automatically during Functions startup via DI.
/// Uses simplified configuration approach without complex transformations.
/// </summary>
SimpleConfigurationHelper.ConfigureUpdateFunctions(updateFunctions, builder.Configuration);

var app = builder.Build();

app.Run();

/// <summary>
/// Validates critical configuration settings at startup to catch errors early.
/// Implements configuration validation best practice.
/// </summary>
/// <param name="configuration">The application configuration to validate.</param>
static void ValidateConfiguration(IConfiguration configuration)
{
    // Validate UpdateServer configuration
    var updateServer = configuration.GetSection("UpdateServer");
    if (string.IsNullOrWhiteSpace(updateServer["ServiceUrl"]))
        throw new InvalidOperationException("UpdateServer:ServiceUrl is required");
    
    if (string.IsNullOrWhiteSpace(updateServer["ContentUrl"]))
        throw new InvalidOperationException("UpdateServer:ContentUrl is required");
        
    if (!int.TryParse(updateServer["MaxUpdateCount"], out var maxCount) || maxCount <= 0)
        throw new InvalidOperationException("UpdateServer:MaxUpdateCount must be a positive integer");

    // Validate Storage configuration  
    var storage = configuration.GetSection("Storage");
    if (string.IsNullOrWhiteSpace(storage["MetadataPath"]))
        throw new InvalidOperationException("Storage:MetadataPath is required");
        
    if (string.IsNullOrWhiteSpace(storage["ContentPath"]))
        throw new InvalidOperationException("Storage:ContentPath is required");

    // Validate FunctionSchedules configuration
    var schedules = configuration.GetSection("FunctionSchedules");
    ValidateTimeSpanFormat(schedules["SyncCritical"], "FunctionSchedules:SyncCritical");
    ValidateTimeSpanFormat(schedules["SyncComprehensive"], "FunctionSchedules:SyncComprehensive");
    ValidateTimeSpanFormat(schedules["HealthCheck"], "FunctionSchedules:HealthCheck");
}

/// <summary>
/// Validates that a configuration value is a valid TimeSpan format.
/// </summary>
/// <param name="value">The value to validate.</param>
/// <param name="configKey">The configuration key for error reporting.</param>
static void ValidateTimeSpanFormat(string? value, string configKey)
{
    if (string.IsNullOrWhiteSpace(value))
        throw new InvalidOperationException($"{configKey} is required");
        
    if (!TimeSpan.TryParse(value, out _))
        throw new InvalidOperationException($"{configKey} must be a valid TimeSpan format (e.g., '02:00:00')");
}