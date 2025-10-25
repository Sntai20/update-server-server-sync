using AppHost;

/// <summary>
/// Entry point for the .NET Aspire application host that orchestrates the distributed
/// Microsoft Update Server-Server Sync application, including Azure Functions, storage emulators,
/// and service bus infrastructure.
/// </summary>
var builder = DistributedApplication.CreateBuilder(args);

/// <summary>
/// Configures Azurite storage emulator with fixed ports for blob, queue, and table services.
/// Uses consistent port assignments defined in <see cref="AzuriteDefaults"/> to ensure
/// reliable local development and testing environments.
/// </summary>
var storage = builder
    .AddAzureStorage("Storage")
    .RunAsEmulator(emulator => emulator
        .WithBlobPort(AzuriteDefaults.BlobPort)
        .WithQueuePort(AzuriteDefaults.QueuePort)
        .WithTablePort(AzuriteDefaults.TablePort)
        .WithArgs("--skipApiVersionCheck"));

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

/// <summary>
/// Builds and validates service configuration from application settings, including storage paths,
/// service URLs, and operational parameters. Ensures storage resources are properly initialized
/// before starting the application.
/// </summary>
var serviceConfiguration = ConfigurationHelper.BuildServiceConfiguration(builder.Configuration);
ConfigurationHelper.ValidateAndSetupStorage(builder, serviceConfiguration);

/// <summary>
/// Configures the UpdateEngine Azure Functions project with all required dependencies:
/// storage emulator, service bus, message queues, and service configuration.
/// Enables external HTTP endpoints for SOAP web services and health monitoring.
/// </summary>
var updateFunctions = builder.AddAzureFunctionsProject<Projects.UpdateEngine>("UpdateEngine")
    .WithExternalHttpEndpoints()
    .WithHostStorage(storage)
    .WithReference(serviceBus)
    .WithReference(contentSyncQueue)
    .WithReference(prioritySyncQueue)
    .WithReference(standardSyncQueue)
    .WaitFor(serviceBus);

/// <summary>
/// Applies service configuration and storage settings to the Azure Functions environment,
/// including metadata store paths, content store paths, and service endpoint URLs.
/// </summary>
ConfigurationHelper.ConfigureUpdateFunctions(
    updateFunctions,
    serviceConfiguration,
    builder.Configuration.GetSection("Storage"));

var app = builder.Build();

app.Run();