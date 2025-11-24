# Quick Reference: New Codebase Structure

**Last Updated**: 2025-01-XX  
**Status**: ? All changes complete

---

## ?? Project Locations

### Core Infrastructure
```
Configuration/src/Configuration.csproj              ? Shared configuration
ServiceDefaults/src/ServiceDefaults.csproj          ? Aspire defaults
```

### UpdateEngine (Dual Hosting)
```
UpdateEngine.Core/src/UpdateEngine.Core.csproj          ? Shared business logic
UpdateEngine.Functions/src/UpdateEngine.csproj                ? Azure Functions
UpdateEngine.Functions/test/UpdateEngineTest.csproj           ? Tests
```

### Hosting Models
```
WorkerService/src/WorkerService.csproj              ? Long-running service
UpdateEngine.AppHost/src/AppHost.csproj                          ? Aspire orchestration
```

### Domain Libraries
```
microsoft-update-partition/src/                      ? Metadata storage
microsoft-update-webservices/src/                    ? SOAP services
microsoft-update-endpoints/src/                      ? Endpoints
microsoft-update-upstream-source/src/                ? Upstream sync
```

### CLI Tools
```
upsync/src/upsync.csproj                            ? Sync CLI
update-cli/src/update-cli.csproj                    ? Update CLI
```

---

## ?? Common Commands

### Build Specific Project
```powershell
# Configuration
dotnet build Configuration/src/Configuration.csproj

# UpdateEngine.Core (shared library)
dotnet build UpdateEngine.Core/src/UpdateEngine.Core.csproj

# WorkerService
dotnet build WorkerService/src/WorkerService.csproj

# Azure Functions
dotnet build UpdateEngine.Functions/src/UpdateEngine.csproj

# AppHost
dotnet build UpdateEngine.AppHost/src/AppHost.csproj
```

### Run Locally
```powershell
# Aspire AppHost (recommended - starts everything)
cd UpdateEngine.AppHost
dotnet run --project src/AppHost.csproj

# Azure Functions only
cd UpdateEngine.Functions
func start

# WorkerService only
cd WorkerService/src
dotnet run
```

### Run Tests
```powershell
# All tests
dotnet test

# Integration tests
dotnet test --filter "Category=Integration"

# In-memory tests
.\scripts\test\Run-InMemoryTests.ps1
```

---

## ?? Key Files by Feature

### Sync Operations
```
UpdateEngine.Core/src/Orchestrators/SyncOrchestrator.cs       ? Main sync logic
UpdateEngine.Core/src/Services/SyncService.cs                 ? Sync service
UpdateEngine.Core/src/Models/SyncModels.cs                    ? Request/response models
WorkerService/src/Workers/SyncWorker.cs                   ? Background worker
UpdateEngine.Functions/src/Functions/Core/UnifiedSyncFunction.cs    ? HTTP trigger
```

### Metadata Operations
```
UpdateEngine.Core/src/Orchestrators/MetadataOrchestrator.cs   ? Metadata logic
UpdateEngine.Core/src/Services/QueryService.cs                ? Query service
UpdateEngine.Functions/src/Functions/Core/MetadataAccessFunctions.cs ? HTTP endpoints
```

### Health Monitoring
```
UpdateEngine.Core/src/HealthChecks/                           ? All health checks
UpdateEngine.Core/src/Services/HealthService.cs               ? Health service
WorkerService/src/Controllers/HealthController.cs         ? Health API
```

### Configuration
```
Configuration/src/AppConfig.cs                            ? Main config
Configuration/src/SyncConfiguration.cs                    ? Sync settings
Configuration/src/StorageConfiguration.cs                 ? Storage settings
Configuration/src/CacheConfiguration.cs                   ? Cache settings
```

---

## ?? Project Dependencies

### UpdateEngine.Core Dependencies
```
UpdateEngine.Core.csproj references:
  � Configuration/src/Configuration.csproj
  � microsoft-update-partition/src/
  � microsoft-update-upstream-source/src/
  � microsoft-update-webservices/src/
```

### WorkerService Dependencies
```
WorkerService/src/WorkerService.csproj references:
  � Configuration/src/Configuration.csproj
  � ServiceDefaults/src/ServiceDefaults.csproj
  � UpdateEngine.Core/src/UpdateEngine.Core.csproj
```

### UpdateEngine Dependencies
```
UpdateEngine.Functions/src/UpdateEngine.csproj references:
  � Configuration/src/Configuration.csproj
  � UpdateEngine.Core/src/UpdateEngine.Core.csproj
  � All microsoft-update-* domain libraries
```

### AppHost Dependencies
```
UpdateEngine.AppHost/src/AppHost.csproj references:
  � Configuration/src/Configuration.csproj (not as resource)
  � UpdateEngine.Functions/src/UpdateEngine.csproj
  � WorkerService/src/WorkerService.csproj
```

---

## ?? Code Reuse Map

### Shared in UpdateEngine.Core (95% reuse)
- ? Orchestrators (`ISyncOrchestrator`, `IMetadataOrchestrator`, `IContentOrchestrator`)
- ? Services (`ISyncService`, `IQueryService`, `IHealthService`, etc.)
- ? Models (`UnifiedSyncRequest`, `SyncOperationResult`, etc.)
- ? Health Checks (all 5 health checks)
- ? Cache Service

### Hosting-Specific (5% unique per hosting model)
- Azure Functions: HTTP triggers, timer triggers
- Worker Service: BackgroundService, REST API controllers

---

## ?? Important Namespaces

### Use These ?
```csharp
using Configuration;                          // App configuration
using UpdateEngine.Core.Orchestrators;        // Business logic orchestrators
using UpdateEngine.Core.Services;             // Business services
using UpdateEngine.Core.Models;               // Data models
using Microsoft.UpdateServices.WorkerService; // Worker service components
```

### Don't Use These ?
```csharp
using UpdateEngine.Services;                  // OLD - removed
using UpdateEngine.Models;                    // OLD - removed
using UpdateEngine.Orchestrators;             // OLD - removed
```

---

## ?? Quick Start

### First Time Setup
```powershell
# Clone repository
git clone https://github.com/microsoft/update-server-server-sync.git
cd update-server-server-sync

# Restore packages
dotnet restore

# Build everything
dotnet build

# Run with Aspire
cd UpdateEngine.AppHost
dotnet run --project src/AppHost.csproj
```

### Daily Development
```powershell
# Pull latest changes
git pull

# Build (incremental)
dotnet build

# Run Aspire AppHost
cd UpdateEngine.AppHost
dotnet run --project src/AppHost.csproj
```

### Adding New Feature
1. Add business logic to `UpdateEngine.Core/src/`
2. Add HTTP trigger to `UpdateEngine.Functions/src/Functions/`
3. Add background worker or API to `WorkerService/src/`
4. Update tests in `UpdateEngine.Functions/test/`
5. Build and test: `dotnet build && dotnet test`

---

## ?? Troubleshooting

### "Cannot find UpdateEngine.Core"
```powershell
# Rebuild UpdateEngine.Core
dotnet build UpdateEngine.Core/src/UpdateEngine.Core.csproj --no-incremental
```

### "Project reference path not found"
Check these paths updated to new structure:
- `Configuration/src/Configuration.csproj` (not `Configuration/Configuration.csproj`)
- `ServiceDefaults/src/ServiceDefaults.csproj` (not `ServiceDefaults/ServiceDefaults/ServiceDefaults.csproj`)
- `WorkerService/src/WorkerService.csproj` (not `WorkerService/WorkerService.csproj`)

### "Namespace does not exist"
Update to new namespaces:
- `UpdateEngine.Services` ? `UpdateEngine.Core.Services`
- `UpdateEngine.Models` ? `UpdateEngine.Core.Models`
- `UpdateEngine.Orchestrators` ? `UpdateEngine.Core.Orchestrators`

### Clean Build
```powershell
dotnet clean
dotnet build --no-incremental
```

---

## ?? Documentation Index

### Migration Guides
- **docs/guides/MIGRATION_COMPLETE.md** - UpdateEngine.Core migration
- **docs/guides/FOLDER_STRUCTURE_REORGANIZATION.md** - Folder restructuring
- **docs/guides/COMPLETE_REORGANIZATION_SUMMARY.md** - Combined summary

### Testing Guides
- **docs/guides/WEEK4_DAY3_TESTING_GUIDE.md** - Testing strategies
- **docs/guides/INMEMORY_TESTING_GUIDE.md** - In-memory testing
- **docs/guides/QUICKSTART_DUAL_HOSTING_TESTS.md** - Dual hosting tests

### Scripts
- **scripts/maintenance/Reorganize-FolderStructure.ps1** - Folder reorganization
- **scripts/migration/Migrate-UpdateEngineCore.ps1** - Core migration
- **scripts/test/Run-InMemoryTests.ps1** - In-memory tests
- **scripts/test/Test-DualHosting.ps1** - Dual hosting tests

---

## ? Validation Commands

### Verify Structure
```powershell
# Check project files exist at new locations
Test-Path Configuration/src/Configuration.csproj          # Should be True
Test-Path ServiceDefaults/src/ServiceDefaults.csproj      # Should be True
Test-Path WorkerService/src/WorkerService.csproj          # Should be True
Test-Path UpdateEngine.Core/src/UpdateEngine.Core.csproj      # Should be True

# Check old locations don't exist
Test-Path Configuration/Configuration.csproj              # Should be False
Test-Path ServiceDefaults/ServiceDefaults/ServiceDefaults.csproj  # Should be False
Test-Path WorkerService/WorkerService.csproj              # Should be False
```

### Verify Builds
```powershell
# Build all 6 key projects
$projects = @(
    "Configuration/src/Configuration.csproj",
    "ServiceDefaults/src/ServiceDefaults.csproj",
    "UpdateEngine/core/UpdateEngine.Core.csproj",
    "WorkerService/src/WorkerService.csproj",
    "UpdateEngine/src/UpdateEngine.csproj",
    "AppHost/src/AppHost.csproj"
)

foreach ($proj in $projects) {
    Write-Host "Building $proj..." -ForegroundColor Cyan
    dotnet build $proj --no-incremental -v quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ? Success" -ForegroundColor Green
    } else {
        Write-Host "  ? Failed" -ForegroundColor Red
    }
}
```

---

**Quick Reference Last Updated**: 2025-01-XX  
**For detailed information, see**: docs/guides/COMPLETE_REORGANIZATION_SUMMARY.md
