# Clean-CorruptedStores.ps1 Usage Guide

## Quick Start

```powershell
# Preview what would be deleted (safe - no changes made)
./scripts/maintenance/Clean-CorruptedStores.ps1 -DryRun

# Interactive cleanup with confirmation
./scripts/maintenance/Clean-CorruptedStores.ps1

# Force cleanup without confirmation
./scripts/maintenance/Clean-CorruptedStores.ps1 -Force

# Clean corrupted stores AND Azurite data
./scripts/maintenance/Clean-CorruptedStores.ps1 -Force -IncludeAzurite
```

## Features

### ? Enhanced Detection

The script now detects stores in ALL build configurations:
- ? `out/UpdateEngine/Debug/net9.0/`
- ? `out/UpdateEngine/x64/Debug/net9.0/` (often missed!)
- ? `out/UpdateEngine/Release/net9.0/`
- ? `out/UpdateEngine/x64/Release/net9.0/`
- ? WorkerService `data/` directories
- ? Source directory stores (if they exist)
- ? Azurite data (with `-IncludeAzurite` flag)

### ?? Disk Space Reporting

Shows exactly how much space will be freed:
```
Found: C:\...\LocalMetadataStore
  Size: 45.23 GB

Total disk space to be freed: 93.36 GB
```

### ?? Process Lock Detection

Automatically detects processes that might prevent cleanup:
```
Found 2 process(es) that might lock the stores:
  • PID 12345: dotnet
  • PID 67890: func

You may need to stop these processes before cleaning:
  • Stop AppHost (Ctrl+C in terminal)
  • Or use: Get-Process -Name dotnet,func | Stop-Process -Force
```

### ?? Dry Run Mode

Preview changes without making them:
```powershell
./scripts/maintenance/Clean-CorruptedStores.ps1 -DryRun
```

Output:
```
DRY RUN MODE - No changes will be made
...
DRY RUN - Would delete 8 directory(ies) (93.36 GB)

Run without -DryRun to actually delete these directories.
```

### ? Better Error Handling

Provides specific guidance for common errors:

**Error: "Being used by another process"**
```
? Error: The process cannot access the file because it is being used by another process.
  Tip: Stop AppHost and try again, or use: Get-Process -Name dotnet | Stop-Process -Force
```

**Error: "Access denied"**
```
? Error: Access to the path is denied.
  Tip: Run PowerShell as Administrator
```

## Common Scenarios

### Scenario 1: "Duplicate Key" Error on Startup

**Symptom**: Functions crash with `An item with the same key has already been added`

**Solution**:
```powershell
# 1. Stop AppHost (Ctrl+C)
# 2. Clean corrupted stores
./scripts/maintenance/Clean-CorruptedStores.ps1 -Force
# 3. Restart AppHost
cd UpdateEngine.AppHost/src
dotnet run
```

### Scenario 2: Running Out of Disk Space

**Problem**: Dev machine running low on space

**Solution**:
```powershell
# Preview disk space that can be freed
./scripts/maintenance/Clean-CorruptedStores.ps1 -DryRun

# If it shows significant space (10+ GB), clean it
./scripts/maintenance/Clean-CorruptedStores.ps1 -Force
```

### Scenario 3: Reset Everything

**Problem**: Want to start fresh with clean Azurite + stores

**Solution**:
```powershell
# Stop AppHost first (Ctrl+C)

# Clean everything including Azurite
./scripts/maintenance/Clean-CorruptedStores.ps1 -Force -IncludeAzurite

# Restart AppHost
cd UpdateEngine.AppHost/src
dotnet run
```

### Scenario 4: CI/CD Pipeline

**Use Case**: Clean builds in CI/CD

**Solution**:
```powershell
# In CI pipeline before build
./scripts/maintenance/Clean-CorruptedStores.ps1 -Force -WhatIf:$false
```

Or simpler:
```powershell
# Clean output directory entirely
dotnet clean
Remove-Item -Path "out" -Recurse -Force -ErrorAction SilentlyContinue
```

## Parameters Reference

| Parameter | Type | Description |
|-----------|------|-------------|
| `-Force` | Switch | Skip confirmation prompts |
| `-DryRun` | Switch | Preview without deleting |
| `-IncludeAzurite` | Switch | Also clean Azurite storage emulator data |
| `-WhatIf` | Switch | PowerShell built-in "what if" mode |

## Output Examples

### Successful Cleanup

```
??????????????????????????????????????????????????????????????????
?  Clean Corrupted Metadata Stores                               ?
??????????????????????????????????????????????????????????????????

Scanning for corrupted metadata stores...
Found: C:\Repos\update-server-server-sync\out\UpdateEngine\Debug\net9.0\LocalMetadataStore
  Size: 45.23 GB
Found: C:\Repos\update-server-server-sync\out\UpdateEngine\Debug\net9.0\LocalContentStore
  Size: 48.13 GB

???????????????????????????????????????????????????????????????

Found 2 directory(ies) to clean:
  • C:\Repos\update-server-server-sync\out\UpdateEngine\Debug\net9.0\LocalMetadataStore
    45.23 GB
  • C:\Repos\update-server-server-sync\out\UpdateEngine\Debug\net9.0\LocalContentStore
    48.13 GB

Total disk space to be freed: 93.36 GB

Cleaning corrupted stores...
Removing: C:\...\LocalMetadataStore... ?
Removing: C:\...\LocalContentStore... ?

??????????????????????????????????????????????????????????????????
?  Cleanup Complete                                              ?
??????????????????????????????????????????????????????????????????

Cleaned: 2 directory(ies)
Freed disk space: 93.36 GB

Next steps:
  1. Start AppHost: cd UpdateEngine.AppHost/src && dotnet run
  2. Fresh stores will be created automatically
  3. Verify startup in Aspire Dashboard: https://localhost:15001
  4. Run sync: curl -X POST http://localhost:7071/api/sync
```

### No Stores Found

```
??????????????????????????????????????????????????????????????????
?  Clean Corrupted Metadata Stores                               ?
??????????????????????????????????????????????????????????????????

Scanning for corrupted metadata stores...
? No corrupted stores found. All clean!
```

### Dry Run

```
??????????????????????????????????????????????????????????????????
?  Clean Corrupted Metadata Stores                               ?
??????????????????????????????????????????????????????????????????

? DRY RUN MODE - No changes will be made

...

? DRY RUN - Would delete 2 directory(ies) (93.36 GB)

Run without -DryRun to actually delete these directories.
```

## Integration with Development Workflow

### Daily Development

```powershell
# Before starting work each day
./scripts/maintenance/Clean-CorruptedStores.ps1 -DryRun

# If shows large stores, clean them
./scripts/maintenance/Clean-CorruptedStores.ps1 -Force
```

### Before Pull Requests

```powershell
# Clean build
dotnet clean
./scripts/maintenance/Clean-CorruptedStores.ps1 -Force

# Fresh build
dotnet build

# Test
dotnet test
```

### When Switching Branches

```powershell
# Clean everything
./scripts/maintenance/Clean-CorruptedStores.ps1 -Force -IncludeAzurite

# Checkout new branch
git checkout feature-branch

# Fresh start
cd UpdateEngine.AppHost/src
dotnet run
```

## Troubleshooting

### Script Won't Delete Some Directories

**Symptoms**:
```
Removing: C:\...\LocalMetadataStore... ?
  Error: The process cannot access the file because it is being used by another process.
```

**Solutions** (try in order):

1. **Stop AppHost gracefully**:
   ```powershell
   # Press Ctrl+C in AppHost terminal
   ```

2. **Stop all dotnet processes**:
   ```powershell
   Get-Process -Name dotnet | Stop-Process -Force
   Get-Process -Name func | Stop-Process -Force
   ```

3. **Close Visual Studio**

4. **Run as Administrator**:
   ```powershell
   # Right-click PowerShell ? "Run as Administrator"
   ./scripts/maintenance/Clean-CorruptedStores.ps1 -Force
   ```

5. **Restart computer** (if all else fails)

### Script Takes Too Long

**Cause**: Large stores (50+ GB) take time to calculate size

**Workaround**:
```powershell
# Skip size calculation, just delete
Remove-Item -Path "out\UpdateEngine" -Recurse -Force
```

### Want to Keep Some Stores

**Solution**: Delete specific directories manually
```powershell
# Keep Debug, delete Release
Remove-Item -Path "out\UpdateEngine\Release" -Recurse -Force
Remove-Item -Path "out\UpdateEngine\x64\Release" -Recurse -Force
```

## See Also

- **Main Issue**: `docs/fixes/CORRUPTED_METADATA_STORE_FIX.md`
- **Startup Issues**: `docs/STARTUP_FIX_SUMMARY.md`
- **Build Verification**: `docs/BUILD_VERIFICATION.md`
- **Testing Guide**: `docs/guides/QUICK_START_DOWNSTREAM_SYNC.md`

---

**Script Location**: `scripts/maintenance/Clean-CorruptedStores.ps1`  
**Last Updated**: 2025-01-16  
**Tested On**: Windows 11, PowerShell 7.x
