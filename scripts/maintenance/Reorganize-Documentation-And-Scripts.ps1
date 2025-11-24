#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Reorganizes documentation and scripts into proper folder structures.

.DESCRIPTION
    This script moves scattered documentation files and scripts into organized
    folders within docs/ and scripts/ directories, following the project's
    documentation standards.

.PARAMETER WhatIf
    Shows what changes would be made without actually making them.

.PARAMETER Verbose
    Shows detailed progress information.

.EXAMPLE
    .\Reorganize-Documentation-And-Scripts.ps1 -WhatIf
    Shows what would be moved without making changes.

.EXAMPLE
    .\Reorganize-Documentation-And-Scripts.ps1
    Performs the reorganization.
#>

[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

Write-Host "🔄 Starting Documentation and Scripts Reorganization" -ForegroundColor Cyan
Write-Host "Repository Root: $repoRoot" -ForegroundColor Gray
Write-Host ""

# Helper function to move files safely
function Move-FileIfExists {
    param(
        [string]$Source,
        [string]$Destination,
        [string]$Description
    )
    
    if (Test-Path $Source) {
        $destDir = Split-Path -Parent $Destination
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        
        if ($PSCmdlet.ShouldProcess($Source, "Move to $Destination")) {
            Write-Host "  ✓ $Description" -ForegroundColor Green
            Move-Item -Path $Source -Destination $Destination -Force
        } else {
            Write-Host "  [WhatIf] Would move: $Description" -ForegroundColor Yellow
        }
        return $true
    }
    return $false
}

#region Documentation Reorganization

Write-Host "📚 DOCUMENTATION REORGANIZATION" -ForegroundColor Cyan
Write-Host ""

# 1. Move root-level completion/summary docs to historical
Write-Host "Moving completion summaries to historical..." -ForegroundColor White
$historicalPath = Join-Path $repoRoot "docs\historical\migration-summaries"

Move-FileIfExists `
    -Source (Join-Path $repoRoot "COMMIT_MESSAGE.md") `
    -Destination (Join-Path $historicalPath "COMMIT_MESSAGE.md") `
    -Description "Commit message template → historical"

Move-FileIfExists `
    -Source (Join-Path $repoRoot "CONFIGURATION_CONSOLIDATION_COMPLETE.md") `
    -Destination (Join-Path $historicalPath "CONFIGURATION_CONSOLIDATION_COMPLETE.md") `
    -Description "Configuration consolidation summary → historical"

Move-FileIfExists `
    -Source (Join-Path $repoRoot "SHARED_CONFIGURATION_COMPLETE.md") `
    -Destination (Join-Path $historicalPath "SHARED_CONFIGURATION_COMPLETE.md") `
    -Description "Shared configuration summary → historical"

# 2. Move UpdateEngine.Cli documentation to docs/guides
Write-Host "`nMoving UpdateEngine.Cli documentation..." -ForegroundColor White
$guidesPath = Join-Path $repoRoot "docs\guides"
$cliPath = Join-Path $repoRoot "UpdateEngine.Cli"

Move-FileIfExists `
    -Source (Join-Path $cliPath "ARCHITECTURE_CLEANUP_SUMMARY.md") `
    -Destination (Join-Path $guidesPath "UPDATE_CLI_ARCHITECTURE.md") `
    -Description "CLI architecture → guides (renamed for consistency)"

Move-FileIfExists `
    -Source (Join-Path $cliPath "DOWNLOAD_GUIDE.md") `
    -Destination (Join-Path $guidesPath "CLI_DOWNLOAD_GUIDE.md") `
    -Description "Download guide → guides"

Move-FileIfExists `
    -Source (Join-Path $cliPath "QUICKSTART.md") `
    -Destination (Join-Path $guidesPath "CLI_QUICKSTART.md") `
    -Description "CLI quickstart → guides"

Move-FileIfExists `
    -Source (Join-Path $cliPath "MULTI_PLATFORM_IMPLEMENTATION.md") `
    -Destination (Join-Path $guidesPath "CLI_MULTI_PLATFORM.md") `
    -Description "Multi-platform implementation → guides"

Move-FileIfExists `
    -Source (Join-Path $cliPath "IPAK_INTEGRATION_SUMMARY.md") `
    -Destination (Join-Path $historicalPath "CLI_IPAK_INTEGRATION_SUMMARY.md") `
    -Description "IPAK integration summary → historical"

Move-FileIfExists `
    -Source (Join-Path $cliPath "COMPREHENSIVE_TEST_RESULTS.md") `
    -Destination (Join-Path $historicalPath "CLI_COMPREHENSIVE_TEST_RESULTS.md") `
    -Description "CLI test results → historical"

# 3. Move UpdateEngine.Functions documentation
Write-Host "`nMoving UpdateEngine.Functions documentation..." -ForegroundColor White
$functionsPath = Join-Path $repoRoot "UpdateEngine.Functions\src"

Move-FileIfExists `
    -Source (Join-Path $functionsPath "Functions\RESTRUCTURING_SUMMARY.md") `
    -Destination (Join-Path $historicalPath "FUNCTIONS_RESTRUCTURING_SUMMARY.md") `
    -Description "Functions restructuring → historical"

# 4. Organize docs root configuration files into guides
Write-Host "`nOrganizing configuration documentation..." -ForegroundColor White
$docsRoot = Join-Path $repoRoot "docs"

$configDocs = @(
    "CONFIGURATION.md",
    "CONFIGURATION_GUIDE.md",
    "CONFIGURATION_IMPLEMENTATION_SUMMARY.md",
    "CONFIGURATION_QUICK_REFERENCE.md",
    "CONFIGURATION_SIMPLIFICATION.md",
    "CONFIGURATION_SIMPLIFICATION_RESULTS.md"
)

foreach ($doc in $configDocs) {
    $source = Join-Path $docsRoot $doc
    $dest = Join-Path $guidesPath $doc
    if ((Test-Path $source) -and -not (Test-Path $dest)) {
        Move-FileIfExists -Source $source -Destination $dest -Description "$doc → guides/"
    }
}

# 5. Organize weekly reports in docs/guides
Write-Host "`nOrganizing weekly reports..." -ForegroundColor White
$weeklyReportsPath = Join-Path $repoRoot "docs\historical\weekly-reports"

$weeklyReports = Get-ChildItem -Path $guidesPath -Filter "WEEK*.md" -ErrorAction SilentlyContinue
foreach ($report in $weeklyReports) {
    Move-FileIfExists `
        -Source $report.FullName `
        -Destination (Join-Path $weeklyReportsPath $report.Name) `
        -Description "Weekly report: $($report.Name) → historical"
}

# 6. Move architecture documents
Write-Host "`nOrganizing architecture documentation..." -ForegroundColor White
$archPath = Join-Path $repoRoot "docs\architecture"

$archDocs = @(
    "Architecture.md",
    "ARCHITECTURE_DECISIONS.md",
    "ARCHITECTURE_DECISIONS_SNIPPET.md",
    "DATA_FLOW_ARCHITECTURE_GUIDE.md",
    "REPOSITORY_STRUCTURE.md"
)

foreach ($doc in $archDocs) {
    $source = Join-Path $guidesPath $doc
    Move-FileIfExists -Source $source -Destination (Join-Path $archPath $doc) -Description "$doc → architecture/"
}

# 7. Move troubleshooting guides
Write-Host "`nOrganizing troubleshooting documentation..." -ForegroundColor White
$troubleshootPath = Join-Path $repoRoot "docs\troubleshooting"

$troubleshootDocs = @(
    "SYNC_TROUBLESHOOTING.md",
    "CONFIGURATION_TROUBLESHOOTING.md",
    "CONTENT_SYNC_TROUBLESHOOTING_RESULTS.md",
    "AZURE_STORAGE_CONNECTION_FIX.md",
    "AZURE_BLOB_CONTAINER_FIX.md",
    "AZURE_SDK_VERIFICATION_SUMMARY.md",
    "APPHOST_DUPLICATE_ENDPOINT_FIX.md",
    "DOMAIN_SERVICES_REGISTRATION_FIX.md",
    "CONFIGURATION_LOADING_FIXES.md",
    "WCF_NET9_FIX_GUIDE.md",
    "QUICK_FIX_BUILD_LOCK.md"
)

foreach ($doc in $troubleshootDocs) {
    $source = Join-Path $guidesPath $doc
    Move-FileIfExists -Source $source -Destination (Join-Path $troubleshootPath $doc) -Description "$doc → troubleshooting/"
}

# 8. Move deployment guides
Write-Host "`nOrganizing deployment documentation..." -ForegroundColor White
$deployPath = Join-Path $repoRoot "docs\deployment"

$deployDocs = @(
    "PRODUCTION_DEPLOYMENT_GUIDE.md",
    "SCALING_AND_DEPLOYMENT_GUIDE.md"
)

foreach ($doc in $deployDocs) {
    $source = Join-Path $guidesPath $doc
    Move-FileIfExists -Source $source -Destination (Join-Path $deployPath $doc) -Description "$doc → deployment/"
}

#endregion

#region Scripts Reorganization

Write-Host "`n⚙️ SCRIPTS REORGANIZATION" -ForegroundColor Cyan
Write-Host ""

$scriptsRoot = Join-Path $repoRoot "scripts"

# 1. Move setup scripts
Write-Host "Organizing setup scripts..." -ForegroundColor White
$setupPath = Join-Path $scriptsRoot "setup"

Move-FileIfExists `
    -Source (Join-Path $scriptsRoot "configure-storage.ps1") `
    -Destination (Join-Path $setupPath "Configure-Storage.ps1") `
    -Description "Configure-Storage.ps1 → setup/"

Move-FileIfExists `
    -Source (Join-Path $scriptsRoot "configure-storage.sh") `
    -Destination (Join-Path $setupPath "configure-storage.sh") `
    -Description "configure-storage.sh → setup/"

Move-FileIfExists `
    -Source (Join-Path $scriptsRoot "start-with-storage.sh") `
    -Destination (Join-Path $setupPath "start-with-storage.sh") `
    -Description "start-with-storage.sh → setup/"

# 2. Move build scripts
Write-Host "`nOrganizing build scripts..." -ForegroundColor White
$buildPath = Join-Path $scriptsRoot "build"

Move-FileIfExists `
    -Source (Join-Path $scriptsRoot "validate-build.ps1") `
    -Destination (Join-Path $buildPath "Validate-Build.ps1") `
    -Description "Validate-Build.ps1 → build/"

# 3. Move test scripts
Write-Host "`nOrganizing test scripts..." -ForegroundColor White
$testPath = Join-Path $scriptsRoot "test"

Move-FileIfExists `
    -Source (Join-Path $scriptsRoot "Run-InMemoryTests.ps1") `
    -Destination (Join-Path $testPath "Run-InMemoryTests.ps1") `
    -Description "Run-InMemoryTests.ps1 → test/"

Move-FileIfExists `
    -Source (Join-Path $scriptsRoot "Test-AzuriteIntegration.ps1") `
    -Destination (Join-Path $testPath "Test-AzuriteIntegration.ps1") `
    -Description "Test-AzuriteIntegration.ps1 → test/"

Move-FileIfExists `
    -Source (Join-Path $scriptsRoot "test-azurite-config.ps1") `
    -Destination (Join-Path $testPath "Test-AzuriteConfig.ps1") `
    -Description "Test-AzuriteConfig.ps1 → test/"

Move-FileIfExists `
    -Source (Join-Path $scriptsRoot "test-startup.ps1") `
    -Destination (Join-Path $testPath "Test-Startup.ps1") `
    -Description "Test-Startup.ps1 → test/"

Move-FileIfExists `
    -Source (Join-Path $scriptsRoot "Test-SyncWithDiagnostics.ps1") `
    -Destination (Join-Path $testPath "Test-SyncWithDiagnostics.ps1") `
    -Description "Test-SyncWithDiagnostics.ps1 → test/"

Move-FileIfExists `
    -Source (Join-Path $repoRoot "UpdateEngine.Functions\src\test-config.ps1") `
    -Destination (Join-Path $testPath "Test-FunctionsConfig.ps1") `
    -Description "test-config.ps1 → test/ (from Functions/src)"

# 4. Move maintenance scripts
Write-Host "`nOrganizing maintenance scripts..." -ForegroundColor White
$maintPath = Join-Path $scriptsRoot "maintenance"

Move-FileIfExists `
    -Source (Join-Path $scriptsRoot "Fix-WCF-ServiceReferences.ps1") `
    -Destination (Join-Path $maintPath "Fix-WCF-ServiceReferences.ps1") `
    -Description "Fix-WCF-ServiceReferences.ps1 → maintenance/"

Move-FileIfExists `
    -Source (Join-Path $scriptsRoot "Regenerate-WCF-Net9.ps1") `
    -Destination (Join-Path $maintPath "Regenerate-WCF-Net9.ps1") `
    -Description "Regenerate-WCF-Net9.ps1 → maintenance/"

Move-FileIfExists `
    -Source (Join-Path $scriptsRoot "Regenerate-WCF-Net9-OfflineFirst.ps1") `
    -Destination (Join-Path $maintPath "Regenerate-WCF-Net9-OfflineFirst.ps1") `
    -Description "Regenerate-WCF-Net9-OfflineFirst.ps1 → maintenance/"

Move-FileIfExists `
    -Source (Join-Path $scriptsRoot "Regenerate-WCFReferences.ps1") `
    -Destination (Join-Path $maintPath "Regenerate-WCFReferences.ps1") `
    -Description "Regenerate-WCFReferences.ps1 → maintenance/"

Move-FileIfExists `
    -Source (Join-Path $scriptsRoot "Rename-UpdateEngine-Step1.ps1") `
    -Destination (Join-Path $maintPath "Rename-UpdateEngine-Step1.ps1") `
    -Description "Rename-UpdateEngine-Step1.ps1 → maintenance/"

Move-FileIfExists `
    -Source (Join-Path $scriptsRoot "Update-Namespaces-Step5.ps1") `
    -Destination (Join-Path $maintPath "Update-Namespaces-Step5.ps1") `
    -Description "Update-Namespaces-Step5.ps1 → maintenance/"

# 5. Copy deployment scripts (keep originals in Deployment/)
Write-Host "`nOrganizing deployment scripts..." -ForegroundColor White
$deployScriptsPath = Join-Path $scriptsRoot "deployment"

if (Test-Path (Join-Path $repoRoot "Deployment\deploy.ps1")) {
    if ($PSCmdlet.ShouldProcess("Deployment\deploy.ps1", "Copy to scripts/deployment/")) {
        Copy-Item -Path (Join-Path $repoRoot "Deployment\deploy.ps1") `
                  -Destination (Join-Path $deployScriptsPath "Deploy-Azure.ps1") `
                  -Force
        Write-Host "  ✓ Deploy-Azure.ps1 copied to deployment/" -ForegroundColor Green
    }
}

if (Test-Path (Join-Path $repoRoot "Deployment\deploy.sh")) {
    if ($PSCmdlet.ShouldProcess("Deployment\deploy.sh", "Copy to scripts/deployment/")) {
        Copy-Item -Path (Join-Path $repoRoot "Deployment\deploy.sh") `
                  -Destination (Join-Path $deployScriptsPath "deploy-azure.sh") `
                  -Force
        Write-Host "  ✓ deploy-azure.sh copied to deployment/" -ForegroundColor Green
    }
}

#endregion

#region Create Index Files

Write-Host "`n📋 CREATING INDEX FILES" -ForegroundColor Cyan
Write-Host ""

# Create scripts README
$scriptsReadme = @"
# Scripts Directory

This directory contains all automation scripts for the update-server-server-sync project.

## 📁 Directory Structure

``````
scripts/
├── setup/          # Installation and configuration scripts
├── build/          # Build and validation scripts  
├── test/           # Testing scripts
├── maintenance/    # Maintenance and regeneration scripts
├── migration/      # Migration scripts
└── deployment/     # Deployment scripts
``````

## 🔧 Setup Scripts

Scripts for initial setup and configuration:

- **Configure-Storage.ps1** - Configure Azure Storage or local file storage
- **configure-storage.sh** - Shell script version of storage configuration
- **start-with-storage.sh** - Start application with storage configured

## 🏗️ Build Scripts

Scripts for building and validating the codebase:

- **Validate-Build.ps1** - Validate build across all projects

## 🧪 Test Scripts

Scripts for running various tests:

- **Run-InMemoryTests.ps1** - Run in-memory tests
- **Test-AzuriteIntegration.ps1** - Test Azure Storage emulator integration
- **Test-AzuriteConfig.ps1** - Validate Azurite configuration
- **Test-Startup.ps1** - Test application startup
- **Test-SyncWithDiagnostics.ps1** - Test sync with diagnostic output
- **Test-FunctionsConfig.ps1** - Test Azure Functions configuration
- **Test-DownstreamSync.ps1** - Test downstream sync functionality
- **Test-DualHosting.ps1** - Test dual hosting scenarios
- **Test-LanguageConfig.ps1** - Test language configuration
- **Test-UpdateEngineFunctions-Standalone.ps1** - Test Functions standalone
- **Run-ConsolidationTests.ps1** - Run consolidation tests
- **Run-PostCleanupTests.ps1** - Run post-cleanup validation tests

## 🔧 Maintenance Scripts

Scripts for maintenance tasks:

- **Fix-WCF-ServiceReferences.ps1** - Fix WCF service references
- **Regenerate-WCF-Net9.ps1** - Regenerate WCF references for .NET 9
- **Regenerate-WCF-Net9-OfflineFirst.ps1** - Offline-first WCF regeneration
- **Regenerate-WCFReferences.ps1** - General WCF reference regeneration
- **Rename-UpdateEngine-Step1.ps1** - Rename UpdateEngine components
- **Update-Namespaces-Step5.ps1** - Update namespace references
- **Reorganize-FolderStructure.ps1** - Reorganize folder structure
- **Reorganize-Documentation-And-Scripts.ps1** - This reorganization script

## 🚀 Deployment Scripts

Scripts for deploying to Azure:

- **Deploy-Azure.ps1** - Deploy to Azure using PowerShell
- **deploy-azure.sh** - Deploy to Azure using bash

Note: Original deployment scripts remain in the ``Deployment/`` folder.

## 📚 Usage

Most scripts support ``-WhatIf`` parameter to preview changes:

``````powershell
.\scripts\setup\Configure-Storage.ps1 -WhatIf
``````

For detailed help on any script:

``````powershell
Get-Help .\scripts\setup\Configure-Storage.ps1 -Detailed
``````
"@

if ($PSCmdlet.ShouldProcess("scripts\README.md", "Create index file")) {
    Set-Content -Path (Join-Path $scriptsRoot "README.md") -Value $scriptsReadme -Force
    Write-Host "  ✓ Created scripts/README.md" -ForegroundColor Green
}

# Create docs README
$docsReadme = @"
# Documentation Directory

This directory contains all documentation for the update-server-server-sync project.

## 📁 Directory Structure

``````
docs/
├── guides/             # User guides and how-tos
├── architecture/       # Architecture documentation
├── troubleshooting/    # Troubleshooting guides
├── deployment/         # Deployment guides
├── fixes/              # Bug fix documentation
├── implementations/    # Implementation details
├── proposals/          # Design proposals
├── historical/         # Historical documentation
│   ├── weekly-reports/        # Weekly progress reports
│   └── migration-summaries/   # Migration completion summaries
├── api/                # API documentation (generated)
└── examples/           # Code examples
``````

## 📖 Key Documentation

### Getting Started

- **guides/QUICKSTART_DUAL_HOSTING_TESTS.md** - Quick start guide
- **guides/README.md** - Guide index
- **architecture/Architecture.md** - System architecture overview

### Configuration

- **guides/CONFIGURATION_GUIDE.md** - Configuration guide
- **guides/CONFIGURATION_QUICK_REFERENCE.md** - Quick reference
- **guides/STORAGE_GUIDE.md** - Storage configuration

### Testing

- **guides/TESTING_GUIDE.md** - Testing strategies
- **guides/INMEMORY_TESTING_GUIDE.md** - In-memory testing

### Troubleshooting

- **troubleshooting/SYNC_TROUBLESHOOTING.md** - Sync issues
- **troubleshooting/CONFIGURATION_TROUBLESHOOTING.md** - Configuration issues
- **troubleshooting/WCF_NET9_FIX_GUIDE.md** - WCF .NET 9 fixes

### Deployment

- **deployment/PRODUCTION_DEPLOYMENT_GUIDE.md** - Production deployment
- **deployment/SCALING_AND_DEPLOYMENT_GUIDE.md** - Scaling guide

## 🔍 Finding Documentation

Use the search function in your editor or:

``````powershell
# Search for specific topics
Get-ChildItem -Path docs -Filter "*sync*.md" -Recurse
``````

## 📝 Contributing

When adding new documentation:

1. Place it in the appropriate subdirectory
2. Use descriptive, UPPERCASE names for guide files
3. Update relevant README files
4. Follow existing formatting conventions
"@

if ($PSCmdlet.ShouldProcess("docs\README.md", "Create index file")) {
    Set-Content -Path (Join-Path $docsRoot "README.md") -Value $docsReadme -Force
    Write-Host "  ✓ Created docs/README.md" -ForegroundColor Green
}

#endregion

Write-Host "`n✅ Reorganization Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  • Documentation organized into themed folders" -ForegroundColor White
Write-Host "  • Scripts categorized by purpose" -ForegroundColor White
Write-Host "  • Index README files created" -ForegroundColor White
Write-Host "  • Historical documents preserved" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Review the moved files in their new locations" -ForegroundColor White
Write-Host "  2. Update any references in code or other docs" -ForegroundColor White
Write-Host "  3. Commit the changes to version control" -ForegroundColor White
