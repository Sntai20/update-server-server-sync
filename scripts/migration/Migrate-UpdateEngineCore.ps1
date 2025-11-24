# Migrate-UpdateEngineCore.ps1
# Automates migration of shared code from UpdateEngine to UpdateEngine.Core

$ErrorActionPreference = "Stop"

Write-Host "=== UpdateEngine.Core Migration Script ===" -ForegroundColor Cyan
Write-Host ""

$sourceRoot = "UpdateEngine.Functions/src/Core"
$destRoot = "UpdateEngine.Core/src"

# Files and folders to migrate
$itemsToMove = @(
    "HealthChecks",
    "Models",
    "Orchestrators",
    "Services"
    # Note: ServiceCollectionExtensions.cs already exists in dest
)

# Step 1: Create directory structure
Write-Host "Step 1: Creating directory structure in UpdateEngine.Core..." -ForegroundColor Yellow

foreach ($item in $itemsToMove) {
    $destPath = Join-Path $destRoot $item
    if (-not (Test-Path $destPath)) {
        New-Item -ItemType Directory -Path $destPath -Force | Out-Null
        Write-Host "  Created: $destPath" -ForegroundColor Green
    }
}

# Step 2: Copy files (don't delete originals yet - safer migration)
Write-Host ""
Write-Host "Step 2: Copying files to UpdateEngine.Core..." -ForegroundColor Yellow

foreach ($item in $itemsToMove) {
    $sourcePath = Join-Path $sourceRoot $item
    $destPath = Join-Path $destRoot $item
    
    if (Test-Path $sourcePath) {
        Get-ChildItem -Path $sourcePath -Filter "*.cs" -Recurse | ForEach-Object {
            $relativePath = $_.FullName.Substring((Join-Path (Get-Location) $sourcePath).Length + 1)
            $destFile = Join-Path $destPath $relativePath
            $destDir = Split-Path $destFile -Parent
            
            if (-not (Test-Path $destDir)) {
                New-Item -ItemType Directory -Path $destDir -Force | Out-Null
            }
            
            Copy-Item -Path $_.FullName -Destination $destFile -Force
            Write-Host "  Copied: $relativePath" -ForegroundColor Green
        }
    }
}

Write-Host ""
Write-Host "Step 3: Files copied successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps (Manual):" -ForegroundColor Cyan
Write-Host "1. Build UpdateEngine.Core project to verify no errors"
Write-Host "2. Update UpdateEngine.csproj to reference UpdateEngine.Core"
Write-Host "3. Update WorkerService.csproj to reference UpdateEngine.Core"
Write-Host "4. Remove 'UpdateEngine.Functions/src/Core' folder after verifying build"
Write-Host "5. Run full test suite"
Write-Host ""
Write-Host "Commands to run:" -ForegroundColor Yellow
Write-Host "  dotnet build UpdateEngine/core/UpdateEngine.Core.csproj"
Write-Host "  dotnet build WorkerService/WorkerService.csproj"
Write-Host "  dotnet build UpdateEngine/src/UpdateEngine.csproj"
Write-Host ""
