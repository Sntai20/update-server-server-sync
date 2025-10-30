# Windows Server 2022 Update Management with update-cli

The `update-cli` tool includes specialized commands for downloading and managing Windows Server 2022 updates in enterprise environments. This functionality follows the DRY (Don't Repeat Yourself) principle with a modular, extensible architecture that can be easily adapted for other Windows/Server versions.

## Architecture

The implementation uses a layered architecture following DRY principles:

- **`BaseWindowsDownloadHandler`** - Abstract base class providing common download functionality
- **`Server2022CommandHandler`** - Concrete implementation for Windows Server 2022 specifics
- **`CommandHandlers`** - Main command router delegating to specialized handlers
- **`UpdateEngineClient`** - HTTP client for UpdateEngine API communication

This design eliminates code duplication and makes it easy to add support for additional Windows/Server versions by simply extending the base class.

### Benefits of the DRY Architecture

- **Maintainability**: Common functionality is centralized in the base class
- **Extensibility**: New Windows/Server versions require minimal code
- **Consistency**: All Windows/Server handlers behave identically
- **Testing**: Shared logic can be tested once in the base class
- **Code Quality**: Reduced duplication means fewer bugs and easier refactoring

### Adding New Windows/Server Support

To add support for a new Windows/Server version, simply create a new handler:

```csharp
public class Server2025CommandHandler : BaseWindowsDownloadHandler
{
    public Server2025CommandHandler(UpdateEngineClient client) : base(client) { }
    
    protected override string[] GetSearchTerms(bool securityOnly) =>
        securityOnly ? new[] { "Security Updates" } 
                    : new[] { "Security Updates", "Critical Updates", "Windows Server 2025" };
    
    protected override string GetDisplayName() => "Windows Server 2025";
}
```

## Quick Start

### 1. Start UpdateEngine
```bash
# Start UpdateEngine (from repo root)
cd AppHost/src && dotnet run
```

### 2. Download Updates
```bash
# Download security updates for Windows Server 2022
update-cli server2022 download "C:\Server2022Updates" --security-only

# Download all update types (security, critical, rollups)
update-cli server2022 download "C:\Server2022Updates" --max-updates 100

# Download without syncing first (use existing metadata)
update-cli server2022 download "C:\Server2022Updates" --skip-sync
```

## Command Reference

### server2022 download

Downloads Windows Server 2022 updates with intelligent filtering and progress reporting.

**Syntax:**
```bash
update-cli server2022 download <downloadPath> [options]
```

**Arguments:**
- `<downloadPath>` - Directory where updates will be downloaded

**Options:**
- `--security-only` - Download only security updates (default: false)
- `--max-updates <number>` - Maximum number of updates to process (default: 50)
- `--skip-sync` - Skip metadata synchronization (default: false)
- `--url <url>` - UpdateEngine base URL (default: http://localhost:7071)
- `--timeout <seconds>` - Request timeout (default: 300)

## What the Command Does

### 1. Directory Setup
Creates organized directory structure:
```
<downloadPath>/
├── metadata/          # Update metadata (JSON files)
├── content/           # Update content files (.msu, .cab, etc.)
└── logs/              # Operation logs and reports
    ├── search-results.json
    ├── update-ids.txt
    └── download-report.json
```

### 2. Connectivity Check
Verifies UpdateEngine is accessible and responding before proceeding.

### 3. Metadata Synchronization (Optional)
- Syncs latest metadata from Microsoft Update servers
- Can be skipped with `--skip-sync` to use existing local metadata
- Handles sync failures gracefully

### 4. Intelligent Search
Searches for relevant updates using multiple search terms:

**Security Only Mode:**
- "Security Updates"

**Full Mode:**
- "Security Updates"
- "Critical Updates" 
- "Update Rollups"
- "Windows Server 2022"

### 5. Metadata Download
- Downloads update metadata for each found update
- Stores metadata as JSON files for analysis
- Progress reporting during download

### 6. Content Analysis
- Parses metadata to identify download candidates
- Filters by size (skips updates > 1GB by default)
- Prioritizes based on update classification

### 7. Content Download
- Downloads actual update files (.msu, .cab, etc.)
- Real-time progress reporting with transfer rates
- Verifies downloaded file integrity

### 8. Comprehensive Reporting
Generates detailed JSON report with:
- Operation summary and timestamps
- Search terms and results
- Success/failure counts
- Total download size and file counts
- Lists of successful and failed downloads

## Usage Examples

### Basic Security Download
```bash
# Download security updates to C:\Updates
update-cli server2022 download "C:\Updates" --security-only
```

### Large Enterprise Download
```bash
# Download up to 200 updates of all types
update-cli server2022 download "\\FileServer\Updates\Server2022" --max-updates 200
```

### Offline/Cached Download
```bash
# Use existing metadata without syncing
update-cli server2022 download "C:\Updates" --skip-sync --max-updates 100
```

### Custom UpdateEngine Location
```bash
# Use UpdateEngine on different server
update-cli server2022 download "C:\Updates" --url "http://update-server:8080"
```

## Output and Reports

### Console Output
The command provides color-coded progress output:
- 📁 Directory operations
- 🔍 Search and connectivity checks
- 📡 Metadata synchronization
- 📥 Metadata downloads
- 📦 Content downloads
- ✓ Success indicators
- ⚠ Warnings
- ✗ Errors

### Generated Files

**download-report.json** - Comprehensive operation report:
```json
{
  "Timestamp": "2025-01-24T10:30:00.000Z",
  "DownloadPath": "C:\\Updates",
  "SecurityOnly": true,
  "MaxUpdates": 50,
  "UpdatesFound": 125,
  "MetadataSuccess": 45,
  "ContentSuccess": 42,
  "TotalDownloadMB": 1250.5,
  "TotalDownloadGB": 1.22,
  "SuccessfulDownloads": ["KB5001234", "KB5005678"],
  "FailedDownloads": ["KB5009999"]
}
```

**search-results.json** - Raw search results from each search term
**update-ids.txt** - List of all discovered update IDs

## Enterprise Integration

### Scheduled Execution
Use Windows Task Scheduler or PowerShell to automate:

```powershell
# PowerShell script for scheduled downloads
$downloadPath = "C:\Enterprise\Updates\Server2022"
$maxUpdates = 100

# Run download
& update-cli server2022 download $downloadPath --max-updates $maxUpdates --security-only

# Check results
$report = Get-Content "$downloadPath\logs\download-report.json" | ConvertFrom-Json
if ($report.ContentSuccess -gt 0) {
    Write-Host "Downloaded $($report.ContentSuccess) updates ($($report.TotalDownloadGB) GB)"
    # Trigger deployment automation
}
```

### Network Considerations
- **Bandwidth**: Configure `--max-updates` based on available bandwidth
- **Storage**: Ensure adequate disk space (typically 50-500MB per update)
- **Firewall**: UpdateEngine needs access to Microsoft Update servers
- **Proxy**: Configure system proxy settings if required

### Security Best Practices
1. **Validation**: Review `download-report.json` before deployment
2. **Testing**: Test updates in lab environment first
3. **Staging**: Use staged deployment approach
4. **Monitoring**: Monitor update installation success rates
5. **Rollback**: Maintain rollback procedures

## Troubleshooting

### Common Issues

**UpdateEngine Connection Failed**
```
✗ Cannot connect to UpdateEngine: Connection refused
```
- Ensure UpdateEngine is running: `cd AppHost/src && dotnet run`
- Check URL with `--url` option
- Verify firewall settings

**No Updates Found**
```
⚠ No updates found
```
- Run metadata sync: `update-cli sync metadata`
- Check categories: `update-cli categories`
- Verify search terms in search-results.json

**Download Failures**
```
✗ Content download failed: 3 updates
```
- Review failed downloads in download-report.json
- Check disk space
- Retry individual downloads with `update-cli download content <updateId>`

### Log Analysis
Review generated logs for detailed information:
- `logs/download-report.json` - Operation summary
- `logs/search-results.json` - Search results per term
- `logs/update-ids.txt` - All discovered update IDs

## Integration with Deployment Tools

### WSUS Integration
Downloaded updates can be imported into WSUS:
```powershell
# Import downloaded updates to WSUS
$wsus = Get-WsusServer
$updateFiles = Get-ChildItem "C:\Updates\content" -Filter "*.msu"
foreach ($file in $updateFiles) {
    # Import logic here
}
```

### ConfigMgr Integration
Updates can be added to Configuration Manager:
```powershell
# Add to ConfigMgr software update packages
Import-Module ConfigurationManager
$updates = Get-ChildItem "C:\Updates\content"
# ConfigMgr integration logic
```

### Direct Installation
For standalone servers:
```powershell
# Install updates directly
$updates = Get-ChildItem "C:\Updates\content" -Filter "*.msu"
foreach ($update in $updates) {
    Write-Host "Installing $($update.Name)"
    Start-Process -FilePath "wusa.exe" -ArgumentList "$($update.FullName)", "/quiet", "/norestart" -Wait
}
```

## Best Practices

### Performance Optimization
- Use `--skip-sync` for repeated downloads in short timeframes
- Adjust `--max-updates` based on network capacity
- Run during maintenance windows for large downloads

### Storage Management
- Monitor disk space in download directory
- Implement cleanup procedures for old updates
- Consider network storage for centralized management

### Change Management
- Test updates in isolated environment first
- Maintain update deployment schedules
- Document update approval processes
- Track update installation compliance

### Monitoring and Alerting
- Monitor download success rates
- Alert on failed downloads
- Track storage usage
- Monitor UpdateEngine health

This built-in functionality provides enterprise-ready Windows Server 2022 update management directly through the update-cli tool, eliminating the need for separate PowerShell scripts while providing comprehensive automation and reporting capabilities.