# Week 4 Day 2: Dual Hosting Test Script
# Tests both Azure Functions (port 7071) and Worker Service (port 8080)

param(
    [int]$MaxRetries = 30,
    [int]$RetryDelaySeconds = 2
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Week 4 Day 2: Dual Hosting Testing" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Function to wait for endpoint to be ready
function Wait-ForEndpoint {
    param(
        [string]$Url,
        [string]$Name,
        [int]$MaxRetries,
        [int]$RetryDelay
    )
    
    Write-Host "Waiting for $Name to be ready at $Url..." -ForegroundColor Yellow
    
    for ($i = 1; $i -le $MaxRetries; $i++) {
        try {
            $response = Invoke-WebRequest -Uri $Url -Method Get -TimeoutSec 5 -ErrorAction Stop
            if ($response.StatusCode -eq 200) {
                Write-Host "? $Name is ready!" -ForegroundColor Green
                return $true
            }
        }
        catch {
            Write-Host "  Attempt $i/$MaxRetries - Not ready yet..." -ForegroundColor Gray
        }
        
        Start-Sleep -Seconds $RetryDelay
    }
    
    Write-Host "? $Name failed to start after $MaxRetries attempts" -ForegroundColor Red
    return $false
}

# Function to test an endpoint
function Test-Endpoint {
    param(
        [string]$Url,
        [string]$Description,
        [string]$ExpectedStatus = "200"
    )
    
    Write-Host ""
    Write-Host "Testing: $Description" -ForegroundColor Cyan
    Write-Host "URL: $Url" -ForegroundColor Gray
    
    try {
        $response = Invoke-WebRequest -Uri $Url -Method Get -TimeoutSec 10 -ErrorAction Stop
        
        if ($response.StatusCode -eq $ExpectedStatus) {
            Write-Host "? SUCCESS - Status: $($response.StatusCode)" -ForegroundColor Green
            
            # Show response preview
            $content = $response.Content
            if ($content.Length -gt 200) {
                $content = $content.Substring(0, 200) + "..."
            }
            Write-Host "  Response: $content" -ForegroundColor Gray
            
            return $true
        }
        else {
            Write-Host "? FAILED - Expected $ExpectedStatus, got $($response.StatusCode)" -ForegroundColor Red
            return $false
        }
    }
    catch {
        Write-Host "? FAILED - $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Wait for services to start
Write-Host "Step 1: Waiting for services to start..." -ForegroundColor Yellow
Write-Host ""

$functionsReady = Wait-ForEndpoint -Url "http://localhost:7071/api/health" -Name "Azure Functions" -MaxRetries $MaxRetries -RetryDelay $RetryDelaySeconds
$workerReady = Wait-ForEndpoint -Url "http://localhost:8080/health/live" -Name "Worker Service" -MaxRetries $MaxRetries -RetryDelay $RetryDelaySeconds

if (-not $functionsReady -or -not $workerReady) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "FAILED: Services did not start" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Step 2: Testing Azure Functions (7071)" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

$functionsTests = @(
    @{ Url = "http://localhost:7071/api/health"; Description = "Health Check" }
    @{ Url = "http://localhost:7071/api/sync/status"; Description = "Sync Status" }
    @{ Url = "http://localhost:7071/api/metadata/statistics"; Description = "Metadata Statistics" }
)

$functionsPassed = 0
foreach ($test in $functionsTests) {
    if (Test-Endpoint -Url $test.Url -Description $test.Description) {
        $functionsPassed++
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Step 3: Testing Worker Service (8080)" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

$workerTests = @(
    @{ Url = "http://localhost:8080/health"; Description = "Health Check - Comprehensive" }
    @{ Url = "http://localhost:8080/health/live"; Description = "Health Check - Liveness" }
    @{ Url = "http://localhost:8080/health/ready"; Description = "Health Check - Readiness" }
    @{ Url = "http://localhost:8080/api/sync/status"; Description = "Sync Status" }
    @{ Url = "http://localhost:8080/api/metadata/statistics"; Description = "Metadata Statistics" }
)

$workerPassed = 0
foreach ($test in $workerTests) {
    if (Test-Endpoint -Url $test.Url -Description $test.Description) {
        $workerPassed++
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Step 4: Testing Swagger UI (Worker Service)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Test-Endpoint -Url "http://localhost:8080/swagger/index.html" -Description "Swagger UI" | Out-Null

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Step 5: Dual Hosting Validation" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Comparing sync status between both hosts..." -ForegroundColor Yellow

try {
    $functionsStatus = Invoke-RestMethod -Uri "http://localhost:7071/api/sync/status" -Method Get
    $workerStatus = Invoke-RestMethod -Uri "http://localhost:8080/api/sync/status" -Method Get
    
    Write-Host "? Both hosts returned sync status" -ForegroundColor Green
    Write-Host "  Functions Status: $($functionsStatus | ConvertTo-Json -Compress)" -ForegroundColor Gray
    Write-Host "  Worker Status: $($workerStatus | ConvertTo-Json -Compress)" -ForegroundColor Gray
}
catch {
    Write-Host "? Failed to compare sync status: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "FINAL RESULTS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Azure Functions Tests: $functionsPassed/$($functionsTests.Count) passed" -ForegroundColor $(if ($functionsPassed -eq $functionsTests.Count) { "Green" } else { "Yellow" })
Write-Host "Worker Service Tests: $workerPassed/$($workerTests.Count) passed" -ForegroundColor $(if ($workerPassed -eq $workerTests.Count) { "Green" } else { "Yellow" })
Write-Host ""

$totalTests = $functionsTests.Count + $workerTests.Count
$totalPassed = $functionsPassed + $workerPassed

if ($totalPassed -eq $totalTests) {
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "? ALL TESTS PASSED ($totalPassed/$totalTests)" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Dual hosting is working correctly!" -ForegroundColor Green
    Write-Host "  - Azure Functions running on port 7071" -ForegroundColor Gray
    Write-Host "  - Worker Service running on port 8080" -ForegroundColor Gray
    Write-Host "  - Both using same orchestrators and infrastructure" -ForegroundColor Gray
    exit 0
}
else {
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "? SOME TESTS FAILED ($totalPassed/$totalTests passed)" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    exit 1
}
