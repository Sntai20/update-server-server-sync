# Week 4 Day 3: Dual Hosting Testing Guide

## ?? Overview

**Date**: January 20, 2025  
**Status**: ?? Ready for Testing  
**Objective**: Validate both hosting models (Azure Functions + Worker Service) work correctly with shared orchestrators

## ? Prerequisites (All Complete)

- ? WorkerService project created and builds
- ? WorkerService added to solution file
- ? Configuration loading fixed (AddSharedAppConfiguration)
- ? AppHost integration complete
- ? Zero compilation errors

## ?? Testing Plan

### Phase 1: Startup Validation (30 minutes)

#### Step 1: Start AppHost
```bash
cd UpdateEngine.AppHost/src
dotnet run
```

**Expected Output**:
```
Building...
info: Aspire.Hosting.DistributedApplication[0]
      Aspire version: 9.0.0+...
      Distributed application started: http://localhost:15888
      
Resources:
  - storage (Azurite)
  - redis (Redis)
  - functions (Azure Functions) - http://localhost:7071
  - worker (WorkerService) - http://localhost:8080
```

#### Step 2: Verify Services Started
```bash
# Check Azure Functions
curl http://localhost:7071/api/health

# Check Worker Service
curl http://localhost:8080/health
```

**Expected**:
- ? Both return HTTP 200 OK
- ? Health check JSON shows all checks passing

#### Step 3: Check Aspire Dashboard
Open browser: http://localhost:15888

**Verify**:
- ? All 4 resources show "Running" status
- ? No errors in logs
- ? Metrics being collected

### Phase 2: Health Check Validation (20 minutes)

#### Test Azure Functions Health Endpoints
```bash
# Get health status
curl http://localhost:7071/api/GetHealthStatus

# Expected: Detailed health check results
```

#### Test Worker Service Health Endpoints
```bash
# Comprehensive health check
curl http://localhost:8080/health

# Liveness probe (Kubernetes-style)
curl http://localhost:8080/health/live

# Readiness probe (Kubernetes-style)
curl http://localhost:8080/health/ready
```

**Expected Results**:
```json
// /health
{
  "status": "Healthy",
  "totalDuration": "00:00:00.123",
  "entries": {
    "metadata-store": { "status": "Healthy", "description": "..." },
    "content-store": { "status": "Healthy", "description": "..." },
    "azure-storage": { "status": "Healthy", "description": "..." },
    "redis": { "status": "Healthy", "description": "..." }
  }
}

// /health/live
{ "status": "Healthy" }

// /health/ready
{ "status": "Healthy" }
```

### Phase 3: REST API Endpoint Testing (45 minutes)

#### Sync Endpoints

**Azure Functions**:
```bash
# Get sync status
curl http://localhost:7071/api/GetSyncStatus

# Start sync operation
curl -X POST http://localhost:7071/api/StartSync \
  -H "Content-Type: application/json" \
  -d '{"syncType":"Categories","action":"Start"}'
```

**Worker Service**:
```bash
# Get sync status
curl http://localhost:8080/api/sync/status

# Start sync operation
curl -X POST http://localhost:8080/api/sync/execute \
  -H "Content-Type: application/json" \
  -d '{"syncType":"Categories","action":"Start"}'
```

**Validation**:
- ? Both hosts return same response structure
- ? Both use same orchestrator (ISyncOrchestrator)
- ? Both return consistent results

#### Metadata Endpoints

**Azure Functions**:
```bash
# Get store status
curl http://localhost:7071/api/GetStoreStatus

# Query metadata
curl -X POST http://localhost:7071/api/QueryMetadata \
  -H "Content-Type: application/json" \
  -d '{"query":"*","maxResults":10}'
```

**Worker Service**:
```bash
# Get store status
curl http://localhost:8080/api/metadata/store/status

# Query metadata
curl -X POST http://localhost:8080/api/metadata/query \
  -H "Content-Type: application/json" \
  -d '{"query":"*","maxResults":10}'
```

**Validation**:
- ? Both return same metadata
- ? Both use same orchestrator (IMetadataOrchestrator)
- ? Response times comparable

### Phase 4: Background Worker Validation (30 minutes)

#### Check Worker Startup Logs

In Aspire Dashboard, check WorkerService logs for:

**SyncWorker**:
```
[SyncWorker] Worker starting...
[SyncWorker] Sync interval: 01:00:00
[SyncWorker] Sync on startup: True
[SyncWorker] Worker started
```

**HealthCheckWorker**:
```
[HealthCheckWorker] Worker starting...
[HealthCheckWorker] Health check interval: 00:05:00
[HealthCheckWorker] Worker started
```

#### Test Configuration Hot-Reload

1. **Edit WorkerService/appsettings.Development.json**:
```json
{
  "Sync": {
    "SyncIntervalMinutes": 30  // Change from 60 to 30
  }
}
```

2. **Save file** (don't restart WorkerService)

3. **Check logs** for:
```
[SyncWorker] Configuration changed
[SyncWorker] New sync interval: 00:30:00
```

**Validation**:
- ? Worker detects configuration change
- ? Worker updates interval without restart
- ? IOptionsMonitor hot-reload working

### Phase 5: Redis Caching Validation (30 minutes)

#### Test Cache Miss ? Cache Hit

**First Request (Cache MISS)**:
```bash
# Query metadata (should miss cache)
time curl -X POST http://localhost:8080/api/metadata/query \
  -H "Content-Type: application/json" \
  -d '{"query":"*","maxResults":10}'

# Expected: ~500-1000ms (queries storage)
```

**Second Request (Cache HIT)**:
```bash
# Same query (should hit cache)
time curl -X POST http://localhost:8080/api/metadata/query \
  -H "Content-Type: application/json" \
  -d '{"query":"*","maxResults":10}'

# Expected: ~50-100ms (reads from Redis)
```

#### Validate Cache Keys in Redis

```bash
# Connect to Redis container
docker exec -it update-server-server-sync-redis-1 redis-cli

# List cache keys
KEYS metadata:*

# Expected:
# 1) "metadata:query:*:10"
```

#### Test Cache Invalidation

```bash
# Start sync (should invalidate metadata cache)
curl -X POST http://localhost:8080/api/sync/execute \
  -H "Content-Type: application/json" \
  -d '{"syncType":"Categories","action":"Start"}'

# Check Redis again
docker exec -it update-server-server-sync-redis-1 redis-cli KEYS metadata:*

# Expected: Empty or different keys (cache invalidated)
```

### Phase 6: Comparison Testing (30 minutes)

#### Test Same Request on Both Hosts

**Script**:
```bash
# Test sync status
echo "=== Azure Functions ==="
curl http://localhost:7071/api/GetSyncStatus | jq .

echo "=== Worker Service ==="
curl http://localhost:8080/api/sync/status | jq .

# Test store status
echo "=== Azure Functions ==="
curl http://localhost:7071/api/GetStoreStatus | jq .

echo "=== Worker Service ==="
curl http://localhost:8080/api/metadata/store/status | jq .
```

**Validation**:
- ? Both return same data structure
- ? Both return same values
- ? Response times comparable
- ? Both use same orchestrators

## ?? Test Results Tracking

### Test Checklist

#### Startup
- [ ] AppHost starts without errors
- [ ] Azurite container running
- [ ] Redis container running
- [ ] Azure Functions running (port 7071)
- [ ] Worker Service running (port 8080)
- [ ] Aspire Dashboard accessible (port 15888)

#### Health Checks
- [ ] Azure Functions: `/api/GetHealthStatus` returns 200 OK
- [ ] Worker Service: `/health` returns 200 OK
- [ ] Worker Service: `/health/live` returns 200 OK
- [ ] Worker Service: `/health/ready` returns 200 OK
- [ ] All health checks show "Healthy" status

#### REST API Endpoints
- [ ] Sync endpoints work on both hosts
- [ ] Metadata endpoints work on both hosts
- [ ] Responses consistent between hosts
- [ ] Error handling consistent

#### Background Workers
- [ ] SyncWorker starts successfully
- [ ] HealthCheckWorker starts successfully
- [ ] Configuration hot-reload working
- [ ] Workers log startup messages

#### Redis Caching
- [ ] Cache MISS on first request
- [ ] Cache HIT on second request
- [ ] Cache keys visible in Redis
- [ ] Cache invalidation after sync
- [ ] Performance improvement measured

#### Comparison
- [ ] Same orchestrators used by both hosts
- [ ] Response structures identical
- [ ] Data values identical
- [ ] Response times comparable

## ?? Known Issues

### 1. Solution File Duplicate Name
**Issue**: `dotnet build microsoft-update.sln` fails with duplicate "ServiceDefaults" name  
**Workaround**: Build projects individually or use AppHost  
**Impact**: Low (AppHost works correctly)

### 2. First Request After Cold Start
**Issue**: First request may timeout after AppHost cold start  
**Expected Behavior**: Retry after 10-15 seconds  
**Impact**: Low (only affects first request)

## ?? Success Criteria

### Must Pass
- ? Both services start without errors
- ? All health checks pass
- ? REST APIs return expected data
- ? Background workers start correctly
- ? Redis caching improves performance

### Should Pass
- ? Configuration hot-reload works
- ? Cache invalidation works correctly
- ? Response times comparable between hosts
- ? No errors in Aspire Dashboard logs

### Nice to Have
- ? Performance improvement >50% with caching
- ? Background workers execute on schedule
- ? Metrics visible in Aspire Dashboard

## ?? Next Steps

After all tests pass:

1. **Document Results**: Create `WEEK4_DAY3_SUMMARY.md`
2. **Create Test Script**: Automate testing with `scripts/test/Test-DualHosting.ps1`
3. **Update Progress**: Update `IMPLEMENTATION_SUMMARY.md`
4. **Begin Day 4**: Create integration test fixtures

## ?? Related Documentation

- [IMPLEMENTATION_SUMMARY.md](./IMPLEMENTATION_SUMMARY.md) - Overall roadmap
- [WORKERSERVICE_ADDED_TO_SOLUTION.md](./WORKERSERVICE_ADDED_TO_SOLUTION.md) - Solution integration
- [WorkerService/README.md](../../WorkerService/README.md) - WorkerService usage guide
- [CACHING_GUIDE.md](./CACHING_GUIDE.md) - Redis caching details

---

**Created**: 2025-01-20  
**Status**: ?? Ready for Testing  
**Estimated Time**: 3-4 hours  
**Complexity**: Medium

