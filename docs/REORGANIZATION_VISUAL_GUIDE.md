# Repository Organization - Visual Guide

## Overview

This guide provides a visual representation of the newly organized repository structure after the November 2025 reorganization.

## 📁 Complete Directory Structure

```
update-server-server-sync/
│
├── 📄 README.md                      # Main repository README
├── 📄 LICENSE                        # License file
├── 📄 SECURITY.md                    # Security policy
├── 📄 microsoft-update.sln           # Main solution file
│
├── 📚 docs/                          # All documentation (well-organized)
│   ├── 📄 README.md                  # Documentation index
│   ├── 📄 REORGANIZATION_COMPLETE.md # This reorganization summary
│   │
│   ├── 🏗️  architecture/            # System architecture (5 files)
│   │   ├── Architecture.md
│   │   ├── ARCHITECTURE_DECISIONS.md
│   │   ├── ARCHITECTURE_DECISIONS_SNIPPET.md
│   │   ├── DATA_FLOW_ARCHITECTURE_GUIDE.md
│   │   └── REPOSITORY_STRUCTURE.md
│   │
│   ├── 📖 guides/                    # User guides (70 files)
│   │   ├── README.md
│   │   ├── CONFIGURATION_GUIDE.md    # ⭐ Essential
│   │   ├── STORAGE_GUIDE.md          # ⭐ Essential
│   │   ├── TESTING_GUIDE.md          # ⭐ Essential
│   │   ├── INMEMORY_TESTING_GUIDE.md
│   │   ├── CLI_QUICKSTART.md
│   │   ├── CLI_DOWNLOAD_GUIDE.md
│   │   ├── UPDATE_CLI_ARCHITECTURE.md
│   │   └── ... (65 more guides)
│   │
│   ├── 🔧 troubleshooting/           # Problem-solving guides (11 files)
│   │   ├── WCF_NET9_FIX_GUIDE.md     # ⭐ Important
│   │   ├── SYNC_TROUBLESHOOTING.md
│   │   ├── CONFIGURATION_TROUBLESHOOTING.md
│   │   ├── AZURE_STORAGE_CONNECTION_FIX.md
│   │   └── ... (7 more troubleshooting guides)
│   │
│   ├── 🚀 deployment/                # Deployment guides (2 files)
│   │   ├── PRODUCTION_DEPLOYMENT_GUIDE.md
│   │   └── SCALING_AND_DEPLOYMENT_GUIDE.md
│   │
│   ├── 📜 historical/                # Historical documentation (29 files)
│   │   ├── weekly-reports/           # Weekly progress (23 files)
│   │   │   ├── WEEK1_COMPLETION_SUMMARY.md
│   │   │   ├── WEEK2_COMPLETION_SUMMARY.md
│   │   │   ├── WEEK3_COMPLETION_SUMMARY.md
│   │   │   └── ... (20 more weekly reports)
│   │   │
│   │   └── migration-summaries/      # Migration docs (6 files)
│   │       ├── COMMIT_MESSAGE.md
│   │       ├── CONFIGURATION_CONSOLIDATION_COMPLETE.md
│   │       ├── SHARED_CONFIGURATION_COMPLETE.md
│   │       └── ... (3 more summaries)
│   │
│   ├── 🔨 fixes/                     # Bug fix documentation (4 files)
│   ├── 💡 implementations/           # Implementation details (1 file)
│   ├── 📋 proposals/                 # Design proposals (1 file)
│   ├── 📚 api/                       # API documentation (generated)
│   └── 💻 examples/                  # Code examples
│
├── ⚙️  scripts/                      # All automation scripts (organized)
│   ├── 📄 README.md                  # Scripts reference guide
│   │
│   ├── 🔧 setup/                     # Setup scripts (1 PS1, 2 shell)
│   │   ├── Configure-Storage.ps1     # ⭐ Essential
│   │   ├── configure-storage.sh
│   │   └── start-with-storage.sh
│   │
│   ├── 🏗️  build/                    # Build scripts (1 PS1)
│   │   └── Validate-Build.ps1        # ⭐ Essential
│   │
│   ├── 🧪 test/                      # Testing scripts (12 PS1)
│   │   ├── Run-InMemoryTests.ps1     # ⭐ Essential
│   │   ├── Test-AzuriteIntegration.ps1
│   │   ├── Test-AzuriteConfig.ps1
│   │   ├── Test-Startup.ps1
│   │   ├── Test-FunctionsConfig.ps1
│   │   ├── Test-DownstreamSync.ps1
│   │   ├── Test-DualHosting.ps1
│   │   └── ... (5 more test scripts)
│   │
│   ├── 🔧 maintenance/               # Maintenance scripts (8 PS1)
│   │   ├── Reorganize-Documentation-And-Scripts.ps1  # This script!
│   │   ├── Regenerate-WCF-Net9.ps1
│   │   ├── Fix-WCF-ServiceReferences.ps1
│   │   └── ... (5 more maintenance scripts)
│   │
│   ├── 🔄 migration/                 # Migration scripts (1 PS1)
│   │   └── Migrate-UpdateEngineCore.ps1
│   │
│   └── 🚀 deployment/                # Deployment scripts (1 PS1, 1 shell)
│       ├── Deploy-Azure.ps1
│       └── deploy-azure.sh
│
├── 💻 src/                           # Core libraries
│   ├── microsoft-update-partition/
│   ├── microsoft-update-webservices/
│   ├── microsoft-update-endpoints/
│   ├── microsoft-update-upstream-package-source/
│   ├── samples/
│   └── tools/
│
├── ⚡ UpdateEngine.Functions/        # Azure Functions (.NET 9)
│   ├── src/
│   │   ├── Functions/                # Organized function endpoints
│   │   └── README.md
│   └── test/
│
├── 🔧 UpdateEngine.Core/             # Shared core library
│   └── src/
│
├── 🎯 UpdateEngine.Cli/              # Command-line interface
│   ├── src/
│   └── README.md
│
├── 🌐 UpdateEngine.Endpoints/        # ASP.NET Core endpoints
├── 📦 UpdateEngine.Metadata/         # Metadata management
├── 🔄 UpdateEngine.UpstreamSource/   # Upstream sync
├── 🧩 UpdateEngine.WebServices/      # Web services
├── ⚙️  UpdateEngine.Configuration/   # Configuration management
├── 🏥 UpdateEngine.ServiceDefaults/  # Service defaults
├── 👷 UpdateEngine.WorkerService/    # Worker service
├── 🔧 UpdateEngine.SyncTool/         # Sync tool
│
├── 🎭 UpdateEngine.AppHost/          # .NET Aspire orchestration
│   └── src/
│
└── 🚀 Deployment/                    # Azure deployment templates
    ├── main.bicep
    ├── deploy.ps1                    # Original (referenced by scripts/)
    └── deploy.sh                     # Original (referenced by scripts/)
```

## 📊 Organization Statistics

### Documentation

| Category | Files | Purpose |
|----------|-------|---------|
| 📖 Guides | 70 | Active how-to guides and references |
| 🏗️ Architecture | 5 | System design and structure |
| 🔧 Troubleshooting | 11 | Problem-solving guides |
| 🚀 Deployment | 2 | Production deployment guides |
| 📜 Historical | 29 | Preserved development history |
| 🔨 Fixes | 4 | Bug fix documentation |
| 💡 Implementations | 1 | Implementation details |
| 📋 Proposals | 1 | Design proposals |
| **TOTAL** | **123** | **Well-organized documentation** |

### Scripts

| Category | PowerShell | Shell | Purpose |
|----------|-----------|-------|---------|
| 🔧 Setup | 1 | 2 | Installation and configuration |
| 🏗️ Build | 1 | 0 | Build validation |
| 🧪 Test | 12 | 0 | Testing automation |
| 🔧 Maintenance | 8 | 0 | Code maintenance |
| 🔄 Migration | 1 | 0 | Migration utilities |
| 🚀 Deployment | 1 | 1 | Azure deployment |
| **TOTAL** | **24** | **3** | **Comprehensive automation** |

## 🎯 Quick Navigation

### For New Contributors

1. **Start here**: `README.md` (root)
2. **Configuration**: `docs/guides/CONFIGURATION_GUIDE.md`
3. **Quick start**: `docs/guides/CLI_QUICKSTART.md`
4. **Testing**: Run `.\scripts\test\Run-InMemoryTests.ps1`

### For Developers

1. **Architecture**: `docs/architecture/Architecture.md`
2. **Testing guide**: `docs/guides/INMEMORY_TESTING_GUIDE.md`
3. **Troubleshooting**: `docs/troubleshooting/` folder
4. **All scripts**: `scripts/README.md`

### For DevOps

1. **Deployment**: `docs/deployment/PRODUCTION_DEPLOYMENT_GUIDE.md`
2. **Setup scripts**: `scripts/setup/`
3. **Build validation**: `scripts/build/Validate-Build.ps1`
4. **Bicep templates**: `Deployment/main.bicep`

## 🔍 Finding What You Need

### Search by Purpose

```powershell
# All configuration documentation
Get-ChildItem docs -Filter "*config*.md" -Recurse

# All test scripts
Get-ChildItem scripts\test -Filter "*.ps1"

# All architecture docs
Get-ChildItem docs\architecture

# All troubleshooting guides
Get-ChildItem docs\troubleshooting
```

### Browse by Category

```powershell
# Documentation index
Get-Content docs\README.md

# Scripts index
Get-Content scripts\README.md

# This visual guide
Get-Content docs\REORGANIZATION_VISUAL_GUIDE.md
```

## ✨ Key Improvements

### Before

- ❌ Documentation scattered across root, UpdateEngine.Cli, UpdateEngine.Functions
- ❌ Scripts mixed at scripts/ root level
- ❌ No clear categorization
- ❌ Hard to find related content
- ❌ Completion summaries cluttering root

### After

- ✅ All documentation in `docs/` with clear categories
- ✅ All scripts in `scripts/` organized by purpose
- ✅ Clear separation of active vs. historical docs
- ✅ Easy navigation with README files
- ✅ Clean root directory with only essentials

## 🚀 Getting Started

### Run Tests

```powershell
# Fast tests (no infrastructure)
.\scripts\test\Run-InMemoryTests.ps1

# Integration tests
.\scripts\test\Test-AzuriteIntegration.ps1
```

### Configure Storage

```powershell
# Interactive setup
.\scripts\setup\Configure-Storage.ps1
```

### Build and Validate

```powershell
# Validate entire solution
.\scripts\build\Validate-Build.ps1
```

### Deploy to Azure

```powershell
# Deploy with Bicep
.\scripts\deployment\Deploy-Azure.ps1
```

## 📚 Related Documentation

- **[Complete Reorganization Summary](./REORGANIZATION_COMPLETE.md)** - Detailed changelog
- **[Documentation Index](./README.md)** - Documentation table of contents
- **[Scripts Reference](../scripts/README.md)** - Scripts documentation
- **[Repository Structure](./architecture/REPOSITORY_STRUCTURE.md)** - Detailed structure

---

**Last Updated**: November 23, 2025  
**Script**: `scripts/maintenance/Reorganize-Documentation-And-Scripts.ps1`  
**Related**: `docs/REORGANIZATION_COMPLETE.md`
