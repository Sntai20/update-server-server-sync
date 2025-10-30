# UpdateEngine CLI Tool

A command-line interface for querying and managing the Microsoft Update Server-Server Sync UpdateEngine.

## Features

- **Health Monitoring**: Check UpdateEngine health status
- **Configuration**: View server configuration
- **Synchronization**: Trigger metadata and content sync operations
- **Statistics**: Get store statistics and metrics
- **Search**: Search for updates by category
- **Update Details**: Get detailed information about specific updates
- **Downloads**: Download update metadata and content files
- **Windows Server 2022**: Specialized bulk download operations for Windows Server 2022
- **Maintenance**: Reindex the metadata store
- **Categories**: List available update categories

## Installation

### Build from Source

```bash
dotnet build
dotnet publish -c Release
```

### Run from Development

```bash
dotnet run -- [command] [options]
```

## Usage

### Basic Commands

```bash
# Check health status
update-cli health

# Get server configuration
update-cli config

# Get store statistics
update-cli stats

# List available categories
update-cli categories
```

### Synchronization

```bash
# Sync metadata from upstream
update-cli sync metadata

# Sync content from upstream
update-cli sync content
```

### Search and Query

```bash
# Search for updates in a category
update-cli search "Security Updates"

# Get details for a specific update
update-cli details {update-id}
```

### Downloads

```bash
# List available downloads for an update
update-cli download list {update-id}

# Download update metadata as JSON
update-cli download metadata {update-id}
update-cli download metadata {update-id} --output custom-metadata.json

# Download update content files
update-cli download content {update-id}
update-cli download content {update-id} --output /path/to/download
update-cli download content {update-id} --progress  # Show download progress
```

### Windows Server 2022 Operations

```bash
# Download security updates for Windows Server 2022
update-cli server2022 download "C:\Server2022Updates" --security-only

# Download all update types (security, critical, rollups)
update-cli server2022 download "C:\Server2022Updates" --max-updates 100

# Download without syncing first (use existing metadata)
update-cli server2022 download "C:\Server2022Updates" --skip-sync

# Comprehensive enterprise download
update-cli server2022 download "\\FileServer\Updates\Server2022" --max-updates 200
```

### Maintenance

```bash
# Reindex the metadata store
update-cli reindex
```

### Global Options

```bash
# Specify UpdateEngine URL
update-cli health --url http://localhost:7071

# Set request timeout (seconds)
update-cli health --timeout 60
```

## Configuration

The tool can be configured via:

1. **appsettings.json** file:
```json
{
  "UpdateEngine": {
    "BaseUrl": "http://localhost:7071",
    "Timeout": "00:05:00"
  }
}
```

2. **Environment variables**:
```bash
export UpdateEngine__BaseUrl=http://localhost:7071
export UpdateEngine__Timeout=00:05:00
```

3. **Command-line options** (highest priority):
```bash
update-cli health --url http://my-server:8080 --timeout 120
```

## Examples

### Check if UpdateEngine is running
```bash
update-cli health
```

### Get comprehensive status
```bash
update-cli stats
update-cli config
```

### Perform manual sync
```bash
# Sync metadata first
update-cli sync metadata

# Then sync content if needed
update-cli sync content
```

### Search for security updates
```bash
update-cli search "Security Updates"
```

### Maintenance operations
```bash
# Reindex if needed
update-cli reindex

# Check health after maintenance
update-cli health
```

## Error Handling

The CLI tool provides meaningful error messages and appropriate exit codes:
- **0**: Success
- **1**: Error occurred (check output for details)

Common issues:
- **Connection refused**: UpdateEngine is not running
- **Timeout**: Operation took too long (increase timeout)
- **404 errors**: Endpoint not available (check UpdateEngine version)

## Dependencies

- .NET 9.0
- System.CommandLine for command parsing
- HttpClient for API communication
- Microsoft.Extensions.* for configuration and DI

## Documentation

- **[Architecture Guide](../../docs/guides/UPDATE_CLI_ARCHITECTURE.md)** - Detailed architecture documentation and DRY principles
- **[Windows Server 2022 CLI Guide](../../docs/guides/SERVER2022_CLI_GUIDE.md)** - Comprehensive guide for enterprise Windows Server 2022 update management
- **[Download Guide](DOWNLOAD_GUIDE.md)** - General download functionality
- **[Quick Start](QUICKSTART.md)** - Getting started guide

## Related

- [UpdateEngine Documentation](../../../UpdateEngine/README.md)
- [AppHost Documentation](../../../AppHost/README.md)
- [Configuration Documentation](../../../Configuration/README.md)