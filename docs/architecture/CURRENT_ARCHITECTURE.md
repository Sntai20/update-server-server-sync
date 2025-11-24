# Current System Architecture (November 2025)

**Status**: Active - reflects current codebase structure  
**Last Updated**: November 23, 2025  
**Target Framework**: .NET 9.0

---

## Overview

The Microsoft Update Server-Server Sync implementation provides a comprehensive solution for browsing the Microsoft Update catalog, synchronizing metadata locally, and serving updates to downstream clients. The system supports both traditional ASP.NET Core hosting and modern Azure Functions serverless deployment.

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                      Microsoft Update Catalog                        │
│                    (Upstream Update Source)                          │
└─────────────────────────┬───────────────────────────────────────────┘
                          │
                          │ HTTPS/SOAP
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│                   UpdateEngine System                                │
│                                                                       │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │           Hosting Layer (Choose One or Both)                   │  │
│  │                                                                 │  │
│  │  ┌──────────────────────┐    ┌──────────────────────┐        │  │
│  │  │ Azure Functions      │    │ Worker Service       │        │  │
│  │  │ (Serverless)         │    │ (ASP.NET Core)       │        │  │
│  │  │ UpdateEngine.        │    │ UpdateEngine.        │        │  │
│  │  │   Functions/         │    │   WorkerService/     │        │  │
│  │  └──────────┬───────────┘    └──────────┬───────────┘        │  │
│  │             │                             │                     │  │
│  │             └─────────────┬───────────────┘                     │  │
│  │                           │                                     │  │
│  └───────────────────────────┼─────────────────────────────────────┘
│                               │                                       │
│  ┌───────────────────────────▼─────────────────────────────────────┐│
│  │           Core Business Logic Layer                              ││
│  │           UpdateEngine.Core/                                     ││
│  │                                                                  ││
│  │  ┌──────────────────┐  ┌──────────────────┐  ┌───────────────┐││
│  │  │  Orchestrators   │  │    Services      │  │   Models      │││
│  │  │  • Sync          │  │  • Cache         │  │  • Config     │││
│  │  │  • Metadata      │  │  • Queue         │  │  • Requests   │││
│  │  │  • Content       │  │  • Anomaly Det.  │  │  • Responses  │││
│  │  └──────────────────┘  └──────────────────┘  └───────────────┘││
│  │                                                                  ││
│  └──────────────────────────┬───────────────────────────────────────┘│
│                              │                                        │
│  ┌──────────────────────────▼────────────────────────────────────┐  │
│  │         Storage Abstraction Layer                              │  │
│  │         UpdateEngine.Metadata/                                 │  │
│  │                                                                 │  │
│  │  ┌─────────────────────┐    ┌─────────────────────┐          │  │
│  │  │  IMetadataStore     │    │  IContentStore      │          │  │
│  │  │  • PackageStore     │    │  • FileSystem       │          │  │
│  │  │  • Azure Blob       │    │  • Azure Blob       │          │  │
│  │  └─────────────────────┘    └─────────────────────┘          │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                       │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │         Web Services Layer                                     │  │
│  │         UpdateEngine.WebServices/ & UpdateEngine.Endpoints/    │  │
│  │                                                                 │  │
│  │  • SOAP Web Services (WSUS Protocol)                          │  │
│  │  • ClientWebService, ServerSyncWebService                     │  │
│  │  • SimpleAuthWebService, ReportingWebService                  │  │
│  └───────────────────────────────────────────────────────────────┘  │
│                                                                       │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │         Upstream Sync Layer                                    │  │
│  │         UpdateEngine.UpstreamSource/                           │  │
│  │                                                                 │  │
│  │  • UpstreamServerClient                                       │  │
│  │  • UpstreamCategoriesSource                                   │  │
│  │  • UpstreamUpdatesSource                                      │  │
│  └───────────────────────────────────────────────────────────────┘  │
└───────────────────────────────┬───────────────────────────────────────┘
                                │
                                │ HTTPS/SOAP
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     Downstream Clients                               │
│         (WSUS Servers, Windows Update Clients)                      │
└─────────────────────────────────────────────────────────────────────┘
```

## Project Structure

```
update-server-server-sync/
│
├── UpdateEngine.Functions/       # Azure Functions implementation
│   ├── src/
│   │   ├── Functions/           # HTTP/Timer/ServiceBus triggers
│   │   │   ├── Core/            # Essential operations
│   │   │   ├── Management/      # Admin operations
│   │   │   └── Intelligence/    # ML/Anomaly detection
│   │   ├── Program.cs           # DI & startup configuration
│   │   └── host.json            # Functions runtime config
│   └── test/                    # Function tests
│
├── UpdateEngine.Core/            # Shared business logic
│   └── src/
│       ├── Orchestrators/       # High-level workflows
│       │   ├── SyncOrchestrator.cs
│       │   ├── MetadataOrchestrator.cs
│       │   └── ContentOrchestrator.cs
│       ├── Services/            # Domain services
│       │   ├── CacheService.cs
│       │   ├── QueueService.cs
│       │   └── AnomalyDetectionService.cs
│       ├── Models/              # DTOs and domain models
│       └── HealthChecks/        # Health monitoring
│
├── UpdateEngine.WorkerService/   # ASP.NET Core Worker Service
│   └── src/
│       ├── Controllers/         # REST API controllers
│       └── Program.cs           # DI & startup
│
├── UpdateEngine.AppHost/         # .NET Aspire orchestration
│   └── src/
│       └── Program.cs           # Multi-service orchestration
│
├── UpdateEngine.Metadata/        # Storage abstractions
│   └── src/
│       ├── IMetadataStore.cs
│       ├── IContentStore.cs
│       └── PackageStore implementations
│
├── UpdateEngine.WebServices/     # SOAP web services
│   └── src/
│       ├── ClientWebService.cs
│       └── ServerSyncWebService.cs
│
├── UpdateEngine.Endpoints/       # ASP.NET Core endpoints
│   └── src/
│       └── Startup.cs
│
├── UpdateEngine.UpstreamSource/  # Microsoft Update client
│   └── src/
│       ├── UpstreamServerClient.cs
│       └── Source implementations
│
├── UpdateEngine.Configuration/   # Configuration management
│   └── src/
│       └── AppConfig.cs         # Centralized configuration
│
├── UpdateEngine.Cli/             # Command-line interface
│   └── src/
│       └── Download handlers for Windows updates
│
└── UpdateEngine.SyncTool/        # Legacy sync tool
    └── src/
        └── upsync utility
```

## Data Flow

### 1. Upstream Sync Flow

```
Microsoft Update Catalog
    ↓ (SOAP/HTTPS)
UpstreamServerClient
    ↓
SyncOrchestrator
    ↓
IMetadataStore (Azure Blob or Local)
    ↓
Metadata indexed and queryable
```

### 2. Client Request Flow

```
Downstream Client (WSUS/Windows Update)
    ↓ (SOAP/HTTPS)
Azure Functions OR Worker Service
    ↓
WebServiceFunctions (SOAP endpoint)
    ↓
MetadataOrchestrator / ContentOrchestrator
    ↓
IMetadataStore / IContentStore
    ↓
Response (Update metadata or content)
```

## Key Components

### Core Orchestrators

Located in `UpdateEngine.Core/src/Orchestrators/`

| Orchestrator | Purpose | Key Methods |
|--------------|---------|-------------|
| **SyncOrchestrator** | Manages upstream synchronization | `SyncFromUpstreamAsync()`, `SyncCategoriesAsync()` |
| **MetadataOrchestrator** | Handles metadata queries and exports | `GetUpdateDetailsAsync()`, `ExportUpdatesAsync()` |
| **ContentOrchestrator** | Manages content delivery | `GetContentAsync()`, `GetContentMetadataAsync()` |

### Domain Services

Located in `UpdateEngine.Core/src/Services/`

| Service | Purpose | Dependencies |
|---------|---------|--------------|
| **CacheService** | Distributed caching (Redis) | `IDistributedCache` |
| **QueueService** | Async message queuing | `Azure.Storage.Queues` |
| **AnomalyDetectionService** | ML-based anomaly detection | ML.NET |

### Storage Abstractions

Located in `UpdateEngine.Metadata/src/`

| Interface | Implementations | Purpose |
|-----------|----------------|---------|
| **IMetadataStore** | PackageStore (local), Azure.PackageStore (blob) | Update metadata storage |
| **IContentStore** | FileSystemContentStore, BlobContentStore | Update content files storage |

### Azure Functions

Located in `UpdateEngine.Functions/src/Functions/`

**Core Functions** (19 functions):
- **WebServiceFunctions** (5): SOAP web services for WSUS protocol
- **ContentDeliveryFunctions** (6): Content download and metadata
- **MetadataAccessFunctions** (8): Query, export, driver matching

**Management Functions** (17 functions):
- **UnifiedSyncFunctions** (11): Sync operations (categories, updates, incremental)
- **UnifiedHealthFunctions** (5): Health checks and diagnostics
- **DiagnosticFunctions** (3): Store info, metrics, configuration

**Intelligence Functions** (2 functions):
- **AnomalyDetectionFunctions**: ML-based anomaly detection

## Configuration

### Centralized Configuration

All configuration managed through `UpdateEngine.Configuration/AppConfig.cs`:

```csharp
public class AppConfig
{
    public string MetadataStorePath { get; set; }
    public string ContentStorePath { get; set; }
    public string ServiceUrl { get; set; }
    public bool EnableCache { get; set; }
    public string RedisConnectionString { get; set; }
    // ... additional properties
}
```

### Configuration Sources (Priority Order)

1. **Environment variables** (highest priority)
2. **local.settings.json** (local development only)
3. **appsettings.{Environment}.json**
4. **appsettings.json**
5. **Configuration/shared/** (lowest priority)

### Key Configuration Files

- **UpdateEngine.Functions/src/local.settings.json** - Azure Functions local settings
- **UpdateEngine.WorkerService/src/appsettings.json** - Worker Service settings
- **UpdateEngine.UpdateEngine.AppHost/src/appsettings.json** - Aspire orchestration settings
- **UpdateEngine.Configuration/shared/appsettings.shared.json** - Shared defaults

## Deployment Models

### 1. Azure Functions (Serverless)

**Recommended for**: Elastic scaling, cost optimization, event-driven workloads

```bash
# Deploy via Azure CLI
cd UpdateEngine.Functions/src
func azure functionapp publish <function-app-name>

# Deploy via Bicep
az deployment group create \
  --resource-group <rg-name> \
  --template-file Deployment/main.bicep
```

### 2. Worker Service (Traditional Hosting)

**Recommended for**: Consistent workloads, on-premises, full control

```bash
# Run locally
cd UpdateEngine.WorkerService/src
dotnet run

# Deploy as Windows Service
sc create UpdateEngineService binPath="<path-to-exe>"
```

### 3. Dual Hosting (Hybrid)

**Recommended for**: Maximum flexibility, gradual migration

```bash
# Run via Aspire (orchestrates both)
cd UpdateEngine.AppHost/src
dotnet run
```

## Storage Options

### Metadata Storage

| Option | Implementation | Use Case |
|--------|----------------|----------|
| **Local File System** | PackageStore | Development, small-scale |
| **Azure Blob Storage** | Azure.PackageStore | Production, cloud-native |

### Content Storage

| Option | Implementation | Use Case |
|--------|----------------|----------|
| **Local File System** | FileSystemContentStore | On-premises, low latency |
| **Azure Blob Storage** | BlobContentStore | Production, scalability |

## Caching & Performance

### Distributed Caching

**Implementation**: Redis via Azure Cache for Redis or self-hosted

**Cached Data**:
- Metadata statistics (5 min TTL)
- Update details (60 min TTL)
- Content availability (15 min TTL)

**Configuration**:
```json
{
  "EnableCache": true,
  "RedisConnectionString": "<redis-connection-string>"
}
```

### Performance Characteristics

| Operation | Without Cache | With Cache | Improvement |
|-----------|---------------|------------|-------------|
| Get Update Details | ~200ms | ~10ms | 20x |
| List Available Updates | ~500ms | ~25ms | 20x |
| Get Metadata Stats | ~300ms | ~15ms | 20x |

## Health Monitoring

### Health Check Endpoints

**Azure Functions**:
- `GET /api/health` - Overall health status
- `GET /api/health/metadata` - Metadata store health
- `GET /api/health/content` - Content store health
- `GET /api/health/upstream` - Upstream connection health

**Worker Service**:
- `GET /health` - Overall health status
- `GET /health/ready` - Readiness probe
- `GET /health/live` - Liveness probe

### Monitoring Integration

- **Application Insights**: Telemetry, traces, metrics
- **Health Checks**: Built-in ASP.NET Core health checks
- **.NET Aspire Dashboard**: Multi-service monitoring

## Security

### Authentication & Authorization

- **Azure Functions**: Function-level keys (development) or Azure AD (production)
- **Worker Service**: ASP.NET Core authentication middleware
- **Upstream Sync**: TLS 1.2+ required for Microsoft Update catalog

### Data Protection

- **At Rest**: Azure Storage encryption (256-bit AES)
- **In Transit**: TLS 1.2+ for all HTTPS communication
- **Secrets Management**: Azure Key Vault integration

## Testing Strategy

### Test Pyramid

```
         /\
        /  \  E2E Tests (Integration with Aspire)
       /────\
      /      \ Integration Tests (Real storage, no network)
     /────────\
    /          \ Unit Tests (Mocked dependencies)
   /────────────\
```

### Test Projects

- **UpdateEngine.Functions/test/** - Function and integration tests
- **UpdateEngine.Core/test/** - Unit tests for orchestrators and services

### Running Tests

```bash
# Fast unit tests (in-memory)
.\scripts\test\Run-InMemoryTests.ps1

# Integration tests (requires Azurite)
.\scripts\test\Test-AzuriteIntegration.ps1

# All tests
dotnet test
```

## Development Tools

### Local Development

**Option 1: Aspire (Recommended)**
```bash
cd UpdateEngine.AppHost/src
dotnet run
```

**Option 2: Azure Functions CLI**
```bash
cd UpdateEngine.Functions/src
func start
```

### Debugging

- **Visual Studio**: F5 debugging with Aspire launch profile
- **VS Code**: `.vscode/launch.json` configurations available
- **Azure Functions Core Tools**: `func start --verbose`

## Migration Notes

### From Legacy upsync

The original `src/tools/upsync` CLI tool is being superseded by:
- **UpdateEngine.Cli** - Modern cross-platform CLI
- **UpdateEngine.Functions** - Serverless automation
- **UpdateEngine.SyncTool** - Compatibility layer

### Project Renames (2025)

| Old Name | New Name | Notes |
|----------|----------|-------|
| `UpdateEngine.Functions/` | `UpdateEngine.Functions/` | Clarity |
| `UpdateEngine.AppHost/` | `UpdateEngine.AppHost/` | Consistency |
| `azure-functions/` | `UpdateEngine.Functions/` | Deprecated |

## Related Documentation

- **[Architecture Decisions](./ARCHITECTURE_DECISIONS.md)** - Historical design decisions (contains old paths)
- **[Repository Structure](./REPOSITORY_STRUCTURE.md)** - Detailed file organization
- **[Data Flow Guide](../guides/DATA_FLOW_ARCHITECTURE_GUIDE.md)** - Detailed data flow diagrams
- **[Configuration Guide](../guides/CONFIGURATION_GUIDE.md)** - Complete configuration reference
- **[Storage Guide](../guides/STORAGE_GUIDE.md)** - Storage configuration and best practices
- **[Testing Guide](../guides/TESTING_GUIDE.md)** - Comprehensive testing strategies

---

**Note**: This document reflects the current (November 2025) architecture. For historical context and evolution of design decisions, see [ARCHITECTURE_DECISIONS.md](./ARCHITECTURE_DECISIONS.md), which contains code examples from earlier project phases with old directory structures.

**Last Reviewed**: November 23, 2025  
**Maintainer**: UpdateEngine Team  
**Status**: ✅ Current and Accurate
