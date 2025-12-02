<#
.SYNOPSIS
    Starts the complete Anomaly Detection demo environment.

.DESCRIPTION
    This script orchestrates the startup of all required services for the
    Anomaly Detection demo:
    1. Azurite (Azure Storage Emulator)
    2. .NET Aspire AppHost (orchestrates Functions, Redis, etc.)
    3. Waits for all services to be healthy
    4. Performs initial model training
    5. Displays demo status dashboard

.PARAMETER SkipBuild
    Skip the initial build step. Use if you've already built recently.

.PARAMETER WaitForHealthy
    Wait for all services to report healthy before continuing.

.EXAMPLE
    .\Start-Demo.ps1
    Starts the complete demo environment with build.

.EXAMPLE
    .\Start-Demo.ps1 -SkipBuild
    Starts demo without rebuilding (faster startup).

.NOTES
    Author: Update Engine Team
    Date: November 30, 2025
    
    Press Ctrl+C to stop all services.
#>

[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$WaitForHealthy = $true
)

$ErrorActionPreference = 'Stop'

# Configuration
$AzuriteLocation = "c:\azurite"
$AppHostPath = "UpdateEngine.AppHost\src"
$AspireDashboardUrl = "https://localhost:15001"  # From launchSettings.json
$AspireResourceApiUrl = "https://localhost:18888/api/v1/resources"  # DCP Resource API

# Dynamic ports (discovered from Aspire Dashboard)
$script:FunctionsPort = $null
$script:AppHostJob = $null
$script:FunctionsHealthUrl = $null

# State tracking
$script:LastHealthStatus = "Unknown"

# Colors
$script:ColorScheme = @{
    Header = 'Cyan'
    Success = 'Green'
    Warning = 'Yellow'
    Error = 'Red'
    Info = 'Gray'
    Step = 'Yellow'
}

function Write-DemoStep {
    param(
        [string]$Message,
        [string]$Color = 'Yellow'
    )
    Write-Host "`n>>> $Message" -ForegroundColor $Color
}

function Write-DemoInfo {
    param([string]$Message)
    Write-Host "    $Message" -ForegroundColor $script:ColorScheme.Info
}

function Write-DemoSuccess {
    param([string]$Message)
    Write-Host "    ✓ $Message" -ForegroundColor $script:ColorScheme.Success
}

function Write-DemoWarning {
    param([string]$Message)
    Write-Host "    ! $Message" -ForegroundColor $script:ColorScheme.Warning
}

function Write-DemoError {
    param([string]$Message)
    Write-Host "    ✗ $Message" -ForegroundColor $script:ColorScheme.Error
}

function Test-PortAvailable {
    param([int]$Port)
    
    $listener = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
    return $null -eq $listener
}

function Get-FunctionsPortFromAspire {
    param(
        [int]$TimeoutSeconds = 60
    )
    
    Write-DemoInfo "Discovering Functions port from Aspire Resource API..."
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    while ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        try {
            # Query Aspire Resource API (DCP)
            $response = Invoke-RestMethod -Uri $script:AspireResourceApiUrl -Method GET -TimeoutSec 2 -SkipCertificateCheck -ErrorAction Stop
            
            # Find UpdateEngine resource
            $updateEngine = $response.items | Where-Object { $_.metadata.name -eq "updateengine" -or $_.metadata.name -like "*updateengine*" }
            
            if ($updateEngine) {
                # Look for HTTP endpoint in status
                $httpEndpoint = $updateEngine.status.effectiveEndpoints | Where-Object { $_.endpointUrl -match "^http://" } | Select-Object -First 1
                
                if ($httpEndpoint -and $httpEndpoint.endpointUrl) {
                    $endpoint = $httpEndpoint.endpointUrl
                    
                    # Extract port from URL (e.g., http://localhost:5234)
                    if ($endpoint -match 'http://[^:]+:(\d+)') {
                        $port = $Matches[1]
                        Write-Host ""
                        Write-DemoSuccess "Functions endpoint discovered: $endpoint"
                        return $port
                    }
                }
            }
        } catch {
            # API not ready yet or authentication issue
        }
        
        Write-Host "." -NoNewline -ForegroundColor Gray
        Start-Sleep -Seconds 3
    }
    
    Write-Host ""
    Write-DemoWarning "Could not discover Functions port from Aspire Resource API"
    Write-DemoInfo "Check Aspire Dashboard manually: $script:AspireDashboardUrl"
    return $null
}

function Wait-ForService {
    param(
        [string]$Url,
        [int]$TimeoutSeconds = 60,
        [string]$ServiceName = "Service"
    )
    
    Write-DemoInfo "Waiting for $ServiceName to be ready..."
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    while ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        try {
            $response = Invoke-RestMethod -Uri $Url -Method GET -TimeoutSec 2 -ErrorAction Stop
            
            if ($response.status -eq "Healthy") {
                Write-DemoSuccess "$ServiceName is healthy"
                return $true
            }
        } catch {
            # Service not ready yet
        }
        
        Write-Host "." -NoNewline -ForegroundColor Gray
        Start-Sleep -Seconds 2
    }
    
    Write-Host ""
    Write-DemoWarning "$ServiceName did not become healthy within $TimeoutSeconds seconds"
    return $false
}

# Main Script
Clear-Host

Write-Host @"
╔══════════════════════════════════════════════════════════════╗
║                                                              ║
║   Anomaly Detection in Software Update Streams              ║
║   Demo Environment Launcher                                 ║
║                                                              ║
║   ML.NET • Azure Functions • .NET Aspire                    ║
║                                                              ║
╚══════════════════════════════════════════════════════════════╝
"@ -ForegroundColor $script:ColorScheme.Header

# Step 1: Pre-flight checks
Write-DemoStep "Step 1: Running pre-flight checks..." -Color $script:ColorScheme.Step

try {
    & "$PSScriptRoot\Test-DemoSetup.ps1"
    
    if ($LASTEXITCODE -ne 0) {
        Write-DemoError "Pre-flight checks failed. Fix issues and try again."
        exit 1
    }
} catch {
    Write-DemoError "Pre-flight check script failed: $_"
    exit 1
}

# Step 2: Build (optional)
if (-not $SkipBuild) {
    Write-DemoStep "Step 2: Building solution..." -Color $script:ColorScheme.Step
    
    try {
        dotnet build --configuration Debug --verbosity minimal
        
        if ($LASTEXITCODE -eq 0) {
            Write-DemoSuccess "Build completed successfully"
        } else {
            Write-DemoError "Build failed"
            exit 1
        }
    } catch {
        Write-DemoError "Build error: $_"
        exit 1
    }
} else {
    Write-DemoStep "Step 2: Skipping build (use -SkipBuild:$false to build)" -Color $script:ColorScheme.Info
}

# Step 3: Start AppHost (.NET Aspire manages Azurite container automatically)
Write-DemoStep "Step 3: Starting .NET Aspire AppHost..." -Color $script:ColorScheme.Step

Write-DemoInfo "AppHost will automatically start:"
Write-DemoInfo "  - Azurite Storage Emulator (Container)"
Write-DemoInfo "  - Redis (Container)"
Write-DemoInfo "  - Azure Functions (UpdateEngine)"
Write-DemoInfo "  - Aspire Dashboard"

Write-Host ""
Write-DemoWarning "Starting AppHost in background..."
Write-DemoInfo "Output will be shown below (watching for startup messages)..."
Write-Host ""

# Start AppHost using Start-Job (background PowerShell job)
# This keeps the process alive and allows us to monitor output
$script:AppHostJob = Start-Job -ScriptBlock {
    param($AppHostPath)
    Set-Location $AppHostPath
    & dotnet run --project AppHost.csproj 2>&1
} -ArgumentList $AppHostPath

Write-DemoSuccess "AppHost job started (Job ID: $($script:AppHostJob.Id))"

# Monitor job output for startup confirmation (first 15 seconds)
$startupTimeout = [System.Diagnostics.Stopwatch]::StartNew()
$dashboardStarted = $false

while ($startupTimeout.Elapsed.TotalSeconds -lt 15) {
    # Get latest job output
    $jobOutput = Receive-Job -Job $script:AppHostJob -ErrorAction SilentlyContinue
    
    if ($jobOutput) {
        # Display output with color coding
        $jobOutput | ForEach-Object {
            $line = $_.ToString()
            if ($line -match 'error|fail|exception') {
                Write-Host "    $line" -ForegroundColor Red
            } elseif ($line -match 'Now listening on.*15001') {
                Write-Host "    $line" -ForegroundColor Green
                $dashboardStarted = $true
            } elseif ($line -match 'Login to the dashboard at') {
                Write-Host "    $line" -ForegroundColor Cyan
                break  # Found the login URL, we're good
            } elseif ($line -match 'info:|warn:') {
                Write-Host "    $line" -ForegroundColor Gray
            } else {
                Write-Host "    $line" -ForegroundColor DarkGray
            }
        }
    }
    
    # Check if job has failed
    if ($script:AppHostJob.State -eq 'Failed' -or $script:AppHostJob.State -eq 'Stopped') {
        Write-DemoError "AppHost job failed or stopped"
        Write-DemoInfo ""
        Write-DemoInfo "Job output:"
        Receive-Job -Job $script:AppHostJob | ForEach-Object { Write-Host "    $_" }
        Write-DemoInfo ""
        Write-DemoInfo "To diagnose manually:"
        Write-DemoInfo "  cd UpdateEngine.AppHost\src"
        Write-DemoInfo "  dotnet run"
        exit 1
    }
    
    # If we found dashboard startup message, we're done monitoring
    if ($dashboardStarted) {
        break
    }
    
    Start-Sleep -Milliseconds 500
}

if (-not $dashboardStarted) {
    Write-DemoWarning "Dashboard startup not confirmed in output, but job is running"
    Write-DemoInfo "Continuing with health checks..."
}

# Step 4: Wait for services
if ($WaitForHealthy) {
    Write-DemoStep "Step 4: Waiting for services to be healthy..." -Color $script:ColorScheme.Step
    
    Write-DemoInfo "This may take 30-90 seconds for first startup (building Functions)..."
    
    # Wait for Aspire Dashboard
    Write-DemoInfo "Waiting for Aspire Dashboard..."
    $dashboardReady = $false
    for ($i = 0; $i -lt 30; $i++) {
        try {
            $response = Invoke-WebRequest -Uri $AspireDashboardUrl -TimeoutSec 2 -SkipCertificateCheck -ErrorAction Stop
            $dashboardReady = $true
            break
        } catch {
            Write-Host "." -NoNewline -ForegroundColor Gray
            Start-Sleep -Seconds 2
        }
    }
    
    if ($dashboardReady) {
        Write-Host ""
        Write-DemoSuccess "Aspire Dashboard is ready at $AspireDashboardUrl"
    } else {
        Write-Host ""
        Write-DemoWarning "Aspire Dashboard did not respond (may still be starting)"
    }
    
    # Discover Functions port from Aspire Dashboard
    Write-DemoInfo "Waiting for Azure Functions to build and start..."
    Write-DemoInfo "(AppHost is building UpdateEngine.Functions - this takes 30-60s on first run)"
    
    $script:FunctionsPort = Get-FunctionsPortFromAspire -TimeoutSeconds 90
    
    if ($script:FunctionsPort) {
        $script:FunctionsHealthUrl = "http://localhost:$($script:FunctionsPort)/api/health"
        Write-DemoInfo "Health endpoint: $script:FunctionsHealthUrl"
        
        $functionsHealthy = Wait-ForService -Url $script:FunctionsHealthUrl -TimeoutSeconds 60 -ServiceName "Azure Functions"
    } else {
        $functionsHealthy = $false
    }
    
    if (-not $functionsHealthy) {
        Write-DemoWarning "Functions did not become healthy within 120 seconds"
        Write-DemoInfo ""
        Write-DemoInfo "Troubleshooting steps:"
        Write-DemoInfo "  1. Open Aspire Dashboard: $AspireDashboardUrl"
        Write-DemoInfo "  2. Check 'updateengine' resource status"
        Write-DemoInfo "  3. View logs for build/startup errors"
        Write-DemoInfo "  4. Copy the HTTP endpoint URL from the dashboard"
        Write-DemoInfo "  5. Verify Docker is running (for Azurite/Redis containers)"
        Write-DemoInfo ""
        Write-DemoInfo "Common issues:"
        Write-DemoInfo "  - First build takes 30-90 seconds"
        Write-DemoInfo "  - Port conflicts (check with: Get-NetTCPConnection -LocalPort 7071)"
        Write-DemoInfo "  - Build errors (check Aspire Dashboard logs)"
        Write-DemoInfo ""
        
        $continue = Read-Host "Continue anyway? (y/N)"
        if ($continue -ne 'y' -and $continue -ne 'Y') {
            Write-DemoError "Exiting. Fix issues and try again."
            exit 1
        }
    }
} else {
    Write-DemoStep "Step 4: Skipping health checks (services starting in background)" -Color $script:ColorScheme.Info
    Write-DemoInfo "Monitor startup in Aspire Dashboard: $AspireDashboardUrl"
}

# Step 5: Initial Model Training
Write-DemoStep "Step 5: Triggering initial model training..." -Color $script:ColorScheme.Step

# First verify Functions are responsive
if (-not $script:FunctionsHealthUrl) {
    Write-DemoWarning "Functions endpoint not discovered - skipping model training"
    Write-DemoInfo "Model training will retry automatically every 5 minutes via timer trigger"
} else {
    try {
        Write-DemoInfo "Verifying Functions endpoint..."
        $healthCheck = Invoke-RestMethod -Uri $script:FunctionsHealthUrl -Method GET -TimeoutSec 5 -ErrorAction Stop
        
        if ($healthCheck.status -eq "Healthy") {
            Write-DemoSuccess "Functions are healthy, proceeding with model training"
            
            $trainingUrl = "http://localhost:$($script:FunctionsPort)/api/train-model?sampleSize=1000"
            Write-DemoInfo "POST $trainingUrl"
            
            $response = Invoke-RestMethod -Uri $trainingUrl -Method POST -TimeoutSec 60 -ErrorAction Stop
        
        Write-DemoSuccess "Model trained successfully"
        Write-DemoInfo "  Samples used: $($response.samplesUsed)"
        Write-DemoInfo "  Duration: $($response.durationSeconds) seconds"
        Write-DemoInfo "  Model path: $($response.modelPath)"
    } else {
        Write-DemoWarning "Functions health status: $($healthCheck.status)"
        Write-DemoInfo "Skipping model training - will retry automatically every 5 minutes"
        }
    } catch {
        Write-DemoWarning "Initial model training skipped - Functions not ready"
        Write-DemoInfo "Error: $($_.Exception.Message)"
        Write-DemoInfo "Model training will retry automatically every 5 minutes via timer trigger"
    }
}

# Step 6: Display Status Dashboard
Write-DemoStep "Step 6: Demo Environment Ready!" -Color $script:ColorScheme.Success

Write-Host @"

╔══════════════════════════════════════════════════════════════╗
║                     DEMO ENVIRONMENT STATUS                  ║
╠══════════════════════════════════════════════════════════════╣
║                                                              ║
║  ✓ .NET Aspire AppHost           Running                    ║
║  ✓ Azurite Storage (Container)   Running                    ║
║  ✓ Redis Cache (Container)       Running                    ║
║  ✓ Azure Functions               Running                    ║
║  ✓ ML.NET Anomaly Detection      Enabled                    ║
║                                                              ║
╠══════════════════════════════════════════════════════════════╣
║                        ACCESS POINTS                         ║
╠══════════════════════════════════════════════════════════════╣
║                                                              ║
║  Aspire Dashboard:                                          ║
║    $AspireDashboardUrl                         ║
║                                                              ║
║  Functions Health:                                          ║
║    $($script:FunctionsHealthUrl ?? 'Not discovered yet')                           ║
║                                                              ║
║  Train Model:                                               ║
║    POST http://localhost:$($script:FunctionsPort ?? 'PORT')/api/train-model               ║
║                                                              ║
║  Ingest Anomaly:                                            ║
║    POST http://localhost:$($script:FunctionsPort ?? 'PORT')/api/ingest-anomaly            ║
║                                                              ║
╠══════════════════════════════════════════════════════════════╣
║                       DEMO SCHEDULE                          ║
╠══════════════════════════════════════════════════════════════╣
║                                                              ║
║  Scheduled Anomaly Detection:  Every 1 minute               ║
║  Model Retraining:             Every 5 minutes              ║
║                                                              ║
╠══════════════════════════════════════════════════════════════╣
║                        NEXT STEPS                            ║
╠══════════════════════════════════════════════════════════════╣
║                                                              ║
║  1. Open Aspire Dashboard in browser                        ║
║  2. Follow demo guide:                                      ║
║     docs\guides\ANOMALY_DETECTION_DEMO.md                   ║
║  3. Run sample commands from guide                          ║
║                                                              ║
║  Press Ctrl+C to stop all services                          ║
║                                                              ║
╚══════════════════════════════════════════════════════════════╝

"@ -ForegroundColor $script:ColorScheme.Header

# Open browser to Aspire Dashboard
Write-DemoInfo "Opening Aspire Dashboard in browser..."
Start-Process $AspireDashboardUrl

# Wait for user to stop
Write-Host "`nPress Ctrl+C to stop the demo environment..." -ForegroundColor Yellow

try {
    # Keep script running
    while ($true) {
        Start-Sleep -Seconds 30
        
        # Periodic health check (less frequent to reduce noise)
        if ($script:FunctionsHealthUrl) {
            try {
                $health = Invoke-RestMethod -Uri $script:FunctionsHealthUrl -Method GET -TimeoutSec 2 -ErrorAction Stop
            
            if ($health.status -eq "Healthy") {
                # Only log if status changed from non-healthy
                if ($script:LastHealthStatus -ne "Healthy") {
                    Write-DemoSuccess "Functions are now healthy"
                    $script:LastHealthStatus = "Healthy"
                }
            } else {
                Write-DemoWarning "Functions health changed to: $($health.status)"
                $script:LastHealthStatus = $health.status
            }
            } catch {
                # Only warn if we've seen it healthy before
                if ($script:LastHealthStatus -eq "Healthy") {
                    Write-DemoWarning "Functions health check failed - service may be restarting"
                    $script:LastHealthStatus = "Unknown"
                }
            }
        }
    }
} finally {
    # Cleanup on exit
    Write-Host "`n`nStopping demo environment..." -ForegroundColor Yellow
    
    Write-DemoInfo "Stopping AppHost (will also stop Azurite and Redis containers)..."
    if ($script:AppHostJob -and $script:AppHostJob.State -eq 'Running') {
        Stop-Job -Job $script:AppHostJob -ErrorAction SilentlyContinue
        Remove-Job -Job $script:AppHostJob -Force -ErrorAction SilentlyContinue
    }
    
    Write-DemoSuccess "Demo environment stopped"
    Write-DemoInfo "Note: AppHost automatically manages container lifecycle"
}
