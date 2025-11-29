# Corrupted Metadata Store Fix

**Date**: 2025-01-16  
**Issue**: Azure Functions crashing on startup  
**Error**: `System.ArgumentException: An item with the same key has already been added`  
**Status**: ? **FIXED**

## Problem Description

The Azure Functions were crashing immediately on startup with a duplicate key error:

```
System.ArgumentException: An item with the same key has already been added. 
Key: MicrosoftUpdate:00000000-0000-0000-0000-000000000000:0
   at UpdateEngine.Metadata.Storage.Local.DirectoryPackageStore.ReadIdentities()
```

### Root Cause

The metadata store had **93.3 GB of corrupted data with duplicate package identities** in:
- `out/UpdateEngine/Debug/net9.0/LocalMetadataStore`
- `out/UpdateEngine/Debug/net9.0/LocalContentStore`  
- `out/UpdateEngine/x64/Debug/net9.0/LocalMetadataStore`
- `out/UpdateEngine/x64/Debug/net9.0/LocalContentStore`

## Solution

### 1. Deleted Corrupted Stores

```powershell
Get-ChildItem -Path "out\UpdateEngine" -Recurse -Directory | 
    Where-Object { $_.Name -like "Local*Store" } | 
    Remove-Item -Recurse -Force
```

**Result**: Deleted 93.3 GB of corrupted data

### 2. Created Maintenance Script

Created `scripts/maintenance/Clean-CorruptedStores.ps1`:
```powershell
./scripts/maintenance/Clean-CorruptedStores.ps1 -Force
```

### 3. Updated .gitignore

Add these patterns to prevent future corruption:
```gitignore
# Local metadata/content stores
**/LocalMetadataStore/
**/LocalContentStore/
out/**/LocalMetadataStore/
out/**/LocalContentStore/
```

## Testing the Fix

```bash
cd UpdateEngine.AppHost/src
dotnet run
```

**Expected**: Functions start without errors, using Azurite for storage.

## Prevention

1. Always use AppHost (ensures Azurite is used)
2. Never run Functions standalone without proper storage config
3. Clean output directories regularly: `dotnet clean`
4. Use cleanup script if you encounter duplicate key errors

---

**Issue Resolved**: 2025-01-16  
**Data Cleanup**: 93.3 GB  
**Impact**: Critical - Prevented all Function operations
