#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test UpdateEngine Azure Functions startup in standalone mode (without Aspire)
.DESCRIPTION
    This script starts the Azure Functions host and monitors the startup logs to verify
    that the application initializes correctly with local storage (no Azure Storage).
#>

param(
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

Write-Host "=== Testing UpdateEngine Azure Functions Standalone Startup ===" -ForegroundColor Cyan
Write-Host ""

# Navigate to UpdateEngine source directory
$functionsDir = Join-Path $PSScriptRoot "..\..\UpdateEngine\src"
Push-Location $functionsDir

try {
    Write-Host "Starting Azure Functions host..." -ForegroundColor Yellow
    Write-Host "Directory: $functionsDir" -ForegroundColor Gray
    Write-Host ""
    
    # Start func host in background
    $job = Start-Job -ScriptBlock {
        param($dir)
        Set-Location $dir
        func start --port 7071 2>&1
    } -ArgumentList (Get-Location).Path
    
    Write-Host "Waiting for startup logs (timeout: $TimeoutSeconds seconds)..." -ForegroundColor Yellow
    
    $startTime = Get-Date
    $foundMetadataLog = $false
    $foundContentLog = $false
    $foundError = $false
    
    while (((Get-Date) - $startTime).TotalSeconds -lt $TimeoutSeconds) {
        # Get job output
        $output = Receive-Job -Job $job 2>&1 | Out-String
        
        if ($output) {
            Write-Host $output
            
            # Check for successful metadata store initialization
            if ($output -match "Metadata store initialized successfully") {
                $foundMetadataLog = $true
                Write-Host "? Metadata store initialized" -ForegroundColor Green
            }
            
            # Check for content store initialization
            if ($output -match "Content store initialized successfully") {
                $foundContentLog = $true
                Write-Host "? Content store initialized" -ForegroundColor Green
            }
            
            # Check for local storage logs
            if ($output -match "Opening local file system metadata store") {
                Write-Host "? Using local file system storage (as expected)" -ForegroundColor Green
            }
            
            # Check for errors
            if ($output -match "Failed to initialize storage services" -or 
                $output -match "Azure Storage connection string not found") {
                $foundError = $true
                Write-Host "? Storage initialization failed" -ForegroundColor Red
                break
            }
            
            # Check if Functions host is running
            if ($output -match "Host started" -or $output -match "Job host started") {
                Write-Host "? Azure Functions host started successfully" -ForegroundColor Green
                break
            }
        }
        
        Start-Sleep -Milliseconds 500
    }
    
    Write-Host ""
    Write-Host "=== Test Results ===" -ForegroundColor Cyan
    
    if ($foundError) {
        Write-Host "? FAILED: Storage initialization error detected" -ForegroundColor Red
        exit 1
    }
    
    if ($foundMetadataLog -or $foundContentLog) {
        Write-Host "? PASSED: Storage services initialized successfully" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "? TIMEOUT: Could not verify storage initialization within $TimeoutSeconds seconds" -ForegroundColor Yellow
        Write-Host "  The Functions host may still be starting up." -ForegroundColor Yellow
        exit 2
    }
    
} finally {
    # Clean up
    if ($job) {
        Write-Host ""
        Write-Host "Stopping Azure Functions host..." -ForegroundColor Yellow
        Stop-Job -Job $job -ErrorAction SilentlyContinue
        Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
    }
    
    Pop-Location
}
