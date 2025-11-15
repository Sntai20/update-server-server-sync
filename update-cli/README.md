# UpdateEngine CLI Tool

A command-line interface for querying and managing the Microsoft Update Server-Server Sync UpdateEngine with unified API support.

## Features

- **Health Monitoring**: Check UpdateEngine health status with multiple scopes (basic, full, sync, store)
- **Configuration**: View server configuration
- **Synchronization**: Trigger metadata and content sync operations with unified endpoints
  - Comprehensive metadata sync
  - Critical updates sync (security/critical only)
  - Content sync with configurable time range
- **Statistics**: Get store statistics and metrics
- **Content Status**: Check content synchronization status
- **Search**: Search for updates by category
- **Update Details**: Get detailed information about specific updates
- **Downloads**: Download update metadata and content files
- **Bulk Operations**: Specialized bulk download for Windows Server 2022/2025 and Windows 11
- **Maintenance**: Smart store reindexing with pre-checks
- **Categories**: List available update categories

## Unified API Support

This CLI tool is fully compatible with the unified Azure Functions API endpoints:

| Feature | Unified Endpoint | Legacy Endpoint |
|---------|-----------------|-----------------|
| Health Check | `/api/UniversalHealth` | `/api/QueryMetadataStoreStatus` |
| Metadata Sync | `/api/UniversalSync` | `/api/SyncMetadata` |
| Content Sync | `/api/UniversalSync` | `/api/SyncContent` |
| Store Status | `/api/GetStoreStatus` | `/api/GetStoreStatistics` |
| Reindex | `/api/StoreManagement` | `/api/ReindexStore` |
| Content Status | `/api/QueryContentStatus` | *New* |
| Reindex Check | `/api/CheckReindexRequired` | *New* |

## Installation

### Build from Source

dotnet build
dotnet publish -c Release

### Run from Development

dotnet run -- [command] [options]

## Usage

### Basic Commands

# Check health status (with scope support)
update-cli health --scope basic   # Quick check (default)
update-cli health --scope full    # Comprehensive system health
update-cli health --scope sync    # Sync subsystem health
update-cli health --scope store   # Store subsystem health

# Get server configuration
update-cli config

# Get store statistics
update-cli stats

# Get content sync status (NEW)
update-cli content-status

# List available categories
update-cli categories

### Synchronization (Unified API)

# Comprehensive metadata sync (categories + all updates)
update-cli sync metadata

# Critical updates only (security + critical, faster)
update-cli sync critical

# Content sync with time range
update-cli sync content --days-back 7   # Last 7 days
update-cli sync content --days-back 30  # Last 30 days (default)

### Search and Query

# Search for updates in a category
update-cli search "Security Updates"

# Get details for a specific update
update-cli details {update-id}

### Downloads

# List available downloads for an update
update-cli download list {update-id}

# Download update metadata as JSON
update-cli download metadata {update-id}
update-cli download metadata {update-id} --output custom-metadata.json

# Download update content files
update-cli download content {update-id}
update-cli download content {update-id} --output /path/to/download
update-cli download content {update-id} --progress  # Show download progress

### Bulk Download Operations

#### Windows Server 2022

# Security updates only
update-cli server2022 "C:\Server2022Updates" --security-only

# All update types (security, critical, rollups)
update-cli server2022 "C:\Server2022Updates" --max-updates 100

# Download without syncing first (use existing metadata)
update-cli server2022 "C:\Server2022Updates" --skip-sync

# IPAK-compatible folder structure
update-cli server2022 "C:\Server2022Updates" --ipak-compatible

# Comprehensive enterprise download
update-cli server2022 "\\FileServer\Updates\Server2022" --max-updates 200

#### Windows Server 2025

# Security updates for Server 2025
update-cli server2025 "C:\Server2025Updates" --security-only --max-updates 50

#### Windows 11

# Windows 11 updates
update-cli windows11 "C:\Windows11Updates" --security-only --max-updates 50

### Maintenance Operations

# Smart reindex (checks if needed first - NEW)
update-cli reindex

# Check health after maintenance
update-cli health --scope full

### Global Options

# Specify UpdateEngine URL
update-cli health --url http://localhost:7071

# Set request timeout (seconds)
update-cli health --timeout 300

# Combine options
update-cli sync metadata --url http://my-server:8080 --timeout 600

## Configuration

The tool can be configured via:

1. **appsettings.json** file:
{
  "UpdateEngine": {
    "BaseUrl": "http://localhost:7071",
    "Timeout": "00:05:00"
  }
}

2. **Environment variables**:
export UpdateEngine__BaseUrl=http://localhost:7071
export UpdateEngine__Timeout=00:05:00

3. **Command-line options** (highest priority):
update-cli health --url http://my-server:8080 --timeout 120

## Examples

### Initial Setup Workflow

# Check if UpdateEngine is running
update-cli health --scope full

# Sync metadata from Microsoft Update
update-cli sync metadata

# Sync recent content
update-cli sync content --days-back 30

# Verify everything is working
update-cli stats
update-cli content-status

### Daily Operations Workflow

# Quick health check
update-cli health

# Sync critical updates only (faster)
update-cli sync critical

# Check what got synced
update-cli stats

### Maintenance Workflow

# Check if store needs reindexing
update-cli reindex

# Full health check
update-cli health --scope full

# Get detailed statistics
update-cli stats
update-cli content-status

## New Features in Unified API

### 1. Health Check Scopes
update-cli health --scope basic   # Lightweight check
update-cli health --scope full    # Complete system health
update-cli health --scope sync    # Sync subsystem status
update-cli health --scope store   # Store subsystem status

### 2. Critical Updates Sync
# Sync only security and critical updates (faster than comprehensive)
update-cli sync critical

### 3. Content Time Range
# Sync content from last 7 days
update-cli sync content --days-back 7

### 4. Smart Reindex
# Automatically checks if reindex is needed before executing
update-cli reindex

### 5. Content Status
# Check content synchronization status
update-cli content-status

## Performance Tips

- Use `sync critical` for daily operations (faster than `sync metadata`)
- Specify `--days-back` for content sync to limit download size
- Use `--skip-sync` with bulk download commands if metadata is already synced
- Increase `--timeout` for large sync operations

## Performance Benchmarks

| Operation | Time | Notes |
|-----------|------|-------|
| `health --scope basic` | < 1s | Fastest health check |
| `health --scope full` | < 2s | Complete system check |
| `sync critical` | 2-5 min | Network dependent |
| `sync metadata` | 10-30 min | First sync is slower |
| `sync content --days-back 7` | 5-15 min | Depends on update count |

## Error Handling

The CLI tool provides meaningful error messages and appropriate exit codes:
- **0**: Success
- **1**: Error occurred (check output for details)

Common issues:
- **Connection refused**: UpdateEngine is not running
- **Timeout**: Operation took too long (increase timeout)
- **404 errors**: Endpoint not available
- **Sync failures**: Check UpdateEngine logs

## Troubleshooting

### UpdateEngine Not Running
Error: Connection refused
Solution: Start UpdateEngine with `cd UpdateEngine && func start`

### Request Timeout
Error: The operation has timed out
Solution: Increase timeout with `--timeout 600`

### Sync Failures
Error: Sync operation failed
Solutions:
1. Check UpdateEngine logs
2. Verify network connectivity
3. Check metadata store is writable

## Documentation

- **[Testing Guide](TESTING.md)** - Comprehensive testing guide
- **[Quick Reference](QUICK_REFERENCE.md)** - Command quick reference
- **[Architecture Guide](../../docs/guides/UPDATE_CLI_ARCHITECTURE.md)** - Architecture docs
- **[Windows Server 2022 CLI Guide](../../docs/guides/SERVER2022_CLI_GUIDE.md)** - Enterprise guide

## Dependencies

- .NET 9.0
- System.CommandLine for command parsing
- HttpClient for API communication
- Microsoft.Extensions.* for configuration and DI

## Command Reference

### Health Commands
- `health --scope basic` - Quick check
- `health --scope full` - Complete system health
- `health --scope sync` - Sync subsystem health
- `health --scope store` - Store subsystem health

### Sync Commands
- `sync metadata` - Comprehensive metadata sync
- `sync critical` - Critical updates only
- `sync content --days-back N` - Content sync

### Bulk Commands
- `server2022 <path>` - Windows Server 2022 bulk download
- `server2025 <path>` - Windows Server 2025 bulk download
- `windows11 <path>` - Windows 11 bulk download
