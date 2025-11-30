# Content Storage Quick Reference
**Fast lookup guide for Windows Update content file storage**

## Quick Facts

| Aspect | Details |
|--------|---------|
| **Storage Method** | Content-addressed (by hash) |
| **File Extensions** | None (files stored without extensions) |
| **Hash Algorithm** | SHA-1 (primary), SHA-256 (secondary) |
| **Deduplication** | Automatic (same hash = same file) |
| **Block Size** | 64MB chunks for large files |
| **Marker Files** | `.complete` (Blob) or `.done` (FileSystem) |

---

## Storage Locations

### Azure Blob Storage (BlobContentStore)

```
Container: "data"
Prefix: "content"

Structure:
  data/content/{hash}                  ? Binary content
  data/content/{hash}.complete         ? Completion marker

Example:
  data/content/3fa2b8c1a0d5e7f9abc123def456789012345678
  data/content/3fa2b8c1a0d5e7f9abc123def456789012345678.complete
```

### File System (FileSystemContentStore)

```
RootPath: "./store"
ContentDirectory: "content"

Structure:
  content/{lastByte}/{hash}/{hash}      ? Binary content
  content/{lastByte}/{hash}/{hash}.done ? Completion marker (contains original filename)

Example:
  content/8/3fa2b8c1a0d5e7f9abc123def456789012345678/3fa2b8c1a0d5e7f9abc123def456789012345678
  content/8/3fa2b8c1a0d5e7f9abc123def456789012345678/3fa2b8c1a0d5e7f9abc123def456789012345678.done
```

### Azurite (Local Development)

```
Path: UpdateEngine.AppHost\src\out\azurite-data\__blobstorage__

Structure:
  __blobstorage__/
    ??? 0xxx.../          ? Blobs starting with 0
    ??? 1xxx.../          ? Blobs starting with 1
    ??? 3xxx.../
    ??? 7xxx.../
```

---

## Configuration

### Enable Azure Blob Storage

```json
{
  "UpdateEngine": {
    "StorageConfiguration": {
      "UseAzureStorageForContent": true,
      "ContentContainerName": "data"
    }
  }
}
```

### Enable File System Storage

```json
{
  "UpdateEngine": {
    "StorageConfiguration": {
      "UseAzureStorageForContent": false
    }
  }
}
```

### Increase Download Limit

```json
{
  "UpdateEngine": {
    "ServiceConfiguration": {
      "MaxUpdateCount": 100  // Default: 5 (too low for production)
    }
  }
}
```

---

## Common Commands

### Check Content File Count (Azure Blob)

```powershell
az storage blob list --container-name data --prefix content/ --output table | Measure-Object
```

### Check Content File Count (File System)

```powershell
Get-ChildItem -Path "content" -Recurse -File | Where-Object { $_.Name -notmatch '\.(done|complete)$' } | Measure-Object
```

### Check Content Size (File System)

```powershell
Get-ChildItem -Path "content" -Recurse -File | Where-Object { $_.Name -notmatch '\.(done|complete)$' } | Measure-Object -Property Length -Sum
```

### List Azurite Content Files

```powershell
Get-ChildItem -Path "C:\Users\{USER}\Repos\update-server-server-sync\UpdateEngine.AppHost\src\out\azurite-data\__blobstorage__" -Recurse -File
```

### Delete Incomplete Downloads (File System)

```powershell
# Find files without .done markers
Get-ChildItem -Path "content" -Recurse -File | Where-Object { 
    $_.Name -notmatch '\.(done|complete)$' 
} | ForEach-Object {
    $markerPath = "$($_.FullName).done"
    if (-not (Test-Path $markerPath)) {
        Write-Host "Deleting incomplete: $($_.FullName)"
        Remove-Item -Path $_.FullName -Force
    }
}
```

### Verify Hash Integrity

```powershell
# Verify file hash matches filename (File System)
Get-ChildItem -Path "content" -Recurse -File | Where-Object { 
    $_.Name -notmatch '\.(done|complete)$' 
} | ForEach-Object {
    $expectedHash = $_.Name
    $actualHash = (Get-FileHash -Path $_.FullName -Algorithm SHA1).Hash.ToLower()
    
    if ($actualHash -ne $expectedHash) {
        Write-Warning "Hash mismatch: $($_.FullName)"
        Write-Warning "Expected: $expectedHash"
        Write-Warning "Actual: $actualHash"
    }
}
```

---

## File Type Examples

| Original Name | Size | Hash (Example) | Storage Path |
|---------------|------|----------------|--------------|
| `Windows10.0-KB5001234-x64.cab` | 500MB | `3fa2b8c1...` | `content/3fa2b8c1...` |
| `nvdisplay.container.cab` | 43MB | `f9e8d7c6...` | `content/f9e8d7c6...` |
| `mpas-fe.exe` | 1.2MB | `0a1b2c3d...` | `content/0a1b2c3d...` |

**Note:** No file extensions in storage. Original filename preserved in:
- Update metadata (`UpdateFile.FileName`)
- FileSystem marker file content (`.done`)

---

## Download Process (Simplified)

```
1. Check if file exists ? Check marker file (.complete / .done)
2. If exists ? Return immediately (no download)
3. If not exists:
   a. HTTP HEAD ? Get file size from Microsoft servers
   b. Validate size matches metadata
   c. Download in 64MB blocks
   d. Stage blocks to blob / Write to disk
   e. Commit block list / Verify hash
   f. Write marker file
   g. Return success
```

---

## Troubleshooting Checklist

### Problem: Only 42 files after overnight sync

**Solution:**
1. Check `MaxUpdateCount` in appsettings.json (increase to 100+)
2. Restart Azure Functions
3. Wait for next sync cycle (5 minutes)
4. Verify increase: `az storage blob list --container-name data --prefix content/ | Measure-Object`

---

### Problem: File exists but no marker

**Solution:**
1. Delete incomplete file
2. Restart sync
3. Check logs for download errors

```powershell
# Azure Blob
az storage blob delete --container-name data --name content/{hash}

# File System
Remove-Item -Path "content/{subdirectory}/{hash}/{hash}" -Force
```

---

### Problem: Hash mismatch error

**Solution:**
1. Automatic retry (3 attempts with exponential backoff)
2. If all retries fail, check:
   - Network stability
   - Microsoft Update source status
   - BlobContentStoreMetrics for retry counts

---

### Problem: Cannot find Azurite files

**Solution:**
1. Verify Azurite is running: `Get-Process azurite`
2. Start Azurite: `azurite --silent`
3. Check connection string: `"BlobStorageConnection": "UseDevelopmentStorage=true"`
4. Verify data directory: `UpdateEngine.AppHost\src\out\azurite-data\__blobstorage__`

---

## Metrics to Monitor

| Metric | Description | Alert Threshold |
|--------|-------------|-----------------|
| `BlobContentStoreMetrics.DownloadsCompleted` | Successful downloads | N/A |
| `BlobContentStoreMetrics.DownloadsFailed` | Failed downloads | > 5% of total |
| `BlobContentStoreMetrics.BytesDownloaded` | Total bytes downloaded | N/A |
| `BlobContentStoreMetrics.BlockRetriesTotal` | Retry attempts | > 10% of blocks |
| `BlobContentStoreMetrics.ActiveDownloads` | Concurrent downloads | Monitor for bottlenecks |

---

## Code Examples

### Check if file exists

```csharp
var contentFile = update.Files.First();

if (contentStore.Contains(contentFile))
{
    Console.WriteLine($"File {contentFile.FileName} already downloaded");
}
```

### Download file

```csharp
await contentStore.DownloadAsync(contentFile, CancellationToken.None);
```

### Get file stream

```csharp
using var stream = contentStore.Get(contentFile);
// Process stream (read binary content)
```

### Get file URI (with SAS token)

```csharp
string uri = contentStore.GetUri(contentFile);
// Use URI to download from blob storage (10-minute window)
```

---

## Storage Estimates

| Scenario | Updates | Avg Size | Total Storage |
|----------|---------|----------|---------------|
| **Small (Dev)** | 10-20 | 500MB | 5-10GB |
| **Medium (Test)** | 100-500 | 500MB | 50-250GB |
| **Large (Prod)** | 1000-5000 | 500MB | 500GB-2.5TB |
| **Enterprise** | 10,000+ | 500MB | 5TB+ |

**Deduplication Savings:** 30-50% reduction in total storage

---

## Best Practices

? **Use Azurite for local development** (fast, free)  
? **Use Azure Blob Storage for production** (scalable, durable)  
? **Set MaxUpdateCount appropriately** (100+ for production)  
? **Monitor download metrics** (detect failures early)  
? **Enable detailed logging** (troubleshoot issues)  
? **Run maintenance jobs** (cleanup old content)  
? **Use lifecycle policies** (Azure Blob tiering)  

---

**Related Documentation:**
- [CONTENT_STORAGE_ARCHITECTURE.md](CONTENT_STORAGE_ARCHITECTURE.md) - Full technical details
- [BlobContentStore.cs](../../UpdateEngine.Metadata/src/Storage/AzureBlob/BlobContentStore.cs) - Azure Blob implementation
- [FileSystemContentStore.cs](../../UpdateEngine.Metadata/src/Storage/FileSystem/FileSystemContentStore.cs) - File System implementation
- [BlobContentStoreMetrics.cs](../../UpdateEngine.Metadata/src/Storage/AzureBlob/BlobContentStoreMetrics.cs) - Metrics definitions

---

**Last Updated:** 2024-01-XX  
**Version:** 1.0
