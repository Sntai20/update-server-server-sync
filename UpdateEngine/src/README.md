# Microsoft Update Server Azure Functions

This project converts the Microsoft Update server code into Azure isolated C# functions, allowing you to run a Windows Update server in Azure Functions.

## Overview

The Azure Functions implementation provides the same functionality as the original ASP.NET Core server but in a serverless environment:

- **Client Sync Functions**: Handle Windows Update client requests (MUv6 protocol)
- **Server Sync Functions**: Handle WSUS server-to-server synchronization
- **Content Functions**: Serve update content files
- **Authentication Functions**: Handle client and server authentication

## Architecture

```text
┌─────────────────┐    ┌──────────────────────┐    ┌─────────────────┐
│  Windows Update │───▶│  Azure Functions     │───▶│  Storage        │
│  Clients        │    │  (HTTP Triggered)    │    │  (Metadata &    │
└─────────────────┘    └──────────────────────┘    │  Content)       │
                                                   └─────────────────┘
```

### Functions

1. **ClientWebService** (`/api/ClientWebService/client.asmx`) - SOAP endpoint for Windows Update clients
2. **SimpleAuthWebService** (`/api/SimpleAuthWebService/SimpleAuth.asmx`) - Client authentication
3. **ServerSyncWebService** (`/api/ServerSyncWebService/ServerSyncWebService.asmx`) - WSUS server sync
4. **DssAuthWebService** (`/api/DssAuthWebService/DssAuthWebService.asmx`) - Server authentication
5. **GetMicrosoftUpdateContent** (`/api/content/{contentHash}`) - Content delivery
6. **FetchConfiguration** (`/api/FetchConfiguration`) - Fetch server configuration from upstream
7. **FetchCategories** (`/api/FetchCategories`) - Sync product categories and classifications
8. **FetchUpdates** (`/api/FetchUpdates`) - Download update metadata from Microsoft Update
9. **ReindexStore** (`/api/ReindexStore`) - Rebuild metadata search indices
10. **GetStoreStatus** (`/api/GetStoreStatus`) - Get metadata store status and statistics

## Prerequisites

- .NET 9.0 SDK
- Azure Functions Core Tools v4
- Azure subscription
- Update metadata and content (sync from Microsoft Update first)

## Configuration

### Local Development

Configure `local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "MetadataStorePath": "./store",
    "ContentStorePath": "./content", 
    "ServiceConfigurationJson": "{\"ServiceUrl\":\"http://localhost:7071\",\"ContentUrl\":\"http://localhost:7071/api/content\"}",
    "ContentHttpRoot": "http://localhost:7071/api/content"
  }
}
```

### Azure Production

Use the ARM template in `/deploy` folder or configure these application settings:

- `MetadataStorageConnection`: Connection string to metadata storage
- `ContentStorageConnection`: Connection string to content storage  
- `MetadataStorePath`: Path/container for metadata
- `ContentStorePath`: Path/container for content
- `ContentHttpRoot`: Base URL for serving content
- `ServiceConfigurationJson`: Service configuration JSON

## Building and Running

### Local Development

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run locally
func start
```

### Deploy to Azure

```bash
# Using ARM template
az deployment group create \
  --resource-group your-rg \
  --template-file deploy/azuredeploy.json \
  --parameters deploy/azuredeploy.parameters.json

# Or using Function Apps CLI
func azure functionapp publish your-function-app-name
```

## Usage

### Configure Windows Update Clients

Set Group Policy to point to your Azure Function:

1. **Specify intranet Microsoft update service location**
   - Set intranet update service: `https://your-function-app.azurewebsites.net/api/ClientWebService`
   - Set intranet statistics server: `https://your-function-app.azurewebsites.net/api/ClientWebService`

### Configure WSUS Servers

Point WSUS to sync from your Azure Function:

- Upstream server URL: `https://your-function-app.azurewebsites.net/api/ServerSyncWebService`

## Content Delivery

The content function supports:

- HTTP GET and HEAD requests
- Range requests for partial downloads
- SHA1 and SHA256 content addressing
- Proper MIME types and caching headers

Content URLs follow the pattern:

```
https://your-function-app.azurewebsites.net/api/content/{hash}
```

Where `{hash}` is the hex-encoded SHA1 or SHA256 hash of the content.

## Metadata Synchronization

The metadata sync functions provide the same capabilities as the upsync command-line tool but in a serverless environment:

### FetchConfiguration

```bash
POST /api/FetchConfiguration
Content-Type: application/json

{
  "UpstreamEndpoint": "https://your-upstream-server.com"  // Optional, defaults to Microsoft Update
}
```

### FetchCategories  

```bash
POST /api/FetchCategories
Content-Type: application/json

{
  "UpstreamEndpoint": "https://your-upstream-server.com"  // Optional
}
```

### FetchUpdates

```bash
POST /api/FetchUpdates  
Content-Type: application/json

{
  "UpstreamEndpoint": "https://your-upstream-server.com",  // Optional
  "UpdateIds": ["12345678-1234-1234-1234-123456789abc"],   // Optional, specific updates
  "ProductFilters": ["guid1", "guid2"],                    // Optional, product categories
  "ClassificationFilters": ["guid3", "guid4"]             // Optional, classifications
}
```

### ReindexStore

```bash
POST /api/ReindexStore
Content-Type: application/json

{
  "ForceReindex": false  // Optional, force reindex even if not required
}
```

### GetStoreStatus

```bash
GET /api/GetStoreStatus
```

Returns metadata store statistics and health information.

## Monitoring

The functions include comprehensive logging and can be monitored through:

- Application Insights (configured automatically)
- Azure Functions runtime logs
- Custom metrics and telemetry

## Security Considerations

- Use Azure AD authentication for production
- Configure network restrictions as needed
- Enable HTTPS only (automatic in Azure Functions)
- Use managed identities for storage access
- Review and configure CORS settings

## Limitations

- SOAP message handling is simplified - you may need to implement full SOAP parsing for complex scenarios
- Content storage is configured for Azure Blob Storage or local file system
- Function timeout limits may affect large sync operations

## Troubleshooting

### Common Issues

1. **Missing metadata store**: Ensure you've synced updates first using the upsync tool
2. **Content not found**: Verify content storage configuration and that content files exist
3. **SOAP parsing errors**: Check request format and implement proper SOAP envelope parsing
4. **Authentication failures**: Verify service configuration JSON is properly formatted

### Debugging

Enable verbose logging in `host.json`:

```json
{
  "logging": {
    "logLevel": {
      "default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

## Contributing

This is a conversion of the existing Microsoft Update server code. For improvements to the core functionality, see the main repository.

For Azure Functions specific issues:

1. Fork the repository
2. Create a feature branch
3. Submit a pull request

## License

MIT License - see the main repository for details.
