# Quick Start: Dual Hosting Testing

## ?? Start Services

### Option 1: Use AppHost (Recommended)

```powershell
# Open terminal in workspace root
cd AppHost/src

# Start everything (Azurite + Redis + Functions + Worker Service)
dotnet run
```

**What This Starts**:
- ? Azurite (Azure Storage) - port 10000
- ? Redis - port 6379
- ? Azure Functions (UpdateEngine) - port 7071
- ? Worker Service - port 8080
- ? Aspire Dashboard - http://localhost:15888

**Wait Time**: 30-60 seconds for all services to be ready

---

### Option 2: Start Services Individually

```powershell
# Terminal 1: Start Azurite
azurite --silent --location c:\azurite --debug c:\azurite\debug.log

# Terminal 2: Start Redis
redis-server

# Terminal 3: Start Azure Functions
cd UpdateEngine/src
func start

# Terminal 4: Start Worker Service
cd WorkerService
dotnet run
```

---

## ? Verify Services

### Quick Health Check

```powershell
# Check Azure Functions
curl http://localhost:7071/api/health

# Check Worker Service
curl http://localhost:8080/health/live

# If both return 200 OK, you're ready!
```

### Aspire Dashboard

```powershell
# Open Aspire Dashboard (if using AppHost)
start http://localhost:15888
```

Dashboard shows:
- Service status (green = healthy)
- Logs from all services
- Metrics and traces
- Resource dependencies

---

## ?? Run Tests

### Automated Tests

```powershell
# Run full test suite (recommended)
.\scripts\test\Test-DualHosting.ps1

# Expected output:
# Azure Functions Tests: 3/3 passed ?
# Worker Service Tests: 5/5 passed ?
# ALL TESTS PASSED (8/8) ?
```

### Manual Tests

```powershell
# Test Azure Functions (port 7071)
curl http://localhost:7071/api/sync/status
curl http://localhost:7071/api/metadata/statistics

# Test Worker Service (port 8080)
curl http://localhost:8080/api/sync/status
curl http://localhost:8080/api/metadata/statistics

# Test Health Checks
curl http://localhost:8080/health
curl http://localhost:8080/health/live
curl http://localhost:8080/health/ready

# Test Swagger UI (Worker Service)
start http://localhost:8080/swagger
```

---

## ?? Verify Background Workers

### Check Logs for Worker Startup

Look for these messages in Worker Service logs:

```
SyncWorker starting with interval: 60 minutes
HealthCheckWorker starting with interval: 5 minutes
```

### Test Configuration Hot-Reload

```powershell
# 1. Edit WorkerService/appsettings.Development.json
# 2. Change SyncIntervalMinutes from 60 to 5
# 3. Save file
# 4. Wait ~10 seconds
# 5. Check logs for: "Configuration changed, updating interval to 5 minutes"
```

---

## ?? Verify Redis Caching

### Test Cache HIT/MISS

```powershell
# First query (cache MISS)
curl http://localhost:8080/api/metadata/statistics
# Check logs: "Cache MISS for key: updateengine:metadata:statistics"

# Second query (cache HIT)
curl http://localhost:8080/api/metadata/statistics  
# Check logs: "Cache HIT for key: updateengine:metadata:statistics"
```

### Check Redis Directly

```powershell
# Open Redis CLI
redis-cli

# List all keys
> KEYS updateengine:*

# Get a specific key
> GET updateengine:metadata:statistics

# Check TTL (time to live)
> TTL updateengine:metadata:statistics
```

---

## ?? Troubleshooting

### Port Already in Use

```powershell
# Find process using port
netstat -ano | findstr "7071"
netstat -ano | findstr "8080"

# Kill process
Stop-Process -Id <PID> -Force
```

### Service Won't Start

```powershell
# Clean and rebuild
dotnet clean
dotnet build

# Check for errors
dotnet build --no-incremental
```

### Azurite Connection Error

```powershell
# Verify Azurite is running
curl http://localhost:10000

# Start Azurite manually if needed
azurite --silent
```

### Redis Connection Error

```powershell
# Verify Redis is running
redis-cli ping
# Should return: PONG

# Start Redis manually if needed
redis-server
```

---

## ?? Success Criteria

? **Day 2 is complete when all these pass:**

- [ ] Both services start without errors
- [ ] All health checks return 200 OK
- [ ] Test script passes 8/8 tests
- [ ] Background workers log startup messages
- [ ] Redis shows cache HITs and MISSes
- [ ] Aspire Dashboard shows all services healthy
- [ ] Same endpoints return consistent responses

---

## ?? Next Steps

After successful validation:

1. **Document findings** in WEEK4_DAY2_SUMMARY.md
2. **Update roadmap** in IMPLEMENTATION_SUMMARY.md
3. **Proceed to Day 3-4** - Create test infrastructure

---

## ?? Need Help?

**Check logs**:
- Azure Functions: Console output or `.azure-functions-core-tools/logs`
- Worker Service: Console output
- Aspire Dashboard: http://localhost:15888 ? Logs tab

**Common issues**:
- Port conflicts ? Kill processes using ports
- Storage errors ? Verify Azurite is running
- Cache errors ? Verify Redis is running
- Config errors ? Check appsettings.json format

**Still stuck?**:
- Review `docs/guides/WEEK4_DAY2_INSTRUCTIONS.md` for detailed guidance
- Check `docs/guides/WEEK4_DAY2_PROGRESS.md` for known issues

---

**Estimated Time**: 2-3 hours  
**Difficulty**: Medium  
**Prerequisites**: Day 1 complete ?
