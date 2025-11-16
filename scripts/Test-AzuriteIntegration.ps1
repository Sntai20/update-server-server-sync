# Test Aspire + Azurite Integration

Write-Host "🔍 Testing Aspire + Azurite Integration" -ForegroundColor Cyan

# Check if AppHost is running
$aspireProcess = Get-Process -Name "AppHost" -ErrorAction SilentlyContinue
if ($aspireProcess) {
    Write-Host "✅ AppHost is running (PID: $($aspireProcess.Id))" -ForegroundColor Green
} else {
    Write-Host "❌ AppHost is not running. Please start it first." -ForegroundColor Red
    Write-Host "   Run: cd AppHost && dotnet run" -ForegroundColor Yellow
    exit 1
}

# Check Aspire dashboard
try {
    $dashboardResponse = Invoke-WebRequest -Uri "http://localhost:18888" -TimeoutSec 5 -ErrorAction Stop
    Write-Host "✅ Aspire Dashboard is accessible at http://localhost:18888" -ForegroundColor Green
} catch {
    Write-Host "⚠️  Aspire Dashboard might not be ready at http://localhost:18888" -ForegroundColor Yellow
}

# Check if Functions are running
try {
    $functionsResponse = Invoke-WebRequest -Uri "http://localhost:7071/api/HealthCheck" -TimeoutSec 10 -ErrorAction Stop
    Write-Host "✅ UpdateEngine Functions are running on port 7071" -ForegroundColor Green
} catch {
    Write-Host "⚠️  UpdateEngine Functions might not be ready yet" -ForegroundColor Yellow
    Write-Host "   Check Aspire Dashboard for startup status" -ForegroundColor Yellow
}

# Check storage diagnostics
try {
    $storageResponse = Invoke-WebRequest -Uri "http://localhost:7071/api/StorageDiagnostics" -TimeoutSec 10 -ErrorAction Stop
    $storageInfo = $storageResponse.Content | ConvertFrom-Json
    
    Write-Host "📊 Storage Configuration:" -ForegroundColor Cyan
    Write-Host "   Metadata Store Type: $($storageInfo.MetadataStoreType)" -ForegroundColor White
    Write-Host "   Content Store Type: $($storageInfo.ContentStoreType)" -ForegroundColor White
    Write-Host "   Metadata Connection: $($storageInfo.MetadataConnection -replace 'AccountKey=[^;]+', 'AccountKey=***')" -ForegroundColor White
    
    if ($storageInfo.MetadataStoreType -like "*Azure*") {
        Write-Host "✅ Metadata is using Azure Storage (should be Azurite)" -ForegroundColor Green
    } else {
        Write-Host "❌ Metadata is using Local Storage (should be Azure/Azurite)" -ForegroundColor Red
    }
    
    if ($storageInfo.ContentStoreType -like "*Azure*") {
        Write-Host "✅ Content is using Azure Storage (should be Azurite)" -ForegroundColor Green
    } else {
        Write-Host "❌ Content is using Local Storage (should be Azure/Azurite)" -ForegroundColor Red
    }
    
} catch {
    Write-Host "❌ Could not get storage diagnostics" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Check for containers in Azurite
Write-Host "🔍 Checking Azurite containers..." -ForegroundColor Cyan

try {
    # Try to connect to Azurite storage explorer endpoint
    $azuriteResponse = Invoke-WebRequest -Uri "http://localhost:10000" -TimeoutSec 5 -ErrorAction Stop
    Write-Host "✅ Azurite is accessible on port 10000" -ForegroundColor Green
} catch {
    Write-Host "⚠️  Could not connect to Azurite on port 10000" -ForegroundColor Yellow
    Write-Host "   Check if Azurite container is running in Aspire Dashboard" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "🎯 Next Steps:" -ForegroundColor Cyan
Write-Host "1. Open Aspire Dashboard: http://localhost:18888" -ForegroundColor White
Write-Host "2. Check 'Storage' resource for Azurite container status" -ForegroundColor White
Write-Host "3. Trigger a sync: POST http://localhost:7071/api/UniversalSync" -ForegroundColor White
Write-Host "4. Check storage again to see if metadata/content appears" -ForegroundColor White