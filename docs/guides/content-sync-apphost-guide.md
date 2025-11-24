# Content Sync with AppHost Configuration

Since you're using the AppHost configuration, the content sync setup is controlled through the `appsettings.json` files in `UpdateEngine.AppHost/src/`. Here's how to configure and use content sync effectively:

## Current Configuration Status

Based on your `appsettings.Development.json`, content sync is **enabled** with these key settings:

```json
{
  "Features": {
    "EnableContentSync": true  // ✅ Content sync is enabled
  },
  "Storage": {
    "ContentStorePath": "./content",  // Local content directory
    "UseAzureStorage": true  // Using Azure Storage (Azurite emulator)
  },
  "AzureWebJobs": {
    "SyncContent": {
      "Disabled": false  // ✅ SyncContent function is enabled
    }
  }
}
```

## How to Use Content Sync with AppHost

### 1. Start the Application with AppHost

```bash
cd UpdateEngine.AppHost/src
dotnet run
```

This starts:
- ✅ Azure Functions on port 7071
- ✅ Azurite storage emulator
- ✅ Content sync configured with Azure Blob storage

### 2. Available Content Sync Endpoints

Once running, these endpoints are available:

```bash
# Check content storage status
curl http://localhost:7071/api/GetContentStatus

# Sync content (limited for testing)
curl -X POST http://localhost:7071/api/SyncContent \
  -H "Content-Type: application/json" \
  -d '{"MaxItems": 1}'

# Query for updates with files
curl -X POST http://localhost:7071/api/QueryMetadata \
  -H "Content-Type: application/json" \
  -d '{"IncludeUpdates": true, "HasFiles": true, "MaxResults": 3}'
```

### 3. Content Sync Request Structure

The `SyncContent` endpoint accepts these parameters:

```json
{
  "ProductFilters": ["Windows 10", "Windows 11"],
  "ClassificationFilters": ["Security Updates", "Critical Updates"],
  "UpdatedAfter": "2024-01-01T00:00:00Z",
  "MaxItems": 5  // Limit for testing - updates can be large!
}
```

### 4. Expected Content Structure

With Azurite (Azure Storage emulator), content files are stored as:

```
Azure Blob Container: "data" (from ContentContainerName)
├── {sha256-hash-1}  // First update file
├── {sha256-hash-2}  // Second update file
└── {sha256-hash-n}  // Additional files...
```

You can view the blob storage using:
- **Azure Storage Explorer** pointed to `http://127.0.0.1:10000/devstoreaccount1`
- **Azurite storage browser** extensions in VS Code

### 5. Content Sync Workflow

```bash
# 1. First sync metadata (required before content)
curl -X POST http://localhost:7071/api/SyncMetadata \
  -H "Content-Type: application/json" \
  -d '{"SyncCategories": true, "SyncUpdates": true, "FilterType": "critical"}'

# 2. Query what updates have content files
curl -X POST http://localhost:7071/api/QueryMetadata \
  -H "Content-Type: application/json" \
  -d '{"IncludeUpdates": true, "HasFiles": true, "MaxResults": 3}'

# 3. Sync content for those updates
curl -X POST http://localhost:7071/api/SyncContent \
  -H "Content-Type: application/json" \
  -d '{"MaxItems": 1}'  # Start small!

# 4. Verify content was stored
curl http://localhost:7071/api/GetContentStatus
```

### 6. Configuration Customization

To modify content sync behavior, edit `UpdateEngine.AppHost/src/appsettings.Development.json`:

```json
{
  "Storage": {
    "ContentStorePath": "./custom-content",  // Change local path
    "UseAzureStorage": false,  // Switch to local filesystem
    "ContentContainerName": "my-updates"  // Custom container name
  },
  "Service": {
    "MaxUpdateCount": 5,  // Limit updates for testing
    "SupportedCategories": [
      "Security Updates",  // Only sync security updates
      "Critical Updates"
    ]
  }
}
```

### 7. Content Access for Windows Update Clients

Once content is synced, it's served via:

```
GET http://localhost:7071/api/GetUpdateContent/{hash}
```

This endpoint:
- ✅ Supports HTTP range requests (for partial downloads)
- ✅ Returns proper content-type headers
- ✅ Integrates with Windows Update protocol

### 8. Monitoring and Diagnostics

```bash
# Get detailed storage diagnostics
curl http://localhost:7071/api/StorageDiagnostics

# Check overall system status
curl http://localhost:7071/api/GetStoreStatus

# Monitor content download progress (check logs)
```

### 9. Local vs Azure Storage

**Current (Azure/Azurite):**
- ✅ Production-like behavior
- ✅ Supports large files efficiently
- ✅ Concurrent access safe
- ❌ Requires Azurite running

**Local Filesystem Option:**
```json
{
  "Storage": {
    "UseAzureStorage": false,
    "ContentStorePath": "./content"
  }
}
```
- ✅ Simple file access
- ✅ No dependencies
- ❌ Less scalable

### 10. Performance Considerations

- **Start Small**: Use `MaxItems: 1-3` for initial testing
- **Update Size**: Security updates can be 50MB-2GB each
- **Network**: Downloads come directly from Microsoft CDN
- **Storage**: Ensure adequate disk space (updates accumulate)

This setup gives you a full content sync experience that matches how the system would work in production!