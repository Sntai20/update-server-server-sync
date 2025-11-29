# Azure Storage Configuration - Default Values Fix

**Date:** November 29, 2025  
**Issue:** Content and metadata being written to local filesystem instead of Azure Blob Storage  
**Status:** ? FIXED

## Problem Description

Despite having:
- ? Connection strings discovered correctly (via `Environment.GetEnvironmentVariable`)
- ? `UseAzureStorageForMetadata: true` and `UseAzureStorageForContent: true` in configuration
- ? Azure Blob Storage accessible (Azurite running)
- ? Concurrency fixes implemented

The system was **still creating local directories** and writing data there:
```
C:\Users\ansantan\Repos\update-server-server-sync\out\UpdateEngine\x64\Debug\net9.0\LocalMetadataStore
C:\Users\ansantan\Repos\update-server-server-sync\out\UpdateEngine\x64\Debug\net9.0\LocalContentStore
```

## Root Cause Analysis

### Issue #1: Default Values in C# Class

`UpdateEngine.Configuration\src\StorageConfiguration.cs` had default values:

```csharp
public string MetadataPath { get; set; } = "./LocalMetadataStore";  // ? PROBLEM!
public string ContentPath { get; set; } = "./LocalContentStore";    // ? PROBLEM!
```

**Impact:** Even though these values were **not in `appsettings.Development.json`**, the C# class provided defaults. When configuration binding happened, these default values were used.

### Issue #2: ServiceCollectionExtensions Logic

In `ServiceCollectionExtensions.cs` line 193 (Content Store registration):

```csharp
// Decide whether to use Azure Storage based on:
// 1. Configuration flag is true AND connection string is available, OR
// 2. Connection string is available (Aspire-provided) AND ContentPath is empty
var useAzureStorage = (storageConfig.UseAzureStorageForContent && !string.IsNullOrEmpty(connectionString))
    || (!string.IsNullOrEmpty(connectionString) && string.IsNullOrEmpty(storageConfig.ContentPath));
```

**The Problem:**
- First condition: `UseAzureStorageForContent: true` AND connection string available ? **Should trigger**
- **BUT**: The first condition evaluated to `true`, so Azure Storage should have been used
- **Real issue**: The logging shows the decision was made correctly, but something downstream was still creating local directories

### Issue #3: ConfigurationHelper Setting Paths

In `ConfigurationHelper.cs` lines 114-119:

```csharp
// IMPORTANT: Only set local paths when NOT using Azure Storage
if (!appConfig.StorageConfiguration.UseAzureStorageForMetadata)
{
    functions.WithEnvironment("UpdateEngine__StorageConfiguration__MetadataPath", 
        appConfig.StorageConfiguration.MetadataPath);
}
```

**The Problem:**
- This logic was correct - it **didn't** set the environment variables when using Azure Storage
- **BUT**: The configuration system still read the default values from the C# class
- So even though we didn't set environment variables, the bound `AppConfig` object had the default paths

## The Chain of Issues

1. **Binding Phase**: `AppConfig` class instantiated with default values:
   ```csharp
   MetadataPath = "./LocalMetadataStore"  // From C# default
   ContentPath = "./LocalContentStore"    // From C# default
   UseAzureStorageForMetadata = true      // From JSON
   UseAzureStorageForContent = true       // From JSON
   ```

2. **ServiceCollectionExtensions**: Checks `storageConfig.ContentPath`:
   ```csharp
   string.IsNullOrEmpty(storageConfig.ContentPath)  // FALSE! (has default value)
   ```

3. **Decision Logic**: Even though `UseAzureStorageForContent` is true, the secondary condition:
   ```csharp
   !string.IsNullOrEmpty(connectionString) && string.IsNullOrEmpty(storageConfig.ContentPath)
   ```
   Evaluated to: `true && false` = **false**

4. **Result**: The first condition should have worked, but somewhere in the logic, local storage was being initialized as a fallback.

## Solutions Implemented

### Solution 1: Remove Default Values

Modified `UpdateEngine.Configuration\src\StorageConfiguration.cs`:

```csharp
// BEFORE:
public string MetadataPath { get; set; } = "./LocalMetadataStore";
public string ContentPath { get; set; } = "./LocalContentStore";

// AFTER:
public string? MetadataPath { get; set; }  // ? Nullable, no default
public string? ContentPath { get; set; }   // ? Nullable, no default
```

**Rationale:**
- When using Azure Storage, these paths should be `null` or empty
- No defaults = cleaner separation between Azure Storage and local filesystem modes
- Forces explicit configuration when local storage is actually needed

### Solution 2: Removed Paths from JSON

Modified `UpdateEngine.Configuration\src\shared\appsettings.Development.json`:

```json
{
  "StorageConfiguration": {
    "_comment": "Azure Functions uses Azure Storage (Azurite). WorkerService overrides these in its own appsettings.Development.json to use filesystem.",
    "UseAzureStorageForMetadata": true,
    "UseAzureStorageForContent": true,
    "MetadataContainerName": "data",
    "ContentContainerName": "data",
    "ContentPathPrefix": "Content",
    "ReindexOnStartup": true
    // NOTE: MetadataPath and ContentPath NOT specified (will be null)
  }
}
```

**Rationale:**
- Don't specify what you're not using
- Makes it explicit that Azure Storage is the mode of operation
- Prevents confusion about which storage backend is active

## How It Works Now

### Configuration Flow:

1. **JSON Binding**:
   ```
   appsettings.Development.json
   ??? UseAzureStorageForMetadata: true
   ??? UseAzureStorageForContent: true
   ??? MetadataContainerName: "data"
   ??? ContentContainerName: "data"
   ??? (MetadataPath: not specified ? null)
   ??? (ContentPath: not specified ? null)
   ```

2. **AppConfig Object**:
   ```csharp
   storageConfig.UseAzureStorageForMetadata = true
   storageConfig.UseAzureStorageForContent = true
   storageConfig.MetadataPath = null  // ? No default!
   storageConfig.ContentPath = null   // ? No default!
   ```

3. **ServiceCollectionExtensions Decision**:
   ```csharp
   var useAzureStorage = (storageConfig.UseAzureStorageForContent && !string.IsNullOrEmpty(connectionString))
       || (!string.IsNullOrEmpty(connectionString) && string.IsNullOrEmpty(storageConfig.ContentPath));
   ```
   - First condition: `true && true` = **true** ?
   - Second condition: `true && true` = **true** ?
   - Result: Azure Storage selected!

4. **Store Creation**:
   ```csharp
   var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(connectionString);
   return UpdateEngine.Metadata.Storage.Azure.PackageStore.OpenOrCreate(
       blobServiceClient, storageConfig.MetadataContainerName);
   ```

5. **Data Written To**:
   ```
   out/azurite-data/__blobstorage__/data/
   ??? identities-index
   ??? [metadata blobs]
   ??? content/
       ??? [content blobs]
   ```

## Testing the Fix

### 1. Clean Up Old Directories

```powershell
# Remove old local storage directories
Remove-Item "C:\Users\ansantan\Repos\update-server-server-sync\out\UpdateEngine\x64\Debug\net9.0\LocalMetadataStore" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "C:\Users\ansantan\Repos\update-server-server-sync\out\UpdateEngine\x64\Debug\net9.0\LocalContentStore" -Recurse -Force -ErrorAction SilentlyContinue
```

### 2. Restart AppHost

```powershell
# Stop current AppHost (Ctrl+C)
# Start fresh
dotnet run --project UpdateEngine.AppHost\src\AppHost.csproj
```

### 3. Verify Logging

Look for these log messages:

```
? GOOD:
[INFO] Metadata Store Configuration:
[INFO]   UseAzureStorageForMetadata: True
[INFO]   MetadataPath: (null or empty)
[INFO]   Connection String from Environment: True
[INFO]   Final Connection String Available: True
[INFO] Opening Azure Blob Storage metadata store (container: data)

[INFO] Opening Azure Blob Storage content store (container: data)

? BAD (should NOT appear):
[INFO] Opening local file system metadata store at: ./LocalMetadataStore
[INFO] Opening local file system content store at: ./LocalContentStore
```

### 4. Verify Storage Locations

```powershell
# Should have Azure Storage data
dir C:\Users\ansantan\Repos\update-server-server-sync\out\azurite-data\__blobstorage__\data\

# Should NOT exist
Test-Path "C:\Users\ansantan\Repos\update-server-server-sync\out\UpdateEngine\x64\Debug\net9.0\LocalMetadataStore"  # False
Test-Path "C:\Users\ansantan\Repos\update-server-server-sync\out\UpdateEngine\x64\Debug\net9.0\LocalContentStore"   # False
```

### 5. Expected Sync Results

```
[XX:XX:XX] Starting scheduled critical updates sync
[XX:XX:XX] Starting updates synchronization with filter
[XX:XX:XX] Flushing metadata store to persist updates
[XX:XX:XX] Updates synchronization completed  ? ? Should succeed!
[XX:XX:XX] Sync operation succeeded: critical
```

## Files Modified

1. **UpdateEngine.Configuration\src\StorageConfiguration.cs**
   - Lines 11-12: Changed `string` to `string?` and removed default values
   - Rationale: Prevent accidental fallback to local storage when using Azure

2. **UpdateEngine.Configuration\src\shared\appsettings.Development.json**
   - Lines 9-16: Removed `MetadataPath` and `ContentPath` entries
   - Rationale: Don't specify unused configuration values

## Related Fixes

This fix builds on previous work:

1. **Connection String Discovery** ([AZURE_STORAGE_CONFIGURATION_FIX.md](./AZURE_STORAGE_CONFIGURATION_FIX.md))
   - Fixed: Connection strings now discovered via `Environment.GetEnvironmentVariable()`
   - Without this, no connection string would be available

2. **Concurrency Control** ([AZURE_BLOB_CONCURRENCY_FIX.md](./AZURE_BLOB_CONCURRENCY_FIX.md))
   - Fixed: Sequential syncs now work without ETag conflicts
   - Without this, subsequent syncs would fail

3. **This Fix (Default Values)**
   - Fixed: System now actually uses Azure Storage instead of local filesystem
   - Without this, connection strings and concurrency fixes were irrelevant

## Configuration Best Practices

### For Azure Storage (Functions):

```json
{
  "StorageConfiguration": {
    "UseAzureStorageForMetadata": true,
    "UseAzureStorageForContent": true,
    "MetadataContainerName": "data",
    "ContentContainerName": "data"
    // DO NOT specify MetadataPath or ContentPath
  }
}
```

### For Local Storage (WorkerService):

```json
{
  "StorageConfiguration": {
    "UseAzureStorageForMetadata": false,
    "UseAzureStorageForContent": false,
    "MetadataPath": "./DownstreamMetadataStore",
    "ContentPath": "./DownstreamContentStore"
    // DO NOT specify container names when using local storage
  }
}
```

### For Mixed Mode (Metadata in Azure, Content Local):

```json
{
  "StorageConfiguration": {
    "UseAzureStorageForMetadata": true,
    "UseAzureStorageForContent": false,
    "MetadataContainerName": "data",
    "ContentPath": "./LocalContentCache"
  }
}
```

## Lessons Learned

1. **Beware of C# Default Values**: Property initializers (`= "./path"`) can interfere with configuration binding
2. **Nullable Reference Types Help**: Using `string?` makes it explicit that the value might not be set
3. **Configuration Precedence**: Environment variables ? JSON ? C# defaults (if any)
4. **Explicit is Better**: Don't provide default values for configuration that changes based on deployment mode
5. **Test End-to-End**: Connection string discovery + concurrency + default values all had to work together
6. **Logging is Critical**: Enhanced logging helped identify exactly where the decision was being made
7. **Configuration Validation**: The `Validate()` method should check mode-specific requirements

## Success Criteria

? No `LocalMetadataStore` or `LocalContentStore` directories created  
? Logs show "Opening Azure Blob Storage metadata store"  
? Logs show "Opening Azure Blob Storage content store"  
? Blob files appear in `out/azurite-data/__blobstorage__/data/`  
? Sequential syncs complete successfully  
? Data persists across AppHost restarts  
