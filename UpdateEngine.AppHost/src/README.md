# Microsoft Update Functions AppHost

This project provides Aspire-based orchestration for the Microsoft Update Functions, enabling containerized development and deployment with Azure Storage Emulator integration. The AppHost has been updated to support the new service layer architecture with comprehensive monitoring and health checks.

## Overview

The AppHost uses .NET Aspire to orchestrate the Microsoft Update Functions along with supporting services like Azure Storage Emulator, providing a complete development environment that closely mirrors production Azure environments. The application now includes enhanced configuration for the deduplicated service layer architecture.

## Prerequisites

- .NET 9.0 SDK
- .NET Aspire packages (installed via NuGet)
- Azure Functions Core Tools 4.x
- Docker (for containerized resources)

## New Service Layer Architecture

The AppHost now supports the consolidated Azure Functions architecture with:

### Service Layer Components
- **ISyncService**: Metadata and content synchronization
- **IQueryService**: Metadata queries and exports
- **IHealthService**: System health monitoring

### Consolidated Functions
- **MetadataSyncFunctions**: Unified sync operations (HTTP + Timer triggers)
- **ContentSyncFunctions**: Content management 
- **MetadataQueryFunctions**: Query operations
- **StoreManagementFunctions**: Administrative tasks
- **AutomatedSyncFunctions**: Scheduling and orchestration

## Configuration

### Enhanced Automatic Configuration

Use the root-level configuration scripts with the updated AppHost:

```bash
# Configure and start with Azure Storage Emulator (recommended for development)
./configure-storage.sh --storage-mode AzureEmulator --use-apphost

# Configure and start with FileSystem storage
./configure-storage.sh --storage-mode FileSystem --use-apphost
```

### Manual Startup

```bash
cd AppHost
dotnet run
```

### Configuration Options

The AppHost now supports comprehensive configuration through environment variables and appsettings:

```json
{
  "MetadataStorePath": "./store",
  "ContentStorePath": "./content",
  "FeatureFlags": {
    "EnableScheduledSync": true,
    "EnableContentSync": true,
    "EnableHealthMonitoring": true
  }
}
```

## Architecture

### Enhanced Components

1. **Azure Storage Emulator**: Containerized Azure Storage service with health checks
2. **Microsoft Update Functions**: Azure Functions with service layer architecture
3. **Aspire Dashboard**: Web-based monitoring with enhanced metrics
4. **Health Monitoring**: Automated health checks and dependency tracking

### Resource Dependencies

```
Azure Storage Emulator → Microsoft Update Functions (with health checks)
```

## Available Endpoints

Once the AppHost is running, the following endpoints are available:

### Sync Operations (New Unified API)
- `POST /api/SyncMetadata` - Manual metadata synchronization
- `POST /api/SyncContent` - Manual content synchronization

### Query Operations (Enhanced)
- `GET /api/StoreStatus` - Detailed store status and statistics
- `POST /api/QueryMetadata` - Flexible metadata queries
- `POST /api/MatchDrivers` - Hardware driver matching
- `POST /api/ExportMetadata` - Export metadata in various formats

### Administrative Operations (New)
- `GET /api/HealthCheck` - Comprehensive system health check
- `POST /api/ReindexStore` - Force metadata store reindexing
- `GET /api/AvailableFilters` - Get available product/classification filters

### SOAP Endpoints (Unchanged)
- `POST /api/ClientWebService/client.asmx` - Windows Update client sync
- `POST /api/ServerWebService/server.asmx` - WSUS server-to-server sync

### Content Serving (Unchanged)
- `GET /api/content/{hash}` - Download update content files

## Automated Scheduling

The AppHost configures automated operations:

- **Every 4 hours**: Critical updates synchronization
- **Daily at 2 AM UTC**: Comprehensive metadata sync
- **Weekly Sunday 3 AM UTC**: Content synchronization
- **Weekly Sunday 1 AM UTC**: Maintenance tasks
- **Hourly**: Health monitoring and metrics collection

## Monitoring and Health Checks

### Aspire Dashboard Integration

Access the enhanced Aspire dashboard at `http://localhost:15888` for:
- Real-time function execution metrics
- Health check status monitoring
- Resource dependency visualization
- Log aggregation and filtering

### Health Check Endpoints

- `GET /api/HealthCheck` - Overall system health
- `GET /api/SyncHealth` - Sync operation specific health
- `GET /api/StoreStatus` - Metadata store health and statistics

### Development Features

- **Enhanced Logging**: Detailed startup information and endpoint listing
- **Dependency Tracking**: Visual representation of service dependencies
- **Configuration Validation**: Automatic validation of service configuration
- **Hot Reload Support**: Development-time configuration changes

## Migration from Previous Architecture

If you're migrating from the previous AppHost configuration:

1. **No Breaking Changes**: All existing endpoints remain functional
2. **Enhanced Configuration**: Additional configuration options available
3. **Improved Monitoring**: Better health checks and dependency tracking
4. **Service Layer**: Functions now use testable service layer architecture

## Troubleshooting

### Common Issues

1. **Functions not starting**: Verify Azure Functions Core Tools 4.x is installed
2. **Storage connection issues**: Ensure Docker is running for storage emulator
3. **Port conflicts**: Check that port 7071 is available
4. **Health check failures**: Check metadata and content store paths

### Debug Mode

Run with enhanced logging:

```bash
cd AppHost
dotnet run --environment Development --verbosity detailed
```

### Health Verification

After startup, verify all components are healthy:

```bash
# Check overall health
curl http://localhost:7071/api/HealthCheck

# Check store status  
curl http://localhost:7071/api/StoreStatus

# Verify Aspire dashboard
open http://localhost:15888
```

## Production Deployment

When deploying to production:

1. **Update Storage Configuration**: Replace emulator with actual Azure Storage
2. **Configure Authentication**: Set appropriate authorization levels
3. **Scale Configuration**: Adjust timer intervals for production load
4. **Monitoring**: Connect to production monitoring services
5. **Security**: Configure network security groups and access policies

## Service Layer Testing

The AppHost supports comprehensive testing:

```bash
# Run service layer unit tests
dotnet test ../../UpdateEngine.Functions/test/UpdateEngineTest --filter "Category!=Integration"

# Run integration tests against AppHost
dotnet test ../../UpdateEngine.Functions/test/UpdateEngineTest --filter "Category=Integration"
```

## Performance Monitoring

Monitor performance through:
- Aspire dashboard metrics
- Azure Functions runtime metrics
- Custom health check endpoints
- Storage operation statistics

The AppHost now provides a complete development environment that mirrors the production architecture while supporting the new service layer pattern for improved maintainability and testability.