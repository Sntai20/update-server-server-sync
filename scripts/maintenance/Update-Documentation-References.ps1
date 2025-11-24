#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Updates outdated project directory references in documentation.

.DESCRIPTION
    This script systematically updates all documentation files to use correct
    project directory names, replacing old references with current structure.

.PARAMETER WhatIf
    Shows what changes would be made without actually making them.

.PARAMETER Path
    Specific path to update. Defaults to docs/ directory.

.EXAMPLE
    .\Update-Documentation-References.ps1 -WhatIf
    Preview changes without making them.

.EXAMPLE
    .\Update-Documentation-References.ps1
    Update all documentation references.
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Path = "docs"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$targetPath = Join-Path $repoRoot $Path

Write-Host "`n🔄 Updating Documentation References" -ForegroundColor Cyan
Write-Host "Repository Root: $repoRoot" -ForegroundColor Gray
Write-Host "Target Path: $targetPath" -ForegroundColor Gray
Write-Host ""

# Define replacement patterns
$replacements = @(
    @{
        Pattern = '(?<!")UpdateEngine/src/'
        Replacement = 'UpdateEngine.Functions/src/'
        Description = 'UpdateEngine/src/ → UpdateEngine.Functions/src/'
    },
    @{
        Pattern = '(?<!")UpdateEngine/test/'
        Replacement = 'UpdateEngine.Functions/test/'
        Description = 'UpdateEngine/test/ → UpdateEngine.Functions/test/'
    },
    @{
        Pattern = '(?<!")UpdateEngine/core/'
        Replacement = 'UpdateEngine.Core/src/'
        Description = 'UpdateEngine/core/ → UpdateEngine.Core/src/'
    },
    @{
        Pattern = '`UpdateEngine/(?!\.)'
        Replacement = '`UpdateEngine.Functions/'
        Description = 'Code blocks: UpdateEngine/ → UpdateEngine.Functions/'
    },
    @{
        Pattern = '\[UpdateEngine/\]'
        Replacement = '[UpdateEngine.Functions/]'
        Description = 'Links: UpdateEngine/ → UpdateEngine.Functions/'
    },
    @{
        Pattern = '^\s*UpdateEngine/$'
        Replacement = 'UpdateEngine.Functions/'
        Description = 'Standalone: UpdateEngine/ → UpdateEngine.Functions/'
    },
    @{
        Pattern = '(?<!")AppHost/src/'
        Replacement = 'UpdateEngine.AppHost/src/'
        Description = 'AppHost/src/ → UpdateEngine.AppHost/src/'
    },
    @{
        Pattern = '`AppHost/'
        Replacement = '`UpdateEngine.AppHost/'
        Description = 'Code blocks: AppHost/ → UpdateEngine.AppHost/'
    },
    @{
        Pattern = '\[AppHost/\]'
        Replacement = '[UpdateEngine.AppHost/]'
        Description = 'Links: AppHost/ → UpdateEngine.AppHost/'
    },
    @{
        Pattern = 'cd AppHost\b'
        Replacement = 'cd UpdateEngine.AppHost'
        Description = 'Commands: cd AppHost → cd UpdateEngine.AppHost'
    },
    @{
        Pattern = 'cd UpdateEngine\b(?!\.)'
        Replacement = 'cd UpdateEngine.Functions'
        Description = 'Commands: cd UpdateEngine → cd UpdateEngine.Functions'
    }
)

# Get all markdown files
$markdownFiles = Get-ChildItem -Path $targetPath -Filter "*.md" -Recurse -File

$totalFiles = $markdownFiles.Count
$filesChanged = 0
$totalReplacements = 0

Write-Host "📄 Processing $totalFiles markdown files..." -ForegroundColor Cyan
Write-Host ""

foreach ($file in $markdownFiles) {
    $content = Get-Content -Path $file.FullName -Raw
    $originalContent = $content
    $fileReplacements = 0
    
    # Apply each replacement pattern
    foreach ($replacement in $replacements) {
        $matches = [regex]::Matches($content, $replacement.Pattern)
        if ($matches.Count -gt 0) {
            $content = $content -replace $replacement.Pattern, $replacement.Replacement
            $fileReplacements += $matches.Count
        }
    }
    
    # If content changed, save it
    if ($content -ne $originalContent) {
        $relativePath = $file.FullName.Replace($repoRoot, "").TrimStart('\', '/')
        
        if ($PSCmdlet.ShouldProcess($relativePath, "Update $fileReplacements reference(s)")) {
            Set-Content -Path $file.FullName -Value $content -NoNewline
            Write-Host "  ✓ Updated $relativePath ($fileReplacements changes)" -ForegroundColor Green
            $filesChanged++
            $totalReplacements += $fileReplacements
        } else {
            Write-Host "  [WhatIf] Would update $relativePath ($fileReplacements changes)" -ForegroundColor Yellow
        }
    }
}

Write-Host ""
Write-Host "✅ Update Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  • Files processed: $totalFiles" -ForegroundColor White
Write-Host "  • Files changed: $filesChanged" -ForegroundColor White
Write-Host "  • Total replacements: $totalReplacements" -ForegroundColor White
Write-Host ""

if ($filesChanged -gt 0) {
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Review the changes" -ForegroundColor White
    Write-Host "  2. Run tests to verify nothing broke" -ForegroundColor White
    Write-Host "  3. Commit the updated documentation" -ForegroundColor White
    Write-Host ""
    Write-Host "Verification command:" -ForegroundColor Cyan
    Write-Host "  git diff --stat" -ForegroundColor Gray
}
