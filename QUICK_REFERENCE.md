# ?? Quick Reference Guide

**Last Updated**: 2025-01-24

## ?? Table of Contents

- [Getting Started](#getting-started)
- [Common Tasks](#common-tasks)
- [Troubleshooting](#troubleshooting)
- [Testing](#testing)
- [Development Workflows](#development-workflows)

---

## Getting Started

### First Time Setup

```powershell
# 1. Clone repository
git clone https://github.com/Sntai20/update-server-server-sync
cd update-server-server-sync

# 2. Configure storage
./scripts/setup/Configure-Storage.ps1

# 3. Build solution
dotnet build

# 4. Run tests
./scripts/test/Run-InMemoryTests.ps1
```

### Prerequisites

- ? **.NET 9 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
- ? **Azure Functions Core Tools** - `npm install -g azure-functions-core-tools@4`
- ? **Docker Desktop** (optional) - For Azurite
- ? **PowerShell 7+** - For scripts

---

## Common Tasks

### Development

| Task | Command |
|------|---------|
| **Build Solution** | `dotnet build` |
| **Clean Build** | `dotnet clean && dotnet build` |
| **Restore Packages** | `dotnet restore` |
| **Run Functions Locally** | `dotnet run --project AppHost` |
| **Stop Functions** | `Ctrl + C` |

### Testing

| Task | Command |
|------|---------|
| **Run In-Memory Tests** | `./scripts/test/Run-InMemoryTests.ps1` |
| **Run All Tests** | `dotnet test` |
| **Run Specific Test** | `dotnet test --filter "FullyQualifiedName~TestName"` |
| **Test with Diagnostics** | `./scripts/test/Test-SyncWithDiagnostics.ps1` |

### Storage Configuration

| Task | Command |
|------|---------|
| **Configure Storage** | `./scripts/setup/Configure-Storage.ps1` |
| **Use Local Storage** | Edit `appsettings.json`: `"UseAzureStorage": false` |
| **Use Azurite** | Edit `appsettings.json`: `"UseAzureStorage": true` |
| **Verify Containers** | See [Container Verification](./docs/troubleshooting/CONTAINER_VERIFICATION.md) |

### Maintenance

| Task | Command |
|------|---------|
| **Validate Build** | `./scripts/build/Validate-Build.ps1` |
| **Fix WCF References** | `./scripts/maintenance/Fix-WCF-ServiceReferences.ps1` |
| **Regenerate WCF** | `./scripts/maintenance/Regenerate-WCF-Net9.ps1` |

---

## Troubleshooting

### Quick Fixes

| Problem | Solution |
|---------|----------|
| **Build fails** | Run `dotnet clean && dotnet build` |
| **Functions won't start** | Check if port 7071 is in use |
| **Storage errors** | Run `./scripts/setup/Configure-Storage.ps1` |
| **WCF proxy errors** | Run `./scripts/maintenance/Fix-WCF-ServiceReferences.ps1` |
| **Test failures** | Delete `bin/` and `obj/` folders, rebuild |

### Detailed Guides

| Issue | Guide |
|-------|-------|
| **Storage Issues** | [Storage Troubleshooting](./docs/troubleshooting/TROUBLESHOOTING_STORAGE.md) |
| **Sync Issues** | [Sync Troubleshooting](./docs/troubleshooting/SYNC_TROUBLESHOOTING.md) |
| **WCF .NET 9 Issues** | [WCF Fix Guide](./docs/troubleshooting/WCF_NET9_FIX_GUIDE.md) |
| **Container Issues** | [Container Verification](./docs/troubleshooting/CONTAINER_VERIFICATION.md) |

### Error Messages

<details>
<summary><b>"The store does not exist or is corrupt"</b></summary>

```powershell
# Solution 1: Initialize store
./scripts/setup/Configure-Storage.ps1

# Solution 2: Delete and recreate
Remove-Item ./store -Recurse -Force
dotnet run --project AppHost
```
</details>

<details>
<summary><b>"Method not supported on this proxy"</b></summary>

```powershell
# Fix WCF service references
./scripts/maintenance/Fix-WCF-ServiceReferences.ps1
dotnet build
```
</details>

<details>
<summary><b>"Container does not exist"</b></summary>

```powershell
# Verify and create containers
./scripts/setup/Configure-Storage.ps1
```
</details>

---

## Testing

### Test Organization

```
?? tests/
??? Unit Tests (Fast, no dependencies)
?   ??? ServiceTests
?   ??? ModelTests
?   ??? In-Memory Tests ? (Fastest!)
?
??? Integration Tests (Require Azurite)
?   ??? Endpoint Tests
?   ??? Sync Tests
?
??? Performance Tests (Slow)
    ??? Load Tests
```

### Running Different Test Suites

```powershell
# ? Fast tests only (no infrastructure)
./scripts/test/Run-InMemoryTests.ps1

# ?? All tests (requires Azurite)
dotnet test

# ?? Specific test category
dotnet test --filter "Category=Unit"
dotnet test --filter "FullyQualifiedName~InMemoryIntegrationTests"

# ?? With coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Results

| Suite | Tests | Duration | Dependencies |
|-------|-------|----------|--------------|
| In-Memory | 10/10 ? | ~1-2 sec | None |
| Service Tests | 6/11 ?? | ~7 sec | None (has pre-existing failures) |
| Integration | Varies | ~30 sec | Azurite + Functions |
| Full Suite | All | ~2-3 min | All infrastructure |

---

## Development Workflows

### ?? Standard Development Cycle

```powershell
# 1. Create feature branch
git checkout -b feature/my-feature

# 2. Make changes
# ... edit files ...

# 3. Test changes
./scripts/test/Run-InMemoryTests.ps1

# 4. Build and validate
./scripts/build/Validate-Build.ps1

# 5. Commit and push
git add .
git commit -m "feat: my feature"
git push origin feature/my-feature

# 6. Create PR
# Go to GitHub and create pull request
```

### ?? Test-Driven Development

```powershell
# 1. Write failing test
# ... create test ...

# 2. Run test (should fail)
dotnet test --filter "FullyQualifiedName~MyNewTest"

# 3. Implement feature
# ... write code ...

# 4. Run test (should pass)
dotnet test --filter "FullyQualifiedName~MyNewTest"

# 5. Refactor and repeat
```

### ?? Debugging Issues

```powershell
# 1. Reproduce issue
./scripts/test/Test-SyncWithDiagnostics.ps1

# 2. Check logs
# Look at Functions console output

# 3. Run diagnostics
Invoke-RestMethod -Uri "http://localhost:7071/api/StorageDiagnostics" | ConvertTo-Json -Depth 5

# 4. Get detailed errors
Invoke-RestMethod -Uri "http://localhost:7071/api/GetStoreStatus" | ConvertTo-Json
```

### ?? Release Workflow

```powershell
# 1. Ensure main branch is up to date
git checkout main
git pull upstream main

# 2. Create release branch
git checkout -b release/v1.0.0

# 3. Update version numbers
# ... edit version files ...

# 4. Run full test suite
dotnet test

# 5. Build release
dotnet build -c Release

# 6. Tag release
git tag -a v1.0.0 -m "Release v1.0.0"
git push upstream v1.0.0
```

---

## ?? Documentation Links

### Essential Reading

- [Main README](./README.md) - Project overview
- [Storage Guide](./docs/guides/STORAGE_GUIDE.md) - Storage configuration
- [Testing Guide](./docs/development/INMEMORY_TESTING_GUIDE.md) - Testing strategies
- [Migration Guide](./docs/guides/MIGRATION_SUMMARY.md) - Azure Storage migration

### API Documentation

- [API Reference](./src/documentation/docfx-config/index.md)
- [Code Examples](./src/documentation/docfx-config/examples/)
- [Azure Functions Guide](./UpdateEngine/README.md)
- [Triggers Guide](./UpdateEngine/src/TRIGGERS_GUIDE.md)

### Architecture

- [Project Structure](./REPOSITORY_STRUCTURE.md)
- [Copilot Instructions](./.github/copilot-instructions.md)
- [Upgrade Reports](./.github/upgrades/)

---

## ?? Getting Help

### Self-Service

1. **Search Issues** - Check [GitHub Issues](https://github.com/microsoft/update-server-server-sync/issues)
2. **Read Docs** - Check [docs/](./docs/) directory
3. **Run Diagnostics** - Use diagnostic scripts in [scripts/test/](./scripts/test/)

### Contact

- **Bug Reports** - [Create an Issue](https://github.com/microsoft/update-server-server-sync/issues/new)
- **Feature Requests** - [Create an Issue](https://github.com/microsoft/update-server-server-sync/issues/new)
- **Security Issues** - See [SECURITY.md](./SECURITY.md)

---

## ?? Pro Tips

### Performance

- ? Use in-memory tests during development (fastest)
- ? Run full tests before pushing
- ? Use `--no-build` flag when running tests multiple times
- ? Enable parallel test execution with `--parallel`

### Productivity

```powershell
# Create aliases in your PowerShell profile
Set-Alias -Name build -Value "dotnet build"
Set-Alias -Name test -Value "./scripts/test/Run-InMemoryTests.ps1"
Set-Alias -Name run -Value "dotnet run --project AppHost"

# Quick rebuild
function rebuild { dotnet clean; dotnet build }

# Quick test cycle
function quicktest { dotnet build --no-restore; dotnet test --no-build }
```

### Git Workflow

```bash
# Useful git aliases
git config --global alias.co checkout
git config --global alias.br branch
git config --global alias.ci commit
git config --global alias.st status
git config --global alias.unstage 'reset HEAD --'
git config --global alias.last 'log -1 HEAD'
```

---

**For more detailed information, see the full [REPOSITORY_STRUCTURE.md](./REPOSITORY_STRUCTURE.md) guide.**
