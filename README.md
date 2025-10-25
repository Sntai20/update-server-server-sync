# Windows Update Services Server-Server Sync Protocol

Provide a C# implementation (.NET Core) of the Microsoft Update Server-Server sync protocol, both client and server.

Use this library to:
* Programmatically browse the Microsoft Update catalog
* Sync updates locally and run advanced queries on update metadata
* Export updates to WSUS
* Run an upstream update server in ASP.NET Core and serve updates to downstream WSUS servers
* Run an update server in ASP.NET Core and serve updates to Windows Update clients
* **NEW: Run as Azure Functions with .NET 9 support**

## ?? Quick Start

```powershell
# Clone repository
git clone https://github.com/microsoft/update-server-server-sync
cd update-server-server-sync

# Build
dotnet build

# Run tests (no infrastructure required!)
./scripts/test/Run-InMemoryTests.ps1

# Or run with Azure Functions
dotnet run --project AppHost
```

**?? For detailed instructions, see [QUICK_REFERENCE.md](./QUICK_REFERENCE.md)**

## 📂 Repository Organization

This repository is organized as follows:

- **[docs/](./docs/)** - All documentation (guides, troubleshooting, development)
- **[scripts/](./scripts/)** - All automation scripts (setup, build, test, maintenance)
- **[src/](./src/)** - Core libraries and implementation
- **[UpdateEngine/](./UpdateEngine/)** - Azure Functions implementation (.NET 9)
- **[AppHost/](./AppHost/)** - .NET Aspire application host
- **[test/](./test/)** - Test projects

**📚 For complete structure details, see [REPOSITORY_STRUCTURE.md](./REPOSITORY_STRUCTURE.md)**

## 📖 Documentation

### Essential Guides

- **[Quick Reference](./QUICK_REFERENCE.md)** - Quick start guide for developers
- **[Storage Configuration](./docs/guides/STORAGE_GUIDE.md)** - Configure Azure Storage or local storage
- **[Testing Guide](./docs/development/INMEMORY_TESTING_GUIDE.md)** - In-memory testing (no infrastructure required)
- **[Migration Summary](./docs/guides/MIGRATION_SUMMARY.md)** - Azure Storage migration guide

### Troubleshooting

- **[WCF .NET 9 Fixes](./docs/troubleshooting/WCF_NET9_FIX_GUIDE.md)** - Fix WCF compatibility issues
- **[Storage Issues](./docs/troubleshooting/TROUBLESHOOTING_STORAGE.md)** - Troubleshoot storage problems
- **[Sync Issues](./docs/troubleshooting/SYNC_TROUBLESHOOTING.md)** - Troubleshoot synchronization issues
- **[Container Verification](./docs/troubleshooting/CONTAINER_VERIFICATION.md)** - Verify Azure containers

### API Documentation

- **[API Reference](https://microsoft.github.io/update-server-server-sync/)** - Complete API documentation
- **[Code Examples](https://microsoft.github.io/update-server-server-sync/examples/categories-fetch.html)** - Usage examples

## 🛠️ Common Tasks

| Task | Command |
|------|---------|
| **Build** | `dotnet build` |
| **Test (Fast)** | `./scripts/test/Run-InMemoryTests.ps1` |
| **Test (All)** | `dotnet test` |
| **Run Functions** | `dotnet run --project AppHost/src/AppHost.csproj` |
| **Configure Storage** | `./scripts/setup/Configure-Storage.ps1` |
| **Validate Build** | `./scripts/build/Validate-Build.ps1` |

**📖 For more commands, see [QUICK_REFERENCE.md](./QUICK_REFERENCE.md)**

## Reference the library in your project

Visual Studio 2022 with .NET Core development tools is required to build the solution provided at `build/microsoft-update.sln`.

## Use the upsync utility

The upsync command line utility is provided as a sample for using the library. Upsync can be used to browse Microsoft's update catalog, sync updates locally and serve them to Windows Update clients or downstream WSUS servers.

You can build upsync in Visual Studio; it builds from the same solution as the library.

Or download and unzip upsync from [https://github.com/microsoft/update-server-server-sync/releases](https://github.com/microsoft/update-server-server-sync/releases)

See [upsync examples](https://github.com/microsoft/update-server-server-sync/wiki/UpSync-V3-examples)

## ✨ What's New in .NET 9

- 🚀 **Azure Functions support** - Run as serverless Azure Functions
- ⚡ **In-memory testing** - Fast tests with no infrastructure
- ☁️ **Azure Storage integration** - Modern Azure.Storage.Blobs SDK
- 🔧 **WCF .NET 9 compatibility** - Fixed service reference issues
- 💾 **Persistent storage** - Azurite with Docker volumes

**📚 For migration details, see [docs/guides/MIGRATION_SUMMARY.md](./docs/guides/MIGRATION_SUMMARY.md)**

## 🧪 Testing

This project includes comprehensive testing with **no infrastructure required** for fast development:

```powershell
# Fast in-memory tests (no Azurite/Docker needed!)
./scripts/test/Run-InMemoryTests.ps1

# Full integration tests (requires Azurite)
dotnet test --filter "Category=Integration"

# All tests
dotnet test
```

**?? For testing strategies, see [docs/development/INMEMORY_TESTING_GUIDE.md](./docs/development/INMEMORY_TESTING_GUIDE.md)**

# Contributing

This project welcomes contributions and suggestions. Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

---

## ?? Additional Resources

- [Repository Structure Guide](./REPOSITORY_STRUCTURE.md) - Detailed organization guide
- [Quick Reference](./QUICK_REFERENCE.md) - Developer quick reference
- [Reorganization Summary](./REORGANIZATION_SUMMARY.md) - How we organized this repo
- [Security Policy](./SECURITY.md) - Security and vulnerability reporting
