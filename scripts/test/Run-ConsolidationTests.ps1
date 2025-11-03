#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Run integration tests to validate function consolidation
.DESCRIPTION
    Runs specific integration tests to ensure the consolidated functions work correctly
#>

param(
    [switch]$SkipBuild,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

Write-Host "🧪 Running Function Consolidation Integration Tests" -ForegroundColor Green

$projectPath = "d:\repos\update-server-server-sync-fork\UpdateEngine\test\UpdateEngineTest.csproj"

if (-not $SkipBuild) {
    Write-Host "🔨 Building test project..." -ForegroundColor Yellow
    dotnet build $projectPath
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build failed" -ForegroundColor Red
        exit 1
    }
}

# Test categories in order of importance
$TestSuites = @(
    @{ 
        Name = "Consolidation Validation Tests"
        Filter = "FullyQualifiedName~ConsolidationValidationTests"
        Description = "Tests that validate the consolidation worked correctly"
    },
    @{ 
        Name = "Unified Sync Tests"
        Filter = "FullyQualifiedName~UnifiedSyncIntegrationTests"
        Description = "Tests for the new unified sync functions"
    },
    @{ 
        Name = "Unified Health Tests"
        Filter = "FullyQualifiedName~UnifiedHealthIntegrationTests"
        Description = "Tests for the new unified health functions"
    },
    @{ 
        Name = "Full Workflow Tests"
        Filter = "FullyQualifiedName~FullSyncWorkflowTests"
        Description = "End-to-end workflow tests"
    },
    @{ 
        Name = "Updated Content Sync Tests"
        Filter = "FullyQualifiedName~SyncContentIntegrationTests"
        Description = "Content sync tests using new endpoints"
    }
)

$allPassed = $true

foreach ($suite in $TestSuites) {
    Write-Host "`n🔍 Running: $($suite.Name)" -ForegroundColor Cyan
    Write-Host "   $($suite.Description)" -ForegroundColor Gray
    
    $verbosity = if ($Verbose) { "normal" } else { "minimal" }
    
    try {
        dotnet test $projectPath --filter $suite.Filter --verbosity $verbosity --no-build
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "   ✅ PASSED: $($suite.Name)" -ForegroundColor Green
        } else {
            Write-Host "   ❌ FAILED: $($suite.Name)" -ForegroundColor Red
            $allPassed = $false
        }
    }
    catch {
        Write-Host "   ❌ ERROR: $($suite.Name) - $($_.Exception.Message)" -ForegroundColor Red
        $allPassed = $false
    }
}

Write-Host "`n📊 Integration Test Summary" -ForegroundColor Magenta

if ($allPassed) {
    Write-Host "🎉 ALL INTEGRATION TESTS PASSED!" -ForegroundColor Green
    Write-Host "✅ Function consolidation is working correctly" -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ SOME TESTS FAILED" -ForegroundColor Red
    Write-Host "Please review the failing tests and fix any issues" -ForegroundColor Yellow
    exit 1
}