using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Storage Emulator as a containerized resource
var storage = builder.AddAzureStorage("storage").RunAsEmulator();

// Add the Microsoft Update Functions using executable approach
var updateFunctions = builder.AddExecutable("update-functions", "func", "../MicrosoftUpdateFunctions/src", "start", "--port", "7071")
    .WithEnvironment("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated")
    .WithEnvironment("AzureWebJobsSecretStorageType", "files")
    .WithEnvironment("AZURE_FUNCTIONS_ENVIRONMENT", "Development")
    .WithEnvironment("AzureWebJobsStorage", "UseDevelopmentStorage=true")
    .WithEnvironment("ContentHttpRoot", "http://localhost:7071/api/content")
    .WithEnvironment("ServiceConfigurationJson", """
        {
            "ServiceUrl": "http://localhost:7071",
            "ContentUrl": "http://localhost:7071/api/content",
            "MaxUpdateCount": 1000,
            "SupportedCategories": ["Security Updates", "Critical Updates", "Feature Packs"]
        }
        """)
    .WithHttpEndpoint(port: 7071, name: "http");

var app = builder.Build();

app.Run();
