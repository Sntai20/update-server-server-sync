# UpdateEngine Namespace Update Script - Step 5
# Updates namespace declarations and using statements in all .cs files

Write-Host "🚀 Starting Namespace Update (Option B)" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Continue"
$repoRoot = Split-Path $PSScriptRoot -Parent

# Define namespace mappings (old → new)
$namespaceMappings = @{
    "Microsoft.PackageGraph.MicrosoftUpdate.Endpoints" = "UpdateEngine.Endpoints"
    "Microsoft.PackageGraph.MicrosoftUpdate.Source" = "UpdateEngine.UpstreamSource"
    "Microsoft.PackageGraph.MicrosoftUpdate.Metadata" = "UpdateEngine.Metadata"
    "Microsoft.PackageGraph.MicrosoftUpdate" = "UpdateEngine.Metadata"
    "Microsoft.UpdateServices.WebServices" = "UpdateEngine.WebServices"
    "Microsoft.UpdateServices.WorkerService" = "UpdateEngine.WorkerService"
    "Microsoft.PackageGraph.Utilitites.Upsync" = "UpdateEngine.SyncTool"
    "UpdateCli" = "UpdateEngine.Cli"
    "Configuration" = "UpdateEngine.Configuration"
}

# Get all .cs files
Write-Host "📁 Finding all .cs files..." -ForegroundColor Yellow
$csFiles = Get-ChildItem -Path $repoRoot -Filter "*.cs" -Recurse | Where-Object {
    $_.FullName -notmatch '\\obj\\' -and 
    $_.FullName -notmatch '\\bin\\' -and
    $_.FullName -notmatch '\\Connected Services\\'
}

Write-Host "   Found $($csFiles.Count) .cs files" -ForegroundColor Gray
Write-Host ""

# Update each file
$updatedCount = 0
$errorCount = 0

foreach ($file in $csFiles) {
    try {
        $content = Get-Content $file.FullName -Raw
        $originalContent = $content
        
        # Update namespace declarations
        foreach ($mapping in $namespaceMappings.GetEnumerator()) {
            # Update namespace declarations: namespace OldName { → namespace NewName {
            $content = $content -replace "namespace\s+$([regex]::Escape($mapping.Key))\b", "namespace $($mapping.Value)"
            
            # Update using statements: using OldName; → using NewName;
            $content = $content -replace "using\s+$([regex]::Escape($mapping.Key))\b", "using $($mapping.Value)"
        }
        
        # Check if content changed
        if ($content -ne $originalContent) {
            Set-Content -Path $file.FullName -Value $content -NoNewline
            Write-Host "  ✓ Updated: $($file.FullName.Replace($repoRoot, ''))" -ForegroundColor Green
            $updatedCount++
        }
    }
    catch {
        Write-Host "  ✗ Error updating $($file.FullName): $_" -ForegroundColor Red
        $errorCount++
    }
}

Write-Host ""
Write-Host "✅ Namespace update complete" -ForegroundColor Green
Write-Host "   Updated: $updatedCount files" -ForegroundColor Gray
Write-Host "   Errors: $errorCount files" -ForegroundColor $(if ($errorCount -gt 0) { 'Red' } else { 'Gray' })
Write-Host ""
Write-Host "📝 Next: Run build to verify changes" -ForegroundColor Cyan
