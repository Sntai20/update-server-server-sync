# MicrosoftUpdateFunctions - Deduplicated Architecture

## Overview

This folder contains the deduplicated and reorganized Azure Functions implementation for Microsoft Update Server-Server sync operations. The architecture has been streamlined to eliminate code duplication and ensure all features are properly supported through a service layer pattern.

## Architecture Changes

### Service Layer Pattern
- **ISyncService**: Handles all synchronization operations (metadata, content)
- **IQueryService**: Manages metadata queries and exports
- **IHealthService**: Provides system health monitoring and diagnostics

### Consolidated Functions
- **MetadataSyncFunctions**: Unified sync operations (replaces old MetadataSyncFunctions and MetadataSyncFunctionsImproved)
- **ContentSyncFunctions**: Content download and management
- **MetadataQueryFunctions**: Read-only metadata operations
- **StoreManagementFunctions**: Administrative and maintenance operations
- **AutomatedSyncFunctions**: Automated scheduling and orchestration
- **ClientSyncFunctions**: SOAP client endpoints (unchanged)
- **ServerSyncFunctions**: SOAP server endpoints (unchanged)
- **ContentFunctions**: Content serving endpoints (unchanged)

## Removed Duplications

1. **MetadataSyncFunctionsImproved.cs** - Merged into unified MetadataSyncFunctions.cs
2. **Inline sync logic** - Extracted to ISyncService
3. **Duplicate health checks** - Consolidated in IHealthService
4. **Redundant query operations** - Unified in IQueryService

## Key Features

### Trigger Types
- **HTTP Triggers**: Interactive API endpoints for manual operations
- **Timer Triggers**: Scheduled automation (daily comprehensive, 4-hour critical updates)
- **Maintenance Triggers**: Weekly store maintenance and health monitoring

### Service Layer Benefits
- **Testability**: Business logic separated from Azure Functions infrastructure
- **Reusability**: Services can be used across multiple function types
- **Maintainability**: Single source of truth for core operations
- **Mockability**: Full unit test coverage with service mocking

### Configuration Management
All configuration through appsettings and environment variables (CI/CD pipeline compatible):
```json
{
  "MetadataStorePath": "./store",
  "ContentStorePath": "./content",
  "ServiceConfigurationJson": "{\"ServiceUrl\":\"https://your-functions.azurewebsites.net\"}"
}
```

## Testing Strategy

### Unit Tests
- **Service Layer Tests**: Mock dependencies, test business logic
- **Function Tests**: Mock services, test HTTP handling and trigger behavior

### Integration Tests
- **End-to-End Tests**: Real Azure Functions with test fixtures
- **Storage Integration**: Validate actual store operations
- **SOAP Endpoint Tests**: Full protocol compliance testing

## Usage Examples

### Manual Sync Operations
```bash
# Sync critical updates only
POST /api/SyncMetadata
{
  "syncUpdates": true,
  "filterType": "critical"
}

# Comprehensive sync with categories
POST /api/SyncMetadata
{
  "syncCategories": true,
  "syncUpdates": true,
  "filterType": "comprehensive"
}
```

### Query Operations
```bash
# Get store status
GET /api/StoreStatus

# Query metadata with filters
POST /api/QueryMetadata
{
  "productFilters": ["Windows 10", "Windows 11"],
  "classificationFilters": ["Security Updates"],
  "maxResults": 100
}
```

### Administrative Operations
```bash
# Health check
GET /api/HealthCheck

# Force reindex
POST /api/ReindexStore
```

## Automated Scheduling

- **Daily 2 AM UTC**: Comprehensive metadata sync
- **Every 4 hours**: Critical updates sync
- **Weekly Sunday 3 AM UTC**: Content sync
- **Weekly Sunday 1 AM UTC**: Maintenance tasks
- **Hourly**: Health monitoring

## Migration Notes

If migrating from the old duplicated structure:
1. Remove references to `MetadataSyncFunctionsImproved`
2. Update any custom API calls to use the new unified endpoints
3. Update CI/CD pipelines to use the new service registration pattern
4. Run the test suite to verify all functionality is preserved

## Dependencies

All core Microsoft.PackageGraph libraries remain the same:
- microsoft-update-partition
- microsoft-update-webservices  
- microsoft-update-endpoints
- microsoft-update-upstream-package-source

New service layer has no additional external dependencies.