# Fast Development Schedule Strategy

**Purpose**: Aggressive schedules for rapid development feedback while preventing concurrent sync conflicts

## Development Schedule Design

### Core Principle: **Time-Staggered Execution**

Instead of simple intervals that can overlap, we use **staggered minute markers** to ensure sync operations never collide:

```
Timeline (1 hour view):
:00 - Critical Sync starts
:05 - Comprehensive Sync starts
:08 - Content Sync starts
:10 - Critical Sync starts
:15 - Comprehensive Sync starts
:18 - Content Sync starts
... repeats every 10 minutes
```

### Schedule Configuration

```json
{
  "SyncCriticalSchedule": "0 0,10,20,30,40,50 * * * *",      // Every 10 min at :00, :10, :20, :30, :40, :50
  "SyncComprehensiveSchedule": "0 5,15,25,35,45,55 * * * *", // Every 10 min at :05, :15, :25, :35, :45, :55
  "SyncContentSchedule": "0 8,18,28,38,48,58 * * * *",       // Every 10 min at :08, :18, :28, :38, :48, :58
  "ScheduledHealthCheckSchedule": "0 */5 * * * *",            // Every 5 min (lightweight, can overlap)
  "MaintenanceSchedule": "0 0 */1 * * *",                     // Hourly at top of hour
  "AnomalyDetectionSchedule": "0 */10 * * * *"                // Every 10 min at :00, :10, :20, etc.
}
```

### Time Offset Strategy

| Function | Frequency | Offset | Typical Duration | Conflicts |
|----------|-----------|--------|------------------|-----------|
| **Critical Sync** | Every 10 min | :00 mark | 2-5 min | None (with locking) |
| **Comprehensive Sync** | Every 10 min | :05 mark | 3-8 min | None (5 min buffer) |
| **Content Sync** | Every 10 min | :08 mark | 5-15 min | None (with locking) |
| **Health Check** | Every 5 min | Any | <30 sec | None (lightweight) |
| **Anomaly Detection** | Every 10 min | :00 mark | <1 min | Can overlap with Critical (intentional) |
| **Maintenance** | Every hour | :00 mark | 5-10 min | Rare overlap |

### Why This Works

#### 1. **Time Separation**
```
12:00:00 - Critical Sync starts
12:05:00 - Comprehensive Sync starts (Critical likely done or wrapping up)
12:08:00 - Content Sync starts (Critical done, Comprehensive might overlap)
12:10:00 - Critical Sync starts again
```

**Key**: Each major sync type starts 5+ minutes apart, giving previous operations time to complete.

#### 2. **Concurrent Protection via Locking**
Even if schedules overlap due to long-running operations, the `SemaphoreSlim` locks prevent conflicts:

```csharp
// In SyncService.cs
public async Task SyncContentAsync(...)
{
    if (!await this.contentLock.WaitAsync(0, cancellationToken))
    {
        this.logger.LogWarning("Content sync skipped - another content download is in progress");
        return;  // Gracefully skip
    }
    try {
        // Perform sync
    }
    finally {
        this.contentLock.Release();
    }
}
```

#### 3. **File-Level Protection**
ContentDownloader improvements handle remaining edge cases:
- Checks if file is already complete
- Uses `FileShare.Read` for concurrent access
- Gracefully skips locked files

### Expected Behavior

#### Normal Operation (Logs)
```
[12:00:00] Critical Sync starts
[12:03:45] Critical Sync completes
[12:05:00] Comprehensive Sync starts
[12:10:00] Critical Sync starts
[12:12:30] Comprehensive Sync completes
[12:15:00] Comprehensive Sync starts
```

#### When Operations Overlap (Protected by Locking)
```
[12:00:00] Critical Sync starts
[12:05:00] Comprehensive Sync starts
[12:08:00] Content Sync: "Content sync skipped - another content download is in progress"
[12:10:00] Critical Sync starts
[12:11:00] Critical Sync completes
[12:15:00] Comprehensive Sync completes
[12:18:00] Content Sync starts (now succeeds)
```

### Performance Characteristics

**Frequency Comparison**:

| Scenario | Old Dev | New Dev | Production |
|----------|---------|---------|------------|
| Critical Sync | Every 3 min (20/hr) | Every 10 min (6/hr) | Every 4 hours (0.25/hr) |
| Comprehensive | Every 6 min (10/hr) | Every 10 min (6/hr) | Daily (0.04/hr) |
| Content Sync | Every 9 min (6.7/hr) | Every 10 min (6/hr) | Daily (0.04/hr) |

**Benefits**:
- ? **Fast feedback**: 10-minute cycle vs 30-minute+ in conservative schedule
- ? **No conflicts**: Time offsets + locking prevent overlaps
- ? **Predictable**: Runs at consistent minute markers (easier to test)
- ? **Observable**: Clear patterns in logs for debugging

### Testing Strategy

#### Test 1: Cold Start
```bash
# Start Functions at arbitrary time (e.g., 12:03)
# Observe which timers fire first
Expected: Next scheduled times align with minute markers
  - 12:05 - Comprehensive
  - 12:08 - Content  
  - 12:10 - Critical
```

#### Test 2: Overlap Handling
```bash
# Manually trigger slow sync during scheduled time
POST http://localhost:7071/api/UniversalSync
Body: { "syncType": "comprehensive", "syncContent": true }

# Wait for scheduled timer to fire
Expected Log: "Sync operation skipped - another sync in progress"
```

#### Test 3: Long-Running Operations
```bash
# Monitor a full hour of operation
# Count sync attempts vs completions

Expected:
  - 6 Critical attempts, 5-6 completions
  - 6 Comprehensive attempts, 5-6 completions
  - 6 Content attempts, 4-5 completions (may take longer)
  - Skipped syncs logged with warnings
```

### Monitoring

#### Success Metrics
- **Completion Rate**: 80%+ of scheduled syncs complete
- **Skip Rate**: <20% of syncs skipped due to locking
- **Error Rate**: 0% file locking errors (IOExceptions)
- **Duration**: Most syncs complete in <5 minutes

#### Warning Signs
```
?? "Sync skipped" messages every cycle
   ? Operations taking too long, consider 15-min intervals

?? IOException: File is being used by another process
   ? Locking isn't working, check SyncService changes

?? Network timeouts on large files
   ? Need retry logic or longer HttpClient timeout
```

### Adjustment Guidelines

#### If Syncs Complete Too Quickly (< 2 min average)
**Make more aggressive**:
```json
"SyncCriticalSchedule": "0 */5 * * * *",  // Every 5 minutes
"SyncComprehensiveSchedule": "0 */7 * * * *",
"SyncContentSchedule": "0 */9 * * * *"
```

#### If Syncs Take Too Long (> 8 min average)
**Make more conservative**:
```json
"SyncCriticalSchedule": "0 */15 * * * *",  // Every 15 minutes
"SyncComprehensiveSchedule": "0 */20 * * * *",
"SyncContentSchedule": "0 */25 * * * *"
```

#### If Seeing Frequent Skips (> 30%)
**Increase stagger time**:
```json
"SyncCriticalSchedule": "0 0,15,30,45 * * * *",      // Every 15 min at :00
"SyncComprehensiveSchedule": "0 5,20,35,50 * * * *", // Every 15 min at :05
"SyncContentSchedule": "0 10,25,40,55 * * * *"       // Every 15 min at :10
```

### CRON Expression Quick Reference

**Format**: `second minute hour day month dayOfWeek`

Common patterns:
```
"0 */5 * * * *"          - Every 5 minutes
"0 0,15,30,45 * * * *"   - Every 15 minutes at :00, :15, :30, :45
"0 5,15,25,35,45,55 * * * *" - Every 10 minutes starting at :05
"0 0 * * * *"            - Every hour at top of hour
"0 0 */6 * * *"          - Every 6 hours
```

### Rollback Plan

If fast schedules cause issues:

**Option 1: Conservative Development** (30+ min intervals)
```json
"SyncCriticalSchedule": "0 */30 * * * *",
"SyncComprehensiveSchedule": "0 0 */2 * * *",
"SyncContentSchedule": "0 0 */3 * * *"
```

**Option 2: Manual Triggering Only**
```json
"EnableScheduledSync": false  // Disable all timer triggers
```
Then trigger syncs manually via HTTP:
```bash
curl -X POST http://localhost:7071/api/UniversalSync \
  -H "Content-Type: application/json" \
  -d '{"syncType":"critical"}'
```

### Related Documentation

- [Concurrent Sync Fixes](./2025-11-23-concurrent-sync-and-file-locking-fixes.md) - Locking implementation
- [Configuration Guide](../CONFIGURATION_GUIDE.md) - Full configuration reference
- [Configuration Unification Proposal](../proposals/2025-11-23-configuration-unification.md) - Long-term strategy

---

**Current Status**: Fast development schedule with 10-minute cycles and time-staggered execution  
**Last Updated**: 2025-11-23  
**Recommended For**: Active development, rapid iteration, frequent testing
