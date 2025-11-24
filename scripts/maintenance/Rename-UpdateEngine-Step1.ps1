# UpdateEngine Rename Script - Option B (Hybrid Approach)
# This script renames all folders and updates project files while keeping NuGet package names

Write-Host "🚀 Starting UpdateEngine.* Naming Standardization (Option B)" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent

# Step 1: Check for uncommitted changes
Write-Host "📋 Step 1: Checking git status..." -ForegroundColor Yellow
$gitStatus = git status --porcelain
if ($gitStatus) {
    Write-Host "⚠️  Warning: You have uncommitted changes:" -ForegroundColor Yellow
    Write-Host $gitStatus
    $continue = Read-Host "Continue anyway? (y/n)"
    if ($continue -ne 'y') {
        Write-Host "❌ Aborted by user" -ForegroundColor Red
        exit 1
    }
}

# Step 2: Folder renames
Write-Host ""
Write-Host "📁 Step 2: Renaming folders..." -ForegroundColor Yellow

$renames = @(
    @{Old="microsoft-update-partition"; New="UpdateEngine.Metadata"},
    @{Old="microsoft-update-webservices"; New="UpdateEngine.WebServices"},
    @{Old="microsoft-update-endpoints"; New="UpdateEngine.Endpoints"},
    @{Old="microsoft-update-upstream-source"; New="UpdateEngine.UpstreamSource"},
    @{Old="UpdateEngine"; New="UpdateEngine.Functions"},
    @{Old="WorkerService"; New="UpdateEngine.WorkerService"},
    @{Old="update-cli"; New="UpdateEngine.Cli"},
    @{Old="upsync"; New="UpdateEngine.SyncTool"},
    @{Old="Configuration"; New="UpdateEngine.Configuration"},
    @{Old="AppHost"; New="UpdateEngine.AppHost"},
    @{Old="ServiceDefaults"; New="UpdateEngine.ServiceDefaults"}
)

foreach ($rename in $renames) {
    $oldPath = Join-Path $repoRoot $rename.Old
    $newPath = Join-Path $repoRoot $rename.New
    
    if (Test-Path $oldPath) {
        Write-Host "  Renaming: $($rename.Old) → $($rename.New)" -ForegroundColor Gray
        Move-Item -Path $oldPath -Destination $newPath -Force
        Write-Host "  ✓ Renamed $($rename.Old)" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  Skipped: $oldPath not found" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "✅ Step 2 Complete: All folders renamed" -ForegroundColor Green
Write-Host ""
Write-Host "📝 Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Run: git add -A" -ForegroundColor White
Write-Host "  2. Review changes" -ForegroundColor White
Write-Host "  3. Continue with project file updates" -ForegroundColor White
Write-Host ""
Write-Host "⚠️  Note: Close and reopen VS Code after running this script" -ForegroundColor Yellow
