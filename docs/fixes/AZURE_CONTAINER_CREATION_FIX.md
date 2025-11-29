# Azure Blob Storage Container Creation Fix

**Date**: 2025-11-23  
**Status**: ? Complete  
**Impact**: Critical - Blocks Azure Blob Storage usage

## Problem

The Update Engine was failing to start when using Azure Blob Storage for metadata because the "data" container was not being created automatically. Error message:

```
Azure.RequestFailedException: The specified container does not exist.
RequestId:...
Status: 404 (The specified container does not exist.)
```

### Root Cause

In `ServiceCollectionExtensions.cs` (line 159), when registering the `IMetadataStore` service with Azure Blob Storage, the code was calling:

```csharp
return UpdateEngine.Metadata.Storage.Azure.PackageStore.Open(container);
```

The `Open()` method **expects the container to already exist** and throws an exception if it doesn't. This is correct behavior for production scenarios where containers are pre-provisioned, but it doesn't work for development/first-run scenarios.

## Solution

Changed the initialization to use `OpenOrCreate()` instead:

```csharp
return UpdateEngine.Metadata.Storage.Azure.PackageStore.OpenOrCreate(blobServiceClient, storageConfig.MetadataContainerName);
```

### Code Changes

#### UpdateEngine.Core/src/ServiceCollectionExtensions.cs

**Before (line 159):**
```csharp
logger?.LogInformation("Opening Azure Blob Storage metadata store (container: {Container})", storageConfig.MetadataContainerName);
// Azure Blob Storage
var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(connectionString);
var container = blobServiceClient.GetBlobContainerClient(storageConfig.MetadataContainerName);
return UpdateEngine.Metadata.Storage.Azure.PackageStore.Open(container);
```

**After:**
```csharp
logger?.LogInformation("Opening Azure Blob Storage metadata store (container: {Container})", storageConfig.MetadataContainerName);
// Azure Blob Storage - use OpenOrCreate to create container if it doesn't exist
var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(connectionString);
return UpdateEngine.Metadata.Storage.Azure.PackageStore.OpenOrCreate(blobServiceClient, storageConfig.MetadataContainerName);
```

### What `OpenOrCreate()` Does

Looking at `ContainerPackageStore.OpenOrCreate()` in `UpdateEngine.Metadata/src/Storage/AzureBlob/ContainerPackageStore.cs` (line 145):

```csharp
public static ContainerPackageStore OpenOrCreate(BlobServiceClient client, string containerName)
{
    var container = client.GetBlobContainerClient(containerName);
    container.CreateIfNotExists();  // ? Creates container if missing

    return new ContainerPackageStore(container, AzurePackageStoreInitializeMode.ResetOnIndexCorruption);
}
```

The `container.CreateIfNotExists()` call is the key - it safely creates the container if needed, or silently succeeds if it already exists.

## Benefits

1. **First-run support**: Works automatically on first startup without manual container creation
2. **Development friendly**: No need to pre-provision Azure Storage containers during development
3. **Idempotent**: Safe to call repeatedly - won't fail if container already exists
4. **Consistent with local storage**: Local file system storage already uses `OpenOrCreate()` pattern
5. **Production safe**: Container creation is a one-time operation and doesn't impact performance

## Testing

### Expected Behavior After Fix

1. **First run** (container doesn't exist):
   - Service starts
   - Container "data" is created automatically
   - Metadata store initializes successfully
   - ? "Opening Azure Blob Storage metadata store (container: data)" logged

2. **Subsequent runs** (container exists):
   - Service starts
   - Container already exists (no-op)
   - Metadata store initializes successfully
   - ? Same behavior as first run

### Test Scenarios

```bash
# Scenario 1: Start with fresh Azure Storage account
# - No containers exist
# - AppHost starts successfully
# - Container "data" is created

# Scenario 2: Start with existing container
# - Container "data" exists
# - AppHost starts successfully
# - No errors about existing container

# Scenario 3: Verify container was created
az storage container show \
    --name data \
    --connection-string "<connection-string>"

# Should return container metadata
```

### Verification Steps

1. **Delete existing container** (if testing fresh start):
   ```bash
   az storage container delete --name data --connection-string "<connection-string>"
   ```

2. **Start AppHost**:
   ```bash
   cd UpdateEngine.AppHost/src
   dotnet run
   ```

3. **Check logs** for:
   ```
   info: UpdateEngine.Metadata.Storage.IMetadataStore[0]
         Opening Azure Blob Storage metadata store (container: data)
   ```

4. **Verify container exists**:
   ```bash
   az storage container list --connection-string "<connection-string>"
   ```

## Related Issues

- **Pattern inconsistency**: Local storage was already using `OpenOrCreate()`, but Azure storage was using `Open()`
- **Development friction**: Required manual container creation before first run
- **Error clarity**: Azure SDK error message was cryptic for new users

## Configuration

The fix works with all Azure Storage configuration methods:

### Method 1: Aspire Service Discovery
```json
{
  "ConnectionStrings": {
    "MetadataStorageConnection": "UseDevelopmentStorage=true"
  }
}
```

### Method 2: Configuration Section
```json
{
  "StorageConfiguration": {
    "UseAzureStorageForMetadata": true,
    "AzureStorageConnectionString": "UseDevelopmentStorage=true",
    "MetadataContainerName": "data"
  }
}
```

### Method 3: Azurite (Development)
```bash
azurite --silent --location ./azurite-data --debug ./azurite-debug.log
```

## Migration Notes

### Existing Deployments

If you have existing deployments with manually created containers:
- ? **No action required** - `CreateIfNotExists()` is idempotent
- ? **Existing data preserved** - No data loss or migration needed
- ? **No breaking changes** - Container permissions unchanged

### New Deployments

For new deployments:
- ? **No pre-provisioning needed** - Containers created automatically
- ? **Simplified setup** - One less manual step
- ?? **Ensure permissions** - Service account must have container creation permissions

### Container Permissions

The Azure Storage account/service principal needs:
- **Storage Blob Data Contributor** role, OR
- **Storage Account Contributor** role, OR
- Connection string with full permissions

## Alternative Approaches Considered

### 1. Pre-provision containers (rejected)
- ? Adds manual setup step
- ? Doesn't work for development (Azurite)
- ? Inconsistent with local storage behavior

### 2. Initialize on first write (rejected)
- ? Delayed error detection
- ? Requires write operation to trigger
- ? More complex error handling

### 3. Startup health check (rejected)
- ? Still requires manual creation
- ? Only detects problem, doesn't fix it
- ? Poor developer experience

### 4. OpenOrCreate() (? **CHOSEN**)
- ? Automatic and transparent
- ? Consistent with file system pattern
- ? Works for all scenarios
- ? Simple implementation

## Files Modified

- `UpdateEngine.Core/src/ServiceCollectionExtensions.cs` - Changed `PackageStore.Open()` to `PackageStore.OpenOrCreate()`

## References

- Azure SDK: `BlobContainerClient.CreateIfNotExists()` - [Docs](https://learn.microsoft.com/en-us/dotnet/api/azure.storage.blobs.blobcontainerclient.createifnotexists)
- `ContainerPackageStore.OpenOrCreate()` - `UpdateEngine.Metadata/src/Storage/AzureBlob/ContainerPackageStore.cs:145`
- Configuration: `docs/guides/STORAGE_GUIDE.md`

---

**Author**: GitHub Copilot  
**Reviewed**: Pending  
**Last Updated**: 2025-11-23
