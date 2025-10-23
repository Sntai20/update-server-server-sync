# Microsoft Update Functions AppHost

This project provides Aspire-based orchestration for the Microsoft Update Functions, enabling containerized development and deployment with Azure Storage Emulator integration.

## Overview

The AppHost uses .NET Aspire to orchestrate the Microsoft Update Functions along with supporting services like Azure Storage Emulator, providing a complete development environment that closely mirrors production Azure environments.

## Prerequisites

- .NET 8.0 SDK
- .NET Aspire packages (installed via NuGet)
- Azure Functions Core Tools 4.x
- Docker (for containerized resources)

## Configuration

### Automatic Configuration

Use the root-level configuration scripts to automatically configure and start the AppHost:

```bash
# Configure and start with Azure Storage Emulator
./configure-storage.sh --storage-mode AzureEmulator --use-apphost

# Configure and start with FileSystem storage
./configure-storage.sh --storage-mode FileSystem --use-apphost
```

### Manual Startup

```bash
cd MicrosoftUpdateFunctions.AppHost
dotnet run
```

## Architecture

### Components

1. **Azure Storage Emulator**: Containerized Azure Storage service
2. **Microsoft Update Functions**: Azure Functions application
3. **Aspire Dashboard**: Web-based monitoring and management

### Resource Dependencies

```
Azure Storage Emulator → Microsoft Update Functions
```

The Functions depend on the storage emulator for:
- Blob storage (metadata and content)
- Queue storage (background processing)
- Table storage (configuration and state)

## Features

### Development Benefits

- **Containerized Storage**: No need to install Azurite separately
- **Service Discovery**: Automatic connection string management
- **Health Monitoring**: Built-in health checks and metrics
- **Hot Reload**: Automatic restarts on code changes

### Production Parity

- **Azure Storage Emulation**: Identical APIs to Azure Storage
- **Networking**: Service-to-service communication patterns
- **Configuration**: Environment-based settings management

## Usage

### Starting Services

The AppHost will automatically:
1. Start Azure Storage Emulator container
2. Configure connection strings
3. Build and start Azure Functions
4. Open Aspire Dashboard in browser

### Accessing Services

- **Azure Functions**: http://localhost:7071
- **Aspire Dashboard**: http://localhost:15000 (auto-opens)
- **Storage Emulator**: Connection managed automatically

### Key Endpoints

- **Store Status**: http://localhost:7071/api/GetStoreStatus
- **Health Check**: http://localhost:7071/api/health
- **SOAP Services**: http://localhost:7071/api/*WebService/*.asmx

## Development Workflow

1. **Code Changes**: Edit files in `../MicrosoftUpdateFunctions/src/`
2. **Auto Rebuild**: AppHost detects changes and rebuilds
3. **Live Reload**: Functions restart automatically
4. **Monitor**: Use Aspire Dashboard to monitor logs and metrics

## Configuration Options

### Storage Modes

The AppHost supports two storage configurations:

#### Azure Storage Emulator
```csharp
var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var functions = builder.AddExecutable("update-functions", ...)
    .WithReference(storage);
```

#### File System
```csharp
var functions = builder.AddExecutable("update-functions", ...)
    .WithEnvironment("AzureWebJobsStorage", "")
    .WithEnvironment("MetadataStorePath", "./store");
```

### Environment Variables

The AppHost automatically configures:
- `FUNCTIONS_WORKER_RUNTIME`: `dotnet-isolated`
- `AzureWebJobsStorage`: Storage connection string
- `ServiceConfigurationJson`: Function configuration
- `ContentHttpRoot`: Content delivery URL

## Monitoring

### Aspire Dashboard

The Aspire Dashboard provides:
- **Service Status**: Real-time health of all services
- **Logs**: Centralized log aggregation
- **Metrics**: Performance and usage metrics
- **Dependencies**: Visual service topology

### Health Checks

Built-in health checks for:
- Azure Functions availability
- Storage connectivity
- SOAP endpoint responsiveness

## Troubleshooting

### Common Issues

1. **Port Conflicts**:
   - Functions: Default port 7071
   - Dashboard: Default port 15000
   - Use `--port` to override if needed

2. **Storage Connection**:
   - Verify Docker is running
   - Check Azurite container status
   - Review connection strings in logs

3. **Build Errors**:
   - Run `dotnet restore`
   - Check project references
   - Verify Aspire packages are installed

### Debug Mode

Start with verbose logging:
```bash
dotnet run --verbosity detailed
```

### Container Issues

Check Docker status:
```bash
docker ps
docker logs <container-id>
```

## Project Structure

```
MicrosoftUpdateFunctions.AppHost/
├── Program.cs                           # AppHost configuration
├── MicrosoftUpdateFunctions.AppHost.csproj  # Project file with Aspire packages
├── bin/                                 # Build outputs
└── obj/                                 # Build intermediates
```

## Dependencies

### NuGet Packages

- `Aspire.Hosting` (9.5.1): Core Aspire hosting
- `Aspire.Hosting.AppHost` (9.5.1): AppHost utilities
- `Aspire.Hosting.Azure.Functions` (9.5.1-preview): Azure Functions integration
- `Aspire.Hosting.Azure.Storage` (9.5.1): Azure Storage emulation

### Project References

- `../MicrosoftUpdateFunctions/src/MicrosoftUpdateFunctions.csproj`

## Advanced Features

### Custom Service Configuration

```csharp
var functions = builder.AddExecutable("update-functions", "func", "../MicrosoftUpdateFunctions/src", "start")
    .WithEnvironment("CUSTOM_SETTING", "value")
    .WithHttpEndpoint(port: 7071, name: "http");
```

### Health Check Customization

```csharp
functions.WithHealthCheck("/health");
```

### Resource Scaling

For production-like testing:
```csharp
var functions = builder.AddExecutable("update-functions", ...)
    .WithReplicas(3);  // Multiple instances
```

## Related Documentation

- [Azure Functions Documentation](../MicrosoftUpdateFunctions/README.md)
- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Configuration Guide](../STORAGE_GUIDE.md)

## Contributing

1. Test changes with both storage modes
2. Verify Docker container behavior
3. Update configuration scripts as needed
4. Document new environment variables
5. Test scaling scenarios

## License

This project is licensed under the MIT License - see the LICENSE file for details.