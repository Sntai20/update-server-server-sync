#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Cleans corrupted metadata stores and Azurite data to fix startup issues

.DESCRIPTION
    This script removes corrupted local metadata stores and Azurite data directories
    that can cause "An item with the same key has already been added" errors on startup.
    
    The script:
    - Detects corrupted metadata stores in multiple locations
    - Shows disk space that will be freed
    - Can run in dry-run mode to preview changes
    - Checks for processes that might lock the stores
    - Provides detailed error reporting

.PARAMETER Force
    Skip confirmation prompts and delete all found stores immediately

.PARAMETER DryRun
    Preview what would be deleted without actually deleting anything

.PARAMETER IncludeAzurite
    Also clean Azurite storage emulator data (default: false)

.EXAMPLE
    ./scripts/maintenance/Clean-CorruptedStores.ps1
    Interactive mode - prompts for confirmation before deletion

.EXAMPLE
    ./scripts/maintenance/Clean-CorruptedStores.ps1 -Force
    Delete all corrupted stores without confirmation

.EXAMPLE
    ./scripts/maintenance/Clean-CorruptedStores.ps1 -DryRun
    Preview what would be deleted without making changes

.EXAMPLE
    ./scripts/maintenance/Clean-CorruptedStores.ps1 -Force -IncludeAzurite
    Delete corrupted stores AND Azurite data
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter()]
    [switch]$Force,
    
    [Parameter()]
    [switch]$DryRun,
    
    [Parameter()]
    [switch]$IncludeAzurite
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Colors
$ColorWarning = "Yellow"
$ColorSuccess = "Green"
$ColorInfo = "Cyan"
$ColorError = "Red"

function Write-Header {
    param([string]$Text)
    Write-Host "`n??????????????????????????????????????????????????????????????????" -ForegroundColor $ColorInfo
    Write-Host "?  $($Text.PadRight(62)) ?" -ForegroundColor $ColorInfo
    Write-Host "??????????????????????????????????????????????????????????????????`n" -ForegroundColor $ColorInfo
}

function Write-Success {
    param([string]$Text)
    Write-Host "? $Text" -ForegroundColor $ColorSuccess
}

function Write-Warning {
    param([string]$Text)
    Write-Host "? $Text" -ForegroundColor $ColorWarning
}

function Write-Error {
    param([string]$Text)
    Write-Host "? $Text" -ForegroundColor $ColorError
}

function Get-DirectorySize {
    param([string]$Path)
    
    if (-not (Test-Path $Path)) {
        return 0
    }
    
    try {
        $size = (Get-ChildItem -Path $Path -Recurse -File -ErrorAction SilentlyContinue | 
                 Measure-Object -Property Length -Sum).Sum
        return $size
    }
    catch {
        return 0
    }
}

function Format-FileSize {
    param([long]$Size)
    
    if ($Size -gt 1TB) {
        return "{0:N2} TB" -f ($Size / 1TB)
    }
    elseif ($Size -gt 1GB) {
        return "{0:N2} GB" -f ($Size / 1GB)
    }
    elseif ($Size -gt 1MB) {
        return "{0:N2} MB" -f ($Size / 1MB)
    }
    elseif ($Size -gt 1KB) {
        return "{0:N2} KB" -f ($Size / 1KB)
    }
    else {
        return "$Size bytes"
    }
}

function Test-ProcessLockingDirectory {
    param([string]$Path)
    
    if (-not (Test-Path $Path)) {
        return $null
    }
    
    # Check for common processes that might lock the directories
    $lockingProcesses = @()
    
    # Check for dotnet.exe processes
    $dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
    if ($dotnetProcesses) {
        $lockingProcesses += $dotnetProcesses | Select-Object Id, ProcessName, @{N="CommandLine";E={
            try {
                (Get-CimInstance Win32_Process -Filter "ProcessId = $($_.Id)" -ErrorAction SilentlyContinue).CommandLine
            }
            catch {
                ""
            }
        }}
    }
    
    # Check for func.exe (Azure Functions Core Tools)
    $funcProcesses = Get-Process -Name "func" -ErrorAction SilentlyContinue
    if ($funcProcesses) {
        $lockingProcesses += $funcProcesses
    }
    
    return $lockingProcesses
}

Write-Header "Clean Corrupted Metadata Stores"

if ($DryRun) {
    Write-Warning "DRY RUN MODE - No changes will be made"
}

# Define paths to check
$pathsToClean = @(
    # Local metadata stores (Functions - source directory)
    "UpdateEngine.Functions\src\LocalMetadataStore",
    "UpdateEngine.Functions\src\LocalContentStore",
    
    # Output directory stores (Debug)
    "out\UpdateEngine\Debug\net9.0\LocalMetadataStore",
    "out\UpdateEngine\Debug\net9.0\LocalContentStore",
    
    # Output directory stores (x64 Debug - often missed!)
    "out\UpdateEngine\x64\Debug\net9.0\LocalMetadataStore",
    "out\UpdateEngine\x64\Debug\net9.0\LocalContentStore",
    
    # Output directory stores (Release)
    "out\UpdateEngine\Release\net9.0\LocalMetadataStore",
    "out\UpdateEngine\Release\net9.0\LocalContentStore",
    
    # Output directory stores (x64 Release)
    "out\UpdateEngine\x64\Release\net9.0\LocalMetadataStore",
    "out\UpdateEngine\x64\Release\net9.0\LocalContentStore",
    
    # WorkerService data directories
    "UpdateEngine.WorkerService\src\data\metadata",
    "UpdateEngine.WorkerService\src\data\content",
    "UpdateEngine.WorkerService\src\data",
    "data\metadata",
    "data\content",
    "data"
)

# Add Azurite paths if requested
if ($IncludeAzurite) {
    $pathsToClean += @(
        "$env:USERPROFILE\.aspire\azurite",
        "$env:LOCALAPPDATA\Microsoft\Azurite"
    )
}

Write-Host "Scanning for corrupted metadata stores..." -ForegroundColor $ColorInfo

$foundPaths = @()
$totalSize = 0

foreach ($path in $pathsToClean) {
    if (Test-Path $path) {
        $fullPath = (Resolve-Path $path).Path
        $size = Get-DirectorySize -Path $fullPath
        
        $foundPaths += [PSCustomObject]@{
            Path = $fullPath
            Size = $size
            SizeFormatted = Format-FileSize -Size $size
        }
        
        $totalSize += $size
        
        Write-Host "Found: $fullPath" -ForegroundColor $ColorWarning
        Write-Host "  Size: $(Format-FileSize -Size $size)" -ForegroundColor $ColorWarning
    }
}

if ($foundPaths.Count -eq 0) {
    Write-Success "No corrupted stores found. All clean!"
    exit 0
}

Write-Host "`n" -NoNewline
Write-Host "???????????????????????????????????????????????????????????????" -ForegroundColor $ColorInfo

Write-Host "`nFound $($foundPaths.Count) directory(ies) to clean:" -ForegroundColor $ColorWarning
foreach ($item in $foundPaths) {
    Write-Host "  • $($item.Path)" -ForegroundColor $ColorWarning
    Write-Host "    $($item.SizeFormatted)" -ForegroundColor Gray
}

Write-Host "`nTotal disk space to be freed: $(Format-FileSize -Size $totalSize)" -ForegroundColor $ColorWarning

# Check for locking processes
Write-Host "`nChecking for processes that might lock these directories..." -ForegroundColor $ColorInfo
$lockingProcesses = Test-ProcessLockingDirectory -Path "."

if ($lockingProcesses -and $lockingProcesses.Count -gt 0) {
    Write-Warning "Found $($lockingProcesses.Count) process(es) that might lock the stores:"
    foreach ($proc in $lockingProcesses) {
        Write-Host "  • PID $($proc.Id): $($proc.ProcessName)" -ForegroundColor $ColorWarning
    }
    Write-Host "`nYou may need to stop these processes before cleaning:" -ForegroundColor $ColorWarning
    Write-Host "  • Stop AppHost (Ctrl+C in terminal)" -ForegroundColor White
    Write-Host "  • Or use: Get-Process -Name dotnet,func | Stop-Process -Force" -ForegroundColor White
    
    if (-not $Force) {
        $continue = Read-Host "`nContinue anyway? (y/N)"
        if ($continue -ne 'y' -and $continue -ne 'Y') {
            Write-Host "Cancelled. Stop the processes and run the script again." -ForegroundColor $ColorInfo
            exit 0
        }
    }
}

# Confirmation
if (-not $Force -and -not $DryRun) {
    Write-Host "`nThis will PERMANENTLY DELETE all local metadata and content data." -ForegroundColor $ColorWarning
    Write-Host "Total: $(Format-FileSize -Size $totalSize) will be freed." -ForegroundColor $ColorWarning
    $confirmation = Read-Host "`nAre you sure you want to continue? (y/N)"
    
    if ($confirmation -ne 'y' -and $confirmation -ne 'Y') {
        Write-Host "Cancelled. No changes made." -ForegroundColor $ColorInfo
        exit 0
    }
}

if ($DryRun) {
    Write-Host "`n" -NoNewline
    Write-Success "DRY RUN - Would delete $($foundPaths.Count) directory(ies) ($(Format-FileSize -Size $totalSize))"
    Write-Host "`nRun without -DryRun to actually delete these directories." -ForegroundColor $ColorInfo
    exit 0
}

# Perform cleanup
Write-Host "`nCleaning corrupted stores..." -ForegroundColor $ColorInfo

$cleaned = 0
$failed = 0
$totalCleaned = 0

foreach ($item in $foundPaths) {
    try {
        Write-Host "Removing: $($item.Path)..." -NoNewline
        
        if ($PSCmdlet.ShouldProcess($item.Path, "Remove directory")) {
            Remove-Item -Path $item.Path -Recurse -Force -ErrorAction Stop
            Write-Host " ?" -ForegroundColor $ColorSuccess
            $cleaned++
            $totalCleaned += $item.Size
        }
    }
    catch {
        Write-Host " ?" -ForegroundColor $ColorError
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor $ColorError
        $failed++
        
        # Provide specific guidance for common errors
        if ($_.Exception.Message -match "being used by another process") {
            Write-Host "  Tip: Stop AppHost and try again, or use: Get-Process -Name dotnet | Stop-Process -Force" -ForegroundColor $ColorWarning
        }
        elseif ($_.Exception.Message -match "Access.*denied") {
            Write-Host "  Tip: Run PowerShell as Administrator" -ForegroundColor $ColorWarning
        }
    }
}

Write-Header "Cleanup Complete"

Write-Host "Cleaned: $cleaned directory(ies)" -ForegroundColor $ColorSuccess
Write-Host "Freed disk space: $(Format-FileSize -Size $totalCleaned)" -ForegroundColor $ColorSuccess

if ($failed -gt 0) {
    Write-Host "Failed: $failed directory(ies)" -ForegroundColor $ColorError
    Write-Host "`nSome directories could not be cleaned. Common solutions:" -ForegroundColor $ColorWarning
    Write-Host "  1. Stop AppHost (Ctrl+C in the terminal running it)" -ForegroundColor White
    Write-Host "  2. Close Visual Studio and any IDEs" -ForegroundColor White
    Write-Host "  3. Stop all dotnet processes: Get-Process -Name dotnet | Stop-Process -Force" -ForegroundColor White
    Write-Host "  4. Run this script as Administrator" -ForegroundColor White
    Write-Host "  5. Restart your computer if processes can't be stopped" -ForegroundColor White
    exit 1
}

Write-Host "`nNext steps:" -ForegroundColor $ColorInfo
Write-Host "  1. Start AppHost: cd UpdateEngine.AppHost/src && dotnet run" -ForegroundColor White
Write-Host "  2. Fresh stores will be created automatically" -ForegroundColor White
Write-Host "  3. Verify startup in Aspire Dashboard: https://localhost:15001" -ForegroundColor White
Write-Host "  4. Run sync: curl -X POST http://localhost:7071/api/sync" -ForegroundColor White

if ($IncludeAzurite) {
    Write-Host "`nNote: Azurite data was also cleaned. All Azure Storage Emulator data is gone." -ForegroundColor $ColorWarning
}

exit 0
