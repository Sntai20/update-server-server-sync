# Test-SyncWithDiagnostics.ps1
# Comprehensive sync test with detailed error capture

param(
    [int]$Port = 49172,
    [int]$MaxUpdates = 1,  # Start with just 1 update
    [string[]]$Categories = @("Security Updates")
)

Write-Host "=== Microsoft Update Sync Diagnostic Test ===" -ForegroundColor Cyan
Write-Host "Port: $Port" -ForegroundColor Yellow
Write-Host "Categories: $($Categories -join ', ')" -ForegroundColor Yellow
Write-Host "Max Updates: $MaxUpdates`n" -ForegroundColor Yellow

# 1. Check if Functions are running
Write-Host "1. Checking if Azure Functions are accessible..." -ForegroundColor Green
try {
    $health = Invoke-RestMethod -Uri "http://localhost:$Port/api/HealthCheck" -ErrorAction Stop
 Write-Host "   ? Functions are running" -ForegroundColor Green
    Write-Host "   System Healthy: $($health.IsHealthy)`n" -ForegroundColor Cyan
} catch {
  Write-Host "   ? Cannot connect to Functions on port $Port" -ForegroundColor Red
    Write-Host "   Make sure Azure Functions are running!" -ForegroundColor Yellow
    exit 1
}

# 2. Check storage is initialized
Write-Host "2. Verifying storage initialization..." -ForegroundColor Green
try {
 $storageDiag = Invoke-RestMethod -Uri "http://localhost:$Port/api/StorageDiagnostics"
    Write-Host "   Metadata Store Initialized: $($storageDiag.Storage.MetadataStore.Initialized)" -ForegroundColor Cyan
    Write-Host "   Current Package Count: $($storageDiag.Storage.MetadataStore.PackageCount)" -ForegroundColor Cyan
    Write-Host " Content Store Configured: $($storageDiag.Storage.ContentStore.Configured)`n" -ForegroundColor Cyan
} catch {
    Write-Host "   ? Storage diagnostics failed" -ForegroundColor Red
}

# 3. Attempt sync with detailed error capture
Write-Host "3. Attempting metadata sync..." -ForegroundColor Green
$syncRequest = @{
  categories = $Categories
    maxUpdates = $MaxUpdates
} | ConvertTo-Json

Write-Host "   Request: $syncRequest" -ForegroundColor DarkGray

try {
    $result = Invoke-RestMethod -Uri "http://localhost:$Port/api/SyncMetadata" `
        -Method Post `
        -Body $syncRequest `
        -ContentType "application/json" `
    -ErrorAction Stop
    
    Write-Host " ? Sync completed successfully!" -ForegroundColor Green
    Write-Host "`nResult:" -ForegroundColor Cyan
    $result | ConvertTo-Json -Depth 10 | Write-Host
    
} catch {
  Write-Host "   ? Sync failed!" -ForegroundColor Red
    Write-Host "`nError Details:" -ForegroundColor Yellow
    Write-Host "   Status Code: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Red
  Write-Host "Status Description: $($_.Exception.Response.StatusDescription)" -ForegroundColor Red
    Write-Host "   Exception Message: $($_.Exception.Message)" -ForegroundColor Red
    
    # Try to get error response body
    if ($_.Exception.Response) {
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
       $responseBody = $reader.ReadToEnd()
    $reader.Close()
            
     Write-Host "`nServer Response Body:" -ForegroundColor Yellow
  Write-Host $responseBody -ForegroundColor Red
        } catch {
        Write-Host "   Could not read response body" -ForegroundColor DarkGray
   }
    }
    
    Write-Host "`n? Check the Azure Functions console output for detailed error logs!" -ForegroundColor Yellow
    Write-Host "   Look for lines containing 'error', 'exception', or stack traces.`n" -ForegroundColor Yellow
}

# 4. Post-sync storage check
Write-Host "4. Post-sync storage check..." -ForegroundColor Green
try {
    $storageDiagAfter = Invoke-RestMethod -Uri "http://localhost:$Port/api/StorageDiagnostics"
    Write-Host "   Package Count After: $($storageDiagAfter.Storage.MetadataStore.PackageCount)" -ForegroundColor Cyan
} catch {
    Write-Host "   ? Storage check failed" -ForegroundColor Red
}

Write-Host "`n=== Diagnostic Test Complete ===" -ForegroundColor Cyan
