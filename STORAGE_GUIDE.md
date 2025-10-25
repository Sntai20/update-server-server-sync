# Storage Configuration Guide

This project supports easy switching between local file system storage and Azure Storage Emulator for the Microsoft Update Azure Functions.

## Quick Start

### Option 1: Using the Configuration Script (Recommended)

#### For Local File System Storage:
```bash
# Configure for local file system
./configure-storage.sh --storage-mode FileSystem

# Configure and start functions directly
./configure-storage.sh --storage-mode FileSystem --start-functions

# Configure and start with AppHost
./configure-storage.sh --storage-mode FileSystem --use-apphost --start-functions
```

#### For Azure Storage Emulator:
```bash
# Configure for Azure Storage Emulator
./configure-storage.sh --storage-mode AzureEmulator

# Configure and start functions directly  
./configure-storage.sh --storage-mode AzureEmulator --start-functions

# Configure and start with AppHost
./configure-storage.sh --storage-mode AzureEmulator --use-apphost --start-functions
```

### Option 2: Manual Configuration

#### Local File System Storage

The Functions will store metadata in `./store` and content in `./content` directories.

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "",
    "MetadataStorePath": "./store",
    "ContentStorePath": "./content",
    "ServiceConfigurationJson": "{...}",
    "ContentHttpRoot": "http://localhost:7071/api/content"
  }
}
```

#### Azure Storage Emulator

The Functions will use Azure Storage Emulator containers for metadata and content.

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "MetadataStorageConnection": "UseDevelopmentStorage=true",
    "ContentStorageConnection": "UseDevelopmentStorage=true",
    "MetadataStorePath": "metadata",
    "ContentStorePath": "content",
    "ServiceConfigurationJson": "{...}",
    "ContentHttpRoot": "http://localhost:7071/api/content"
  }
}
```

## Storage Types Explained

### Local File System Storage
- **Pros**: Simple setup, no external dependencies, fast for development
- **Cons**: Not cloud-native, limited scalability
- **Use Case**: Local development, testing, single-machine deployments

### Azure Storage Emulator
- **Pros**: Cloud-native patterns, container-like experience, scales better
- **Cons**: Requires Azurite or Azure Storage Emulator
- **Use Case**: Development that mimics cloud deployment, integration testing

## Prerequisites

### For Azure Storage Emulator
Install Azurite (recommended):
```bash
npm install -g azurite
```

Start Azurite:
```bash
azurite --silent --location ./azurite-data &
```

### For AppHost (Optional)
Make sure Aspire is installed:
```bash
dotnet workload install aspire
```

## Available Functions

All storage modes support the same HTTP endpoints:

| Endpoint | Description |
|----------|-------------|
| `/api/ClientWebService/client.asmx` | Windows Update client SOAP endpoint |
| `/api/ServerSyncWebService/ServerSyncWebService.asmx` | WSUS server sync SOAP endpoint |
| `/api/GetStoreStatus` | Get metadata store status and statistics |
| `/api/content/{hash}` | Content delivery (SHA1/SHA256 hash-based) |
| `/api/FetchConfiguration` | Fetch server configuration from upstream |
| `/api/FetchCategories` | Sync product categories and classifications |
| `/api/FetchUpdates` | Download update metadata from Microsoft Update |

## Advanced Triggers

When using Azure Storage Emulator, advanced triggers are available:

- **Timer Trigger**: `ScheduledMetadataSync` - Daily sync at 2 AM UTC
- **Service Bus Trigger**: `ProcessMetadataSyncRequest` - Event-driven sync
- **Blob Trigger**: `ProcessSyncConfigFile` - Configuration file processing

These triggers require storage connections and will show warnings with file system storage.

## Troubleshooting

### Functions won't start
1. Check that `func` CLI is installed: `func --version`
2. Verify .NET 9.0 is installed: `dotnet --version`
3. For Azure Storage mode, ensure Azurite is running

### Storage connection errors
1. For Azure Storage mode, verify Azurite is accessible on default ports
2. Check that connection strings are correctly formatted
3. Verify container names are valid (lowercase, no special characters)

### AppHost DCP errors
If you see "Property CliPath" errors, ensure the Aspire workload is properly installed:
```bash
dotnet workload install aspire
```

## Architecture

```
┌─────────────────────┐    ┌──────────────────────┐    ┌─────────────────────┐
│  Configuration      │    │  Azure Functions     │    │  Storage            │
│  Script             │───▶│  (HTTP Triggered)    │───▶│  (File System OR    │
│  (./configure-      │    │                      │    │   Azure Emulator)   │
│   storage.sh)       │    └──────────────────────┘    └─────────────────────┘
└─────────────────────┘              │
                                     ▼
                              ┌──────────────────────┐
                              │  AppHost             │
                              │  (Optional           │
                              │   Orchestration)     │
                              └──────────────────────┘
```

The dynamic storage selection happens in `ServiceCollectionExtensions.cs`, which automatically detects whether to use Azure Storage or file system based on connection string availability.

## Development Workflow

1. **Choose storage mode** based on your development needs
2. **Run configuration script** to set up the environment
3. **Start functions** either directly or through AppHost
4. **Test endpoints** using curl, Postman, or integration tests
5. **Switch modes** as needed for different scenarios

This flexible approach allows you to develop locally with simple file storage and test with cloud-like Azure Storage patterns without changing any code.