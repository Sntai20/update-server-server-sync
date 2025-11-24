# Scripts Directory

This directory contains all automation scripts for the update-server-server-sync project.

## 📁 Directory Structure

```
scripts/
├── setup/          # Installation and configuration scripts
├── build/          # Build and validation scripts  
├── test/           # Testing scripts
├── maintenance/    # Maintenance and regeneration scripts
├── migration/      # Migration scripts
└── deployment/     # Deployment scripts
```

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

Note: Original deployment scripts remain in the `Deployment/` folder.

## 📚 Usage

Most scripts support `-WhatIf` parameter to preview changes:

```powershell
.\scripts\setup\Configure-Storage.ps1 -WhatIf
```

For detailed help on any script:

```powershell
Get-Help .\scripts\setup\Configure-Storage.ps1 -Detailed
```
