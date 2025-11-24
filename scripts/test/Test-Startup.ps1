#!/usr/bin/env pwsh

# Quick startup test for UpdateEngine

param(
    [int]$TimeoutSeconds = 30,
    [switch]$UseAppHost,
    [string]$Configuration = "Debug"
)

Write-Host "=== Testing UpdateEngine Startup ===" -ForegroundColor Green

$ErrorActionPreference = "Continue"

if ($UseAppHost) {
    Write-Host "Testing with AppHost..." -ForegroundColor Yellow
    $workingDir = "AppHost"
    $startCommand = "dotnet"
    $startArgs = @("run", "--configuration", $Configuration)
    $port = 7071
} else {
    Write-Host "Testing with Azure Functions CLI..." -ForegroundColor Yellow
    $workingDir = "UpdateEngine.Functions/src"
    $startCommand = "func"
    $startArgs = @("start", "--port", "7071")
    $port = 7071
}

$currentDir = Get-Location

Write-Host "Using configuration: $Configuration" -ForegroundColor Cyan

try {
    Set-Location $workingDir
    
    # Ensure we have a Debug build before starting
    if ($UseAppHost) {
        Write-Host "Building AppHost project in $Configuration configuration..." -ForegroundColor Yellow
        try {
            $buildResult = dotnet build --configuration $Configuration --verbosity minimal
            if ($LASTEXITCODE -ne 0) {
                Write-Host "✗ Build failed for AppHost project" -ForegroundColor Red
                return
            }
            Write-Host "✓ AppHost project built successfully" -ForegroundColor Green
        } catch {
            Write-Host "✗ Error building AppHost project: $($_.Exception.Message)" -ForegroundColor Red
            return
        }
    } else {
        Write-Host "Building Functions project in $Configuration configuration..." -ForegroundColor Yellow
        try {
            $buildResult = dotnet build --configuration $Configuration --verbosity minimal
            if ($LASTEXITCODE -ne 0) {
                Write-Host "✗ Build failed for Functions project" -ForegroundColor Red
                return
            }
            Write-Host "✓ Functions project built successfully" -ForegroundColor Green
        } catch {
            Write-Host "✗ Error building Functions project: $($_.Exception.Message)" -ForegroundColor Red
            return
        }
    }
    
    Write-Host "Starting application in $workingDir..." -ForegroundColor Cyan
    Write-Host "Command: $startCommand $($startArgs -join ' ')" -ForegroundColor Gray
    
    # Start the process
    $process = Start-Process $startCommand -ArgumentList $startArgs -PassThru -NoNewWindow
    
    Write-Host "Process started with PID: $($process.Id)" -ForegroundColor Cyan
    Write-Host "Waiting for startup (timeout: ${TimeoutSeconds}s)..." -ForegroundColor Yellow
    
    $startTime = Get-Date
    $healthCheckSuccess = $false
    $dotCount = 0
    
    # Wait for the service to be ready
    while ((Get-Date) -lt $startTime.AddSeconds($TimeoutSeconds)) {
        Start-Sleep -Seconds 2
        
        try {
            $response = Invoke-RestMethod -Uri "http://localhost:$port/api/HealthCheck" -Method Get -TimeoutSec 5 -ErrorAction SilentlyContinue
            if ($response) {
                $healthCheckSuccess = $true
                break
            }
        } catch {
            # Continue waiting
        }
        
        # Check if process is still running
        if ($process.HasExited) {
            Write-Host ""
            Write-Host "✗ Process exited unexpectedly with code: $($process.ExitCode)" -ForegroundColor Red
            
            # Try to get some error information
            if ($UseAppHost) {
                Write-Host "Check the AppHost logs for error details." -ForegroundColor Yellow
            } else {
                Write-Host "Check the Functions runtime logs for error details." -ForegroundColor Yellow
            }
            break
        }
        
        # Show progress dots
        $dotCount++
        if ($dotCount % 5 -eq 0) {
            Write-Host " ($($dotCount * 2)s)" -ForegroundColor Gray
        } else {
            Write-Host "." -NoNewline -ForegroundColor Gray
        }
    }
    
    Write-Host ""
    
    if ($healthCheckSuccess) {
        Write-Host "✓ Application started successfully!" -ForegroundColor Green
        Write-Host "✓ Health check endpoint responding" -ForegroundColor Green
        
        # Test a few more endpoints
        $endpoints = @(
            @{ Name = "Store Status"; Url = "http://localhost:$port/api/StoreStatus" },
            @{ Name = "Available Filters"; Url = "http://localhost:$port/api/AvailableFilters" }
        )
        
        Write-Host "`nTesting additional endpoints..." -ForegroundColor Yellow
        
        foreach ($endpoint in $endpoints) {
            try {
                $response = Invoke-RestMethod -Uri $endpoint.Url -Method Get -TimeoutSec 10 -ErrorAction SilentlyContinue
                if ($response) {
                    Write-Host "✓ $($endpoint.Name) endpoint responding" -ForegroundColor Green
                    
                    # Show some response details for debugging
                    if ($endpoint.Name -eq "Store Status" -and $response.PSObject.Properties.Count -gt 0) {
                        Write-Host "  Store has $($response.TotalPackageCount) total packages" -ForegroundColor Gray
                    }
                } else {
                    Write-Host "⚠ $($endpoint.Name) endpoint returned empty response" -ForegroundColor Yellow
                }
            } catch {
                Write-Host "⚠ $($endpoint.Name) endpoint error: $($_.Exception.Message)" -ForegroundColor Yellow
            }
        }
        
        Write-Host "`nApplication is running successfully in $Configuration mode!" -ForegroundColor Green
        Write-Host "Access the application at: http://localhost:$port" -ForegroundColor Cyan
        
        # Show available endpoints
        Write-Host "`nAvailable endpoints:" -ForegroundColor Cyan
        $availableEndpoints = @(
            "GET  /api/HealthCheck - System health status",
            "GET  /api/StoreStatus - Metadata store statistics", 
            "GET  /api/AvailableFilters - Available product/classification filters",
            "POST /api/SyncMetadata - Manual metadata synchronization",
            "POST /api/SyncContent - Manual content synchronization",
            "POST /api/QueryMetadata - Query stored metadata",
            "POST /api/MatchDrivers - Driver matching service"
        )
        
        foreach ($endpoint in $availableEndpoints) {
            Write-Host "  $endpoint" -ForegroundColor White
        }
        
        if ($UseAppHost) {
            Write-Host "`nAspire Dashboard: http://localhost:15888" -ForegroundColor Cyan
        }
        
        Write-Host "`nPress any key to stop the application..."
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        
    } else {
        Write-Host "✗ Application did not start within timeout period" -ForegroundColor Red
        
        if (-not $process.HasExited) {
            Write-Host "Process is still running but health check failed" -ForegroundColor Yellow
            Write-Host "This might indicate a configuration issue or missing dependencies" -ForegroundColor Yellow
        }
        
        # Provide debugging suggestions
        Write-Host "`nDebugging suggestions:" -ForegroundColor Yellow
        Write-Host "1. Check if all required packages are restored: dotnet restore" -ForegroundColor White
        Write-Host "2. Verify the project builds: dotnet build --configuration $Configuration" -ForegroundColor White
        Write-Host "3. Check for missing Azure Functions Core Tools: func --version" -ForegroundColor White
        Write-Host "4. Verify local.settings.json configuration" -ForegroundColor White
        Write-Host "5. Check if port $port is available" -ForegroundColor White
    }
    
} finally {
    # Cleanup
    if ($process -and -not $process.HasExited) {
        Write-Host "Stopping application..." -ForegroundColor Yellow
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        
        # Wait a moment for graceful shutdown
        Start-Sleep -Seconds 2
        
        Write-Host "✓ Application stopped" -ForegroundColor Green
    }
    
    Set-Location $currentDir
}

Write-Host "`nStartup test completed." -ForegroundColor Green

# Show final summary
if ($healthCheckSuccess) {
    Write-Host "`n🎉 SUCCESS: Application started and responded correctly in $Configuration configuration!" -ForegroundColor Green
} else {
    Write-Host "`n❌ FAILED: Application did not start properly" -ForegroundColor Red
    Write-Host "Try running with more verbose output or check the logs for details" -ForegroundColor Yellow
}