# Metadata Sync Troubleshooting Guide

## Critical Bug Fixed: Authentication Parameter Swap

**Issue**: The `ClientAuthenticator.cs` had swapped `accountGuid` and `accountName` parameters in the DSS authentication request.

**Location**: `src/microsoft-update-upstream-package-source/Client/ClientAuthenticator.cs` line 192-193

**Fix Applied**:
```csharp
// ? BEFORE (WRONG):
accountGuid = AccountName,      // String assigned to GUID field
accountName = AccountGuid.ToString() // GUID assigned to name field

// ? AFTER (CORRECT):
accountGuid = AccountGuid.ToString(), // GUID ToString() to GUID field
accountName = AccountName         // String to name field
```

This bug would cause authentication failures when syncing from Microsoft Update servers.

## Testing the Sync After Fix

### 1. Restart Azure Functions

After the fix, you need to restart the Azure Functions host:

**Option A: If using Aspire AppHost**
```powershell
# Stop the current process (Ctrl+C)
# Then restart:
cd D:\repos\update-server-server-sync-fork
dotnet run --project AppHost
```

**Option B: If running Functions directly**
```powershell
# Stop the current process (Ctrl+C)
# Then restart:
cd D:\repos\update-server-server-sync-fork\UpdateEngine\src
func start
```

### 2. Test Sync Operation

Once restarted, try the sync again:

```powershell
$syncRequest = @{
    categories = @("Security Updates")
    maxUpdates = 10
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:54383/api/SyncMetadata" `
    -Method Post `
    -Body $syncRequest `
    -ContentType "application/json"
```

### 3. Watch the Logs

You should see output like:

```
info: Authenticating with Microsoft Update server...
info: Authentication successful
info: Retrieving update metadata...
info: Found 150 security updates
info: Syncing first 10 updates...
info: Sync completed successfully
```

## Common Sync Errors and Solutions

### Error: 500 Internal Server Error

**Symptoms:**
```
Invoke-RestMethod : The remote server returned an error: (500) Internal Server Error.
```

**Possible Causes:**

#### 1. Authentication Failure (FIXED ABOVE)
- **Cause**: Swapped parameters in authentication request
- **Solution**: Apply the fix above and restart Functions

#### 2. Network/Firewall Issues
- **Cause**: Cannot reach Microsoft Update servers
- **Check**: 
  ```powershell
  Test-NetConnection -ComputerName sws.update.microsoft.com -Port 443
  ```
- **Solution**: Check firewall, proxy settings, or corporate network restrictions

#### 3. Missing Dependencies
- **Cause**: Missing NuGet packages or assemblies
- **Check**: Look for `FileNotFoundException` or `TypeLoadException` in logs
- **Solution**: 
  ```powershell
  dotnet restore
  dotnet build
  ```

#### 4. Metadata Store Issues
- **Cause**: Storage initialization failed
- **Check**:
  ```powershell
  curl http://localhost:54383/api/StorageDiagnostics
  ```
- **Solution**: Verify storage is initialized (see TROUBLESHOOTING_STORAGE.md)

### Error: "No categories specified"

**Request:**
```powershell
# Missing categories parameter
$syncRequest = @{
    maxUpdates = 10
} | ConvertTo-Json
```

**Solution:**
```powershell
# Include categories
$syncRequest = @{
    categories = @("Security Updates", "Critical Updates")
    maxUpdates = 10
} | ConvertTo-Json
```

### Error: "Invalid category name"

**Valid Categories:**
- "Security Updates"
- "Critical Updates"
- "Feature Packs"
- "Updates"
- "Drivers"

**Example:**
```powershell
# ? Invalid
categories = @("Security")  # Wrong name

# ? Valid
categories = @("Security Updates")  # Correct
```

### Error: Timeout

**Symptoms:**
```
The operation has timed out.
```

**Causes:**
1. Syncing too many updates at once
2. Slow network connection
3. Microsoft Update servers under load

**Solutions:**

**Reduce batch size:**
```powershell
$syncRequest = @{
    categories = @("Security Updates")
    maxUpdates = 5  # Smaller batch
} | ConvertTo-Json
```

**Increase timeout:**
```powershell
Invoke-RestMethod -Uri "http://localhost:54383/api/SyncMetadata" `
    -Method Post `
 -Body $syncRequest `
    -ContentType "application/json" `
    -TimeoutSec 300  # 5 minutes
```

## Debugging Sync Issues

### 1. Enable Detailed Logging

Add to `appsettings.Development.json`:
```json
{
"Logging": {
    "LogLevel": {
    "Default": "Debug",  // Changed from Information
      "Microsoft.PackageGraph": "Trace"  // Add specific namespace
    }
  }
}
```

### 2. Check Function Logs

Watch for these specific messages:

**Authentication Phase:**
```
info: Authenticating with upstream server
debug: Getting authentication info from https://sws.update.microsoft.com
debug: Retrieved auth plugin info
debug: Getting authorization cookie from DSS
debug: Got authorization cookie, PluginId: {id}
debug: Getting access cookie
info: Authentication successful
```

**Sync Phase:**
```
info: Starting metadata sync
info: Categories: Security Updates
info: Max updates: 10
debug: Querying server for updates...
info: Found 150 matching updates
info: Syncing metadata for 10 updates...
debug: Synced update {guid}
info: Sync completed: 10 updates added
```

### 3. Test Authentication Separately

Create a test script to verify authentication works:

```powershell
# Test-Authentication.ps1
$testAuth = @{
    endpoint = "https://sws.update.microsoft.com"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:54383/api/TestAuthentication" `
    -Method Post `
    -Body $testAuth `
    -ContentType "application/json"
```

### 4. Check Azure Storage After Sync

After a successful sync:

```powershell
# View synced packages
curl http://localhost:54383/api/GetStoreStatus

# Check storage diagnostics
curl http://localhost:54383/api/StorageDiagnostics
```

**Expected Output:**
```json
{
  "Storage": {
    "MetadataStore": {
      "PackageCount": 10,  // Should increase after sync
      "Initialized": true
    }
  }
}
```

### 5. Verify in Azure Storage Explorer

1. Open **Azure Storage Explorer**
2. Navigate to **Emulator** > **Blob Containers** > **metadata**
3. You should see new blobs:
   - `identities-index` (updated)
   - `metadata` (larger size)
   - `index/` (new index files)

## Sync Best Practices

### Start Small
```powershell
# First sync - just 1 update to test
$syncRequest = @{
    categories = @("Security Updates")
    maxUpdates = 1
} | ConvertTo-Json
```

### Incremental Syncing
```powershell
# Sync in batches
foreach ($count in 1, 5, 10, 50) {
    $syncRequest = @{
        categories = @("Security Updates")
 maxUpdates = $count
    } | ConvertTo-Json
    
    Write-Host "Syncing $count updates..."
    Invoke-RestMethod -Uri "http://localhost:54383/api/SyncMetadata" `
      -Method Post `
      -Body $syncRequest `
        -ContentType "application/json"
 
    Start-Sleep -Seconds 2
}
```

### Use Specific Categories
```powershell
# More targeted sync
$syncRequest = @{
    categories = @("Security Updates", "Critical Updates")
    maxUpdates = 20
 classification = "Important"  # If supported
} | ConvertTo-Json
```

## Post-Sync Verification

### Check Package Count
```powershell
$status = Invoke-RestMethod -Uri "http://localhost:54383/api/GetStoreStatus"
Write-Host "Total packages: $($status.PackageCount)"
```

### Query Synced Updates
```powershell
$query = @{
    filter = "security"
    take = 10
} | ConvertTo-Json

$results = Invoke-RestMethod -Uri "http://localhost:54383/api/QueryMetadata" `
    -Method Post `
    -Body $query `
    -ContentType "application/json"

Write-Host "Found $($results.Count) security updates"
```

### Verify Storage Size
```powershell
$diag = Invoke-RestMethod -Uri "http://localhost:54383/api/StorageDiagnostics"
Write-Host "Metadata packages: $($diag.Storage.MetadataStore.PackageCount)"
Write-Host "Content queued: $($diag.Storage.ContentStore.QueuedCount)"
```

## Complete Test Workflow

Save as `Test-MetadataSync.ps1`:

```powershell
Write-Host "=== Testing Metadata Sync ===" -ForegroundColor Cyan

# 1. Check health before sync
Write-Host "`n1. Pre-sync health check..." -ForegroundColor Yellow
$health = Invoke-RestMethod -Uri "http://localhost:54383/api/HealthCheck"
Write-Host "   System healthy: $($health.IsHealthy)" -ForegroundColor Green

# 2. Get current package count
Write-Host "`n2. Getting current package count..." -ForegroundColor Yellow
$beforeDiag = Invoke-RestMethod -Uri "http://localhost:54383/api/StorageDiagnostics"
$beforeCount = $beforeDiag.Storage.MetadataStore.PackageCount
Write-Host "   Current packages: $beforeCount" -ForegroundColor Cyan

# 3. Sync 5 security updates
Write-Host "`n3. Syncing 5 security updates..." -ForegroundColor Yellow
$syncRequest = @{
    categories = @("Security Updates")
    maxUpdates = 5
} | ConvertTo-Json

try {
    $syncResult = Invoke-RestMethod -Uri "http://localhost:54383/api/SyncMetadata" `
        -Method Post `
    -Body $syncRequest `
        -ContentType "application/json"
    
    Write-Host "   ? Sync completed!" -ForegroundColor Green
    
} catch {
    Write-Host "   ? Sync failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "`nCheck the Azure Functions logs for details." -ForegroundColor Yellow
    exit 1
}

# 4. Verify package count increased
Write-Host "`n4. Verifying sync results..." -ForegroundColor Yellow
Start-Sleep -Seconds 2  # Wait for indexing
$afterDiag = Invoke-RestMethod -Uri "http://localhost:54383/api/StorageDiagnostics"
$afterCount = $afterDiag.Storage.MetadataStore.PackageCount
$newPackages = $afterCount - $beforeCount

Write-Host "   Packages before: $beforeCount" -ForegroundColor Cyan
Write-Host "   Packages after: $afterCount" -ForegroundColor Cyan
Write-Host "   New packages: $newPackages" -ForegroundColor Green

# 5. Post-sync health check
Write-Host "`n5. Post-sync health check..." -ForegroundColor Yellow
$healthAfter = Invoke-RestMethod -Uri "http://localhost:54383/api/HealthCheck"
Write-Host "   System healthy: $($healthAfter.IsHealthy)" -ForegroundColor Green

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan
if ($newPackages -gt 0) {
    Write-Host "? SUCCESS: $newPackages packages synced!" -ForegroundColor Green
} else {
    Write-Host "? WARNING: No new packages added (may already be synced)" -ForegroundColor Yellow
}
```

Run with:
```powershell
.\Test-MetadataSync.ps1
```

## Summary

1. ? **Bug Fixed**: Authentication parameter swap corrected
2. ?? **Restart Required**: Restart Azure Functions to apply the fix
3. ?? **Test Again**: Try the sync operation
4. ?? **Monitor**: Watch logs and storage diagnostics
5. ?? **Verify**: Check package count and storage

The authentication bug fix should resolve the 500 error. After restarting, the sync should work correctly!
