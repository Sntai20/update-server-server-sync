#!/bin/bash

# Simple script to start Azure Functions with storage configuration
# This provides a container-like development experience without complex Aspire orchestration

echo "Starting Microsoft Update Azure Functions with Storage Emulator configuration..."

# Navigate to azure-functions directory
cd azure-functions

# Create store and content directories if they don't exist
mkdir -p ./store ./content

# Update local.settings.json with storage configuration
cat > local.settings.json << EOF
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "MetadataStorePath": "./store",
    "ContentStorePath": "./content",
    "ServiceConfigurationJson": "{\"ServiceUrl\":\"http://localhost:7071\",\"ContentUrl\":\"http://localhost:7071/api/content\",\"MaxUpdateCount\":1000,\"SupportedCategories\":[\"Security Updates\",\"Critical Updates\",\"Feature Packs\"]}",
    "ContentHttpRoot": "http://localhost:7071/api/content",
    "AZURE_FUNCTIONS_ENVIRONMENT": "Development",
    "AzureWebJobsSecretStorageType": "files",
    "StorageConnection": "UseDevelopmentStorage=true"
  }
}
EOF

echo "✅ Configuration updated for development storage"
echo "🚀 Starting Azure Functions on http://localhost:7071"
echo ""
echo "Available endpoints:"
echo "  - Client Web Service: http://localhost:7071/api/ClientWebService/client.asmx"
echo "  - Server Sync: http://localhost:7071/api/ServerSyncWebService/ServerSyncWebService.asmx"
echo "  - Store Status: http://localhost:7071/api/GetStoreStatus"
echo "  - Content: http://localhost:7071/api/content/{hash}"
echo ""
echo "Advanced triggers (Timer, ServiceBus, Blob) will show warnings without real storage but HTTP functions will work."
echo ""

# Start Azure Functions
func start --port 7071