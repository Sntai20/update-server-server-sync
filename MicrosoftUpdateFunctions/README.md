# Microsoft Update Functions

This directory contains the Azure Functions implementation for the Microsoft Update Server-Server Sync protocol.

## Directory Structure

```
MicrosoftUpdateFunctions/
├── src/                           # Azure Functions source code
│   ├── Functions/                 # Function implementations
│   ├── Services/                  # Dependency injection and service configuration
│   ├── host.json                  # Azure Functions host configuration
│   ├── local.settings.json        # Local development settings
│   └── MicrosoftUpdateFunctions.csproj
├── tests/                         # Test projects
│   └── MicrosoftUpdateFunctions.Tests/
└── README.md                      # This file
```

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- Azure Functions Core Tools 4.x
- Visual Studio Code (recommended)

### Quick Start

1. **Configure Storage**: Choose between local file system or Azure Storage Emulator
   ```bash
   # From repository root
   ./configure-storage.sh --storage-mode FileSystem --start-functions
   ```

2. **Start Functions Manually**:
   ```bash
   cd MicrosoftUpdateFunctions/src
   func start
   ```

3. **Test Endpoints**:
   ```bash
   curl http://localhost:7071/api/GetStoreStatus
   ```

## Available Storage Modes

### FileSystem Mode
- **Metadata Storage**: Local file system (`./store`)
- **Content Storage**: Local file system (`./content`)
- **Best for**: Development, testing, small deployments

### Azure Storage Emulator Mode
- **Metadata Storage**: Azure Blob containers
- **Content Storage**: Azure Blob containers
- **Best for**: Production-like testing, cloud deployment simulation

## Configuration

### Storage Configuration
Use the root-level configuration scripts:

```bash
# Configure for local file system
./configure-storage.sh --storage-mode FileSystem

# Configure for Azure Storage Emulator
./configure-storage.sh --storage-mode AzureEmulator

# Start functions after configuration
./configure-storage.sh --storage-mode FileSystem --start-functions
```

### Manual Configuration
Edit `src/local.settings.json` directly:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "MetadataStorePath": "./store",
    "ContentStorePath": "./content",
    "ServiceConfigurationJson": "{\"ServiceUrl\":\"http://localhost:7071\",\"ContentUrl\":\"http://localhost:7071/api/content\"}",
    "AZURE_FUNCTIONS_ENVIRONMENT": "Development"
  }
}
```

## Available Functions

### HTTP Functions
- **GetStoreStatus**: `GET /api/GetStoreStatus` - Get metadata store status
- **ClientWebService**: `POST /api/ClientWebService/client.asmx` - SOAP client sync
- **ServerSyncWebService**: `POST /api/ServerSyncWebService/ServerSyncWebService.asmx` - SOAP server sync
- **Content Delivery**: `GET /api/content/{contentHash}` - Download update content
- **Manual Sync**: `POST /api/ManualMetadataSync` - Trigger manual synchronization

### Advanced Triggers
- **ProcessSyncConfigFile**: Blob trigger for configuration changes
- **ProcessMetadataSyncRequest**: Service Bus trigger for sync requests
- **ScheduledMetadataSync**: Timer trigger for automated sync
- **WeeklyFullMetadataSync**: Timer trigger for full refresh

> **Note**: Advanced triggers require Azure Storage connections to function.

## Development

### Building
```bash
dotnet build
```

### Running Tests
```bash
cd tests/MicrosoftUpdateFunctions.Tests
dotnet test
```

### Debugging
1. Open in Visual Studio Code
2. Set breakpoints in Function code
3. Press F5 to start debugging

## Deployment

### Azure Functions
Use the deployment scripts in `src/deploy/`:

```bash
# Deploy to Azure
./src/deploy.sh -g myResourceGroup -n myFunctionApp
```

### Docker
```bash
# Build container
docker build -t microsoft-update-functions .

# Run container
docker run -p 7071:80 microsoft-update-functions
```

## Troubleshooting

### Common Issues

1. **Functions won't start**:
   - Check `local.settings.json` exists
   - Verify .NET 8.0 is installed
   - Run `func --version` to check CLI

2. **Storage errors**:
   - Run `./configure-storage.sh --storage-mode FileSystem`
   - Check directory permissions
   - Verify Azure Storage Emulator is running (for AzureEmulator mode)

3. **Build errors**:
   - Run `dotnet restore`
   - Check project references are correct
   - Verify NuGet packages are restored

### Log Locations
- **Console**: Function output appears in terminal
- **Application Insights**: Configure for cloud logging
- **Files**: Local logs in `./logs/` (if configured)

## Related Projects

- **AppHost**: `../MicrosoftUpdateFunctions.AppHost/` - Aspire orchestration
- **Core Libraries**: `../src/` - Shared metadata and web service libraries
- **Documentation**: `../docs/` - API documentation and guides

## Contributing

1. Follow the existing code style
2. Add tests for new functionality
3. Update documentation
4. Test with both storage modes
5. Verify Azure Functions work locally

## License

This project is licensed under the MIT License - see the LICENSE file for details.