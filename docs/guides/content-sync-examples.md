# Content Sync API Usage Examples

## 1. Configure Content Storage

First, configure `local.settings.json`:

```json
{
  "Values": {
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "MetadataStorePath": "./store",
    "ContentStorePath": "./content",
    "ServiceConfigurationJson": "{\"ServiceUrl\":\"http://localhost:7071\"}"
  }
}
```

## 2. Content Sync Workflow

### Step 1: Check Content Status
```bash
curl http://localhost:7071/api/GetContentStatus
```

Expected response:
```json
{
  "configured": true,
  "type": "FileSystemContentStore",
  "timestamp": "2025-01-27T10:30:00Z"
}
```

### Step 2: Query for Updates with Files
```bash
curl -X POST http://localhost:7071/api/QueryMetadata \
  -H "Content-Type: application/json" \
  -d '{
    "IncludeUpdates": true,
    "HasFiles": true,
    "MaxResults": 5
  }'
```

Expected response structure:
```json
{
  "packages": [
    {
      "id": "12345678-1234-1234-1234-123456789012",
      "title": "Security Update for Windows 11",
      "type": "SoftwareUpdate",
      "files": [
        {
          "fileName": "windows11-kb5034441-x64_abc123.msu",
          "size": 45678901,
          "digest": "abc123def456...",
          "digestAlgorithm": "SHA256"
        }
      ]
    }
  ]
}
```

### Step 3: Sync Content
```bash
curl -X POST http://localhost:7071/api/SyncContent \
  -H "Content-Type: application/json" \
  -d '{
    "MaxItems": 3,
    "ProductFilters": ["Windows 11"],
    "ClassificationFilters": ["Security Updates"]
  }'
```

Expected response:
```json
{
  "success": true,
  "startTime": "2025-01-27T10:30:00Z",
  "endTime": "2025-01-27T10:35:00Z",
  "contentSynced": true,
  "itemsProcessed": 3
}
```

## 3. Content File Structure

When content is downloaded, files are stored by hash:

```
./content/
├── a1b2c3d4e5f6... (SHA256 hash)
├── f6e5d4c3b2a1... (SHA1 hash)
└── ... (more hash-named files)
```

## 4. Content Request Models

### SyncContentRequest
```typescript
{
  "ProductFilters": ["Windows 10", "Windows 11"],
  "ClassificationFilters": ["Security Updates", "Critical Updates"],
  "UpdatedAfter": "2024-01-01T00:00:00Z",
  "MaxItems": 100
}
```

### Content File Information
```typescript
{
  "fileName": "original-file-name.msu",
  "size": 12345678,
  "digest": "sha256-hash-string",
  "digestAlgorithm": "SHA256",
  "downloadUrl": "https://catalog.update.microsoft.com/..."
}
```

## 5. Error Handling

### No Content Store Configured
```json
{
  "error": "Content store not configured"
}
```

### No Files to Download
```json
{
  "success": true,
  "message": "No files to download matching criteria",
  "itemsProcessed": 0
}
```

## 6. Performance Considerations

- **Filtering**: Apply product/classification filters to reduce download size
- **MaxItems**: Limit downloads for testing (e.g., MaxItems: 1-5)
- **Disk Space**: Ensure adequate space - updates can be 50MB-2GB each
- **Network**: Content downloads directly from Microsoft CDN
- **Progress**: Monitor via Function logs during sync operations

## 7. Integration with WSUS/Windows Update

Once content is synced:

1. **Metadata endpoints** tell clients about available updates
2. **Content endpoints** serve the actual files:
   ```
   GET /microsoftupdate/content/{hash}
   ```
3. **Windows Update clients** download content using range requests
4. **WSUS servers** can cache and redistribute content

## 8. Advanced Configuration

### Azure Blob Storage
```json
{
  "Values": {
    "UseAzureStorage": "true",
    "AzureWebJobsStorage": "DefaultEndpointsProtocol=https;AccountName=...",
    "ContentContainerName": "updatecontent"
  }
}
```

### Custom Filtering
```json
{
  "ProductFilters": [
    "Windows 10",
    "Microsoft Edge"
  ],
  "ClassificationFilters": [
    "E6CF1350-C01B-414D-A61F-263D14D133B4",  // Critical Updates GUID
    "0FA1201D-4330-4FA8-8AE9-B877473B6441"   // Security Updates GUID
  ]
}
```