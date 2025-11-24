# Documentation Directory

This directory contains all documentation for the update-server-server-sync project.

## 📁 Directory Structure

```
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
```

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

```powershell
# Search for specific topics
Get-ChildItem -Path docs -Filter "*sync*.md" -Recurse
```

## 📝 Contributing

When adding new documentation:

1. Place it in the appropriate subdirectory
2. Use descriptive, UPPERCASE names for guide files
3. Update relevant README files
4. Follow existing formatting conventions
