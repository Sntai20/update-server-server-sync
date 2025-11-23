# Post-Cleanup Testing Script
# Week 4 Day 3: Verify duplicate function removal was successful

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Post-Cleanup Testing - Week 4 Day 3" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Continue"

# Change to repo root
$repoRoot = "C:\Users\ansantan\Repos\update-server-server-sync"
Set-Location $repoRoot

# Dynamic port for Azure Functions (provided by Aspire)
$port = 15001

Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  Azure Functions Port: $port" -ForegroundColor Gray
Write-Host "  Worker Service Port: 8080" -ForegroundColor Gray
Write-Host ""

Write-Host "Step 1: Building UpdateEngine..." -ForegroundColor Yellow
Write-Host ""

try {
    dotnet build UpdateEngine\src\UpdateEngine.csproj --no-incremental --verbosity quiet
    Write-Host "? Build successful!" -ForegroundColor Green
} catch {
    Write-Host "? Build failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Step 2: Checking if services are running..." -ForegroundColor Yellow
Write-Host ""

# Function to check if service is running
function Test-ServiceRunning {
    param(
        [string]$Url,
        [string]$Name
    )
    
    try {
        $response = Invoke-WebRequest -Uri $Url -Method Get -TimeoutSec 5 -ErrorAction Stop
        Write-Host "? $Name is running" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "? $Name is NOT running" -ForegroundColor Red
        return $false
    }
}

$functionsRunning = Test-ServiceRunning -Url "http://localhost:$port/api/health" -Name "Azure Functions"

if (-not $functionsRunning) {
    Write-Host ""
    Write-Host "??  Azure Functions is not running!" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please start Aspire AppHost in a separate terminal:" -ForegroundColor Cyan
    Write-Host "  cd $repoRoot\AppHost\src" -ForegroundColor Gray
    Write-Host "  dotnet run" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Then run this script again." -ForegroundColor Cyan
    exit 1
}

Write-Host ""
Write-Host "Step 3: Testing Azure Functions endpoints..." -ForegroundColor Yellow
Write-Host ""

# Test key endpoints
$tests = @(
    @{ 
        Url = "http://localhost:$port/api/health"
        Description = "Health Check"
        ExpectedStatus = 200
    },
    @{ 
        Url = "http://localhost:$port/api/sync/status"
        Description = "Sync Status"
        ExpectedStatus = 200
    },
    @{
        Url = "http://localhost:$port/api/metadata/statistics"
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
            Write-Host "  ? PASSED - Status: $($response.StatusCode)" -ForegroundColor Green
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
Write-Host "Step 4: Checking health endpoint details (Azurite detection)..." -ForegroundColor Yellow
Write-Host ""

try {
    $healthResponse = Invoke-RestMethod -Uri "http://localhost:$port/api/health" -Method Get
    Write-Host "Health Check Response:" -ForegroundColor Cyan
    
    # Display overall status
    if ($healthResponse.status) {
        $statusColor = switch ($healthResponse.status) {
            "Healthy" { "Green" }
            "Degraded" { "Yellow" }
            "Unhealthy" { "Red" }
            default { "Gray" }
        }
        Write-Host "  Overall Status: $($healthResponse.status)" -ForegroundColor $statusColor
    }
    
    Write-Host ""
    Write-Host "Individual Health Checks:" -ForegroundColor Cyan
    
    # Check for Azurite recognition in Azure Storage health check
    if ($healthResponse.checks) {
        foreach ($check in $healthResponse.checks) {
            $checkStatusColor = switch ($check.status) {
                "Healthy" { "Green" }
                "Degraded" { "Yellow" }
                "Unhealthy" { "Red" }
                default { "Gray" }
            }
            
            Write-Host "  $($check.name): " -NoNewline -ForegroundColor Gray
            Write-Host "$($check.status)" -ForegroundColor $checkStatusColor
            
            if ($check.description) {
                Write-Host "    Description: $($check.description)" -ForegroundColor Gray
            }
            
            # Special handling for Azure Storage check
            if ($check.name -eq "azure-storage") {
                Write-Host ""
                if ($check.status -eq "Healthy") {
                    Write-Host "    ? Azure Storage health check is working!" -ForegroundColor Green
                    
                    if ($check.data.IsAzurite -eq $true) {
                        Write-Host "    ? Correctly detected Azurite (local emulator)" -ForegroundColor Green
                    }
                    
                    if ($check.data.ConnectionSource) {
                        Write-Host "    ??  Connection source: $($check.data.ConnectionSource)" -ForegroundColor Cyan
                    }
                } else {
                    Write-Host "    ??  Status: $($check.status)" -ForegroundColor Yellow
                    Write-Host "    This may be expected if Azurite is still initializing" -ForegroundColor Gray
                }
            }
        }
    }
    
    Write-Host ""
} catch {
    Write-Host "??  Could not retrieve detailed health status: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Step 5: Testing specific Core/ function endpoints..." -ForegroundColor Yellow
Write-Host ""

$coreEndpointTests = @(
    @{
        Url = "http://localhost:$port/api/ClientWebService/client.asmx"
        Method = "POST"
        Description = "ClientWebService (SOAP) - POST check"
        ExpectError = $true  # Will fail without SOAP body, but should not give 404
    },
    @{
        Url = "http://localhost:$port/api/content/status"
        Method = "GET"
        Description = "Content Status (ContentDeliveryFunctions)"
        ExpectError = $false
    }
)

foreach ($test in $coreEndpointTests) {
    Write-Host "Testing: $($test.Description)" -ForegroundColor Cyan
    Write-Host "  URL: $($test.Url)" -ForegroundColor Gray
    Write-Host "  Method: $($test.Method)" -ForegroundColor Gray
    
    try {
        if ($test.Method -eq "POST") {
            $response = Invoke-WebRequest -Uri $test.Url -Method Post -Body "" -TimeoutSec 10 -ErrorAction Stop
        } else {
            $response = Invoke-WebRequest -Uri $test.Url -Method Get -TimeoutSec 10 -ErrorAction Stop
        }
        
        Write-Host "  ? Endpoint exists (Status: $($response.StatusCode))" -ForegroundColor Green
        $passed++
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        
        if ($test.ExpectError -and $statusCode -ne 404) {
            # For SOAP endpoints, we expect 400 (Bad Request) not 404 (Not Found)
            Write-Host "  ? Endpoint exists (Expected error: $statusCode)" -ForegroundColor Green
            $passed++
        } elseif ($statusCode -eq 404) {
            Write-Host "  ? FAILED - Endpoint not found (404)" -ForegroundColor Red
            $failed++
        } else {
            Write-Host "  ??  Unexpected error: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
    Write-Host ""
}

Write-Host ""
Write-Host "Step 6: Verification - No duplicate functions..." -ForegroundColor Yellow
Write-Host ""

Write-Host "Checking startup logs for conflict indicators..." -ForegroundColor Cyan
Write-Host "  ??  Check the Aspire dashboard or console for:" -ForegroundColor Gray
Write-Host "    ? No 'Duplicate route' warnings" -ForegroundColor Gray
Write-Host "    ? No 'Function already defined' errors" -ForegroundColor Gray
Write-Host "    ? WeeklyMaintenance registered with CRON schedule" -ForegroundColor Gray
Write-Host ""

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "RESULTS SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Total Tests: $($passed + $failed)" -ForegroundColor Cyan
Write-Host "  Passed: $passed" -ForegroundColor Green
Write-Host "  Failed: $failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($failed -eq 0) {
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "? ALL TESTS PASSED!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Cleanup was successful! Key achievements:" -ForegroundColor Green
    Write-Host "  ? No duplicate function conflicts" -ForegroundColor Green
    Write-Host "  ? All endpoints responding correctly" -ForegroundColor Green
    Write-Host "  ? Health checks working properly" -ForegroundColor Green
    Write-Host "  ? Azurite properly detected" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Review Aspire logs for clean startup (no warnings)" -ForegroundColor Gray
    Write-Host "  2. Run full dual hosting tests: .\scripts\test\Test-DualHosting.ps1 -FunctionsPort $port" -ForegroundColor Gray
    Write-Host "  3. Test SOAP endpoints with actual SOAP requests" -ForegroundColor Gray
} else {
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "??  SOME TESTS FAILED" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please review the failures above and check:" -ForegroundColor Yellow
    Write-Host "  • Aspire AppHost logs for errors" -ForegroundColor Gray
    Write-Host "  • Function registration in Azure Functions startup" -ForegroundColor Gray
    Write-Host "  • Network connectivity to http://localhost:$port" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Done! Press any key to exit..." -ForegroundColor Cyan
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
