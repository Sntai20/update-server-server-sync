# Windows Update Services Server-Server Sync Protocol

Provide a C# implementation (.NET Core) of the Microsoft Update Server-Server sync protocol, both client and server.

Use this library to:
* Programmatically browse the Microsoft Update catalog
* Sync updates locally and run advanced queries on update metadata
* Export updates to WSUS
* Run an upstream update server in ASP.NET Core and serve updates to downstream WSUS servers
* Run an update server in ASP.NET Core and serve updates to Windows Update clients
* **NEW: Run as Azure Functions with .NET 9 support**

## 🚀 Quick Start

```powershell
# Clone repository
git clone https://github.com/microsoft/update-server-server-sync
cd update-server-server-sync

# Build (all projects)
dotnet build microsoft-update.sln

# Run tests (no infrastructure required!)
./scripts/test/Run-InMemoryTests.ps1

# Or run with Azure Functions via Aspire
dotnet run --project AppHost/src/AppHost.csproj
```

**📖 For detailed instructions, see [Configuration Guide](./docs/guides/CONFIGURATION_GUIDE.md)**

## 📂 Repository Organization

This repository is organized as follows:

- **[docs/](./docs/)** - All documentation (guides, troubleshooting, development)
- **[scripts/](./scripts/)** - All automation scripts (setup, build, test, maintenance)
- **[src/](./src/)** - Core libraries and implementation
- **[UpdateEngine.Functions/](./UpdateEngine.Functions/)** - Azure Functions implementation (.NET 9)
- **[UpdateEngine.Core/](./UpdateEngine.Core/)** - Shared core library (orchestrators, services, models)
- **[UpdateEngine.AppHost/](./UpdateEngine.AppHost/)** - .NET Aspire application host
- **[test/](./test/)** - Test projects

**📚 For complete structure details, see [docs/architecture/REPOSITORY_STRUCTURE.md](./docs/architecture/REPOSITORY_STRUCTURE.md)**

## 📖 Documentation

### Essential Guides

- **[Quick Reference](./docs/guides/QUICK_REFERENCE.md)** - Quick start guide for developers
- **[Configuration Guide](./docs/guides/CONFIGURATION_GUIDE.md)** - Simplified AppConfig approach
- **[Configuration Migration](./docs/guides/CONFIGURATION_MIGRATION_V2.md)** - Migration from complex configuration
- **[Storage Configuration](./docs/guides/STORAGE_GUIDE.md)** - Configure Azure Storage or local storage
- **[Testing Guide](./docs/guides/INMEMORY_TESTING_GUIDE.md)** - In-memory testing (no infrastructure required)
- **[Migration Summary](./docs/guides/MIGRATION_SUMMARY.md)** - Azure Storage migration guide

### Architecture & Consolidation

- **[Implementation Summary](./docs/guides/IMPLEMENTATION_SUMMARY.md)** - Complete overview of dual hosting + solution integration ⭐ **START HERE**
- **[Dual Hosting Solution Integration](./docs/guides/DUAL_HOSTING_SOLUTION_INTEGRATION.md)** - Integration with AppHost, Configuration, and update-cli
- **[Dual Hosting Model Guide](./docs/guides/DUAL_HOSTING_CONSOLIDATION_GUIDE.md)** - Support both Azure Functions AND Worker Service hosting
- **[Dual Hosting Quick Summary](./docs/guides/DUAL_HOSTING_QUICK_SUMMARY.md)** - Quick reference for dual hosting
- **[UpdateEngine.Core Restructuring](./docs/guides/UPDATEENGINE_CORE_RESTRUCTURING.md)** - Folder structure standardization (Nov 2025)
- **[Function Consolidation Guide](./docs/guides/FUNCTION_CONSOLIDATION_GUIDE.md)** - Comprehensive plan to consolidate 35+ functions to ~20
- **[Function Consolidation Comparison](./docs/guides/FUNCTION_CONSOLIDATION_COMPARISON.md)** - Before/after visual comparison
- **[Consolidation Answer](./docs/guides/CONSOLIDATION_ANSWER.md)** - Quick answers to consolidation questions
- **[Function Restructuring Summary](./docs/historical/migration-summaries/FUNCTIONS_RESTRUCTURING_SUMMARY.md)** - Function catalog

### Troubleshooting

- **[WCF .NET 9 Fixes](./docs/troubleshooting/WCF_NET9_FIX_GUIDE.md)** - Fix WCF compatibility issues
- **[Configuration Issues](./docs/troubleshooting/CONFIGURATION_TROUBLESHOOTING.md)** - Troubleshoot configuration problems
- **[Sync Issues](./docs/troubleshooting/SYNC_TROUBLESHOOTING.md)** - Troubleshoot synchronization issues
- **[Container Verification](./docs/guides/CONTAINER_VERIFICATION.md)** - Verify Azure containers

### API Documentation

- **[API Reference](https://microsoft.github.io/update-server-server-sync/)** - Complete API documentation
- **[Code Examples](https://microsoft.github.io/update-server-server-sync/examples/categories-fetch.html)** - Usage examples

## 🛠️ Common Tasks

| Task | Command |
|------|---------|
| **Build** | `dotnet build` |
| **Test (Fast)** | `./scripts/test/Run-InMemoryTests.ps1` |
| **Test (All)** | `dotnet test` |
| **Run Functions** | `dotnet run --project UpdateEngine.AppHost/src/AppHost.csproj` |
| **Configure Storage** | `./scripts/setup/Configure-Storage.ps1` |
| **Validate Build** | `./scripts/build/Validate-Build.ps1` |

**📖 For more commands, see [docs/guides/QUICK_REFERENCE.md](./docs/guides/QUICK_REFERENCE.md)**

## 🚀 Azure Functions Capabilities

This implementation provides **35+ Azure Functions** organized by functional domain, enabling both traditional ASP.NET Core and modern serverless architectures.

### 📦 Function Organization

Functions are organized into **4 logical domains** for better cohesion and maintainability:

```
UpdateEngine.Functions/src/Functions/
├── Core/                          # Essential operations (19 functions)
│   ├── WebServiceFunctions.cs    # 5 SOAP web services (WSUS protocol)
│   ├── ContentDeliveryFunctions.cs # 6 content operations
│   └── MetadataAccessFunctions.cs  # 8 query/export operations
├── Management/                    # Administrative operations (17 functions)
│   ├── UnifiedSyncFunctions.cs   # 11 sync operations
│   ├── UnifiedHealthFunctions.cs # 5 health/maintenance operations
│   └── DiagnosticFunctions.cs    # 3 diagnostic operations
├── Intelligence/                  # Advanced features (2 functions)
│   └── AnomalyDetectionFunctions.cs # ML-based anomaly detection
└── Shared/                       # Common utilities
    ├── SoapHelpers.cs            # SOAP request/response utilities
    ├── FunctionHelpers.cs        # HTTP/JSON utilities
    └── CommonModels.cs           # Shared request/response models
```

### 🎯 Core Capabilities

#### 1. **Metadata Synchronization & Management** (11 functions)
- Sync categories from upstream Microsoft Update servers
- Sync updates with filtering (products, classifications, dates)
- Comprehensive sync (categories + updates) with automatic rollback
- Pause, resume, cancel sync operations
- Background scheduled sync with timer triggers
- Export sync summaries to CSV manifests in Azure Blob Storage

#### 2. **Content Synchronization & Delivery** (6 functions)
- Download update content files to local or Azure Blob storage
- Serve content with HTTP range request support
- Content status tracking and verification
- Efficient content addressing (SHA1/SHA256 hash-based)
- Automatic content integrity validation

#### 3. **SOAP Web Services (WSUS Protocol)** (5 functions)
- **ClientWebService** - Serve updates to Windows Update clients (MUv6)
- **ServerSyncWebService** - Serve updates to downstream WSUS servers
- **SimpleAuthWebService** - Simple authentication for WSUS
- **DssAuthWebService** - Digital Signature Service authentication
- **ReportingWebService** - Client reporting and telemetry

#### 4. **Health Monitoring & Diagnostics** (8 functions)
- Real-time store status (indexed updates, categories, drivers)
- Sync progress tracking with detailed metrics
- Background health checks with configurable intervals
- Comprehensive diagnostics (configuration, storage, metadata)
- Service availability monitoring
- Automatic health reporting to Azure Blob Storage

#### 5. **Anomaly Detection & Intelligence** (2 functions)
- ML-based anomaly detection for sync operations
- Automatic detection of missing updates, inconsistencies
- Statistical analysis of update patterns
- Scheduled anomaly detection with reporting

#### 6. **Driver Matching & Hardware Compatibility** (1 function)
- Match drivers to hardware IDs (PnP IDs)
- Query driver metadata by hardware compatibility
- Support for Windows driver installation

#### 7. **Update Manifest Generation & Verification** (2 functions)
- Generate detailed CSV manifests with file tracking
  - **Columns**: LastWriteTime, LastSyncTime, FileName, FileHash, FileSize, Id, Title, Type, IsSuperseded, FilePath
- HTTP-triggered manifest verification
- Blob-triggered automatic verification on manifest upload
- Verify file presence from manifest after sync operations

#### 8. **Query & Filtering Operations** (8 functions)
- Query metadata by filter (titles, KB articles, classifications, products)
- Export metadata to JSON/CSV formats
- List available filters (products, classifications)
- Advanced filtering with multiple criteria
- Real-time metadata store status

#### 9. **Store Management Operations** (4 functions)
- Initialize metadata and content stores
- Reindex operations for store optimization
- Store statistics and metrics
- Configuration validation

#### 10. **RESTful HTTP APIs** (35+ endpoints)
All functions expose clean RESTful APIs:
- `GET /api/content/{contentHash}` - Download content
- `POST /api/sync/start` - Start sync operation
- `GET /api/metadata/query` - Query updates
- `POST /api/metadata/export` - Export metadata
- `GET /api/health/status` - Health check
- And 30+ more endpoints

#### 11. **Scheduled Automation** (Timer triggers)
- Background sync on configurable intervals
- Scheduled health checks
- Automatic anomaly detection
- Periodic manifest verification

#### 12. **Event-Driven Processing** (Blob triggers)
- Automatic manifest verification on upload
- Content validation on blob changes
- Reactive processing pipelines

#### 13. **Azure Storage Integration**
- **Metadata Storage**: Azure Blob or Local filesystem
- **Content Storage**: Azure Blob or Local filesystem
- **Reports Storage**: Manifests, health reports, diagnostics in `data/` containers
- Automatic retry logic with exponential backoff
- Connection string or managed identity authentication

#### 14. **Configuration Management**
- Environment-based configuration (appsettings.json, environment variables)
- JSON-based service configuration
- Storage configuration (Azure vs Local)
- Feature flags for optional capabilities
- Validation with helpful error messages

#### 15. **Comprehensive Error Handling**
- Structured exception handling with HTTP status codes
- Detailed error responses with timestamps
- Logging with correlation IDs
- Retry logic for transient failures

#### 16. **Developer Tools**
- In-memory testing (no infrastructure required)
- .NET Aspire orchestration for local development
- Docker support with Azurite for Azure Storage emulation
- Comprehensive test fixtures for integration testing

### 🔧 API Examples

```bash
# Query metadata
curl http://localhost:7071/api/metadata/query \
  -X POST -H "Content-Type: application/json" \
  -d '{"titleFilter":"Cumulative Update","maxResults":10}'

# Start comprehensive sync
curl http://localhost:7071/api/sync/comprehensive/start \
  -X POST -H "Content-Type: application/json" \
  -d '{"productTitles":["Windows 10"],"classificationIds":["<classification-id>"]}'

# Get store status
curl http://localhost:7071/api/health/status

# Download content
curl http://localhost:7071/api/content/abc123def456... -o update.cab

# Access SOAP endpoint (Windows Update clients)
curl http://localhost:7071/api/ClientWebService/client.asmx?wsdl
```

### 📊 Function Statistics

- **Total Functions**: 35+
- **SOAP Services**: 5 (WSUS protocol compliance)
- **HTTP APIs**: 30+ RESTful endpoints
- **Timer Triggers**: 3 scheduled operations
- **Blob Triggers**: 1 event-driven processor
- **Files**: 6 function classes (reduced from 10)
- **Code Reuse**: Shared helpers eliminate ~40% duplication

### 🎨 Architecture Benefits

1. **Cohesive Organization**: Functions grouped by domain (Core, Management, Intelligence)
2. **Loose Coupling**: Shared helpers and models reduce duplication
3. **Scalability**: Serverless functions scale independently
4. **Hybrid Deployment**: Same codebase for Azure Functions or ASP.NET Core
5. **Testability**: Comprehensive test fixtures with in-memory testing
6. **Maintainability**: Clear separation of concerns, ~40% fewer files

### 📖 Detailed Documentation

For comprehensive function details, see:

#### Testing
- **[Testing Strategy](./docs/guides/TESTING_STRATEGY.md)** - Comprehensive testing guide (Unit, Integration, E2E) ⭐ **NEW**
- **[Testing Strategy Quick Ref](./docs/guides/TESTING_STRATEGY_QUICK_REF.md)** - Quick reference for testing
- **[Function Restructuring Summary](./docs/historical/migration-summaries/FUNCTIONS_RESTRUCTURING_SUMMARY.md)** - Complete function catalog
- **[Testing Guide](./docs/guides/TESTING_GUIDE.md)** - Testing strategies
- **[Triggers Guide](./docs/guides/TRIGGERS_GUIDE.md)** - Azure Functions triggers

---

## Reference the library in your project

Visual Studio 2022 with .NET Core development tools is required to build the solution provided at `build/microsoft-update.sln`.

## Use the upsync utility

The upsync command line utility is provided as a sample for using the library. Upsync can be used to browse Microsoft's update catalog, sync updates locally and serve them to Windows Update clients or downstream WSUS servers.

You can build upsync in Visual Studio; it builds from the same solution as the library.

Or download and unzip upsync from [https://github.com/microsoft/update-server-server-sync/releases](https://github.com/microsoft/update-server-server-sync/releases)

See [upsync examples](https://github.com/microsoft/update-server-server-sync/wiki/UpSync-V3-examples)

## ✨ What's New in .NET 9

- 🚀 **Azure Functions support** - Run as serverless Azure Functions
- ⚡ **In-memory testing** - Fast tests with no infrastructure
- ☁️ **Azure Storage integration** - Modern Azure.Storage.Blobs SDK
- 🔧 **WCF .NET 9 compatibility** - Fixed service reference issues
- 💾 **Persistent storage** - Azurite with Docker volumes

**📚 For migration details, see [docs/guides/MIGRATION_SUMMARY.md](./docs/guides/MIGRATION_SUMMARY.md)**

## 🧪 Testing

This project includes comprehensive testing with **no infrastructure required** for fast development:

```powershell
# Fast in-memory tests (no Azurite/Docker needed!)
./scripts/test/Run-InMemoryTests.ps1

# Full integration tests (requires Azurite)
dotnet test --filter "Category=Integration"

# All tests
dotnet test
```

**🧪 For testing strategies, see [docs/guides/INMEMORY_TESTING_GUIDE.md](./docs/guides/INMEMORY_TESTING_GUIDE.md)**

# Contributing

This project welcomes contributions and suggestions. Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

---

## ?? Additional Resources

- [Repository Structure Guide](./REPOSITORY_STRUCTURE.md) - Detailed organization guide
- [Quick Reference](./QUICK_REFERENCE.md) - Developer quick reference
- [Reorganization Summary](./REORGANIZATION_SUMMARY.md) - How we organized this repo
- [Security Policy](./SECURITY.md) - Security and vulnerability reporting
