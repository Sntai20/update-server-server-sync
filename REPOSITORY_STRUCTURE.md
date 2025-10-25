# Repository Structure & Documentation Index

This document provides an organized overview of all documentation and scripts in this repository.

## ?? Repository Organization

```
update-server-server-sync/
??? ?? docs/# Centralized documentation (PROPOSED)
?   ??? guides/      # How-to guides
?   ??? troubleshooting/     # Troubleshooting guides
?   ??? development/      # Development guides
?   ??? api/      # API documentation
?
??? ?? scripts/              # Centralized scripts (PROPOSED)
?   ??? setup/      # Setup and configuration scripts
?   ??? build/    # Build and validation scripts
?   ??? test/         # Testing scripts
?   ??? maintenance/      # Maintenance and regeneration scripts
?
??? MicrosoftUpdateFunctions/     # Azure Functions implementation
??? MicrosoftUpdateFunctions.AppHost/ # Aspire application host
??? src/            # Core libraries
??? tests/         # Test projects
```

## ?? Current Documentation

### Root Level Documentation

| File | Purpose | Audience | Status |
|------|---------|----------|--------|
| [README.md](./README.md) | Main project overview | All | ? Keep |
| [SECURITY.md](./SECURITY.md) | Security policies | All | ? Keep |
| [STORAGE_GUIDE.md](./STORAGE_GUIDE.md) | Storage configuration | Developers | ?? Move to docs/guides/ |
| [WCF_NET9_FIX_GUIDE.md](./WCF_NET9_FIX_GUIDE.md) | .NET 9 WCF fixes | Developers | ?? Move to docs/troubleshooting/ |
| [INMEMORY_TESTING_GUIDE.md](./INMEMORY_TESTING_GUIDE.md) | In-memory testing | Developers | ?? Move to docs/development/ |

### .github Directory

| File | Purpose | Status |
|------|---------|--------|
| [.github/copilot-instructions.md](./.github/copilot-instructions.md) | AI assistant instructions | ? Keep |
| [.github/upgrades/dotnet-upgrade-plan.md](./.github/upgrades/dotnet-upgrade-plan.md) | .NET upgrade plan | ? Keep |
| [.github/upgrades/dotnet-upgrade-report.md](./.github/upgrades/dotnet-upgrade-report.md) | .NET upgrade report | ? Keep |

### MicrosoftUpdateFunctions

| File | Purpose | Status |
|------|---------|--------|
| [MicrosoftUpdateFunctions/README.md](./MicrosoftUpdateFunctions/README.md) | Functions overview | ? Keep |
| [MicrosoftUpdateFunctions/MicrosoftUpdateFunctions - Deduplicate.md](./MicrosoftUpdateFunctions/MicrosoftUpdateFunctions%20-%20Deduplicate.md) | Deduplication notes | ??? Archive or delete |
| [MicrosoftUpdateFunctions/src/README.md](./MicrosoftUpdateFunctions/src/README.md) | Source code overview | ? Keep |
| [MicrosoftUpdateFunctions/src/TRIGGERS_GUIDE.md](./MicrosoftUpdateFunctions/src/TRIGGERS_GUIDE.md) | Azure Functions triggers | ? Keep |
| [MicrosoftUpdateFunctions/tests/.../TESTING_GUIDE.md](./MicrosoftUpdateFunctions/tests/MicrosoftUpdateFunctions.Tests/TESTING_GUIDE.md) | Testing strategies | ?? Merge with INMEMORY_TESTING_GUIDE.md |

### MicrosoftUpdateFunctions.AppHost

| File | Purpose | Status |
|------|---------|--------|
| [MicrosoftUpdateFunctions.AppHost/README.md](./MicrosoftUpdateFunctions.AppHost/README.md) | AppHost overview | ? Keep |
| [MicrosoftUpdateFunctions.AppHost/MIGRATION_SUMMARY.md](./MicrosoftUpdateFunctions.AppHost/MIGRATION_SUMMARY.md) | Migration notes | ?? Move to docs/guides/ |
| [MicrosoftUpdateFunctions.AppHost/CONTAINER_VERIFICATION.md](./MicrosoftUpdateFunctions.AppHost/CONTAINER_VERIFICATION.md) | Container testing | ?? Move to docs/troubleshooting/ |
| [MicrosoftUpdateFunctions.AppHost/SYNC_TROUBLESHOOTING.md](./MicrosoftUpdateFunctions.AppHost/SYNC_TROUBLESHOOTING.md) | Sync issues | ?? Move to docs/troubleshooting/ |
| [MicrosoftUpdateFunctions.AppHost/TROUBLESHOOTING_STORAGE.md](./MicrosoftUpdateFunctions.AppHost/TROUBLESHOOTING_STORAGE.md) | Storage issues | ?? Move to docs/troubleshooting/ |

### src/documentation

| File | Purpose | Status |
|------|---------|--------|
| [src/documentation/docfx-config/index.md](./src/documentation/docfx-config/index.md) | API docs index | ? Keep |
| [src/documentation/docfx-config/api/index.md](./src/documentation/docfx-config/api/index.md) | API reference | ? Keep |
| [src/documentation/docfx-config/examples/*.md](./src/documentation/docfx-config/examples/) | Code examples | ? Keep |

## ?? Current Scripts

### Root Level Scripts

| Script | Purpose | Status |
|--------|---------|--------|
| [configure-storage.ps1](./configure-storage.ps1) | Configure storage | ?? Move to scripts/setup/ |
| [Fix-WCF-ServiceReferences.ps1](./Fix-WCF-ServiceReferences.ps1) | Fix WCF references | ?? Move to scripts/maintenance/ |
| [Regenerate-WCF-Net9-OfflineFirst.ps1](./Regenerate-WCF-Net9-OfflineFirst.ps1) | Regenerate WCF (offline) | ?? Move to scripts/maintenance/ |
| [Regenerate-WCF-Net9.ps1](./Regenerate-WCF-Net9.ps1) | Regenerate WCF (online) | ?? Move to scripts/maintenance/ |
| [Run-InMemoryTests.ps1](./Run-InMemoryTests.ps1) | Run in-memory tests | ?? Move to scripts/test/ |
| [test-startup.ps1](./test-startup.ps1) | Test startup | ?? Move to scripts/test/ |
| [validate-build.ps1](./validate-build.ps1) | Validate build | ?? Move to scripts/build/ |

### AppHost Scripts

| Script | Purpose | Status |
|--------|---------|--------|
| [MicrosoftUpdateFunctions.AppHost/Regenerate-WCFReferences.ps1](./MicrosoftUpdateFunctions.AppHost/Regenerate-WCFReferences.ps1) | Regenerate WCF | ??? Duplicate - use root version |
| [MicrosoftUpdateFunctions.AppHost/Test-SyncWithDiagnostics.ps1](./MicrosoftUpdateFunctions.AppHost/Test-SyncWithDiagnostics.ps1) | Test sync | ?? Move to scripts/test/ |

## ?? Recommended Actions

### Phase 1: Create New Directory Structure (Immediate)

```powershell
# Run this from repository root
./scripts/setup/Reorganize-Repository.ps1
```

### Phase 2: Move Documentation (After review)

1. **Create docs/ directory structure**
2. **Move guides to docs/guides/**
   - STORAGE_GUIDE.md
   - MIGRATION_SUMMARY.md
   - INMEMORY_TESTING_GUIDE.md (merge with TESTING_GUIDE.md)
   
3. **Move troubleshooting to docs/troubleshooting/**
   - WCF_NET9_FIX_GUIDE.md
   - CONTAINER_VERIFICATION.md
   - SYNC_TROUBLESHOOTING.md
   - TROUBLESHOOTING_STORAGE.md

### Phase 3: Move Scripts (After testing)

1. **Create scripts/ directory structure**
2. **Move and organize scripts**
   - Setup scripts ? scripts/setup/
   - Test scripts ? scripts/test/
 - Build scripts ? scripts/build/
   - Maintenance scripts ? scripts/maintenance/

### Phase 4: Update References (Critical!)

1. **Update all script paths in:**
   - GitHub Actions workflows
   - README.md instructions
   - Developer documentation
   - CI/CD pipelines

2. **Update all documentation links**
   - Internal cross-references
   - External references
   - Table of contents

### Phase 5: Cleanup

1. **Archive obsolete files**
   - MicrosoftUpdateFunctions - Deduplicate.md
   - Duplicate scripts
   
2. **Create .deprecated/ folder for historical reference**

## ?? Quick Reference

### For Developers

| Task | Documentation |
|------|---------------|
| Getting Started | [README.md](./README.md) |
| Configure Storage | [STORAGE_GUIDE.md](./STORAGE_GUIDE.md) |
| Run Tests | [INMEMORY_TESTING_GUIDE.md](./INMEMORY_TESTING_GUIDE.md) |
| Fix WCF Issues | [WCF_NET9_FIX_GUIDE.md](./WCF_NET9_FIX_GUIDE.md) |
| Sync Troubleshooting | [AppHost/SYNC_TROUBLESHOOTING.md](./MicrosoftUpdateFunctions.AppHost/SYNC_TROUBLESHOOTING.md) |

### For Operators

| Task | Script |
|------|--------|
| Configure Storage | `./configure-storage.ps1` |
| Run Tests | `./Run-InMemoryTests.ps1` |
| Validate Build | `./validate-build.ps1` |
| Test Sync | `./MicrosoftUpdateFunctions.AppHost/Test-SyncWithDiagnostics.ps1` |

## ?? Migration Status

- [x] Documentation audit complete
- [x] Script audit complete
- [ ] New directory structure created
- [ ] Files moved to new locations
- [ ] References updated
- [ ] Old locations deprecated
- [ ] CI/CD updated

## ?? Notes

- **Do NOT delete files until all references are updated**
- **Test all scripts after moving**
- **Update CI/CD pipelines before merging**
- **Keep this document updated as organization evolves**

---

**Last Updated**: 2025-01-24
**Maintained By**: Development Team
