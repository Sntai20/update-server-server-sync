# Update CLI Architecture Guide

This document describes the architecture of the `update-cli` tool, focusing on the DRY (Don't Repeat Yourself) principles and extensible design patterns used throughout the codebase.

## Overview

The `update-cli` tool follows a layered, command-pattern architecture that separates concerns and promotes code reuse. The design prioritizes maintainability, testability, and extensibility.

## Core Architecture

```
┌─────────────────────────────────────────────────────┐
│                   Program.cs                        │
│              (Entry Point & CLI Setup)              │
└─────────────────────┬───────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────┐
│                CommandHandlers                      │
│            (Main Command Router)                    │
└─────────────────────┬───────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────┐
│          BaseWindowsDownloadHandler                 │
│         (Abstract Base Class - DRY)                │
└─────────────────────┬───────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────┐
│         Server2022CommandHandler                    │
│       (Concrete Implementation)                     │
└─────────────────────┬───────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────┐
│              UpdateEngineClient                     │
│           (HTTP API Communication)                  │
└─────────────────────────────────────────────────────┘
```

## DRY Principles Implementation

### 1. Base Class Pattern

**Problem**: Multiple Windows/Server versions would require duplicate download logic.

**Solution**: `BaseWindowsDownloadHandler` abstract class containing all common functionality.

```csharp
public abstract class BaseWindowsDownloadHandler
{
    // Common functionality for all Windows/Server versions
    protected abstract string[] GetSearchTerms(bool securityOnly);
    protected abstract string GetDisplayName();
    
    // Shared implementation
    public async Task<int> HandleBulkDownloadAsync(...)
    {
        // Common workflow for all versions
    }
}
```

**Benefits**:
- 90% code reuse across different Windows/Server versions
- Consistent behavior and error handling
- Centralized testing of core logic
- Easy maintenance and bug fixes

### 2. Template Method Pattern

The `HandleBulkDownloadAsync` method implements the Template Method pattern:

```csharp
public async Task<int> HandleBulkDownloadAsync(...)
{
    // 1. Create directories (common)
    var (metadata, content, logs) = await CreateDirectoriesAsync(downloadPath);
    
    // 2. Check connectivity (common)
    var connected = await CheckConnectivityAsync();
    
    // 3. Search for updates (version-specific via GetSearchTerms())
    var updateIds = await SearchAndCollectUpdatesAsync(securityOnly, maxUpdates, logsPath);
    
    // 4. Download metadata (common)
    var (metadataSuccess, metadataFailed) = await DownloadMetadataAsync(updateIds, metadataPath);
    
    // 5. Download content (common)
    var (contentSuccess, contentFailed, totalSize) = await DownloadContentAsync(...);
    
    // 6. Generate report (common with version-specific display name)
    await GenerateReportAsync(reportData, logsPath);
}
```

**Benefits**:
- Enforces consistent workflow across all versions
- Allows customization at specific points (search terms, display names)
- Makes the process transparent and debuggable

### 3. Command Pattern

Each command handler encapsulates a specific operation:

```csharp
public class CommandHandlers
{
    public async Task<int> HandleDownloadServer2022Async(...)
    {
        var handler = new Server2022CommandHandler(this.updateEngineClient);
        return await handler.HandleDownloadServer2022Async(...);
    }
}
```

**Benefits**:
- Clear separation of concerns
- Easy to add new commands
- Testable in isolation
- Consistent error handling

## Code Organization

### Directory Structure

```
src/tools/update-cli/
├── Commands/
│   ├── BaseWindowsDownloadHandler.cs    # DRY base class
│   ├── CommandHandlers.cs               # Main command router
│   └── Server2022CommandHandler.cs      # Concrete implementation
├── Services/
│   └── UpdateEngineClient.cs            # API communication
├── Configuration/
│   └── UpdateEngineConfiguration.cs     # Configuration model
└── Program.cs                           # Entry point & CLI setup
```

### Key Classes

#### BaseWindowsDownloadHandler (Abstract)
- **Purpose**: Provides common download functionality for all Windows/Server versions
- **Key Methods**:
  - `HandleBulkDownloadAsync()` - Main workflow template
  - `CreateDirectoriesAsync()` - Directory setup
  - `CheckConnectivityAsync()` - Health checks
  - `SearchAndCollectUpdatesAsync()` - Update discovery
  - `DownloadMetadataAsync()` - Metadata download
  - `DownloadContentAsync()` - Content download
  - `GenerateReportAsync()` - Report generation

#### Server2022CommandHandler (Concrete)
- **Purpose**: Windows Server 2022 specific implementation
- **Key Methods**:
  - `GetSearchTerms()` - Returns Server 2022 specific search terms
  - `GetDisplayName()` - Returns "Windows Server 2022"
  - `HandleDownloadServer2022Async()` - Public API method

#### UpdateEngineClient (Service)
- **Purpose**: HTTP communication with UpdateEngine
- **Key Methods**:
  - `SearchUpdatesAsync()` - Search for updates
  - `DownloadUpdateMetadataAsync()` - Download metadata
  - `DownloadUpdateContentWithProgressAsync()` - Download content with progress
  - `GetHealthStatusAsync()` - Health checks

## Extensibility Patterns

### Adding New Windows/Server Versions

To add support for Windows 11, Windows Server 2025, etc.:

1. **Create new handler** (5-10 lines of code):
```csharp
public class Windows11CommandHandler : BaseWindowsDownloadHandler
{
    public Windows11CommandHandler(UpdateEngineClient client) : base(client) { }
    
    protected override string[] GetSearchTerms(bool securityOnly) =>
        securityOnly ? new[] { "Security Updates" } 
                    : new[] { "Security Updates", "Critical Updates", "Windows 11" };
    
    protected override string GetDisplayName() => "Windows 11";
    
    public async Task<int> HandleDownloadWindows11Async(string downloadPath, bool securityOnly = false, int maxUpdates = 50, bool skipSync = false)
    {
        return await HandleBulkDownloadAsync(downloadPath, securityOnly, maxUpdates, skipSync);
    }
}
```

2. **Add command to Program.cs**:
```csharp
// Add to root command
CreateWindows11Command(commandHandlers)

// Add command creation method
private static Command CreateWindows11Command(CommandHandlers handlers)
{
    var command = new Command("windows11", "Windows 11 bulk download operations");
    // ... setup similar to server2022
    return command;
}
```

3. **Add delegation in CommandHandlers.cs**:
```csharp
public async Task<int> HandleDownloadWindows11Async(string downloadPath, bool securityOnly = false, int maxUpdates = 50, bool skipSync = false)
{
    var handler = new Windows11CommandHandler(this.updateEngineClient);
    return await handler.HandleDownloadWindows11Async(downloadPath, securityOnly, maxUpdates, skipSync);
}
```

**Total effort**: ~20 lines of code for complete new version support!

### Adding New Download Features

To add new features (e.g., different filtering, custom report formats):

1. **Extend base class** with virtual methods for customization points
2. **Override in concrete classes** for version-specific behavior
3. **Maintain backward compatibility** with existing handlers

## Testing Strategy

### Unit Testing
- **Base class testing**: Test common functionality once
- **Concrete class testing**: Test only version-specific logic
- **Mock dependencies**: Use mock `UpdateEngineClient` for isolated testing

### Integration Testing
- **End-to-end testing**: Test complete workflows
- **API integration**: Test with real UpdateEngine
- **File system testing**: Verify directory/file operations

## Configuration Management

### Dependency Injection
```csharp
services.AddHttpClient<UpdateEngineClient>();
services.Configure<UpdateEngineConfiguration>(configuration.GetSection("UpdateEngine"));
services.AddTransient<CommandHandlers>();
```

### Configuration Options
```json
{
  "UpdateEngine": {
    "BaseUrl": "http://localhost:7071",
    "Timeout": "00:05:00"
  }
}
```

## Error Handling Strategy

### Graceful Degradation
- Continue on non-critical failures (individual update downloads)
- Fail fast on critical failures (connectivity, directory creation)
- Comprehensive error reporting in JSON reports

### Logging and Monitoring
- Console output for user feedback
- Structured JSON reports for automation
- Detailed error messages with actionable guidance

## Performance Considerations

### Async/Await Patterns
- All I/O operations are asynchronous
- Proper cancellation token support
- Progress reporting for long-running operations

### Memory Management
- Streaming for large file downloads
- Proper disposal of resources
- Configurable batch sizes

### Network Optimization
- Configurable timeouts
- Retry logic for transient failures
- Parallel downloads where appropriate

## Future Enhancements

### Planned Improvements
1. **Plugin Architecture**: Allow external command handlers
2. **Configuration Profiles**: Predefined configurations for different scenarios
3. **Caching**: Intelligent caching of metadata and search results
4. **Advanced Filtering**: More sophisticated update filtering options
5. **Scheduling**: Built-in scheduling capabilities
6. **Monitoring**: Integration with monitoring systems

### Maintaining DRY Principles
- Always evaluate new features for common patterns
- Refactor shared code into base classes
- Use composition over inheritance where appropriate
- Maintain clear separation of concerns

This architecture ensures the `update-cli` tool remains maintainable, extensible, and follows best practices for enterprise software development.