# Scripts

This directory contains all automation scripts organized by purpose.

## ?? Directory Structure

- **setup/** - Setup and configuration scripts
- **build/** - Build and validation scripts
- **test/** - Testing scripts
- **maintenance/** - Maintenance and regeneration scripts

## ?? Scripts Index

### Setup Scripts
- [Configure-Storage.ps1](../setup/Configure-Storage.ps1) - Configure Azure Storage

### Build Scripts
- [Validate-Build.ps1](../build/Validate-Build.ps1) - Validate build

### Test Scripts
- [Run-InMemoryTests.ps1](../test/Run-InMemoryTests.ps1) - Run in-memory tests
- [Test-Startup.ps1](../test/Test-Startup.ps1) - Test application startup
- [Test-SyncWithDiagnostics.ps1](../../UpdateEngine.AppHost/src/Test-SyncWithDiagnostics.ps1) - Test sync with diagnostics

### Maintenance Scripts
- [Fix-WCF-ServiceReferences.ps1](../maintenance/Fix-WCF-ServiceReferences.ps1) - Fix WCF service references
- [Regenerate-WCF-Net9.ps1](../maintenance/Regenerate-WCF-Net9.ps1) - Regenerate WCF references for .NET 9
- [Regenerate-WCF-Net9-OfflineFirst.ps1](../maintenance/Regenerate-WCF-Net9-OfflineFirst.ps1) - Regenerate WCF (offline mode)

## ?? Quick Start

```powershell
# Run from repository root

# Setup
./scripts/setup/Configure-Storage.ps1

# Build
./scripts/build/Validate-Build.ps1

# Test
./scripts/test/Run-InMemoryTests.ps1
```

## 📖 Documentation

For detailed documentation, see the [docs/guides](../../docs/guides/README.md) directory.
