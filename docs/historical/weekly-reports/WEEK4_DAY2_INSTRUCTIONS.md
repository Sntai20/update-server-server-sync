# Week 4 Day 2: Dual Hosting Test Instructions

## Quick Start

### 1. Start AppHost (both services)

```powershell
# Terminal 1: Start AppHost
cd UpdateEngine.AppHost/src
dotnet run
```

This will start:
- ? **Azurite** (Azure Storage Emulator) - port 10000
- ? **Redis** - port 6379
- ? **Azure Functions** (UpdateEngine) - port 7071
- ? **Worker Service** - port 8080
- ? **Aspire Dashboard** - http://localhost:15888

### 2. Wait for services to be ready (~30-60 seconds)

Watch the console output for:
```
UpdateEngine: Now listening on: http://localhost:7071
WorkerService: Now listening on: http://localhost:8080
```

### 3. Run Dual Hosting Tests

```powershell
# Terminal 2: Run tests
.\scripts\test\Test-DualHosting.ps1
```

This will:
1. Wait for both services to be ready
2. Test Azure Functions endpoints (port 7071)
3. Test Worker Service endpoints (port 8080)
4. Compare responses between both hosts
5. Report results

## Manual Testing

### Azure Functions (Port 7071)

```powershell
# Health check
curl http://localhost:7071/api/health

# Sync status
curl http://localhost:7071/api/sync/status

# Metadata statistics
curl http://localhost:7071/api/metadata/statistics
```

### Worker Service (Port 8080)

```powershell
# Health checks (Kubernetes-compatible)
curl http://localhost:8080/health          # Comprehensive
curl http://localhost:8080/health/live     # Liveness probe
curl http://localhost:8080/health/ready    # Readiness probe

# Same endpoints as Azure Functions
curl http://localhost:8080/api/sync/status
curl http://localhost:8080/api/metadata/statistics

# Swagger UI (Development only)
start http://localhost:8080/swagger
```

### Aspire Dashboard

```powershell
# Open Aspire Dashboard
start http://localhost:15888
```

The dashboard shows:
- Service status (running, stopped, crashed)
- Logs from all services
- Traces and metrics
- Resource dependencies

## What to Validate

### ? Day 2 Checklist

- [ ] **Both services start without errors**
  - Azure Functions on port 7071
  - Worker Service on port 8080

- [ ] **Health checks respond correctly**
  - Functions: `/api/health` returns 200 OK
  - Worker: `/health/live` returns 200 OK
  - Worker: `/health/ready` returns 200 OK

- [ ] **Same endpoints work in both hosts**
  - `/api/sync/status` returns same structure
  - `/api/metadata/statistics` returns same data

- [ ] **Background workers start (Worker Service)**
  - Check logs for "SyncWorker starting"
  - Check logs for "HealthCheckWorker starting"
  - Verify workers respect `EnableScheduledSync` setting

- [ ] **Redis caching works**
  - First query: Cache MISS (check logs)
  - Second query: Cache HIT (check logs)
  - Verify cache key prefix in Redis

- [ ] **Aspire Dashboard shows both services**
  - UpdateEngine (Azure Functions)
  - WorkerService (ASP.NET Core)
  - Storage (Azurite)
  - Redis

## Expected Results

### Health Check Response (Worker Service)

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.1234567",
  "entries": {
    "metadata-store": {
      "status": "Healthy",
      "description": "Metadata store is operational",
      "data": {}
    },
    "redis": {
      "status": "Healthy",
      "description": "Redis connection successful",
      "data": {}
    }
  }
}
```

### Sync Status Response (Both Hosts)

```json
{
  "isRunning": false,
  "lastSyncTime": null,
  "lastSyncResult": null,
  "currentOperation": null
}
```

### Metadata Statistics Response (Both Hosts)

```json
{
  "totalUpdates": 0,
  "totalCategories": 0,
  "lastIndexTime": "2025-01-16T12:00:00Z",
  "indexStatus": "Ready"
}
```

## Troubleshooting

### Services won't start

**Check ports are available:**
```powershell
netstat -ano | findstr "7071 8080 10000 6379"
```

**Kill processes using those ports:**
```powershell
Stop-Process -Id <PID> -Force
```

### Connection errors

**Verify Azurite is running:**
```powershell
curl http://localhost:10000
```

**Verify Redis is running:**
```powershell
redis-cli ping
# Should return: PONG
```

### Background workers not starting

**Check configuration:**
- `appsettings.Development.json` should have `EnableScheduledSync: true`
- Check logs for "SyncWorker" and "HealthCheckWorker"

**Verify configuration hot-reload:**
1. Edit `WorkerService/appsettings.Development.json`
2. Change `SyncIntervalMinutes` from 60 to 5
3. Wait ~10 seconds
4. Check logs - should see "Configuration changed, updating interval"

## Next Steps (Day 3-4)

After successful dual hosting validation:

1. **Create test infrastructure**
   - WorkerServiceTestFixture.cs
   - WebApplicationFactory setup

2. **Write integration tests**
   - Controller tests
   - Background worker tests
   - Configuration hot-reload tests

3. **Document findings**
   - WEEK4_DAY2_SUMMARY.md
   - Update IMPLEMENTATION_SUMMARY.md

## Success Criteria

? **Day 2 is complete when:**

1. Both services start and run simultaneously
2. All health checks pass (6 total)
3. Same endpoints return consistent responses
4. Background workers start and log activity
5. Redis caching demonstrates hits and misses
6. Aspire Dashboard shows all services healthy
7. Test script passes with 100% success rate

---

**Estimated Time**: 3-4 hours
**Current Status**: ? Ready to start
**Documentation**: Will be created after successful validation
