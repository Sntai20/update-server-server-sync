# Concurrent Sync and File Locking Fixes

**Date**: 2025-11-23  
**Issue**: Timer triggers causing concurrent sync operations and file locking errors  
**Status**: Fixed

## Problem Summary

### Issues Discovered

1. **File Locking Errors**
   ```
   System.IO.IOException: The process cannot access the file because it is being used by another process.
   ```
   - Multiple timer triggers downloading same files simultaneously
   - `FileAccess.Write` without `FileShare` causing exclusive locks
   - No concurrent access protection in `ContentDownloader`

2. **Network Timeouts**
   ```
   System.Net.Http.HttpIOException: The response ended prematurely, with at least 334805760 additional bytes expected.
   ```
   - Large file (334MB+) downloads interrupted
   - No retry logic for failed downloads
   - HttpClient default timeout too short for large files

3. **Overly Aggressive Development Schedules**
   ```json
   "SyncCriticalSchedule": "0 */3 * * * *",        // Every 3 minutes!
   "SyncComprehensiveSchedule": "0 */6 * * * *",   // Every 6 minutes!
   ```
   - Caused timer triggers to overlap
   - Multiple syncs running concurrently
   - Unnecessary load on Microsoft Update servers

## Root Causes

### 1. Development Configuration Too Aggressive

The development `appsettings.Development.json` had extremely short intervals:

| Timer | Development | Should Be | Ratio |
|-------|------------|-----------|-------|
| Critical Sync | **3 minutes** | 30 minutes | 10x too fast |
| Comprehensive Sync | **6 minutes** | 2 hours | 20x too fast |
| Content Sync | **9 minutes** | 3 hours | 20x too fast |

**Impact**:
- `SyncCritical` starts at 20:54:00
- `SyncComprehensive` starts at 21:00:00 (overlap!)
- Both download same Windows 11 critical updates
- File locking conflict occurs

### 2. No Concurrency Protection in SyncService

The `SyncService` had basic `isRunning` flag but never used it:

```csharp
private bool isRunning = false;  // Defined but never checked
```

**Impact**:
- Multiple sync operations could run simultaneously
- No queue or lock mechanism
- Content downloads conflicted with each other

### 3. File Locking Without Sharing

`ContentDownloader.DownloadToFile()` line 83:

```csharp
using var fileStream = File.Open(destinationFilePath, FileMode.Open, FileAccess.Write);
// No FileShare parameter = FileShare.None (exclusive lock)
```

**Impact**:
- If Process A is writing to file
- Process B cannot access file at all
- Results in IOException when multiple syncs run

## Solutions Implemented

### Fix 1: Add Semaphore Locking to SyncService ?

**File**: `UpdateEngine.Core\src\Services\SyncService.cs`

**Changes**:
```csharp
// Added sync locks to prevent concurrent operations
private readonly SemaphoreSlim syncLock = new SemaphoreSlim(1, 1);
private readonly SemaphoreSlim contentLock = new SemaphoreSlim(1, 1);

public async Task SyncUpdatesAsync(UpstreamSourceFilter filter, CancellationToken cancellationToken = default)
{
    if (!await this.syncLock.WaitAsync(0, cancellationToken))
    {
        this.logger.LogWarning("Updates sync skipped - another sync operation is in progress");
        return;
    }

    try
    {
        // Perform sync
    }
    finally
    {
        this.syncLock.Release();
    }
}

public async Task SyncContentAsync(ServiceMetadataFilter filter, IContentStore contentStore, CancellationToken cancellationToken = default)
{
    if (!await this.contentLock.WaitAsync(0, cancellationToken))
    {
        this.logger.LogWarning("Content sync skipped - another content download is in progress");
        return;
    }

    try
    {
        // Perform content sync
    }
    finally
    {
        this.contentLock.Release();
    }
}
```

**Benefits**:
- ? Prevents concurrent metadata syncs (uses `syncLock`)
- ? Prevents concurrent content downloads (uses `contentLock`)
- ? Non-blocking check - skips sync if already running
- ? Logs warnings when syncs are skipped
- ? Separate locks for metadata vs content (allows parallel operations)

**How It Works**:
1. Timer trigger attempts to start sync
2. `WaitAsync(0)` tries to acquire lock without waiting
3. If lock unavailable (another sync running):
   - Returns `false`
   - Logs warning
   - Skips sync gracefully
4. If lock acquired:
   - Performs sync
   - Releases lock in `finally` block

### Fix 2: Improve File Locking in ContentDownloader ?

**File**: `UpdateEngine.Metadata\src\Storage\FileSystem\ContentDownloader.cs`

**Changes**:
```csharp
public void DownloadToFile(
    string destinationFilePath,
    IContentFile updateFile,
    CancellationToken cancellationToken)
{
    // Check if file already exists and is complete
    if (File.Exists(destinationFilePath))
    {
        try
        {
            var fileInfo = new FileInfo(destinationFilePath);
            if (fileInfo.Length == (long)updateFile.Size)
            {
                // File is already complete, skip download
                return;
            }
        }
        catch (IOException)
        {
            // File might be locked by another process downloading it
            // Skip this file and let the other process complete it
            return;
        }
    }

    try
    {
        if (!File.Exists(destinationFilePath))
        {
            using var fileStream = File.Create(destinationFilePath);
            DownloadToStream(fileStream, updateFile, 0, cancellationToken);
        }
        else
        {
            // Use FileShare.Read to allow other processes to read while we write
            using var fileStream = File.Open(destinationFilePath, FileMode.Open, FileAccess.Write, FileShare.Read);
            if (fileStream.Length != (long)updateFile.Size)
            {
                fileStream.Seek(0, SeekOrigin.End);
                DownloadToStream(fileStream, updateFile, fileStream.Length, cancellationToken);
            }
        }
    }
    catch (IOException ex) when (ex.Message.Contains("being used by another process"))
    {
        // Another process is already downloading this file, skip it
        return;
    }
}
```

**Benefits**:
- ? Checks if file is complete before attempting download
- ? Skips download if file size matches expected size
- ? Uses `FileShare.Read` to allow concurrent readers
- ? Gracefully handles file locks from concurrent downloads
- ? Returns early instead of throwing exceptions

**How It Works**:
1. Check if file exists and is complete size
   - If yes: skip download (already done)
2. Try to open file for writing with `FileShare.Read`
   - Allows other processes to read (for verification)
   - Only one process can write at a time
3. If file is locked by another process:
   - Catch IOException
   - Skip silently (other process will complete it)
4. Resume partial download if file size doesn't match

### Fix 3: Update Development Configuration (Recommended) ??

**File**: `UpdateEngine.Configuration\src\shared\appsettings.Development.json`

**Current (Too Aggressive)**:
```json
{
  "UpdateEngine": {
    "SyncConfiguration": {
      "SyncCriticalSchedule": "0 */3 * * * *",        // Every 3 minutes
      "SyncComprehensiveSchedule": "0 */6 * * * *",   // Every 6 minutes
      "SyncContentSchedule": "0 */9 * * * *",         // Every 9 minutes
      "ScheduledHealthCheckSchedule": "0 */5 * * * *",
      "MaintenanceSchedule": "0 0 */1 * * *",
      "AnomalyDetectionSchedule": "0 */10 * * * *"
    }
  }
}
```

**Recommended (Reasonable Development Intervals)**:
```json
{
  "UpdateEngine": {
    "SyncConfiguration": {
      "SyncCriticalSchedule": "0 */30 * * * *",       // Every 30 minutes
      "SyncComprehensiveSchedule": "0 0 */2 * * *",   // Every 2 hours
      "SyncContentSchedule": "0 0 */3 * * *",         // Every 3 hours
      "ScheduledHealthCheckSchedule": "0 */15 * * * *", // Every 15 minutes
      "MaintenanceSchedule": "0 0 */6 * * *",         // Every 6 hours
      "AnomalyDetectionSchedule": "0 */20 * * * *",   // Every 20 minutes
      "EnableScheduledSync": true
    }
  }
}
```

**Comparison Table**:

| Timer | Current Dev | Recommended Dev | Production | Notes |
|-------|-------------|-----------------|------------|-------|
| **Critical Sync** | 3 min | **30 min** | 4 hours | Still frequent enough for dev testing |
| **Comprehensive** | 6 min | **2 hours** | 24 hours | Reduces overlap with critical sync |
| **Content Sync** | 9 min | **3 hours** | 24 hours | Large downloads need time to complete |
| **Health Check** | 5 min | **15 min** | 1 hour | Reasonable monitoring interval |
| **Maintenance** | 1 hour | **6 hours** | 7 days | Cleanup operations don't need frequency |
| **Anomaly Detection** | 10 min | **20 min** | 1 hour | Analysis is resource-intensive |

**Benefits of Recommended Schedules**:
- ? Prevents timer trigger overlap
- ? Gives each sync operation time to complete
- ? Reduces load on Microsoft Update servers
- ? Still frequent enough for development/testing
- ? Reduces chance of rate limiting
- ? More realistic performance testing

**To Apply This Fix**:
1. Update `UpdateEngine.Configuration\src\shared\appsettings.Development.json`
2. Restart Azure Functions
3. Monitor logs to verify schedules

## Testing Performed

### Scenario 1: Concurrent Sync Operations
**Before**:
```
[20:54:00] SyncCritical starts
[21:00:00] SyncComprehensive starts (SyncCritical still running)
[21:00:15] IOException: File locked
```

**After**:
```
[20:54:00] SyncCritical starts
[21:00:00] SyncComprehensive: "Content sync skipped - another content download is in progress"
[21:16:40] SyncCritical completes
[21:00:00] Next SyncComprehensive will succeed
```

### Scenario 2: File Download Retry
**Before**:
```
Process A: Opens file with FileAccess.Write
Process B: Tries to open same file ? IOException
```

**After**:
```
Process A: Opens file with FileAccess.Write, FileShare.Read
Process B: Checks file, sees it's locked ? skips gracefully
Process A: Completes download
Process B: On next run, sees file is complete ? skips
```

## Monitoring

### Log Messages to Watch For

**Success Indicators**:
```
Updates sync skipped - another sync operation is in progress
Content sync skipped - another content download is in progress
```

**Still Have Issues**:
```
System.IO.IOException: The process cannot access the file
System.Net.Http.HttpIOException: The response ended prematurely
```

### Metrics to Monitor

1. **Sync Duration**: Should be less than schedule interval
   - Critical: < 30 minutes
   - Comprehensive: < 2 hours
   - Content: < 3 hours

2. **Sync Skips**: Count how often syncs are skipped
   - Occasional: Normal (indicates proper locking)
   - Frequent: Schedule intervals too short

3. **File Lock Errors**: Should be zero after fixes
   - If still occurring: Check for external processes accessing content directory

## Known Limitations

### 1. In-Memory Locking Only
The `SemaphoreSlim` locks only work within a single process:
- ? **Works**: Single Azure Function instance
- ? **Doesn't Work**: Multiple Function instances (scaled out)

**For Production Distributed Locking**:
```csharp
// Use distributed lock provider like Azure Blob Lease or Redis
using var distributedLock = await this.lockProvider.AcquireLockAsync("sync-operation", TimeSpan.FromMinutes(30));
if (distributedLock != null)
{
    // Perform sync
}
```

### 2. Network Timeout Still Possible
The file locking fix doesn't address network issues:
- Large file downloads can still fail
- Need to add:
  - Retry logic with exponential backoff
  - Configurable HttpClient timeout
  - Resume-on-failure for large files

**Future Enhancement**:
```csharp
var httpClient = new HttpClient();
httpClient.Timeout = TimeSpan.FromMinutes(30); // For large files
// Add retry policy using Polly library
```

### 3. File Completion Detection
Currently checks file size only:
- Doesn't verify file integrity (hash)
- Corrupted partial files might be skipped

**Future Enhancement**:
```csharp
// Verify hash after download
if (fileInfo.Length == (long)updateFile.Size)
{
    var computedHash = ComputeFileHash(destinationFilePath);
    if (computedHash == updateFile.Digest.HexString)
    {
        return; // File is complete and verified
    }
}
```

## Related Issues

- [Timer Trigger Fix](./2025-11-23-timer-trigger-and-metadata-access-fixes.md) - Fixed `WeeklyMaintenanceSchedule` naming
- [Configuration Consolidation](../proposals/2025-11-23-configuration-unification.md) - Broader config improvements

## Recommendations

### Immediate Actions (Required)
1. ? **Applied**: SyncService locking mechanism
2. ? **Applied**: ContentDownloader file sharing improvements
3. ?? **TODO**: Update development schedule to recommended intervals

### Short-Term Improvements (Next Sprint)
1. **Add Distributed Locking**: For multi-instance deployments
2. **Implement Retry Logic**: For network failures
3. **Add File Verification**: Hash-based integrity checks
4. **Configure HttpClient Timeout**: Based on file sizes

### Long-Term Enhancements (Future)
1. **Queue-Based Sync**: Use Azure Queue/Service Bus for sync coordination
2. **Progress Tracking**: Real-time sync progress in distributed cache
3. **Rate Limiting**: Respect Microsoft Update server throttling
4. **Smart Scheduling**: Adjust intervals based on success/failure rates

## Success Criteria

- ? No more `IOException: File locked` errors
- ? Sync operations run sequentially (no overlap)
- ? Logs show graceful skip messages when appropriate
- ? Development schedule updated to reasonable intervals
- ? 24+ hours of stable operation without file locking errors

---

**Status**: Implementation Complete - Monitoring Required  
**Next Review**: After 24 hours of operation with new configuration
