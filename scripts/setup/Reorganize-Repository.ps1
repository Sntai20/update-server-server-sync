# Reorganize-Repository.ps1
# Script to reorganize documentation and scripts into a cleaner structure

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$WhatIf,
    
    [Parameter()]
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "=== Repository Reorganization Script ===" -ForegroundColor Cyan
Write-Host ""

# Get repository root
$repoRoot = $PSScriptRoot
if ([string]::IsNullOrEmpty($repoRoot)) {
    $repoRoot = Get-Location
}

Write-Host "Repository Root: $repoRoot" -ForegroundColor Yellow
Write-Host ""

# Define new directory structure
$newDirs = @(
    "docs",
    "docs/guides",
    "docs/troubleshooting",
    "docs/development",
    "scripts",
    "scripts/setup",
    "scripts/build",
    "scripts/test",
 "scripts/maintenance",
    ".deprecated"
)

# Define file moves (Source ? Destination)
$fileMoves = @{
    # Documentation moves
    "STORAGE_GUIDE.md" = "docs/guides/STORAGE_GUIDE.md"
    "INMEMORY_TESTING_GUIDE.md" = "docs/development/INMEMORY_TESTING_GUIDE.md"
    "WCF_NET9_FIX_GUIDE.md" = "docs/troubleshooting/WCF_NET9_FIX_GUIDE.md"
    "MicrosoftUpdateFunctions.AppHost/MIGRATION_SUMMARY.md" = "docs/guides/MIGRATION_SUMMARY.md"
    "MicrosoftUpdateFunctions.AppHost/CONTAINER_VERIFICATION.md" = "docs/troubleshooting/CONTAINER_VERIFICATION.md"
    "MicrosoftUpdateFunctions.AppHost/SYNC_TROUBLESHOOTING.md" = "docs/troubleshooting/SYNC_TROUBLESHOOTING.md"
    "MicrosoftUpdateFunctions.AppHost/TROUBLESHOOTING_STORAGE.md" = "docs/troubleshooting/TROUBLESHOOTING_STORAGE.md"
    
    # Script moves
    "configure-storage.ps1" = "scripts/setup/Configure-Storage.ps1"
    "Run-InMemoryTests.ps1" = "scripts/test/Run-InMemoryTests.ps1"
    "test-startup.ps1" = "scripts/test/Test-Startup.ps1"
    "validate-build.ps1" = "scripts/build/Validate-Build.ps1"
    "Fix-WCF-ServiceReferences.ps1" = "scripts/maintenance/Fix-WCF-ServiceReferences.ps1"
    "Regenerate-WCF-Net9.ps1" = "scripts/maintenance/Regenerate-WCF-Net9.ps1"
    "Regenerate-WCF-Net9-OfflineFirst.ps1" = "scripts/maintenance/Regenerate-WCF-Net9-OfflineFirst.ps1"
"MicrosoftUpdateFunctions.AppHost/Test-SyncWithDiagnostics.ps1" = "scripts/test/Test-SyncWithDiagnostics.ps1"
}

# Define files to deprecate (move to .deprecated)
$deprecateFiles = @(
    "MicrosoftUpdateFunctions/MicrosoftUpdateFunctions - Deduplicate.md"
    "MicrosoftUpdateFunctions.AppHost/Regenerate-WCFReferences.ps1"
)

function Create-Directories {
    Write-Host "Creating new directory structure..." -ForegroundColor Cyan
    
  foreach ($dir in $newDirs) {
$fullPath = Join-Path $repoRoot $dir
        
 if (-not (Test-Path $fullPath)) {
            if ($WhatIf) {
    Write-Host "  [WHATIF] Would create: $dir" -ForegroundColor Yellow
   } else {
     New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
             Write-Host "  ? Created: $dir" -ForegroundColor Green
            }
        } else {
   Write-Host "  ??  Exists: $dir" -ForegroundColor Gray
        }
    }
    
    Write-Host ""
}

function Move-Files {
    Write-Host "Moving files to new locations..." -ForegroundColor Cyan
    
    foreach ($move in $fileMoves.GetEnumerator()) {
        $source = Join-Path $repoRoot $move.Key
        $dest = Join-Path $repoRoot $move.Value
    
   if (Test-Path $source) {
            if ($WhatIf) {
  Write-Host "  [WHATIF] Would move:" -ForegroundColor Yellow
    Write-Host " From: $($move.Key)" -ForegroundColor Gray
           Write-Host "    To:   $($move.Value)" -ForegroundColor Gray
        } else {
    # Ensure destination directory exists
     $destDir = Split-Path $dest -Parent
          if (-not (Test-Path $destDir)) {
  New-Item -ItemType Directory -Path $destDir -Force | Out-Null
          }
             
# Check if destination already exists
    if (Test-Path $dest) {
              if ($Force) {
        Write-Host "  ??  Overwriting: $($move.Value)" -ForegroundColor Yellow
       Remove-Item $dest -Force
           } else {
        Write-Host "  ? Destination exists: $($move.Value)" -ForegroundColor Red
         Write-Host "  Use -Force to overwrite" -ForegroundColor Red
        continue
             }
}
     
          # Move the file
             Move-Item -Path $source -Destination $dest -Force
     Write-Host "  ? Moved: $($move.Key) ? $($move.Value)" -ForegroundColor Green
        }
        } else {
            Write-Host "  ??  Source not found: $($move.Key)" -ForegroundColor Yellow
        }
    }
    
Write-Host ""
}

function Deprecate-Files {
    Write-Host "Deprecating obsolete files..." -ForegroundColor Cyan
    
    foreach ($file in $deprecateFiles) {
        $source = Join-Path $repoRoot $file
  $filename = Split-Path $file -Leaf
  $dest = Join-Path $repoRoot ".deprecated/$filename"
        
        if (Test-Path $source) {
            if ($WhatIf) {
  Write-Host "  [WHATIF] Would deprecate: $file" -ForegroundColor Yellow
            } else {
      $destDir = Split-Path $dest -Parent
      if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
           }
           
              Move-Item -Path $source -Destination $dest -Force
      Write-Host "  ? Deprecated: $file" -ForegroundColor Green
            }
        } else {
     Write-Host "  ??  Not found: $file" -ForegroundColor Gray
        }
  }
    
    Write-Host ""
}

function Create-Redirects {
    Write-Host "Creating redirect files..." -ForegroundColor Cyan
    
    foreach ($move in $fileMoves.GetEnumerator()) {
      $source = Join-Path $repoRoot $move.Key
        $dest = Join-Path $repoRoot $move.Value
        
        # Only create redirect if source doesn't exist (meaning it was moved)
        if (-not (Test-Path $source) -and (Test-Path $dest)) {
        $redirectContent = @"
# This file has been moved

?? **This file has been relocated to maintain better organization.**

## New Location

This file is now located at: ``$($move.Value)``

Please update your bookmarks and references.

## Quick Link

[? Go to new location](../$($move.Value.Replace('\', '/')))

---

*This redirect file can be safely deleted after all references are updated.*
"@
  
      if ($WhatIf) {
    Write-Host "  [WHATIF] Would create redirect: $($move.Key)" -ForegroundColor Yellow
     } else {
      $sourceDir = Split-Path $source -Parent
 if (-not (Test-Path $sourceDir)) {
      New-Item -ItemType Directory -Path $sourceDir -Force | Out-Null
      }
     
           Set-Content -Path $source -Value $redirectContent -Force
       Write-Host "  ? Created redirect: $($move.Key)" -ForegroundColor Green
     }
        }
    }
    
    Write-Host ""
}

function Create-IndexFiles {
    Write-Host "Creating index files..." -ForegroundColor Cyan
    
    # docs/README.md
    $docsIndex = @"
# Documentation

This directory contains all project documentation organized by category.

## ?? Directory Structure

- **guides/** - How-to guides and walkthroughs
- **troubleshooting/** - Troubleshooting guides and solutions
- **development/** - Development guides and best practices

## ?? Documentation Index

### Guides
- [Storage Configuration Guide](guides/STORAGE_GUIDE.md)
- [Migration Summary](guides/MIGRATION_SUMMARY.md)

### Troubleshooting
- [WCF .NET 9 Fix Guide](troubleshooting/WCF_NET9_FIX_GUIDE.md)
- [Container Verification](troubleshooting/CONTAINER_VERIFICATION.md)
- [Sync Troubleshooting](troubleshooting/SYNC_TROUBLESHOOTING.md)
- [Storage Troubleshooting](troubleshooting/TROUBLESHOOTING_STORAGE.md)

### Development
- [In-Memory Testing Guide](development/INMEMORY_TESTING_GUIDE.md)

## ?? Quick Links

- [Main README](../README.md)
- [API Documentation](../src/documentation/docfx-config/index.md)
- [Scripts](../scripts/README.md)
"@
    
    # scripts/README.md
    $scriptsIndex = @"
# Scripts

This directory contains all automation scripts organized by purpose.

## ?? Directory Structure

- **setup/** - Setup and configuration scripts
- **build/** - Build and validation scripts
- **test/** - Testing scripts
- **maintenance/** - Maintenance and regeneration scripts

## ?? Scripts Index

### Setup Scripts
- [Configure-Storage.ps1](setup/Configure-Storage.ps1) - Configure Azure Storage

### Build Scripts
- [Validate-Build.ps1](build/Validate-Build.ps1) - Validate build

### Test Scripts
- [Run-InMemoryTests.ps1](test/Run-InMemoryTests.ps1) - Run in-memory tests
- [Test-Startup.ps1](test/Test-Startup.ps1) - Test application startup
- [Test-SyncWithDiagnostics.ps1](test/Test-SyncWithDiagnostics.ps1) - Test sync with diagnostics

### Maintenance Scripts
- [Fix-WCF-ServiceReferences.ps1](maintenance/Fix-WCF-ServiceReferences.ps1) - Fix WCF service references
- [Regenerate-WCF-Net9.ps1](maintenance/Regenerate-WCF-Net9.ps1) - Regenerate WCF references for .NET 9
- [Regenerate-WCF-Net9-OfflineFirst.ps1](maintenance/Regenerate-WCF-Net9-OfflineFirst.ps1) - Regenerate WCF (offline mode)

## ?? Quick Start

``````powershell
# Run from repository root

# Setup
./scripts/setup/Configure-Storage.ps1

# Build
./scripts/build/Validate-Build.ps1

# Test
./scripts/test/Run-InMemoryTests.ps1
``````

## ?? Documentation

For detailed documentation, see the [docs](../docs/README.md) directory.
"@

    if ($WhatIf) {
        Write-Host "  [WHATIF] Would create docs/README.md" -ForegroundColor Yellow
    Write-Host "  [WHATIF] Would create scripts/README.md" -ForegroundColor Yellow
    } else {
        $docsReadme = Join-Path $repoRoot "docs/README.md"
   $scriptsReadme = Join-Path $repoRoot "scripts/README.md"
  
        if (-not (Test-Path $docsReadme) -or $Force) {
     Set-Content -Path $docsReadme -Value $docsIndex -Force
      Write-Host "  ? Created: docs/README.md" -ForegroundColor Green
     }
        
        if (-not (Test-Path $scriptsReadme) -or $Force) {
        Set-Content -Path $scriptsReadme -Value $scriptsIndex -Force
        Write-Host "  ? Created: scripts/README.md" -ForegroundColor Green
        }
    }
 
    Write-Host ""
}

# Main execution
try {
    if ($WhatIf) {
 Write-Host "?? DRY RUN MODE - No changes will be made" -ForegroundColor Magenta
        Write-Host ""
    }
    
    Create-Directories
    Move-Files
    Deprecate-Files
    Create-Redirects
    Create-IndexFiles
    
    Write-Host "=== Summary ===" -ForegroundColor Cyan
    Write-Host ""
    
    if ($WhatIf) {
      Write-Host "? Dry run completed successfully!" -ForegroundColor Green
 Write-Host ""
        Write-Host "To apply changes, run without -WhatIf:" -ForegroundColor Yellow
        Write-Host "  ./Reorganize-Repository.ps1" -ForegroundColor White
    } else {
        Write-Host "? Repository reorganization completed!" -ForegroundColor Green
        Write-Host ""
  Write-Host "??  Next Steps:" -ForegroundColor Yellow
        Write-Host "  1. Review the changes" -ForegroundColor White
        Write-Host "  2. Update README.md with new paths" -ForegroundColor White
      Write-Host "  3. Update CI/CD pipelines" -ForegroundColor White
        Write-Host "  4. Test all scripts in new locations" -ForegroundColor White
  Write-Host "  5. Commit changes to version control" -ForegroundColor White
        Write-Host ""
        Write-Host "?? See REPOSITORY_STRUCTURE.md for details" -ForegroundColor Cyan
    }
}
catch {
  Write-Host ""
    Write-Host "? Error during reorganization:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Stack Trace:" -ForegroundColor Yellow
    Write-Host $_.ScriptStackTrace -ForegroundColor Gray
    exit 1
}
