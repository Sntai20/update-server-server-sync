# AnomalyDetection Development Schedule

**Purpose**: Optimized schedule for rapid AnomalyDetection feature development  
**Focus**: Quick initial sync, then frequent anomaly analysis runs

## Development Philosophy

When developing AnomalyDetection features, you need:
1. ? **Initial data quickly** - Get metadata and content synced ASAP on startup
2. ? **Fresh data periodically** - Update metadata every 15-20 minutes
3. ? **Frequent anomaly runs** - Test detection logic every 5 minutes
4. ? **Avoid sync conflicts** - Staggered schedules prevent file locking

## Schedule Overview

```
Timeline (30-minute window):
:00 - Critical Sync (metadata) ?
:02 - Comprehensive Sync (categories + updates) ??
:05 - Content Sync (files) + ANOMALY DETECTION ??
:10 - ANOMALY DETECTION ??
:15 - Critical Sync ? + ANOMALY DETECTION ??
:17 - Comprehensive Sync ??
:20 - Content Sync ?? + ANOMALY DETECTION ??
:25 - ANOMALY DETECTION ??
:30 - Critical Sync ? + ANOMALY DETECTION ??
```

## Configuration Details

```json
{
  "SyncConfiguration": {
    "SyncCriticalSchedule": "0 */15 * * * *",           // Every 15 min - Fast metadata updates
    "SyncComprehensiveSchedule": "0 2,17,32,47 * * * *", // Every 15 min offset +2 min
    "SyncContentSchedule": "0 5,20,35,50 * * * *",      // Every 15 min offset +5 min
    "ScheduledHealthCheckSchedule": "0 */10 * * * *",   // Every 10 min - System health
    "MaintenanceSchedule": "0 0 */2 * * *",             // Every 2 hours - Low priority
    "AnomalyDetectionSchedule": "0 */5 * * * *",        // Every 5 min - YOUR FOCUS! ??
    "EnableScheduledSync": true
  }
}
```

### Key Features

| Timer | Interval | Purpose for AnomalyDetection Dev |
|-------|----------|----------------------------------|
| **AnomalyDetection** ?? | **Every 5 min** | Rapid iteration on detection algorithms |
| **Critical Sync** | Every 15 min | Keep metadata fresh with security updates |
| **Comprehensive** | Every 15 min (+2) | Full category/update refresh |
| **Content Sync** | Every 15 min (+5) | Download actual update files for analysis |
| **Health Check** | Every 10 min | Monitor system stability |
| **Maintenance** | Every 2 hours | Background cleanup (won't interfere) |

## Why This Works for AnomalyDetection Development

### 1. **Immediate Feedback Loop** ?
```
:00:00 - Start Functions
:00:15 - First Critical Sync completes (metadata available)
:00:17 - Comprehensive Sync completes (categories ready)
:00:20 - Content Sync starts (files downloading)
:00:25 - First AnomalyDetection run (can analyze metadata)
:05:00 - Next AnomalyDetection run
:10:00 - Another AnomalyDetection run
... every 5 minutes
```

**Result**: Within 25 minutes of startup, you have:
- ? Metadata synced
- ? Categories available
- ? Content downloading
- ? AnomalyDetection running every 5 min

### 2. **Frequent AnomalyDetection Execution**
```csharp
// In AnomalyDetectionService.cs
public double Score(SoftwareUpdate update)
{
    // Your detection logic here runs every 5 minutes!
    // Fast iteration on:
    // - Risk scoring algorithms
    // - Pattern recognition
    // - Anomaly thresholds
    // - Feature extraction
}
```

**Benefits**:
- Change code ? Wait 5 min ? See results
- Test different scoring algorithms quickly
- Observe behavior across multiple update batches
- Debug detection issues in real-time

### 3. **Staggered Sync Schedule Prevents Conflicts**
```
:00 - Critical Sync starts
:02 - Comprehensive Sync starts (Critical wrapping up)
:05 - Content Sync starts (Others likely done) + AnomalyDetection runs
:10 - AnomalyDetection runs (no syncs active)
:15 - Critical Sync + AnomalyDetection (can overlap safely)
```

**No conflicts because**:
- 2-5 minute offsets between sync types
- Semaphore locks prevent concurrent content downloads
- AnomalyDetection only reads metadata (no write conflicts)

### 4. **Rich Data for Analysis**
With 15-minute sync cycles:
- **6 metadata refreshes per hour** - Fresh update data
- **4 content sync runs per hour** - New files to analyze
- **12 anomaly detection runs per hour** - Lots of test data

## Development Workflow

### Initial Setup (First 30 Minutes)
```bash
# 1. Start Azure Functions
dotnet run --project UpdateEngine.AppHost/src/AppHost.csproj

# 2. Monitor startup syncs
# Watch for:
[00:00] Critical Sync starts
[00:15] Critical Sync completes (654 updates)
[00:17] Comprehensive Sync completes (4862 updates)
[00:25] Anomaly Detection runs (analyzes 50 updates)

# 3. First AnomalyDetection logs should appear at :25
```

### Iterative Development Cycle
```bash
# 1. Edit AnomalyDetectionService.cs
# 2. Hot reload or restart Functions
# 3. Wait for next :05, :10, :15, :20, :25, :30, etc.
# 4. Review logs for detection results
# 5. Repeat!
```

### Testing Scenarios

#### Scenario 1: Test New Scoring Algorithm
```csharp
// Edit AnomalyDetectionService.cs
public double Score(SoftwareUpdate update)
{
    var score = 0.0;
    
    // NEW: Check for suspicious file sizes
    if (update.Files?.Any(f => f.Size > 1_000_000_000) == true)
    {
        score += 0.3;  // Large files are suspicious
    }
    
    // Test other heuristics...
    return score;
}

// Wait 5 minutes
// Check logs:
// [12:05:00] Anomaly detected: Update XYZ (Score: 0.85)
// [12:05:00] - Large file detected: 1.2GB
```

#### Scenario 2: Test Pattern Recognition
```csharp
// NEW: Detect unusual update patterns
if (update.Title?.Contains("KB") == false)
{
    score += 0.2;  // Non-KB updates are unusual
}

// Wait 5 minutes ? See results
// Iterate quickly!
```

#### Scenario 3: Test Threshold Tuning
```csharp
// Experiment with different thresholds
var isAnomaly = score > 0.7;  // Try 0.5, 0.7, 0.9
if (isAnomaly)
{
    this.logger.LogWarning("ANOMALY: {Title} (Score: {Score:F2})", 
        update.Title, score);
}

// Every 5 minutes you get feedback on threshold effectiveness
```

## Monitoring Your Development

### Success Indicators

**Expected Log Pattern (every 5 minutes)**:
```
[12:00:00] Scheduled anomaly detection triggered
[12:00:01] Analyzing 50 software updates for anomalies
[12:00:02] POST-SYNC ANOMALY DETECTED: KB5001234 (Score: 0.82)
[12:00:03] Anomaly summary: 2 high-risk, 5 medium-risk
[12:05:00] Scheduled anomaly detection triggered
[12:05:01] Analyzing 50 software updates for anomalies
...
```

**What to Watch For**:
```bash
# Good indicators:
? "Scheduled anomaly detection triggered" every 5 minutes
? "Analyzing X software updates" with X > 0
? Detection results logged with scores
? No "Metadata store not available" errors

# Problems:
? "Anomaly detection is disabled" - Check FeatureFlags
? "No software updates found" - Wait for sync to complete
? "Metadata store not available" - Check DI registration
```

### Debugging Tips

#### If AnomalyDetection Isn't Running
```bash
# Check feature flag
"EnableAnomalyDetection": true  # Must be true

# Check logs for trigger
[12:00:00] Executing 'Functions.ScheduledAnomalyDetection'

# If not firing, check CRON expression
"AnomalyDetectionSchedule": "0 */5 * * * *"  # Every 5 minutes
```

#### If No Updates to Analyze
```bash
# Wait for initial sync
[00:15] Critical Sync completes ? Should have ~600+ updates
[00:25] First AnomalyDetection run ? Can analyze metadata

# If still no updates:
# 1. Check metadata store health
GET http://localhost:7071/api/UniversalHealth?scope=store

# 2. Verify sync completed successfully
# Look for "Updates synchronization completed"
```

#### If Detection Logic Not Working
```csharp
// Add detailed logging in AnomalyDetectionService.cs
this.logger.LogDebug("Evaluating {UpdateId}: {Title}", 
    update.Id.ID, update.Title);
this.logger.LogDebug("File count: {FileCount}", 
    update.Files?.Count() ?? 0);
this.logger.LogDebug("Calculated score: {Score:F2}", score);

// Set logging to Debug in appsettings.Development.json
"UpdateEngine.Core.Services.AnomalyDetectionService": "Debug"
```

## Advanced Configuration

### If You Need Even Faster Iteration

**Option 1: Aggressive AnomalyDetection** (every 2 minutes)
```json
"AnomalyDetectionSchedule": "0 */2 * * * *"
```

**Option 2: Immediate After Sync**
```json
"AnomalyDetectionSchedule": "0 16,18,21,23,36,38,51,53 * * * *"
// Runs 1 minute after each sync type completes
```

### If You Need More Updates to Analyze

**Increase MaxUpdateCount**:
```json
"MaxUpdateCount": 50  // Default is 5, increase for more data
```

**Broaden Classification Filters**:
```json
"SupportedCategories": [
  "Security Updates",
  "Critical Updates", 
  "Definition Updates",
  "Updates",           // ADD: More categories
  "Update Rollups"     // ADD: Even more data
]
```

### If Syncs Are Too Slow

**Option: Reduce Sync Frequency, Increase Anomaly Frequency**
```json
{
  "SyncCriticalSchedule": "0 */20 * * * *",        // Every 20 min (less frequent)
  "SyncComprehensiveSchedule": "0 */30 * * * *",   // Every 30 min
  "SyncContentSchedule": "0 */45 * * * *",         // Every 45 min
  "AnomalyDetectionSchedule": "0 */3 * * * *"      // Every 3 min (MORE frequent!)
}
```

**Rationale**: Once you have initial metadata, you don't need constant syncs. Focus on detection development!

## Manual Testing

### Trigger Manual Sync (Get Data Immediately)
```bash
# Comprehensive sync (metadata + categories)
curl -X POST http://localhost:7071/api/UniversalSync \
  -H "Content-Type: application/json" \
  -d '{"syncType":"comprehensive"}'

# Critical sync with content
curl -X POST http://localhost:7071/api/UniversalSync \
  -H "Content-Type: application/json" \
  -d '{"syncType":"critical"}'
```

### Trigger Manual AnomalyDetection
```bash
# Force anomaly detection to run now
curl -X POST http://localhost:7071/api/TriggerAnomalyDetection
```

### Check Current Data
```bash
# See what updates are available
curl http://localhost:7071/api/UniversalHealth?scope=store

# Check package count
# Should show: "PackageCount": 654+
```

## Performance Expectations

### Typical Timings (Development Machine)

| Operation | Duration | Frequency |
|-----------|----------|-----------|
| **AnomalyDetection** | 1-2 sec | Every 5 min (12/hour) |
| **Critical Sync** | 3-5 min | Every 15 min (4/hour) |
| **Comprehensive Sync** | 5-10 min | Every 15 min (4/hour) |
| **Content Sync** | 10-30 min | Every 15 min (4/hour) |

### Expected Resource Usage
- **CPU**: 10-30% during syncs, <5% during anomaly detection
- **Memory**: 500MB-1GB for metadata store
- **Disk**: 1-5GB for content (grows with syncs)
- **Network**: Burst during syncs (10-50 Mbps), idle between

## Troubleshooting

### Problem: AnomalyDetection Always Shows "No anomalies"
```csharp
// Check your scoring logic
public double Score(SoftwareUpdate update)
{
    // If score never exceeds threshold (0.5), nothing is flagged
    var score = 0.0;
    
    // Add temporary logging
    this.logger.LogInformation("Scoring {Title}: {Score}", 
        update.Title, score);
    
    // Temporarily lower threshold for testing
    return 0.9;  // Force some anomalies for testing
}
```

### Problem: Too Many False Positives
```csharp
// Tune thresholds
if (anomalyScore > 0.8)  // Raise from 0.5 to 0.8
{
    anomalies.Add((softwareUpdate, anomalyResult));
}
```

### Problem: Missing Expected Updates
```bash
# Check filter configuration
"SupportedCategories": [
  "Security Updates",  # ? Your expected category here?
  "Critical Updates"
]

# Verify product filters
"ProductFilters": ["Windows 11"]  # Correct product?
```

## Related Documentation

- [AnomalyDetectionService.cs](../../UpdateEngine.Core/src/Services/AnomalyDetectionService.cs) - Your main development file
- [Concurrent Sync Fixes](../fixes/2025-11-23-concurrent-sync-and-file-locking-fixes.md) - Why syncs don't conflict
- [Fast Development Schedule](./FAST_DEVELOPMENT_SCHEDULE_STRATEGY.md) - Alternative schedules

---

**Current Focus**: AnomalyDetection development with 5-minute iteration cycles  
**Initial Sync**: ~15-25 minutes on first run  
**Iteration Speed**: Change code ? Wait 5 min ? See results  
**Last Updated**: 2025-11-23
