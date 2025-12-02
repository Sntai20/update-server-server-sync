<#
.SYNOPSIS
    Quick verification script for Anomaly Detection demo setup.

.DESCRIPTION
    This script verifies all prerequisites and configurations are in place
    for running the Anomaly Detection demo using AppHost and ML.NET service.

.EXAMPLE
    .\Test-DemoSetup.ps1
    
.NOTES
    Author: Update Engine Team
    Date: November 30, 2025
    
    Run this before starting the demo to catch configuration issues early.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$script:FailureCount = 0

function Write-Check {
    param(
        [Parameter(Mandatory)]
        [string]$Message,
        
        [Parameter(Mandatory)]
        [ValidateSet('Pass', 'Fail', 'Warn', 'Info')]
        [string]$Status
    )
    
    $symbols = @{
        Pass = "[✓]"
        Fail = "[✗]"
        Warn = "[!]"
        Info = "[i]"
    }
    
    $colors = @{
        Pass = 'Green'
        Fail = 'Red'
        Warn = 'Yellow'
        Info = 'Cyan'
    }
    
    Write-Host "$($symbols[$Status]) " -NoNewline -ForegroundColor $colors[$Status]
    Write-Host $Message
    
    if ($Status -eq 'Fail') {
        $script:FailureCount++
    }
}

function Test-Command {
    param([string]$CommandName)
    return $null -ne (Get-Command $CommandName -ErrorAction SilentlyContinue)
}

Write-Host "`n=== Anomaly Detection Demo Setup Verification ===" -ForegroundColor Cyan
Write-Host "Starting checks...`n" -ForegroundColor Gray

# Check 1: .NET SDK
Write-Host "1. Checking .NET SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version
    $majorVersion = [int]($dotnetVersion.Split('.')[0])
    
    if ($majorVersion -ge 9) {
        Write-Check "Found .NET $dotnetVersion" -Status Pass
    } else {
        Write-Check ".NET 9 or later required (found $dotnetVersion)" -Status Fail
    }
} catch {
    Write-Check ".NET SDK not found - Install from https://dot.net" -Status Fail
}

# Check 2: Azure Functions Core Tools
Write-Host "`n2. Checking Azure Functions Core Tools..." -ForegroundColor Yellow
if (Test-Command 'func') {
    try {
        $funcVersion = func --version
        Write-Check "Found Azure Functions Core Tools v$funcVersion" -Status Pass
    } catch {
        Write-Check "Azure Functions Core Tools found but version check failed" -Status Warn
    }
} else {
    Write-Check "Azure Functions Core Tools not found - Install: npm install -g azure-functions-core-tools@4" -Status Fail
}

# Check 3: Docker (for Azurite container)
Write-Host "`n3. Checking Docker..." -ForegroundColor Yellow
if (Test-Command 'docker') {
    try {
        $dockerVersion = docker --version
        Write-Check "Found Docker: $dockerVersion" -Status Pass
        
        # Check if Docker is running
        $dockerInfo = docker info 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Check "Docker daemon is running" -Status Pass
        } else {
            Write-Check "Docker is installed but daemon is not running" -Status Fail
        }
    } catch {
        Write-Check "Docker found but unable to verify status" -Status Warn
    }
} else {
    Write-Check "Docker not found - Required for Azurite container (AppHost manages it automatically)" -Status Fail
}

# Check 4: PowerShell Version
Write-Host "`n4. Checking PowerShell version..." -ForegroundColor Yellow
$psVersion = $PSVersionTable.PSVersion
if ($psVersion.Major -ge 7) {
    Write-Check "PowerShell $($psVersion.ToString())" -Status Pass
} else {
    Write-Check "PowerShell 7+ recommended (found $($psVersion.ToString()))" -Status Warn
}

# Check 5: Repository Structure
Write-Host "`n5. Checking repository structure..." -ForegroundColor Yellow
$requiredPaths = @(
    'UpdateEngine.Functions\src',
    'UpdateEngine.AppHost\src',
    'UpdateEngine.Core\src',
    'UpdateEngine.Configuration\src'
)

foreach ($path in $requiredPaths) {
    if (Test-Path $path) {
        Write-Check "Found $path" -Status Pass
    } else {
        Write-Check "Missing $path" -Status Fail
    }
}

# Check 6: Configuration Files
Write-Host "`n6. Checking configuration files..." -ForegroundColor Yellow
$configPath = 'UpdateEngine.Configuration\src\shared\appsettings.Development.json'

if (Test-Path $configPath) {
    Write-Check "Found $configPath" -Status Pass
    
    try {
        $config = Get-Content $configPath -Raw | ConvertFrom-Json
        
        # Check anomaly detection enabled
        $adEnabled = $config.UpdateEngine.FeatureFlags.EnableAnomalyDetection -and 
                     $config.Features.EnableAnomalyDetection
        
        if ($adEnabled) {
            Write-Check "Anomaly detection enabled in configuration" -Status Pass
        } else {
            Write-Check "Anomaly detection NOT enabled - Set EnableAnomalyDetection=true" -Status Fail
        }
        
        # Check thresholds
        $threshold = $config.AnomalyDetection.AnomalyScoreThreshold
        if ($threshold -eq 0.85) {
            Write-Check "Anomaly threshold set to 0.85 (dev-optimized)" -Status Pass
        } else {
            Write-Check "Threshold is $threshold (expected 0.85 for demo)" -Status Warn
        }
        
        # Check schedules
        $adSchedule = $config.UpdateEngine.SyncConfiguration.AnomalyDetectionSchedule
        if ($adSchedule -eq "0 * * * * *") {
            Write-Check "Anomaly detection runs every 1 minute" -Status Pass
        } else {
            Write-Check "Anomaly schedule: $adSchedule" -Status Info
        }
        
    } catch {
        Write-Check "Failed to parse configuration: $_" -Status Fail
    }
} else {
    Write-Check "Configuration file not found: $configPath" -Status Fail
}

# Check 7: Build Status
Write-Host "`n7. Checking build status..." -ForegroundColor Yellow

# Skip actual build - just check if solution exists
try {
    $slnFiles = Get-ChildItem -Path . -Filter "*.sln" -ErrorAction SilentlyContinue
    
    if ($slnFiles) {
        Write-Check "Found solution file: $($slnFiles[0].Name)" -Status Pass
        Write-Check "Note: Run 'dotnet build' manually to verify build" -Status Info
    } else {
        Write-Check "No solution file found" -Status Warn
    }
} catch {
    Write-Check "Build check failed: $_" -Status Fail
}

# Check 8: Port Availability
Write-Host "`n8. Checking port availability..." -ForegroundColor Yellow
$ports = @{
    7071 = "Azure Functions"
    10000 = "Azurite Blob (Container)"
    10001 = "Azurite Queue (Container)"
    17003 = "Aspire Dashboard"
}

foreach ($port in $ports.Keys) {
    $listener = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    
    if ($listener) {
        Write-Check "Port $port ($($ports[$port])) is IN USE - May need to stop existing process" -Status Warn
    } else {
        Write-Check "Port $port ($($ports[$port])) is available" -Status Pass
    }
}

# Check 9: Azurite Data Directory
Write-Host "`n9. Checking Azurite data directory..." -ForegroundColor Yellow
$azuritePath = 'out\azurite-data'

if (Test-Path $azuritePath) {
    $size = (Get-ChildItem $azuritePath -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
    Write-Check "Azurite data directory exists ($('{0:N2}' -f $size) MB)" -Status Pass
} else {
    Write-Check "Azurite data directory will be created on first run" -Status Info
}

# Check 10: Model File
Write-Host "`n10. Checking for existing ML.NET model..." -ForegroundColor Yellow
$modelPath = 'UpdateEngine.Functions\src\bin\Debug\net9.0\anomaly-model.zip'

if (Test-Path $modelPath) {
    $modelInfo = Get-Item $modelPath
    Write-Check "Found existing model: $($modelInfo.Name) ($('{0:N0}' -f ($modelInfo.Length / 1KB)) KB)" -Status Info
    Write-Check "Last modified: $($modelInfo.LastWriteTime)" -Status Info
} else {
    Write-Check "No model file found - Will train on first run" -Status Info
}

# Summary
Write-Host "`n=== Summary ===" -ForegroundColor Cyan

if ($script:FailureCount -eq 0) {
    Write-Host "✓ All checks passed! Ready to run demo." -ForegroundColor Green
    Write-Host "`nNext steps:" -ForegroundColor Yellow
    Write-Host "  1. Start AppHost: .\scripts\test\Start-Demo.ps1" -ForegroundColor Gray
    Write-Host "     (AppHost automatically starts Azurite container, Redis, and Functions)" -ForegroundColor Gray
    Write-Host "  2. Open dashboard: https://localhost:17003" -ForegroundColor Gray
    Write-Host "  3. Follow demo guide: docs\guides\ANOMALY_DETECTION_DEMO.md" -ForegroundColor Gray
    
    exit 0
} else {
    Write-Host "✗ $script:FailureCount check(s) failed. Fix issues before running demo." -ForegroundColor Red
    Write-Host "`nCommon fixes:" -ForegroundColor Yellow
    Write-Host "  - Install missing tools (see messages above)" -ForegroundColor Gray
    Write-Host "  - Run 'dotnet build' to fix build errors" -ForegroundColor Gray
    Write-Host "  - Check configuration in UpdateEngine.Configuration\src\shared\appsettings.Development.json" -ForegroundColor Gray
    
    exit 1
}
