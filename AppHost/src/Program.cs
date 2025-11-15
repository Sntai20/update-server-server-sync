using AppHost;
using Aspire.Hosting;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Entry point for the .NET Aspire application host that orchestrates the distributed
/// Microsoft Update Server-Server Sync application, including Azure Functions, storage emulators,
/// and service bus infrastructure.
/// </summary>
var builder = DistributedApplication.CreateBuilder(args);

/// <summary>
/// Configures Azurite storage emulator for blob, queue, and table services.
/// Aspire automatically assigns ports and provides connection strings to dependent services.
/// </summary>
var storage = builder
    .AddAzureStorage("Storage")
    .RunAsEmulator();

// Check if Service Bus should be enabled (disable for minimal testing)
var enableServiceBus = builder.Configuration.GetValue<bool>("Features:EnableScheduledSync", false);

/// <summary>
/// Builds service configuration from application settings, including storage paths,
/// service URLs, and operational parameters.
/// </summary>
var serviceConfiguration = ConfigurationHelper.BuildServiceConfiguration(builder.Configuration);

/// <summary>
/// Configures the UpdateEngine Azure Functions project with dependencies.
/// Service Bus and queues are conditionally included based on configuration.
/// Waits for storage to be ready to prevent worker process crashes during startup.
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
/// </summary>
ConfigurationHelper.ConfigureUpdateFunctions(
    updateFunctions,
    serviceConfiguration,
    builder.Configuration.GetSection("Storage"),
    builder.Configuration.GetSection("AzureWebJobs"));

var app = builder.Build();

app.Run();