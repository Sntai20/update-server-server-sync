#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Tests the complete downstream sync flow: Functions ? WorkerService ? Filesystem

.DESCRIPTION
    This script tests the end-to-end downstream sync implementation:
    1. Microsoft Update ? Azure Functions ? Azurite (cloud emulation)
    2. Azure Functions ? WorkerService ? Local Filesystem (downstream cache)

.PARAMETER FunctionsPort
    The port where Azure Functions is running (default: auto-detect from Aspire)

.PARAMETER WorkerServicePort
    The port where WorkerService is running (default: 8080)

.EXAMPLE
    ./scripts/test/Test-DownstreamSync.ps1
    
.EXAMPLE
    ./scripts/test/Test-DownstreamSync.ps1 -FunctionsPort 7071 -WorkerServicePort 8080
#>

[CmdletBinding()]
param(
    [Parameter()]
    [int]$FunctionsPort = 0,  # 0 = auto-detect
    
    [Parameter()]
    [int]$WorkerServicePort = 8080,
    
    [Parameter()]
    [switch]$SkipFunctionsSync,
    
    [Parameter()]
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Colors for output
$ColorSuccess = "Green"
$ColorWarning = "Yellow"
$ColorError = "Red"
$ColorInfo = "Cyan"

function Write-Step {
    param([string]$Message)
    Write-Host "`n=== $Message ===" -ForegroundColor $ColorInfo
}

function Write-Success {
    param([string]$Message)
    Write-Host "? $Message" -ForegroundColor $ColorSuccess
}

function Write-Fail {
    param([string]$Message)
    Write-Host "? $Message" -ForegroundColor $ColorError
}

function Write-Info {
    param([string]$Message)
    Write-Host "? $Message" -ForegroundColor $ColorInfo
}

# Auto-detect Functions port from Aspire Dashboard
function Get-FunctionsPort {
    Write-Info "Auto-detecting Azure Functions port from Aspire Dashboard..."
    
    try {
        # Check if Aspire Dashboard is running (default port 15275)
        $dashboardUrl = "http://localhost:15275"
        $response = Invoke-WebRequest -Uri $dashboardUrl -UseBasicParsing -TimeoutSec 5 -ErrorAction SilentlyContinue
        
        if ($response.StatusCode -eq 200) {
            Write-Success "Aspire Dashboard found at $dashboardUrl"
            Write-Info "Please check the dashboard for the UpdateEngine Functions port"
            Write-Info "Trying common ports: 7071, 7072, 7073..."
            
            # Try common ports
            foreach ($port in @(7071, 7072, 7073, 5000, 5001)) {
                try {
                    $testUrl = "http://localhost:$port/api/GetStoreStatus"
                    $testResponse = Invoke-WebRequest -Uri $testUrl -UseBasicParsing -TimeoutSec 2 -ErrorAction SilentlyContinue
                    if ($testResponse.StatusCode -eq 200) {
                        Write-Success "Found Azure Functions at port $port"
                        return $port
                    }
                }
                catch {
                    # Continue trying
                }
            }
        }
    }
    catch {
        Write-Warning "Could not auto-detect Functions port. Using default 7071"
    }
    
    return 7071
}

# Main test flow
try {
    Write-Host "`n??????????????????????????????????????????????????????????????????" -ForegroundColor $ColorInfo
    Write-Host "?  Downstream Sync End-to-End Test                              ?" -ForegroundColor $ColorInfo
    Write-Host "?  Microsoft Update ? Functions ? WorkerService ? Filesystem    ?" -ForegroundColor $ColorInfo
    Write-Host "??????????????????????????????????????????????????????????????????`n" -ForegroundColor $ColorInfo
    
    # Step 0: Pre-flight checks
    Write-Step "Step 0: Pre-flight Checks"
    
    if ($FunctionsPort -eq 0) {
        $FunctionsPort = Get-FunctionsPort
    }
    
    $functionsBaseUrl = "http://localhost:$FunctionsPort"
    $workerServiceBaseUrl = "http://localhost:$WorkerServicePort"
    
    Write-Info "Functions URL: $functionsBaseUrl"
    Write-Info "WorkerService URL: $workerServiceBaseUrl"
    
    # Check if AppHost is running
    Write-Info "Checking if AppHost is running..."
    try {
        $functionsHealthResponse = Invoke-WebRequest -Uri "$functionsBaseUrl/api/GetStoreStatus" -UseBasicParsing -TimeoutSec 5
        Write-Success "Azure Functions is running"
    }
    catch {
        Write-Fail "Azure Functions is not responding at $functionsBaseUrl"
        Write-Warning "Please start AppHost first:"
        Write-Host "  cd UpdateEngine.AppHost/src" -ForegroundColor Yellow
        Write-Host "  dotnet run" -ForegroundColor Yellow
        exit 1
    }
    
    try {
        $workerHealthResponse = Invoke-WebRequest -Uri "$workerServiceBaseUrl/health" -UseBasicParsing -TimeoutSec 5
        Write-Success "WorkerService is running"
    }
    catch {
        Write-Fail "WorkerService is not responding at $workerServiceBaseUrl"
        Write-Warning "WorkerService should start automatically with AppHost"
        exit 1
    }
    
    # Step 1: Check Functions initial state
    Write-Step "Step 1: Check Azure Functions Initial State"
    
    $functionsStatus = Invoke-RestMethod -Uri "$functionsBaseUrl/api/GetStoreStatus" -Method Get
    Write-Info "Functions Metadata Store:"
    Write-Host "  Total Updates: $($functionsStatus.metadataStore.totalUpdates)" -ForegroundColor White
    Write-Host "  Total Products: $($functionsStatus.metadataStore.totalProducts)" -ForegroundColor White
    Write-Host "  Total Categories: $($functionsStatus.metadataStore.totalCategories)" -ForegroundColor White
    
    # Step 2: Trigger Functions sync from Microsoft Update (if not skipped)
    if (-not $SkipFunctionsSync) {
        Write-Step "Step 2: Trigger Functions Sync from Microsoft Update"
        Write-Info "This may take 5-15 minutes depending on network speed..."
        Write-Info "Triggering critical updates sync..."
        
        try {
            $syncResponse = Invoke-RestMethod -Uri "$functionsBaseUrl/api/sync" -Method Post -Body '{"syncType":"Critical"}' -ContentType "application/json" -TimeoutSec 600
            
            if ($syncResponse.success) {
                Write-Success "Functions sync initiated successfully"
                Write-Info "Sync Job ID: $($syncResponse.jobId)"
            }
            else {
                Write-Warning "Functions sync returned success=false: $($syncResponse.message)"
            }
        }
        catch {
            Write-Warning "Functions sync trigger failed: $($_.Exception.Message)"
            Write-Info "Check Functions logs for details"
        }
        
        # Wait for sync to complete
        Write-Info "Waiting 30 seconds for sync to progress..."
        Start-Sleep -Seconds 30
        
        # Check updated status
        $functionsStatusAfter = Invoke-RestMethod -Uri "$functionsBaseUrl/api/GetStoreStatus" -Method Get
        Write-Info "Functions Metadata Store (after sync):"
        Write-Host "  Total Updates: $($functionsStatusAfter.metadataStore.totalUpdates)" -ForegroundColor White
        
        if ($functionsStatusAfter.metadataStore.totalUpdates -gt $functionsStatus.metadataStore.totalUpdates) {
            Write-Success "Functions successfully synced updates from Microsoft Update"
        }
        else {
            Write-Warning "Functions update count unchanged. Sync may still be in progress."
        }
    }
    else {
        Write-Info "Skipping Functions sync (--SkipFunctionsSync specified)"
    }
    
    # Step 3: Check WorkerService initial state
    Write-Step "Step 3: Check WorkerService Initial State"
    
    try {
        $workerStatus = Invoke-RestMethod -Uri "$workerServiceBaseUrl/api/metadata/status" -Method Get
        Write-Info "WorkerService Metadata Store (filesystem):"
        Write-Host "  Total Packages: $($workerStatus.totalPackages)" -ForegroundColor White
        Write-Host "  Indexed Updates: $($workerStatus.indexedUpdates)" -ForegroundColor White
    }
    catch {
        Write-Info "WorkerService metadata store not initialized yet (expected on first run)"
    }
    
    # Step 4: Trigger WorkerService downstream sync from Functions
    Write-Step "Step 4: Trigger WorkerService Downstream Sync from Functions"
    Write-Info "WorkerService will pull metadata and content from Functions..."
    
    try {
        $workerSyncResponse = Invoke-RestMethod -Uri "$workerServiceBaseUrl/api/sync" -Method Post -Body '{"syncType":"Comprehensive"}' -ContentType "application/json" -TimeoutSec 600
        
        if ($workerSyncResponse.success) {
            Write-Success "WorkerService downstream sync initiated"
            Write-Info "Sync Job ID: $($workerSyncResponse.jobId)"
        }
        else {
            Write-Warning "WorkerService sync returned success=false: $($workerSyncResponse.message)"
        }
    }
    catch {
        Write-Warning "WorkerService sync trigger failed: $($_.Exception.Message)"
        Write-Info "Check WorkerService logs for details"
    }
    
    # Wait for downstream sync to complete
    Write-Info "Waiting 30 seconds for downstream sync to complete..."
    Start-Sleep -Seconds 30
    
    # Step 5: Verify WorkerService filesystem storage
    Write-Step "Step 5: Verify WorkerService Filesystem Storage"
    
    $metadataPath = "./data/metadata"
    $contentPath = "./data/content"
    
    Write-Info "Checking filesystem paths..."
    
    if (Test-Path $metadataPath) {
        Write-Success "Metadata directory exists: $metadataPath"
        
        $metadataFiles = Get-ChildItem -Path $metadataPath -Recurse -File
        Write-Info "  Files in metadata directory: $($metadataFiles.Count)"
        
        if ($metadataFiles.Count -gt 0) {
            $totalSize = ($metadataFiles | Measure-Object -Property Length -Sum).Sum
            Write-Info "  Total size: $([math]::Round($totalSize / 1MB, 2)) MB"
        }
    }
    else {
        Write-Warning "Metadata directory not found: $metadataPath"
    }
    
    if (Test-Path $contentPath) {
        Write-Success "Content directory exists: $contentPath"
        
        $contentFiles = Get-ChildItem -Path $contentPath -Recurse -File
        Write-Info "  Files in content directory: $($contentFiles.Count)"
        
        if ($contentFiles.Count -gt 0) {
            $totalSize = ($contentFiles | Measure-Object -Property Length -Sum).Sum
            Write-Info "  Total size: $([math]::Round($totalSize / 1MB, 2)) MB"
        }
    }
    else {
        Write-Warning "Content directory not found: $contentPath"
    }
    
    # Check WorkerService status after sync
    try {
        $workerStatusAfter = Invoke-RestMethod -Uri "$workerServiceBaseUrl/api/metadata/status" -Method Get
        Write-Info "WorkerService Metadata Store (after downstream sync):"
        Write-Host "  Total Packages: $($workerStatusAfter.totalPackages)" -ForegroundColor White
        Write-Host "  Indexed Updates: $($workerStatusAfter.indexedUpdates)" -ForegroundColor White
        
        if ($workerStatusAfter.totalPackages -gt 0) {
            Write-Success "WorkerService successfully synced from Functions"
        }
    }
    catch {
        Write-Warning "Could not retrieve WorkerService status"
    }
    
    # Step 6: Summary
    Write-Step "Step 6: Test Summary"
    
    Write-Host "`nTest Results:" -ForegroundColor $ColorInfo
    Write-Host "?????????????????????????????????????????????????????" -ForegroundColor $ColorInfo
    
    $functionsHasData = ($functionsStatusAfter.metadataStore.totalUpdates -gt 0)
    $workerHasData = ($workerStatusAfter.totalPackages -gt 0)
    $filesystemHasData = (Test-Path $metadataPath) -and ((Get-ChildItem -Path $metadataPath -Recurse -File).Count -gt 0)
    
    if ($functionsHasData) {
        Write-Success "Functions has metadata (Microsoft Update ? Azurite)"
    }
    else {
        Write-Fail "Functions has no metadata"
    }
    
    if ($workerHasData) {
        Write-Success "WorkerService has metadata (Functions ? Filesystem)"
    }
    else {
        Write-Fail "WorkerService has no metadata"
    }
    
    if ($filesystemHasData) {
        Write-Success "Filesystem has metadata files"
    }
    else {
        Write-Fail "Filesystem has no metadata files"
    }
    
    if ($functionsHasData -and $workerHasData -and $filesystemHasData) {
        Write-Host "`n??????????????????????????????????????????????????????????????????" -ForegroundColor Green
        Write-Host "?  ? SUCCESS: Complete downstream sync flow verified!           ?" -ForegroundColor Green
        Write-Host "?  Microsoft Update ? Functions ? WorkerService ? Filesystem    ?" -ForegroundColor Green
        Write-Host "??????????????????????????????????????????????????????????????????`n" -ForegroundColor Green
        exit 0
    }
    else {
        Write-Host "`n??????????????????????????????????????????????????????????????????" -ForegroundColor Yellow
        Write-Host "?  ? PARTIAL: Some components not syncing correctly             ?" -ForegroundColor Yellow
        Write-Host "?  Check logs for details                                        ?" -ForegroundColor Yellow
        Write-Host "??????????????????????????????????????????????????????????????????`n" -ForegroundColor Yellow
        
        Write-Info "Troubleshooting tips:"
        Write-Host "  1. Check Aspire Dashboard logs: http://localhost:15275" -ForegroundColor White
        Write-Host "  2. Check Functions logs in Aspire Dashboard ? UpdateEngine" -ForegroundColor White
        Write-Host "  3. Check WorkerService logs in Aspire Dashboard ? WorkerService" -ForegroundColor White
        Write-Host "  4. Verify downstream sync is enabled in WorkerService logs" -ForegroundColor White
        Write-Host "  5. See docs/guides/TESTING_DOWNSTREAM_SYNC.md for detailed troubleshooting" -ForegroundColor White
        
        exit 1
    }
}
catch {
    Write-Host "`n??????????????????????????????????????????????????????????????????" -ForegroundColor Red
    Write-Host "?  ? ERROR: Test failed with exception                          ?" -ForegroundColor Red
    Write-Host "??????????????????????????????????????????????????????????????????`n" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 1
}
