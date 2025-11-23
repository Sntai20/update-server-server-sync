# Quick Test Commands - Post Cleanup (Week 4 Day 3)

## Prerequisites
Azure Functions running on port: **15001** (via Aspire)

---

## 1. Start Services (If Not Running)

Open a **new PowerShell terminal** and run:

```powershell
cd C:\Users\ansantan\Repos\update-server-server-sync\AppHost\src
dotnet run
```

Wait for Aspire to start all services:
- ? Azure Functions (port 15001)
- ? Worker Service (ports 8080/8081)
- ? Azurite (Azure Storage emulator)
- ? Redis cache

---

## 2. Run Automated Test Suite

In a **separate PowerShell terminal**:

```powershell
cd C:\Users\ansantan\Repos\update-server-server-sync
.\scripts\test\Run-PostCleanupTests.ps1
```

This will:
- ? Build UpdateEngine
- ? Check if services are running
- ? Test all key endpoints
- ? Verify health checks (including Azurite detection)
- ? Test Core/ function endpoints
- ? Provide summary results

---

## 3. Manual Endpoint Tests

If you prefer to test manually:

```powershell
# Set the dynamic port
$port = 15001

# Test 1: Health Check
curl http://localhost:$port/api/health

# Test 2: Sync Status
curl http://localhost:$port/api/sync/status

# Test 3: Metadata Statistics
curl http://localhost:$port/api/metadata/statistics

# Test 4: Content Status
curl http://localhost:$port/api/content/status

# Test 5: Detailed Health (JSON)
$health = Invoke-RestMethod -Uri "http://localhost:$port/api/health"
$health | ConvertTo-Json -Depth 5

# Test 6: Check Azurite Detection
$health.checks | Where-Object { $_.name -eq "azure-storage" } | Format-List
```

---

## 4. Verify Cleanup Success

### ? What to Look For in Startup Logs:

**Good signs** (cleanup worked):
```
? Functions registered successfully
? ClientWebService - Route: /api/ClientWebService/client.asmx
? GetContent - Route: /api/content/{contentHash}
? WeeklyMaintenance - Timer trigger registered with schedule "0 0 2 */7 * *"
? Host initialized [Functions: X]
```

**Bad signs** (cleanup failed):
```
? Duplicate route conflict detected for route...
? Function 'GetMicrosoftUpdateContent' has the same route as function 'GetContent'
? Function 'ExportMetadata' is already defined in 'MetadataQueryFunctions'
? WeeklyMaintenance: Schedule not found
```

### ? Expected Health Check Results:

```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "azure-storage",
      "status": "Healthy",
      "description": "Azurite (local Azure Storage emulator) is accessible",
      "data": {
        "IsAzurite": true,
        "ConnectionSource": "Aspire",
        "UseAzureForMetadata": true,
        "UseAzureForContent": true
      }
    },
    {
      "name": "metadata-store",
      "status": "Healthy"
    },
    {
      "name": "content-store",
      "status": "Healthy"
    }
  ]
}
```

---

## 5. Test SOAP Endpoints

```powershell
$port = 15001

# ClientWebService (should return SOAP fault without proper request)
Invoke-WebRequest -Uri "http://localhost:$port/api/ClientWebService/client.asmx" -Method POST -Body "" -ErrorAction SilentlyContinue

# ServerSyncWebService
Invoke-WebRequest -Uri "http://localhost:$port/api/ServerSyncWebService/ServerSyncWebService.asmx" -Method POST -Body "" -ErrorAction SilentlyContinue

# SimpleAuthWebService
Invoke-WebRequest -Uri "http://localhost:$port/api/SimpleAuthWebService/SimpleAuth.asmx" -Method POST -Body "" -ErrorAction SilentlyContinue
```

**Note**: These will fail with 400/500 (expected without SOAP body), but should NOT return 404.

---

## 6. Run Full Dual Hosting Tests

```powershell
# Test both Azure Functions and Worker Service
.\scripts\test\Test-DualHosting.ps1 -FunctionsPort 15001 -WorkerPort 8080
```

This comprehensive test will:
- ? Test Azure Functions on port 15001
- ? Test Worker Service on port 8080
- ? Compare responses between both hosts
- ? Verify dual hosting consistency

---

## Expected Results Summary

### Build Status: ?
```
Build succeeded with warnings (expected)
- UpdateEngine.Core: 13 warnings (nullability - safe to ignore)
- UpdateEngine: 1 warning (CA2022 - safe to ignore)
```

### Function Count: ?
**After cleanup, functions count should be reduced by ~8-10 functions**

**Removed duplicates**:
- ClientSyncFunctions.cs (2 functions)
- ServerSyncFunctions.cs (3 functions)
- ContentFunctions.cs (2 functions)
- DownloadFunctions.cs (3 functions)
- MetadataQueryFunctions.cs (5 functions)

**Total removed**: ~15 duplicate functions

### Endpoint Tests: ?
All primary endpoints should return 200 OK:
- `/api/health` ? 200 OK
- `/api/sync/status` ? 200 OK
- `/api/metadata/statistics` ? 200 OK
- `/api/content/status` ? 200 OK

### Health Checks: ?
- `azure-storage`: Healthy (with IsAzurite: true)
- `metadata-store`: Healthy
- `content-store`: Healthy or Degraded (if not configured)
- `redis-cache`: Healthy (if enabled)

---

## Troubleshooting

### Services Not Starting

**Problem**: `Azure Functions is NOT running`

**Solution**:
```powershell
cd C:\Users\ansantan\Repos\update-server-server-sync\AppHost\src
dotnet run
```

### Port Conflicts

**Problem**: `Port 15001 already in use`

**Solution**:
```powershell
# Find process using port
Get-NetTCPConnection -LocalPort 15001 -ErrorAction SilentlyContinue | Select-Object OwningProcess

# Kill the process (if needed)
Stop-Process -Id <ProcessId> -Force
```

### Health Check Issues

**Problem**: Azure Storage health check shows "Unhealthy"

**Solution**:
1. Check if Azurite is running in Aspire dashboard
2. Verify connection string in logs
3. Wait 30 seconds for Azurite to fully initialize

---

## Success Criteria

? **All tests must pass**:
1. Build succeeds (warnings OK)
2. All endpoints return 200 OK
3. No duplicate route conflicts in logs
4. WeeklyMaintenance timer registers successfully
5. Azurite detected in health checks
6. SOAP endpoints exist (return 400/500, not 404)

---

## Quick Status Check

One-liner to verify everything is working:

```powershell
$port = 15001
try { 
    $health = Invoke-RestMethod "http://localhost:$port/api/health"
    $azureStorage = $health.checks | Where-Object { $_.name -eq "azure-storage" }
    Write-Host "Status: $($health.status)" -ForegroundColor Green
    Write-Host "Azure Storage: $($azureStorage.status) (IsAzurite: $($azureStorage.data.IsAzurite))" -ForegroundColor Cyan
} catch { 
    Write-Host "Services not running!" -ForegroundColor Red 
}
```

---

**Last Updated**: 2024-11-26  
**Port**: 15001 (Azure Functions via Aspire)  
**Status**: ? Ready for testing
