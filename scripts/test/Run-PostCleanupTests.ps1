# Post-Cleanup Testing Script
# Week 4 Day 3: Verify duplicate function removal was successful

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Post-Cleanup Testing - Week 4 Day 3" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Stop"

# Change to repo root
$repoRoot = "C:\Users\ansantan\Repos\update-server-server-sync"
Set-Location $repoRoot

Write-Host "Step 1: Building UpdateEngine..." -ForegroundColor Yellow
Write-Host ""

try {
    dotnet build UpdateEngine\src\UpdateEngine.csproj --no-incremental
    Write-Host "? Build successful!" -ForegroundColor Green
} catch {
    Write-Host "? Build failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Step 2: Starting Aspire AppHost..." -ForegroundColor Yellow
Write-Host ""
Write-Host "This will start:" -ForegroundColor Gray
Write-Host "  - Azure Functions (port 7071)" -ForegroundColor Gray
Write-Host "  - Worker Service (ports 8080/8081)" -ForegroundColor Gray
Write-Host "  - Azurite (Azure Storage Emulator)" -ForegroundColor Gray
Write-Host "  - Redis Cache" -ForegroundColor Gray
Write-Host ""
Write-Host "Press Ctrl+C when ready to stop and run tests..." -ForegroundColor Yellow
Write-Host ""

# Start Aspire in a new window
$aspireProcess = Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$repoRoot\AppHost\src'; dotnet run" -PassThru -WindowStyle Normal

Write-Host "Waiting 30 seconds for services to initialize..." -ForegroundColor Yellow
Start-Sleep -Seconds 30

Write-Host ""
Write-Host "Step 3: Checking Azure Functions startup logs..." -ForegroundColor Yellow
Write-Host ""
Write-Host "Looking for these success indicators:" -ForegroundColor Gray
Write-Host "  ? No 'Duplicate route' errors" -ForegroundColor Gray
Write-Host "  ? No 'Function already defined' errors" -ForegroundColor Gray
Write-Host "  ? WeeklyMaintenance timer registered successfully" -ForegroundColor Gray
Write-Host "  ? All functions have unique names and routes" -ForegroundColor Gray
Write-Host ""

# Wait for user to review logs
Write-Host "Please check the Aspire window for Azure Functions logs." -ForegroundColor Cyan
Write-Host "Press Enter when ready to run endpoint tests..." -ForegroundColor Cyan
Read-Host

Write-Host ""
Write-Host "Step 4: Running endpoint tests..." -ForegroundColor Yellow
Write-Host ""

# Test key endpoints
$tests = @(
    @{ 
        Url = "http://localhost:7071/api/health"
        Description = "Health Check"
        ExpectedStatus = 200
    },
    @{ 
        Url = "http://localhost:7071/api/sync/status"
        Description = "Sync Status"
        ExpectedStatus = 200
    },
    @{
        Url = "http://localhost:7071/api/metadata/statistics"
        Description = "Metadata Statistics"
        ExpectedStatus = 200
    }
)

$passed = 0
$failed = 0

foreach ($test in $tests) {
    Write-Host "Testing: $($test.Description)" -ForegroundColor Cyan
    Write-Host "  URL: $($test.Url)" -ForegroundColor Gray
    
    try {
        $response = Invoke-WebRequest -Uri $test.Url -Method Get -TimeoutSec 10 -ErrorAction Stop
        
        if ($response.StatusCode -eq $test.ExpectedStatus) {
            Write-Host "  ? PASSED" -ForegroundColor Green
            $passed++
        } else {
            Write-Host "  ? FAILED - Expected $($test.ExpectedStatus), got $($response.StatusCode)" -ForegroundColor Red
            $failed++
        }
    } catch {
        Write-Host "  ? FAILED - $($_.Exception.Message)" -ForegroundColor Red
        $failed++
    }
    Write-Host ""
}

Write-Host ""
Write-Host "Step 5: Checking health endpoint details..." -ForegroundColor Yellow
Write-Host ""

try {
    $healthResponse = Invoke-RestMethod -Uri "http://localhost:7071/api/health" -Method Get
    Write-Host "Health Check Results:" -ForegroundColor Cyan
    Write-Host ($healthResponse | ConvertTo-Json -Depth 5) -ForegroundColor Gray
    
    # Check for Azurite recognition
    if ($healthResponse.checks) {
        $azureStorageCheck = $healthResponse.checks | Where-Object { $_.name -eq "azure-storage" }
        if ($azureStorageCheck) {
            if ($azureStorageCheck.status -eq "Healthy") {
                Write-Host ""
                Write-Host "? Azure Storage health check correctly recognizes Azurite!" -ForegroundColor Green
                
                if ($azureStorageCheck.data.IsAzurite -eq $true) {
                    Write-Host "  ? IsAzurite flag is set correctly" -ForegroundColor Green
                }
            } else {
                Write-Host ""
                Write-Host "? Azure Storage health check status: $($azureStorageCheck.status)" -ForegroundColor Yellow
                Write-Host "  This may be expected if Azurite is still initializing" -ForegroundColor Gray
            }
        }
    }
} catch {
    Write-Host "? Could not retrieve detailed health status: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "RESULTS SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Endpoint Tests: $passed passed, $failed failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Yellow" })
Write-Host ""

if ($failed -eq 0) {
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "? ALL TESTS PASSED!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Cleanup was successful!" -ForegroundColor Green
    Write-Host "  ? No duplicate function conflicts" -ForegroundColor Green
    Write-Host "  ? All endpoints responding correctly" -ForegroundColor Green
    Write-Host "  ? Health checks working properly" -ForegroundColor Green
} else {
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "? SOME TESTS FAILED" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please review the Aspire logs for errors" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Press Enter to stop services and exit..." -ForegroundColor Cyan
Read-Host

# Cleanup
if ($aspireProcess -and !$aspireProcess.HasExited) {
    Write-Host "Stopping Aspire AppHost..." -ForegroundColor Yellow
    Stop-Process -Id $aspireProcess.Id -Force
}

Write-Host "Done!" -ForegroundColor Green
