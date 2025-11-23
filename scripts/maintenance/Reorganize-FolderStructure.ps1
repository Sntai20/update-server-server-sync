#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Reorganizes the codebase to follow consistent folder structure pattern.

.DESCRIPTION
    Ensures all projects follow the pattern:
    - ProjectName/src/ProjectName.csproj (for source projects)
    - ProjectName/test/ProjectNameTest.csproj (for test projects)

.EXAMPLE
    .\Reorganize-FolderStructure.ps1
#>

param(
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"

# Projects that need restructuring
$restructurePlan = @(
    @{
        CurrentPath = "Configuration\Configuration.csproj"
        TargetPath = "Configuration\src\Configuration.csproj"
        Type = "Source"
    },
    @{
        CurrentPath = "ServiceDefaults\ServiceDefaults\ServiceDefaults.csproj"
        TargetPath = "ServiceDefaults\src\ServiceDefaults.csproj"
        Type = "Source"
    },
    @{
        CurrentPath = "WorkerService\WorkerService.csproj"
        TargetPath = "WorkerService\src\WorkerService.csproj"
        Type = "Source"
    }
)

Write-Host "=== Folder Structure Reorganization Plan ===" -ForegroundColor Cyan
Write-Host ""

if ($WhatIf) {
    Write-Host "Running in WHATIF mode - no changes will be made" -ForegroundColor Yellow
    Write-Host ""
}

function Move-ProjectFiles {
    param(
        [string]$CurrentPath,
        [string]$TargetPath,
        [string]$Type
    )

    $currentFullPath = Join-Path $PSScriptRoot "..\..\" $CurrentPath
    $targetFullPath = Join-Path $PSScriptRoot "..\..\" $TargetPath
    
    if (-not (Test-Path $currentFullPath)) {
        Write-Host "  ? Source not found: $CurrentPath" -ForegroundColor Red
        return $false
    }

    $currentDir = Split-Path $currentFullPath -Parent
    $targetDir = Split-Path $targetFullPath -Parent
    
    Write-Host "  Moving: $CurrentPath" -ForegroundColor Yellow
    Write-Host "      ? $TargetPath" -ForegroundColor Green

    if ($WhatIf) {
        Write-Host "    [WHATIF] Would create: $targetDir" -ForegroundColor Gray
        Write-Host "    [WHATIF] Would move all files from $currentDir to $targetDir" -ForegroundColor Gray
        return $true
    }

    # Create target directory
    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        Write-Host "    ? Created directory: $targetDir" -ForegroundColor Gray
    }

    # Move all files and subdirectories
    Get-ChildItem -Path $currentDir -Force | ForEach-Object {
        $targetItem = Join-Path $targetDir $_.Name
        if (-not (Test-Path $targetItem)) {
            Move-Item -Path $_.FullName -Destination $targetDir -Force
            Write-Host "    ? Moved: $($_.Name)" -ForegroundColor Gray
        }
    }

    return $true
}

function Update-ProjectReferences {
    param(
        [string]$OldPath,
        [string]$NewPath
    )

    Write-Host ""
    Write-Host "Updating project references..." -ForegroundColor Cyan

    # Find all .csproj files
    $allProjects = Get-ChildItem -Path (Join-Path $PSScriptRoot "..\..\") -Recurse -Filter "*.csproj"

    foreach ($project in $allProjects) {
        $content = Get-Content $project.FullName -Raw
        $oldPathNormalized = $OldPath -replace '\\', '\\'
        
        if ($content -match $oldPathNormalized) {
            Write-Host "  Updating references in: $($project.Name)" -ForegroundColor Yellow
            
            if (-not $WhatIf) {
                $content = $content -replace [regex]::Escape($OldPath), $NewPath
                Set-Content -Path $project.FullName -Value $content -NoNewline
                Write-Host "    ? Updated" -ForegroundColor Green
            } else {
                Write-Host "    [WHATIF] Would update $OldPath ? $NewPath" -ForegroundColor Gray
            }
        }
    }
}

function Update-SolutionFile {
    Write-Host ""
    Write-Host "Checking for solution files..." -ForegroundColor Cyan
    
    $solutionFiles = Get-ChildItem -Path (Join-Path $PSScriptRoot "..\..\") -Recurse -Filter "*.sln"
    
    foreach ($sln in $solutionFiles) {
        Write-Host "  Found: $($sln.Name)" -ForegroundColor Yellow
        Write-Host "    ??  Manual update required for solution file" -ForegroundColor Yellow
    }
}

function Remove-EmptyDirectories {
    Write-Host ""
    Write-Host "Cleaning up empty directories..." -ForegroundColor Cyan

    $emptyDirs = @(
        "Configuration",
        "ServiceDefaults\ServiceDefaults",
        "WorkerService"
    )

    foreach ($dir in $emptyDirs) {
        $fullPath = Join-Path $PSScriptRoot "..\..\" $dir
        
        if (Test-Path $fullPath) {
            $items = Get-ChildItem -Path $fullPath -Force
            
            if ($items.Count -eq 0) {
                Write-Host "  Removing empty: $dir" -ForegroundColor Yellow
                
                if (-not $WhatIf) {
                    Remove-Item -Path $fullPath -Force
                    Write-Host "    ? Removed" -ForegroundColor Green
                } else {
                    Write-Host "    [WHATIF] Would remove empty directory" -ForegroundColor Gray
                }
            }
        }
    }
}

# Execute reorganization
Write-Host "Projects to restructure: $($restructurePlan.Count)" -ForegroundColor Cyan
Write-Host ""

$successCount = 0
foreach ($plan in $restructurePlan) {
    Write-Host "[$($successCount + 1)/$($restructurePlan.Count)] $($plan.Type) Project" -ForegroundColor Cyan
    
    $success = Move-ProjectFiles -CurrentPath $plan.CurrentPath -TargetPath $plan.TargetPath -Type $plan.Type
    
    if ($success) {
        Update-ProjectReferences -OldPath $plan.CurrentPath -NewPath $plan.TargetPath
        $successCount++
    }
    
    Write-Host ""
}

Update-SolutionFile
Remove-EmptyDirectories

Write-Host ""
Write-Host "=== Reorganization Summary ===" -ForegroundColor Cyan
Write-Host "  Successfully restructured: $successCount / $($restructurePlan.Count)" -ForegroundColor Green
Write-Host ""

if ($WhatIf) {
    Write-Host "This was a WHATIF run - no changes were made" -ForegroundColor Yellow
    Write-Host "Run without -WhatIf to apply changes" -ForegroundColor Yellow
} else {
    Write-Host "? Folder structure reorganization complete!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Update solution file(s) if needed" -ForegroundColor White
    Write-Host "  2. Run: dotnet build to verify" -ForegroundColor White
    Write-Host "  3. Run: git status to review changes" -ForegroundColor White
    Write-Host "  4. Commit the restructured files" -ForegroundColor White
}

Write-Host ""
