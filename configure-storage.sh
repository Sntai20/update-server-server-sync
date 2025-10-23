#!/bin/bash

# Microsoft Update Functions Storage Configuration Script
# Easily switch between FileSystem and Azure Storage Emulator

STORAGE_MODE="FileSystem"
START_FUNCTIONS=false
USE_APPHOST=false

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --storage-mode)
            STORAGE_MODE="$2"
            shift 2
            ;;
        --start-functions)
            START_FUNCTIONS=true
            shift
            ;;
        --use-apphost)
            USE_APPHOST=true
            shift
            ;;
        --help)
            echo "Usage: $0 [options]"
            echo "Options:"
            echo "  --storage-mode [FileSystem|AzureEmulator]  Set storage mode (default: FileSystem)"
            echo "  --start-functions                          Start Azure Functions after configuration"
            echo "  --use-apphost                             Use AppHost instead of direct func start"
            echo "  --help                                     Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Validate storage mode
if [[ "$STORAGE_MODE" != "FileSystem" && "$STORAGE_MODE" != "AzureEmulator" ]]; then
    echo "❌ Invalid storage mode: $STORAGE_MODE"
    echo "Valid options: FileSystem, AzureEmulator"
    exit 1
fi

echo "🔧 Storage mode set to: $STORAGE_MODE"

# Create local.settings.json based on storage mode
LOCAL_SETTINGS_PATH="./MicrosoftUpdateFunctions/src/local.settings.json"

if [[ "$STORAGE_MODE" == "FileSystem" ]]; then
    echo "📁 Configuring for local file system storage..."
    
    # Create directories
    mkdir -p "./MicrosoftUpdateFunctions/src/store"
    mkdir -p "./MicrosoftUpdateFunctions/src/content"
    
    cat > "$LOCAL_SETTINGS_PATH" << EOF
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "MetadataStorePath": "./store",
    "ContentStorePath": "./content",
    "ServiceConfigurationJson": "{\"ServiceUrl\":\"http://localhost:7071\",\"ContentUrl\":\"http://localhost:7071/api/content\",\"MaxUpdateCount\":1000,\"SupportedCategories\":[\"Security Updates\",\"Critical Updates\",\"Feature Packs\"]}",
    "ContentHttpRoot": "http://localhost:7071/api/content",
    "AZURE_FUNCTIONS_ENVIRONMENT": "Development",
    "AzureWebJobsSecretStorageType": "files"
  }
}
EOF
    
    echo "  ✅ Metadata will be stored in: ./store"
    echo "  ✅ Content will be stored in: ./content"

elif [[ "$STORAGE_MODE" == "AzureEmulator" ]]; then
    echo "🐳 Configuring for Azure Storage Emulator..."
    
    cat > "$LOCAL_SETTINGS_PATH" << EOF
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "StorageConnection": "UseDevelopmentStorage=true",
    "MetadataStorageConnection": "UseDevelopmentStorage=true",
    "ContentStorageConnection": "UseDevelopmentStorage=true",
    "MetadataStorePath": "metadata",
    "ContentStorePath": "content",
    "ServiceConfigurationJson": "{\"ServiceUrl\":\"http://localhost:7071\",\"ContentUrl\":\"http://localhost:7071/api/content\",\"MaxUpdateCount\":1000,\"SupportedCategories\":[\"Security Updates\",\"Critical Updates\",\"Feature Packs\"]}",
    "ContentHttpRoot": "http://localhost:7071/api/content",
    "AZURE_FUNCTIONS_ENVIRONMENT": "Development",
    "AzureWebJobsSecretStorageType": "files"
  }
}
EOF
    
    echo "  ✅ Using Azure Storage Emulator connection"
    echo "  ✅ Metadata container: metadata"
    echo "  ✅ Content container: content"
fi

echo "💾 Updated $LOCAL_SETTINGS_PATH"

# Update AppHost Program.cs if using AppHost
if [[ "$USE_APPHOST" == true ]]; then
    echo "🚀 Configuring AppHost for $STORAGE_MODE mode..."
    
    APPHOST_PATH="./MicrosoftUpdateFunctions.AppHost/Program.cs"
    
    if [[ "$STORAGE_MODE" == "AzureEmulator" ]]; then
        cat > "$APPHOST_PATH" << 'EOF'
using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Storage Emulator as a containerized resource
var storage = builder.AddAzureStorage("storage").RunAsEmulator();

// Add the Microsoft Update Functions with Azure Storage Emulator
var updateFunctions = builder.AddExecutable("update-functions", "func", "../MicrosoftUpdateFunctions/src", "start", "--port", "7071")
    .WithEnvironment("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated")
    .WithEnvironment("AzureWebJobsSecretStorageType", "files")
    .WithEnvironment("AZURE_FUNCTIONS_ENVIRONMENT", "Development")
    .WithEnvironment("ContentHttpRoot", "http://localhost:7071/api/content")
    .WithEnvironment("ServiceConfigurationJson", """
        {
            "ServiceUrl": "http://localhost:7071",
            "ContentUrl": "http://localhost:7071/api/content",
            "MaxUpdateCount": 1000,
            "SupportedCategories": ["Security Updates", "Critical Updates", "Feature Packs"]
        }
        """)
    .WithReference(storage)
    .WithHttpEndpoint(port: 7071, name: "http");

var app = builder.Build();

app.Run();
EOF
    else
        cat > "$APPHOST_PATH" << 'EOF'
using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Add the Microsoft Update Functions with local file system storage
var updateFunctions = builder.AddExecutable("update-functions", "func", "../MicrosoftUpdateFunctions/src", "start", "--port", "7071")
    .WithEnvironment("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated")
    .WithEnvironment("AzureWebJobsStorage", "")
    .WithEnvironment("AzureWebJobsSecretStorageType", "files")
    .WithEnvironment("AZURE_FUNCTIONS_ENVIRONMENT", "Development")
    .WithEnvironment("MetadataStorePath", "./store")
    .WithEnvironment("ContentStorePath", "./content")
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
EOF
    fi
    
    echo "  ✅ Updated AppHost configuration"
fi

# Display current configuration
echo ""
echo "📋 Current Configuration:"
echo "  Storage Mode: $STORAGE_MODE"
echo "  Service URL: http://localhost:7071"
echo "  Content URL: http://localhost:7071/api/content"

if [[ "$STORAGE_MODE" == "AzureEmulator" ]]; then
    echo ""
    echo "⚠️  Note: Azure Storage Emulator must be running for advanced triggers to work."
    echo "   You can start it with: azurite --silent --location ./azurite-data &"
fi

# Start functions if requested
if [[ "$START_FUNCTIONS" == true && "$USE_APPHOST" == true ]]; then
    echo ""
    echo "🚀 Starting AppHost..."
    cd "./MicrosoftUpdateFunctions.AppHost"
    dotnet run
elif [[ "$START_FUNCTIONS" == true ]]; then
    echo ""
    echo "🚀 Starting Azure Functions..."
    cd "./MicrosoftUpdateFunctions/src"
    func start --port 7071
fi