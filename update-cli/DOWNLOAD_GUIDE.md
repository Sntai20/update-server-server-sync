# UpdateEngine Download System

## Overview

The UpdateEngine download system provides comprehensive capabilities for downloading both metadata and content files from Microsoft Updates through a command-line interface. This system extends the existing UpdateEngine infrastructure to enable local file downloads for analysis, distribution, or offline use.

## Architecture

### Components

1. **Azure Functions (UpdateEngine)**
   - `DownloadFunctions.cs` - HTTP endpoints for download operations
   - Integrates with existing `IQueryService` and `IContentStore`

2. **CLI Tool (update-cli)**
   - `DownloadHandlers` - Command handlers for download operations
   - `UpdateEngineClient` - HTTP client with download methods

3. **Storage Integration**
   - Leverages existing metadata and content stores
   - Supports both local filesystem and Azure Blob storage

### API Endpoints

The download system exposes three main endpoints:

```
GET /api/download/list/{updateId}      - List available downloads
GET /api/download/metadata/{updateId}  - Download metadata as JSON
GET /api/download/content/{updateId}   - Download content files
```

## Download Operations

### 1. List Available Downloads

Shows what's available for download for a specific update:

```bash
update-cli download list {update-id}
```

**Returns:**
- Update metadata availability
- Content store availability status
- File information (when available)

### 2. Download Update Metadata

Downloads complete update metadata in JSON format:

```bash
update-cli download metadata {update-id}
update-cli download metadata {update-id} --output metadata.json
```

**Features:**
- Complete update information serialized as JSON
- Structured data including title, description, classification
- Prerequisites and superseded information
- Creation and modification dates

### 3. Download Update Content

Downloads actual update files:

```bash
update-cli download content {update-id}
update-cli download content {update-id} --output /path/to/file
update-cli download content {update-id} --progress
```

**Features:**
- Single file downloads for updates with one file
- ZIP archive for updates with multiple files
- Progress reporting for large downloads
- Automatic directory creation

## Implementation Details

### Metadata Download

The metadata download uses the existing `IQueryService` to locate updates and serialize the complete package information to JSON. This provides:

- Structured, machine-readable update data
- Complete metadata without requiring direct database access
- Standardized JSON format for integration with other tools

### Content Download (Future Enhancement)

**Current Status:** Basic framework implemented with placeholder functionality.

**Planned Implementation:**
- Direct integration with `IContentStore` for file access
- Support for both single files and ZIP archives
- Range request support for resumable downloads
- Content hash verification

### Query Integration

The download system integrates with the existing query infrastructure:

```csharp
var queryRequest = new MetadataQueryRequest
{
    SearchTerm = updateId,
    MaxResults = 10,
    IncludeSuperseded = true
};

var queryResult = await this.queryService.QueryMetadataAsync(queryRequest);
```

## CLI Usage Examples

### Basic Operations

```bash
# Check what's available for download
update-cli download list "12345678-1234-1234-1234-123456789abc"

# Download metadata
update-cli download metadata "12345678-1234-1234-1234-123456789abc"

# Download with custom output path
update-cli download metadata "12345678-1234-1234-1234-123456789abc" --output "KB123456.json"
```

### Advanced Operations

```bash
# Download with progress monitoring
update-cli download content "12345678-1234-1234-1234-123456789abc" --progress

# Download to specific directory
update-cli download content "12345678-1234-1234-1234-123456789abc" --output "/downloads/updates/"

# Batch operations (using shell scripting)
for updateId in $(cat update-list.txt); do
    update-cli download metadata "$updateId" --output "metadata_$updateId.json"
done
```

## Configuration

### UpdateEngine Configuration

The download functionality uses the existing UpdateEngine configuration:

```json
{
  "MetadataStorePath": "./store",
  "ContentStorePath": "./content",
  "UseAzureStorageForMetadata": false,
  "UseAzureStorageForContent": false
}
```

### CLI Configuration

Configure the CLI through `appsettings.json`:

```json
{
  "UpdateEngine": {
    "BaseUrl": "http://localhost:7071",
    "Timeout": "00:05:00"
  }
}
```

## Error Handling

The download system provides comprehensive error handling:

### Common Errors

1. **Update Not Found**
   ```
   Update not found: {update-id}
   HTTP 404 Not Found
   ```

2. **Content Store Not Available**
   ```
   Content store not configured
   HTTP 503 Service Unavailable
   ```

3. **Connection Issues**
   ```
   No connection could be made because the target machine actively refused it
   ```

4. **Invalid Update ID**
   ```
   Invalid update ID format
   HTTP 400 Bad Request
   ```

### CLI Error Handling

The CLI provides user-friendly error messages and returns appropriate exit codes:

- `0` - Success
- `1` - Error (connection, not found, etc.)

## Security Considerations

### Access Control

- Uses `AuthorizationLevel.Anonymous` for HTTP triggers (development)
- Production deployments should implement proper authentication
- Consider rate limiting for download endpoints

### Content Verification

- Content downloads include hash verification when available
- Metadata includes file integrity information
- Users should verify downloaded content against expected hashes

## Performance Considerations

### Metadata Downloads

- Lightweight JSON serialization
- Minimal impact on UpdateEngine performance
- Suitable for batch operations

### Content Downloads

- Direct streaming from content store
- Support for range requests (planned)
- Progress reporting for user experience
- Automatic compression for multiple files

## Future Enhancements

### Planned Features

1. **Enhanced Content Download**
   - Full file access through content store
   - Resumable downloads with range requests
   - Batch download operations
   - Integrity verification

2. **Advanced Filtering**
   - Download by category or classification
   - Date range filtering
   - Size-based filtering

3. **Export Formats**
   - Multiple metadata formats (XML, CSV)
   - Custom export templates
   - Integration with existing export functionality

4. **Monitoring and Logging**
   - Download statistics
   - Usage tracking
   - Performance metrics

### Integration Opportunities

- Integration with existing sync operations
- Automated download scheduling
- Integration with package management systems
- API for third-party tools

## Troubleshooting

### Common Issues

1. **UpdateEngine Not Running**
   - Start UpdateEngine: `cd AppHost/src && dotnet run`
   - Verify endpoints: `update-cli health`

2. **Downloads Not Available**
   - Check content store configuration
   - Verify metadata sync has completed
   - Use `update-cli stats` to check store status

3. **Large Download Timeouts**
   - Increase timeout: `--timeout 1800` (30 minutes)
   - Use progress monitoring: `--progress`

4. **File Not Found**
   - Verify update ID is correct
   - Check if content has been synced
   - Use `download list` to check availability

### Diagnostic Commands

```bash
# Check UpdateEngine status
update-cli health

# Verify store statistics
update-cli stats

# Check available downloads
update-cli download list {update-id}

# Test with verbose logging
DOTNET_ENVIRONMENT=Development update-cli download metadata {update-id}
```

## Contributing

The download system is designed to be extensible. Key areas for contribution:

- Enhanced content store integration
- Additional export formats
- Performance optimizations
- Security enhancements
- Documentation improvements

See the main project README for development setup and contribution guidelines.