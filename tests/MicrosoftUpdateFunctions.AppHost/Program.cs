using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Storage Emulator for metadata and content
var storage = builder.AddAzureStorage("storage").RunAsEmulator();

// Add the Microsoft Update Functions as an executable project
var updateFunctions = builder.AddExecutable("update-functions", "func", "../../azure-functions", "start", "--port", "7071")
    .WithEnvironment("MetadataStorePath", "metadata")
    .WithEnvironment("ContentStorePath", "content")
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