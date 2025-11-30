# Storage Architecture Documentation Index

## Overview
This directory contains comprehensive documentation about how Windows Update **metadata** (update information) and **content files** (binary updates) are stored in the UpdateEngine system.

## Documentation Files

### ?? Metadata Storage Documentation

#### [METADATA_STORAGE_ARCHITECTURE.md](METADATA_STORAGE_ARCHITECTURE.md)
**Complete technical reference for metadata storage system**

**Topics Covered:**
- Page Blob architecture (Azure) vs. File System (local)
- GZip compression (70-90% size reduction)
- Package Store Entry structure
- Indexing and fast queries
- Upload/download caching strategy
- Performance characteristics
- Azurite local development
- Troubleshooting guide

**Length:** ~600 lines  
**Audience:** Developers, DevOps, Architects

---

#### [METADATA_STORAGE_QUICK_REF.md](METADATA_STORAGE_QUICK_REF.md)
**Quick reference guide for metadata storage**

**Topics Covered:**
- Storage locations (Page Blob, FileSystem, Azurite)
- Configuration examples
- Common PowerShell commands
- Package Store Entry structure
- Compression ratios
- Cache strategy
- Code examples
- Storage estimates
- Best practices

**Length:** ~250 lines  
**Audience:** All users (quick lookup)

---

### ?? Content Storage Documentation

#### [CONTENT_STORAGE_ARCHITECTURE.md](CONTENT_STORAGE_ARCHITECTURE.md)
**Complete technical reference for content storage system**

**Topics Covered:**
- Content-addressed storage model (hash-based)
- Azure Blob Storage implementation (`BlobContentStore`)
- File System implementation (`FileSystemContentStore`)
- Download and verification process (64MB blocks)
- Marker files and deduplication
- File types and binary content
- Azurite local development
- Performance characteristics
- Troubleshooting guide

**Length:** ~500 lines  
**Audience:** Developers, DevOps, Architects

---

#### [CONTENT_STORAGE_QUICK_REF.md](CONTENT_STORAGE_QUICK_REF.md)
**Quick reference guide for content storage**

**Topics Covered:**
- Storage locations (Blob, FileSystem, Azurite)
- Configuration examples
- Common PowerShell commands
- File type examples
- Troubleshooting checklist
- Metrics to monitor
- Code examples
- Storage estimates
- Best practices

**Length:** ~200 lines  
**Audience:** All users (quick lookup)

---

## Key Concepts

### Metadata vs. Content

| Aspect | Metadata | Content |
|--------|----------|---------|
| **What** | Update information (JSON) | Binary files (CAB, MSI, EXE) |
| **Size** | Small (5-500 KB compressed) | Large (1MB - 2GB) |
| **Storage** | Page Blob or FileSystem | Block Blob or FileSystem |
| **Compression** | GZip (70-90% reduction) | No compression (already compressed) |
| **Access Pattern** | Frequent queries | Infrequent downloads |

---

### Metadata Storage

**Page Blob Architecture (Azure):**
```
data/metadata                          ? Page blob (compressed packages)
data/identities-index                  ? Package ID ? Offset mapping
data/categories-index                  ? Category ? Package IDs
```

**Key Features:**
- Compressed with GZip (70-90% size reduction)
- Page-aligned for efficient random access
- Indexed for fast queries (O(1) by ID)
- 32MB upload cache, 4MB download cache

**Example Package Entry:**
```json
{
  "Identity": "3fa2b8c1-a0d5-e7f9-abc1-23def4567890",
  "MetadataOffset": 4096,
  "MetadataLength": 2048,
  "FileListOffset": 6144,
  "FileListLength": 1024
}
```

---

### Content Storage

**Content-Addressed Storage (Azure/FileSystem):**
```
data/content/3fa2b8c1a0d5e7f9abc123def456789012345678            ? Binary file
data/content/3fa2b8c1a0d5e7f9abc123def456789012345678.complete  ? Marker
```

**Key Features:**
- Files stored by SHA-1 hash (no extensions)
- Automatic deduplication (same hash = same file)
- 64MB block downloads with retry logic
- Marker files prevent re-downloads

**Example:**
```
Original Name: Windows10.0-KB5001234-x64.cab
Storage Path:  3fa2b8c1a0d5e7f9abc123def456789012345678
```

---

## Quick Start

### 1. Configure Storage Backend

**Metadata (Azure Page Blob):**
```json
{
  "UpdateEngine": {
    "StorageConfiguration": {
      "UseAzureStorageForMetadata": true,
      "MetadataContainerName": "data"
    }
  }
}
```

**Content (Azure Block Blob):**
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

---

### 2. Start Services

**Development (Azurite):**
```powershell
# Start Azurite
azurite --silent

# Start Azure Functions
func start
```

---

### 3. Verify Storage

**Check metadata package count:**
```powershell
az storage blob download --container-name data --name identities-index --file identities.json
(Get-Content identities.json | ConvertFrom-Json).Count
```

**Check content file count:**
```powershell
az storage blob list --container-name data --prefix content/ --output table | Measure-Object
```

---

## Common Scenarios

### Scenario 1: Query update metadata

```csharp
using var store = PackageStore.OpenOrCreate("./store");

var securityUpdates = store
    .OfType<SoftwareUpdate>()
    .Where(u => u.Categories.Contains(securityCategoryGuid))
    .ToList();

Console.WriteLine($"Found {securityUpdates.Count} security updates");
```

---

### Scenario 2: Download update content

```csharp
var updateWithContent = store
    .OfType<SoftwareUpdate>()
    .FirstOrDefault(u => u.Files?.Count() > 0);

var contentStore = BlobContentStore.OpenOrCreate(blobServiceClient, "data", "content", logger);
contentStore.Download(updateWithContent.Files, CancellationToken.None);
```

---

### Scenario 3: Reindex metadata store

```csharp
if (store.IsReindexingRequired)
{
    Console.WriteLine("Reindexing metadata store...");
    store.ReIndex();
    store.Flush();
}
```

---

## Storage Architecture Comparison

### Metadata Storage

| Feature | Azure Page Blob | File System |
|---------|----------------|-------------|
| **Use Case** | Production, scalable | Development, local |
| **Performance** | Good (network latency) | Excellent (local disk) |
| **Scalability** | 1M+ packages | 100K packages |
| **Indexing** | Required | Optional |
| **Caching** | Upload (32MB), Download (4MB) | N/A |

### Content Storage

| Feature | Azure Block Blob | File System |
|---------|-----------------|-------------|
| **Use Case** | Production, scalable | Development, local |
| **Deduplication** | Automatic | Automatic |
| **Block Size** | 64MB | N/A |
| **Marker Files** | `.complete` | `.done` |
| **Max File Size** | 4.75 TB | OS-dependent |

---

## Performance Tips

### Metadata Optimization

? **Enable caching** - Reduce query latency  
? **Reindex after bulk sync** - Keep indexes current  
? **Warm cache on startup** - Load frequently accessed packages  
? **Process in batches** - Avoid memory issues with large datasets  

### Content Optimization

? **Set MaxUpdateCount appropriately** - Control download volume  
? **Monitor disk space** - 500MB per update average  
? **Use lifecycle policies** - Archive old content  
? **Enable compression** - Azure Blob supports transparent compression  

---

## Metrics and Monitoring

### Metadata Metrics

| Metric | Description | Alert Threshold |
|--------|-------------|-----------------|
| `PackagesAdded` | Packages added to store | N/A |
| `PackagesRetrieved` | Packages retrieved | N/A |
| `QueryDuration` | Query latency | > 500ms P95 |
| `FlushDuration` | Flush latency | > 2s P95 |

### Content Metrics

| Metric | Description | Alert Threshold |
|--------|-------------|-----------------|
| `DownloadsCompleted` | Successful downloads | N/A |
| `DownloadsFailed` | Failed downloads | > 5% of total |
| `BytesDownloaded` | Total bytes downloaded | N/A |
| `BlockRetriesTotal` | Block download retries | > 10% of blocks |

---

## Troubleshooting

### Metadata Issues

**Problem:** "Metadata store not found"  
**Solution:** `PackageStore.OpenOrCreate("./store")`

**Problem:** "Reindexing required"  
**Solution:** `store.ReIndex(); store.Flush();`

**Problem:** "Slow queries"  
**Solution:** Enable caching, warm cache, check indexes

---

### Content Issues

**Problem:** "Only 42 files after overnight sync"  
**Solution:** Increase `MaxUpdateCount` from 5 to 100+

**Problem:** "File exists but no marker"  
**Solution:** Delete incomplete file, restart sync

**Problem:** "Hash mismatch error"  
**Solution:** Automatic retry (3 attempts), check network stability

---

## Related Documentation

### In This Directory
- [METADATA_STORAGE_ARCHITECTURE.md](METADATA_STORAGE_ARCHITECTURE.md) - Metadata technical documentation
- [METADATA_STORAGE_QUICK_REF.md](METADATA_STORAGE_QUICK_REF.md) - Metadata quick reference
- [CONTENT_STORAGE_ARCHITECTURE.md](CONTENT_STORAGE_ARCHITECTURE.md) - Content technical documentation
- [CONTENT_STORAGE_QUICK_REF.md](CONTENT_STORAGE_QUICK_REF.md) - Content quick reference

### Implementation Files
- [MetadataStore.cs](../UpdateEngine.Metadata/src/Storage/AzureBlob/MetadataStore.cs) - Azure Blob metadata implementation
- [BlobContentStore.cs](../UpdateEngine.Metadata/src/Storage/AzureBlob/BlobContentStore.cs) - Azure Blob content implementation
- [FileSystemContentStore.cs](../UpdateEngine.Metadata/src/Storage/FileSystem/FileSystemContentStore.cs) - File System content implementation

### Metrics Files
- [MetadataStoreMetrics.cs](../UpdateEngine.Metadata/src/Metrics/MetadataStoreMetrics.cs) - Metadata metrics
- [BlobContentStoreMetrics.cs](../UpdateEngine.Metadata/src/Storage/AzureBlob/BlobContentStoreMetrics.cs) - Content metrics

### Configuration Files
- [appsettings.Development.json](../UpdateEngine.Configuration/src/shared/appsettings.Development.json) - Development settings
- [appsettings.Production.json](../UpdateEngine.Configuration/src/shared/appsettings.Production.json) - Production settings
- [appsettings.defaults.json](../UpdateEngine.Configuration/src/shared/appsettings.defaults.json) - Default settings

### Other Documentation
- [ANOMALY_METRICS.md](../UpdateEngine.Core/src/Metrics/ANOMALY_METRICS.md) - Anomaly detection metrics
- [MODEL_TRAINING_GUIDE.md](../UpdateEngine.Functions/src/Functions/Intelligence/MODEL_TRAINING_GUIDE.md) - ML.NET training guide

---

## Support

**Issues or Questions?**
- Check troubleshooting sections in documentation
- Review logs in Application Insights
- Monitor metrics in Grafana/Azure Monitor
- File GitHub issue with logs and metrics

---

**Last Updated:** 2024-01-XX  
**Version:** 1.0  
**Maintainer:** UpdateEngine Team
