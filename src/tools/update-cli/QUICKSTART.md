# UpdateEngine CLI Tool - Quick Start Guide

## 🎯 **Overview**

The UpdateEngine CLI (`update-cli`) is a command-line tool for querying and managing the Microsoft Update Server-Server Sync UpdateEngine. It provides a comprehensive interface for health monitoring, synchronization, querying updates, and maintenance operations.

## 📦 **Installation & Setup**

### Build the Tool
```bash
# Build from source
dotnet build src/tools/update-cli/update-cli.csproj

# Or build the entire solution
dotnet build microsoft-update.sln
```

### Run the Tool
```bash
# Direct execution
dotnet run --project src/tools/update-cli/update-cli.csproj -- [command]

# Using convenience scripts
src/tools/update-cli/update-cli.cmd [command]    # Windows
src/tools/update-cli/update-cli.sh [command]     # Unix/Linux/macOS
```

## 🚀 **Quick Commands**

### Essential Operations
```bash
# Check if UpdateEngine is running
update-cli health

# Get comprehensive status
update-cli stats

# View configuration
update-cli config

# List available update categories
update-cli categories
```

### Synchronization
```bash
# Sync metadata from Microsoft Update
update-cli sync metadata

# Sync content files (if enabled)
update-cli sync content
```

### Querying Updates
```bash
# Search for updates by category
update-cli search "Security Updates"
update-cli search "Critical Updates"

# Get details for a specific update
update-cli details {update-id}
```

### Maintenance
```bash
# Reindex the metadata store
update-cli reindex
```

## ⚙️ **Configuration**

### Default Configuration (appsettings.json)
```json
{
  "UpdateEngine": {
    "BaseUrl": "http://localhost:7071",
    "Timeout": "00:05:00"
  }
}
```

### Override with Command Line
```bash
# Different server
update-cli health --url http://my-server:8080

# Custom timeout
update-cli sync metadata --timeout 600
```

### Environment Variables
```bash
export UpdateEngine__BaseUrl=http://production-server
export UpdateEngine__Timeout=00:10:00
```

## 🔍 **Available Commands**

| Command | Description | Example |
|---------|-------------|---------|
| `health` | Check UpdateEngine health status | `update-cli health` |
| `config` | Get server configuration | `update-cli config` |
| `stats` | Get store statistics | `update-cli stats` |
| `categories` | List available update categories | `update-cli categories` |
| `sync metadata` | Trigger metadata synchronization | `update-cli sync metadata` |
| `sync content` | Trigger content synchronization | `update-cli sync content` |
| `search <category>` | Search updates by category | `update-cli search "Security Updates"` |
| `details <id>` | Get update details | `update-cli details {update-id}` |
| `reindex` | Reindex metadata store | `update-cli reindex` |

## 📋 **Common Workflows**

### Initial Setup Check
```bash
# 1. Check if UpdateEngine is running
update-cli health

# 2. Verify configuration
update-cli config

# 3. Check current statistics
update-cli stats
```

### Manual Synchronization
```bash
# 1. Sync metadata first
update-cli sync metadata

# 2. Check results
update-cli stats

# 3. Sync content if needed
update-cli sync content
```

### Update Discovery
```bash
# 1. List available categories
update-cli categories

# 2. Search specific category
update-cli search "Security Updates"

# 3. Get details for interesting updates
update-cli details {specific-update-id}
```

## ❌ **Troubleshooting**

### Connection Issues
```bash
# Connection refused - UpdateEngine not running
Error getting health status: No connection could be made because the target machine actively refused it.

# Solution: Start UpdateEngine first
cd AppHost/src && dotnet run
```

### Timeout Issues
```bash
# Increase timeout for slow operations
update-cli sync metadata --timeout 1200  # 20 minutes
```

### Endpoint Not Found
```bash
# 404 errors - check UpdateEngine version
# Some endpoints may not be available in older versions
```

## 🔧 **Development**

### Project Structure
```
src/tools/update-cli/
├── update-cli.csproj          # Project file
├── Program.cs                 # Main entry point & command setup
├── Configuration/
│   └── UpdateEngineConfiguration.cs
├── Services/
│   └── UpdateEngineClient.cs  # HTTP client for API calls
├── Commands/
│   └── CommandHandlers.cs     # Command implementations
├── appsettings.json           # Default configuration
├── README.md                  # Detailed documentation
├── update-cli.cmd            # Windows launcher script
└── update-cli.sh             # Unix launcher script
```

### Dependencies
- **.NET 9.0**: Target framework
- **System.CommandLine**: Command-line parsing
- **Microsoft.Extensions.Hosting**: Dependency injection & configuration
- **Microsoft.Extensions.Http**: HTTP client factory
- **Configuration Project**: Shared configuration classes

## 🌐 **Integration**

The CLI tool communicates with UpdateEngine via HTTP API endpoints:

- **Health**: `GET /api/GetStoreStatus`
- **Configuration**: `GET /api/GetServerConfiguration`
- **Statistics**: `GET /api/GetStoreStatistics`
- **Sync Operations**: `POST /api/SyncMetadata`, `POST /api/SyncContent`
- **Search**: `GET /api/SearchUpdates?category={category}`
- **Details**: `GET /api/GetUpdateDetails?updateId={id}`
- **Maintenance**: `POST /api/ReindexStore`
- **Categories**: `GET /api/GetCategories`

## 📈 **Next Steps**

1. **Start UpdateEngine**: `cd AppHost/src && dotnet run`
2. **Test CLI**: `update-cli health`
3. **Explore Commands**: `update-cli --help`
4. **Customize Configuration**: Edit `appsettings.json` or use command-line options

For detailed documentation, see the full [README.md](README.md) in the CLI project folder.