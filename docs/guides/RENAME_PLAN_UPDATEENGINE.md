# Naming Convention Refactoring Plan: UpdateEngine.*

## 🎯 Goal
Rename all projects to use consistent `UpdateEngine.*` naming pattern throughout the codebase.

## 📋 Complete Mapping

| Current Name | New Name | Type | Impact |
|--------------|----------|------|--------|
| `microsoft-update-partition/` | `UpdateEngine.Metadata/` | Core Library | NuGet Breaking |
| `microsoft-update-webservices/` | `UpdateEngine.WebServices/` | Core Library | NuGet Breaking |
| `microsoft-update-endpoints/` | `UpdateEngine.Endpoints/` | Core Library | NuGet Breaking |
| `microsoft-update-upstream-source/` | `UpdateEngine.UpstreamSource/` | Core Library | NuGet Breaking |
| `UpdateEngine.Functions/` | `UpdateEngine.Functions/` | Azure Functions | No Breaking |
| `UpdateEngine.Core/` | `UpdateEngine.Core/` | ✅ Keep | No Change |
| `WorkerService/` | `UpdateEngine.WorkerService/` | Worker Service | No Breaking |
| `update-cli/` | `UpdateEngine.Cli/` | CLI Tool | No Breaking |
| `upsync/` | `UpdateEngine.SyncTool/` | Utility | No Breaking |
| `Configuration/` | `UpdateEngine.Configuration/` | Shared Config | No Breaking |
| `UpdateEngine.AppHost/` | `UpdateEngine.AppHost/` | Aspire Host | No Breaking |
| `ServiceDefaults/` | `UpdateEngine.ServiceDefaults/` | Aspire Defaults | No Breaking |

## 🔄 Namespace Mappings

| Current Namespace | New Namespace |
|-------------------|---------------|
| `Microsoft.PackageGraph.MicrosoftUpdate` | `UpdateEngine.Metadata` |
| `Microsoft.PackageGraph.MicrosoftUpdate.Endpoints` | `UpdateEngine.Endpoints` |
| `Microsoft.PackageGraph.MicrosoftUpdate.Source` | `UpdateEngine.UpstreamSource` |
| `Microsoft.UpdateServices.WebServices` | `UpdateEngine.WebServices` |
| `Microsoft.UpdateServices.WorkerService` | `UpdateEngine.WorkerService` |
| `Microsoft.PackageGraph.Utilitites.Upsync` | `UpdateEngine.SyncTool` |
| `UpdateCli` | `UpdateEngine.Cli` |
| `Configuration` | `UpdateEngine.Configuration` |
| `UpdateEngine.Core` | `UpdateEngine.Core` ✅ |

## 📦 NuGet Package Impact

### Breaking Changes Required
Since these are already published NuGet packages, we'll need to:

1. **Create new packages** with new names
2. **Mark old packages as deprecated** (add obsolete notice)
3. **Publish migration guide** for consumers
4. **Version as major bump** (e.g., 1.x → 2.0)

### Package Transition Plan

```xml
<!-- Old packages (mark as deprecated) -->
Microsoft.PackageGraph.MicrosoftUpdate v1.x [Deprecated - use UpdateEngine.Metadata v2.0+]
Microsoft.UpdateServices.WebServices v1.x [Deprecated - use UpdateEngine.WebServices v2.0+]

<!-- New packages -->
UpdateEngine.Metadata v2.0.0
UpdateEngine.WebServices v2.0.0
UpdateEngine.Endpoints v2.0.0
UpdateEngine.UpstreamSource v2.0.0
```

## 🔨 Implementation Steps

### Step 1: Folder Renaming (Git Operations)
```powershell
# Use git mv to preserve history
git mv microsoft-update-partition UpdateEngine.Metadata
git mv microsoft-update-webservices UpdateEngine.WebServices
git mv microsoft-update-endpoints UpdateEngine.Endpoints
git mv microsoft-update-upstream-source UpdateEngine.UpstreamSource
git mv UpdateEngine UpdateEngine.Functions
git mv WorkerService UpdateEngine.WorkerService
git mv update-cli UpdateEngine.Cli
git mv upsync UpdateEngine.SyncTool
git mv Configuration UpdateEngine.Configuration
git mv AppHost UpdateEngine.AppHost
git mv ServiceDefaults UpdateEngine.ServiceDefaults
```

### Step 2: Project File Updates
For each `.csproj` file, update:
- `<RootNamespace>` → Match new name
- `<AssemblyName>` → Match new name
- `<PackageId>` → Match new name (for NuGet)
- All `<ProjectReference>` paths

### Step 3: Solution File Update
Update `microsoft-update.sln`:
- Rename to `UpdateEngine.sln`
- Update all project paths
- Update project GUIDs if needed

### Step 4: Code Updates
- Update all `namespace` declarations
- Update all `using` statements
- Update XML documentation references

### Step 5: Configuration & Scripts
- Update paths in `.github/workflows/`
- Update paths in `scripts/`
- Update `Directory.Build.props` if needed
- Update documentation

### Step 6: Validation
- Build entire solution
- Run all tests
- Verify NuGet pack works
- Test Aspire orchestration

## ⚠️ Migration Guide for Consumers

```markdown
# Migration Guide: UpdateEngine 2.0

## Breaking Changes

### Package Name Changes
- `Microsoft.PackageGraph.MicrosoftUpdate` → `UpdateEngine.Metadata`
- `Microsoft.UpdateServices.WebServices` → `UpdateEngine.WebServices`
- `Microsoft.PackageGraph.MicrosoftUpdate.Endpoints` → `UpdateEngine.Endpoints`
- `Microsoft.PackageGraph.MicrosoftUpdate.Source` → `UpdateEngine.UpstreamSource`

### Namespace Changes
```csharp
// Before
using Microsoft.PackageGraph.MicrosoftUpdate;
using Microsoft.UpdateServices.WebServices;

// After  
using UpdateEngine.Metadata;
using UpdateEngine.WebServices;
```

### Migration Steps
1. Update NuGet package references in `.csproj`
2. Replace namespace using statements
3. Rebuild solution
```

## 📊 Risk Assessment

| Risk | Severity | Mitigation |
|------|----------|------------|
| Breaking existing consumers | **High** | Publish migration guide, deprecation notice |
| Build failures during transition | Medium | Incremental approach, frequent testing |
| Missing references | Medium | Automated search/replace, validation |
| Documentation out of sync | Low | Update docs as part of refactor |

## ✅ Success Criteria

- [ ] All projects renamed and building successfully
- [ ] All tests passing
- [ ] Solution loads and builds in Visual Studio
- [ ] Aspire AppHost orchestrates correctly
- [ ] NuGet packages can be created
- [ ] Documentation updated
- [ ] Migration guide published

## 🚀 Estimated Effort

- **Folder & File Renames**: 1-2 hours
- **Code Updates (namespaces/usings)**: 3-4 hours
- **Testing & Validation**: 2-3 hours
- **Documentation Updates**: 1-2 hours
- **Total**: 7-11 hours

## 📝 Next Actions

1. Confirm this plan looks good
2. Create backup/branch
3. Execute Step 1 (folder renames)
4. Execute Step 2 (project updates)
5. Continue through all steps
6. Validate and test

---

**Ready to proceed?** I can start with Step 1 (folder renames) or we can adjust the plan first.
