# Development Schedules Guide

## Overview

This guide explains the timer trigger schedules used in development vs production environments and how to customize them for your workflow.

## Development Schedules (Fast Iteration)

**Purpose**: Rapid feedback during active development and debugging

| Trigger | Schedule | Frequency | Purpose |
|---------|----------|-----------|---------|
| **AnomalyDetection** | `0 * * * * *` | **Every 1 minute** | Rapid testing of anomaly detection logic |
| **ScheduledHealthCheck** | `0 */2 * * * *` | Every 2 minutes | Quick health monitoring |
| **SyncCritical** | `0 */3 * * * *` | Every 3 minutes | Critical updates sync testing |
| **SyncComprehensive** | `0 1,6,11,16,21,26,31,36,41,46,51,56 * * * *` | Every 5 minutes (offset 1 min) | Comprehensive sync testing |
| **SyncContent** | `0 3,8,13,18,23,28,33,38,43,48,53,58 * * * *` | Every 5 minutes (offset 3 min) | Content download testing |
| **Maintenance** | `0 0 */2 * * *` | Every 2 hours | Background maintenance |

### Timeline View (First 10 Minutes)

```
Minute 0:  [Critical][Health][Maintenance]
Minute 1:  [Comprehensive][Anomaly]
Minute 2:  [Health][Anomaly]
Minute 3:  [Critical][Content][Anomaly]
Minute 4:  [Health][Anomaly]
Minute 5:  [Anomaly]
Minute 6:  [Critical][Comprehensive][Health][Anomaly]
Minute 7:  [Anomaly]
Minute 8:  [Content][Health][Anomaly]
Minute 9:  [Critical][Anomaly]
Minute 10: [Health][Anomaly]
```

### Benefits

? **Rapid Feedback**: See results within 1-3 minutes instead of 15+ minutes  
? **Quick Iterations**: Test anomaly detection changes every minute  
? **Staggered Execution**: Syncs offset to avoid conflicts  
? **Fast Debugging**: Health checks every 2 minutes for quick diagnostics

### Trade-offs

?? **Higher Load**: More frequent executions increase system load  
?? **Network Usage**: More upstream sync requests  
?? **Azurite I/O**: Frequent blob operations during development  
?? **Log Volume**: More logs generated (ensure log filtering is configured)

## Production Schedules (Stable)

**Purpose**: Efficient, stable operation with reasonable resource usage

| Trigger | Schedule | Frequency | Purpose |
|---------|----------|-----------|---------|
| **SyncCritical** | `0 */4 * * * *` | Every 4 hours | Security/critical updates |
| **SyncComprehensive** | `0 0 0 * * *` | Daily at midnight | Full catalog sync |
| **SyncContent** | `0 0 2 * * *` | Daily at 2 AM | Content download |
| **ScheduledHealthCheck** | `0 */30 * * * *` | Every 30 minutes | System health monitoring |
| **AnomalyDetectionSchedule** | `0 */15 * * * *` | Every 15 minutes | Anomaly detection |
| **Maintenance** | `0 0 3 * * SUN` | Weekly (Sunday 3 AM) | Database maintenance |

## Customizing Schedules

### Option 1: Edit Shared Configuration (Team-Wide)

```json
// UpdateEngine.Configuration/src/shared/appsettings.Development.json
"SyncConfiguration": {
  "AnomalyDetectionSchedule": "0 */2 * * * *"  // Every 2 minutes
}
```

### Option 2: Local Override (Individual Developer)

```json
// UpdateEngine.Functions/src/local.settings.json
{
  "Values": {
    "UpdateEngine__SyncConfiguration__AnomalyDetectionSchedule": "0 */10 * * * *"
  }
}
```

### Option 3: Environment Variable (Runtime)

```bash
# Windows PowerShell
$env:UpdateEngine__SyncConfiguration__AnomalyDetectionSchedule = "0 */30 * * * *"

# Linux/Mac
export UpdateEngine__SyncConfiguration__AnomalyDetectionSchedule="0 */30 * * * *"
```

## CRON Expression Reference

Azure Functions uses 6-part CRON expressions:

```
{second} {minute} {hour} {day} {month} {day-of-week}

Examples:
0 * * * * *              - Every minute
0 */5 * * * *            - Every 5 minutes
0 0 * * * *              - Every hour
0 30 */2 * * *           - Every 2 hours at :30
0 0 2 * * *              - Daily at 2:00 AM
0 0 0 * * SUN            - Weekly on Sunday at midnight
0 0,15,30,45 * * * *     - Every 15 minutes
```

### Tools

- [Cron Expression Generator](https://crontab.cronhub.io/)
- [Cron Expression Validator](https://crontab.guru/)

## Performance Considerations

### Development Environment

**Recommended for active development:**
- Anomaly Detection: 1-2 minutes (rapid iteration)
- Critical Sync: 3-5 minutes (quick testing)
- Health Checks: 2 minutes (fast monitoring)

**Recommended for background development:**
- Anomaly Detection: 5-10 minutes (less noise)
- Critical Sync: 10-15 minutes (reduced load)
- Health Checks: 5 minutes (periodic monitoring)

### Resource Impact

| Schedule | CPU Impact | Network Impact | Storage I/O |
|----------|-----------|----------------|-------------|
| Every 1 min | High | Medium | High |
| Every 5 min | Medium | Low | Medium |
| Every 15 min | Low | Very Low | Low |

## Troubleshooting

### Too Many Executions

**Symptom**: "Sync skipped - another sync operation is in progress"

**Solution**:
1. Increase schedule intervals
2. Check for long-running operations
3. Review semaphore lock logs

```json
"SyncCriticalSchedule": "0 */5 * * * *"  // Increase from 3 to 5 minutes
```

### Missing Executions

**Symptom**: Timer never fires or fires irregularly

**Solution**:
1. Check CRON expression syntax (6 parts required)
2. Verify AppHost is running
3. Check Azure Functions logs

```bash
# Validate CRON expression
func start --verbose
```

### Conflicting Schedules

**Symptom**: Multiple timers firing simultaneously causing slowness

**Solution**: Stagger schedules by 1-2 minutes

```json
"SyncCriticalSchedule": "0 0,5,10,15,20,25,30,35,40,45,50,55 * * * *",     // Minute 0
"SyncComprehensiveSchedule": "0 2,7,12,17,22,27,32,37,42,47,52,57 * * * *", // Minute 2
"SyncContentSchedule": "0 4,9,14,19,24,29,34,39,44,49,54,59 * * * *"        // Minute 4
```

## Best Practices

1. **Start Conservative**: Begin with slower schedules and increase frequency as needed
2. **Monitor Logs**: Watch for "skipped" messages indicating overlapping executions
3. **Stagger Syncs**: Offset comprehensive/content syncs to avoid conflicts
4. **Test Locally**: Use Azurite to test schedule changes before committing
5. **Document Changes**: Update team if you modify shared configuration
6. **Use Semaphores**: SyncService already has locking to prevent concurrent operations
7. **Profile Performance**: Use health checks to monitor execution times

## Environment-Specific Settings

### Local Development (Your Machine)

```json
// Fast iteration for active development
"AnomalyDetectionSchedule": "0 * * * * *"  // 1 minute
```

### CI/CD Pipeline

```json
// Disable scheduled syncs during tests
"EnableScheduledSync": false
```

### Staging Environment

```json
// Moderate pace matching production patterns
"AnomalyDetectionSchedule": "0 */10 * * * *"  // 10 minutes
```

### Production Environment

```json
// Conservative schedules for stability
"AnomalyDetectionSchedule": "0 */15 * * * *"  // 15 minutes
```

## Related Documentation

- [TRIGGERS_GUIDE.md](TRIGGERS_GUIDE.md) - Detailed timer trigger implementation
- [SYNC_TROUBLESHOOTING.md](SYNC_TROUBLESHOOTING.md) - Sync operation debugging
- [INMEMORY_TESTING_GUIDE.md](INMEMORY_TESTING_GUIDE.md) - Testing without timers

---

**Last Updated**: 2025-01-16  
**Applies To**: UpdateEngine (Azure Functions & WorkerService)
