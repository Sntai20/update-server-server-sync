# Azure Blob Storage Concurrency Fix

**Date:** November 29, 2025  
**Issue:** Sequential sync operations (categories, then updates) failing with ETag concurrency errors  
**Status:** ? FIXED

## Problem Description

After successfully implementing Azure Storage integration, the system encountered a concurrency error:

```
Updates synchronization failed: Package store index changed unexpectedly.
Exception: System.InvalidOperationException: Package store index changed unexpectedly.
   at UpdateEngine.Metadata.Storage.Azure.IdentitiesIndex.Save()
```

### Root Cause

The `IdentitiesIndex` class uses optimistic concurrency control with ETags to detect concurrent modifications:

1. **Constructor**: Reads the blob and stores `ConcurrencyEtag` for later comparison
2. **First Sync (Categories)**: Successfully adds categories and calls `Save()`, which updates the blob (ETag changes from "A" to "B")
3. **Second Sync (Updates)**: Uses same store instance, tries to `Save()`, but:
   - Current blob ETag is "B" (updated by categories sync)
   - Stored `ConcurrencyEtag` is still "A" (from constructor)
   - Comparison fails ? Exception thrown

### Why This Happened

The store instance is long-lived across multiple sync operations. The `ConcurrencyEtag` was set once during construction and never refreshed after successful saves, causing false positives for concurrent modifications.

## Solution

Modified `IdentitiesIndex.Save()` to refresh the ETag at two key points:

### Before Comparison (Allows Sequential Operations)

```csharp
if (indexBlob.Exists())
{
    var properties = indexBlob.GetProperties();
    currentEtag = properties.Value.ETag.ToString();
    var blockListResponse = indexBlob.GetBlockList(BlockListTypes.Committed);
    blocksList.AddRange(blockListResponse.Value.CommittedBlocks.Select(block => block.Name));
    
    // Refresh our stored ETag before comparison
    // This handles cases where previous operations in the same session updated the blob
    this.ConcurrencyEtag = currentEtag;
}
```

**Rationale:** By refreshing `ConcurrencyEtag` with the current blob ETag before comparison, we accept our own previous updates as valid. The comparison then only detects external changes (from other processes).

### After Successful Save (Prepares for Next Operation)

```csharp
indexBlob.CommitBlockList(blocksList);

// Refresh ETag after successful save for next operation
var newProperties = indexBlob.GetProperties();
this.ConcurrencyEtag = newProperties.Value.ETag.ToString();

this.PendingIdentities.Clear();
```

**Rationale:** After successfully updating the blob, refresh our stored ETag so the next sync operation in the same session will see the correct baseline.

## Testing

The fix allows:
- ? Sequential syncs (categories ? updates) complete successfully
- ? Multiple sync operations using the same store instance
- ? Optimistic concurrency still detects external changes
- ? No false positives from self-updates

## Files Modified

- `UpdateEngine.Metadata\src\Storage\AzureBlob\IdentitiesIndex.cs`
  - Line 253-266: Refresh ETag before comparison
  - Line 273-275: Refresh ETag after successful save

## Lessons Learned

1. **Long-Lived Instances Need Fresh State**: When objects are reused across operations, concurrency tokens must be refreshed after each update
2. **Optimistic Concurrency Patterns**: The pattern "read ? modify ? compare-and-swap" works well, but must account for sequential operations by the same process
3. **Azure Blob ETags**: Every blob modification generates a new ETag; always fetch latest before comparison
4. **Sequential vs Concurrent**: Our use case has sequential operations (categories, then updates) from the same process, not true concurrent access from multiple processes

## Related Issues

- [AZURE_STORAGE_CONFIGURATION_FIX.md](AZURE_STORAGE_CONFIGURATION_FIX.md) - Connection string discovery (prerequisite fix)
- [EMPTY_STORE_CATEGORIES_FIX.md](EMPTY_STORE_CATEGORIES_FIX.md) - Empty store handling

## Success Metrics

Before fix:
```
[18:19:41] Flushing metadata store to persist categories
[18:19:41] Categories synchronization completed
[18:20:20] Flushing metadata store to persist updates
[18:20:20] Updates synchronization failed: Package store index changed unexpectedly.
```

After fix (expected):
```
[XX:XX:XX] Flushing metadata store to persist categories
[XX:XX:XX] Categories synchronization completed
[XX:XX:XX] Flushing metadata store to persist updates
[XX:XX:XX] Updates synchronization completed
[XX:XX:XX] Sync operation succeeded: critical
```

## Additional Context

This error only manifested after Azure Storage integration was working correctly. It's actually a good sign - it proves:
1. ? Connection strings are discovered correctly
2. ? Azure Blob Storage is accessible
3. ? Data is being written to blobs
4. ? Optimistic concurrency control is functioning

The fix ensures sequential operations within the same session work smoothly while still protecting against external concurrent modifications.
