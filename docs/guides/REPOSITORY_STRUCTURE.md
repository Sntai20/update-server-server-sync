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
??? UpdateEngine/     # Azure Functions implementation
??? AppHost/ # Aspire application host
??? src/            # Core libraries
??? tests/         # Test projects
```

## ?? Current Documentation

### Root Level Documentation

| File | Purpose | Audience | Status |
|------|---------|----------|--------|
| [README.md](../../README.md) | Main project overview | All | ✅ Keep |
| [SECURITY.md](../../SECURITY.md) | Security policies | All | ✅ Keep |
| [STORAGE_GUIDE.md](./STORAGE_GUIDE.md) | Storage configuration | Developers | ✅ Moved to docs/guides/ |
| [WCF_NET9_FIX_GUIDE.md](./WCF_NET9_FIX_GUIDE.md) | .NET 9 WCF fixes | Developers | ✅ Moved to docs/guides/ |
| [INMEMORY_TESTING_GUIDE.md](./INMEMORY_TESTING_GUIDE.md) | In-memory testing | Developers | ✅ Moved to docs/guides/ |

### .github Directory

| File | Purpose | Status |
|------|---------|--------|
| [.github/copilot-instructions.md](./.github/copilot-instructions.md) | AI assistant instructions | ? Keep |
| [.github/upgrades/dotnet-upgrade-plan.md](./.github/upgrades/dotnet-upgrade-plan.md) | .NET upgrade plan | ? Keep |
| [.github/upgrades/dotnet-upgrade-report.md](./.github/upgrades/dotnet-upgrade-report.md) | .NET upgrade report | ? Keep |

### UpdateEngine

| File | Purpose | Status |
|------|---------|--------|
| [UpdateEngine/README.md](../../UpdateEngine/README.md) | Functions overview | ✅ Keep |
| [UpdateEngine/UpdateEngine - Deduplicate.md](../../UpdateEngine/UpdateEngine%20-%20Deduplicate.md) | Deduplication notes | ⚠️ Archive or delete |
| [UpdateEngine/src/README.md](../../UpdateEngine/src/README.md) | Source code overview | ✅ Keep |
| [TRIGGERS_GUIDE.md](./TRIGGERS_GUIDE.md) | Azure Functions triggers | ✅ Moved to docs/guides/ |
| [TESTING_GUIDE.md](./TESTING_GUIDE.md) | Testing strategies | ✅ Moved to docs/guides/ |

### AppHost

| File | Purpose | Status |
|------|---------|--------|
| [AppHost/README.md](../../AppHost/README.md) | AppHost overview | ✅ Keep |
| [MIGRATION_SUMMARY.md](./MIGRATION_SUMMARY.md) | Migration notes | ✅ Moved to docs/guides/ |
| [CONTAINER_VERIFICATION.md](./CONTAINER_VERIFICATION.md) | Container testing | ✅ Moved to docs/guides/ |
| [SYNC_TROUBLESHOOTING.md](./SYNC_TROUBLESHOOTING.md) | Sync issues | ✅ Moved to docs/guides/ |
| [TROUBLESHOOTING_STORAGE.md](./TROUBLESHOOTING_STORAGE.md) | Storage issues | ✅ Moved to docs/guides/ |

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
| [AppHost/Regenerate-WCFReferences.ps1](./AppHost/Regenerate-WCFReferences.ps1) | Regenerate WCF | ??? Duplicate - use root version |
| [AppHost/Test-SyncWithDiagnostics.ps1](./AppHost/Test-SyncWithDiagnostics.ps1) | Test sync | ?? Move to scripts/test/ |

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
   - UpdateEngine - Deduplicate.md
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
| Sync Troubleshooting | [AppHost/SYNC_TROUBLESHOOTING.md](./AppHost/SYNC_TROUBLESHOOTING.md) |

### For Operators

| Task | Script |
|------|--------|
| Configure Storage | `./configure-storage.ps1` |
| Run Tests | `./Run-InMemoryTests.ps1` |
| Validate Build | `./validate-build.ps1` |
| Test Sync | `./AppHost/Test-SyncWithDiagnostics.ps1` |

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
