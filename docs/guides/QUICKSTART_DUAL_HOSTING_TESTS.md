# Quick Start: Testing Dual Hosting

## ?? 30-Second Start

```bash
# 1. Start everything
cd AppHost/src
dotnet run

# 2. Wait for "Distributed application started" message (15-30 seconds)

# 3. In another terminal, run automated tests
.\scripts\test\Test-DualHosting.ps1 -Verbose
```

## ?? Expected Results

### Phase 1: Startup Validation
```
? PASS - Azure Functions - Health Endpoint
? PASS - Worker Service - Health Endpoint
? PASS - Aspire Dashboard - Accessible
```

### Phase 2: Health Check Validation
```
? PASS - Azure Functions - GetHealthStatus
? PASS - Worker Service - /health (Comprehensive)
? PASS - Worker Service - /health/live (Liveness)
? PASS - Worker Service - /health/ready (Readiness)
```

### Phase 3-6: Endpoint & Comparison Tests
```
? PASS - Azure Functions - GetSyncStatus
? PASS - Worker Service - GET /api/sync/status
? PASS - Azure Functions - GetStoreStatus
? PASS - Worker Service - GET /api/metadata/store/status
? PASS - Worker Service - Metadata Query (Cache MISS)
? PASS - Worker Service - Metadata Query (Cache HIT)
? PASS - Compare Sync Status (Functions vs Worker)
? PASS - Compare Store Status (Functions vs Worker)
```

### Final Summary
```
Total Tests:  14
Passed:       14
Failed:       0
Success Rate: 100%

? ALL TESTS PASSED!
```

## ?? What Gets Tested

### Dual Hosting Validation
- ? Both Azure Functions and Worker Service start correctly
- ? Both use shared orchestrators (ISyncOrchestrator, IMetadataOrchestrator)
- ? Both return identical responses
- ? Both connect to shared infrastructure (Azurite, Redis)

### Health Checks
- ? ASP.NET Core health endpoints (/health, /health/live, /health/ready)
- ? Metadata store health check
- ? Content store health check
- ? Azure storage health check
- ? Redis cache health check

### REST APIs
- ? Sync operations (status, execute)
- ? Metadata queries (store status, query)
- ? Health monitoring (comprehensive checks)

### Caching
- ? Cache MISS on first request
- ? Cache HIT on subsequent requests
- ? Performance improvement measured

### Architecture
- ? Same orchestrators used by both hosts
- ? Configuration loaded from shared sources
- ? Dependency injection working correctly

## ?? Viewing Results

### Aspire Dashboard
Open: http://localhost:15888

**Features**:
- Real-time logs from all services
- Metrics and telemetry
- Resource status (Running/Stopped)
- Distributed tracing

### Service URLs
- **Azure Functions**: http://localhost:7071
- **Worker Service**: http://localhost:8080
- **Azurite**: http://localhost:10000
- **Redis**: localhost:6379

### Health Check Endpoints

**Azure Functions**:
```bash
curl http://localhost:7071/api/GetHealthStatus
```

**Worker Service**:
```bash
# Comprehensive
curl http://localhost:8080/health | jq .

# Liveness (Kubernetes)
curl http://localhost:8080/health/live

# Readiness (Kubernetes)
curl http://localhost:8080/health/ready
```

## ?? Troubleshooting

### Services Don't Start
```bash
# Check if ports are in use
netstat -ano | findstr "7071"
netstat -ano | findstr "8080"

# Kill processes if needed
taskkill /F /PID <process-id>

# Restart AppHost
cd AppHost/src
dotnet run
```

### Tests Fail on First Run
**Issue**: Cold start timeout  
**Solution**: Wait 15-30 seconds after "Distributed application started" message, then retry

### Redis Not Available
**Check Redis container**:
```bash
docker ps | grep redis
```

**Expected**:
```
update-server-server-sync-redis-1   Running
```

**If not running**:
```bash
docker start update-server-server-sync-redis-1
```

### Azurite Not Available
**Check Azurite container**:
```bash
docker ps | grep azurite
```

**If not running, AppHost will start it automatically**

## ?? Detailed Testing Guide

For comprehensive testing procedures, see:
- [WEEK4_DAY3_TESTING_GUIDE.md](./WEEK4_DAY3_TESTING_GUIDE.md) - Complete testing guide
- [WEEK4_PROGRESS_SUMMARY.md](./WEEK4_PROGRESS_SUMMARY.md) - Progress tracking

## ?? Next Steps

After tests pass:

1. **Document Results**
   - Create WEEK4_DAY3_SUMMARY.md
   - Include test output
   - Document any issues found

2. **Manual Testing**
   - Test background workers (check logs)
   - Test configuration hot-reload
   - Compare performance between hosts

3. **Begin Day 4**
   - Create WorkerServiceTestFixture.cs
   - Write integration tests
   - Complete Week 4

---

**Created**: 2025-01-20  
**Status**: ? Ready to Use  
**Time Required**: 5-10 minutes for automated tests

