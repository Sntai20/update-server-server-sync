# Metadata Storage Quick Reference
**Fast lookup guide for Windows Update metadata storage**

## Quick Facts

| Aspect | Details |
|--------|---------|
| **Storage Method** | Page Blob (Azure) or File System (Local) |
| **Compression** | GZip (70-90% size reduction) |
| **Page Size** | 512 bytes (Page Blob alignment) |
| **Upload Cache** | 32MB batch writes |
| **Download Cache** | 4MB range reads |
| **Initial Blob Size** | 32MB, grows in 32MB increments |

---

## Storage Locations

### Azure Blob Storage (Page Blob)

```
Container: "data"

Structure:
  data/metadata                          ? Page blob (compressed packages)
  data/identities-index                  ? Package ID ? Offset mapping
  data/categories-index                  ? Category ? Package IDs
  data/classifications-index             ? Classification ? Package IDs
  data/products-index                    ? Product ? Package IDs

Page Blob Layout:
  ???????????????????????????????????????
  ? Package 1 Metadata (GZip)           ? Offset: 0
  ? Package 1 File List (GZip)          ? Offset: 2048
  ???????????????????????????????????????
  ? Package 2 Metadata (GZip)           ? Offset: 4096
  ? Package 2 File List (GZip)          ? Offset: 6144
  ???????????????????????????????????????
```

### File System Storage

```
RootPath: "./store"

Structure:
  store/
    ??? metadata-index.json              ? Identity index
    ??? categories-index.json            ? Categories index
    ??? classifications-index.json       ? Classifications index
    ??? products-index.json              ? Products index
    ??? packages/                        ? Package metadata
    ?   ??? {guid}.json.gz
    ?   ??? ...
    ??? file-lists/                      ? Separate file lists
        ??? {guid}.json.gz
        ??? ...
```

### Azurite (Local Development)

```
Path: UpdateEngine.AppHost\src\out\azurite-data\__blobstorage__\data

Files:
  data/metadata                          ? Page blob
  data/identities-index                  ? Index blob
  data/categories-index                  ? Index blob
```

---

## Configuration

### Enable Azure Blob Storage

```json
{
  "UpdateEngine": {
    "StorageConfiguration": {
      "UseAzureStorageForMetadata": true,
      "MetadataContainerName": "data",
      "ReindexOnStartup": true
    }
  }
}
```

### Enable File System Storage

```json
{
  "UpdateEngine": {
    "StorageConfiguration": {
      "UseAzureStorageForMetadata": false,
      "ReindexOnStartup": false
    }
  }
}
```

### Enable Caching

```json
{
  "UpdateEngine": {
    "CacheConfiguration": {
      "EnableDistributedCache": true,
      "StatisticsCacheMinutes": 5,
      "UpdateDetailsCacheMinutes": 60
    }
  }
}
```

---

## Common Commands

### Check Package Count (Azure Blob)

```powershell
az storage blob download --container-name data --name identities-index --file identities.json
$identities = Get-Content identities.json | ConvertFrom-Json
$identities.Count
```

### Check Package Count (File System)

```powershell
Get-ChildItem -Path "store\packages" -File | Measure-Object
```

### Check Metadata Size (Azure Blob)

```powershell
az storage blob show --container-name data --name metadata --query "properties.contentLength"
```

### Check Metadata Size (File System)

```powershell
Get-ChildItem -Path "store\packages" -File | Measure-Object -Property Length -Sum
```

### List All Indexes

```powershell
az storage blob list --container-name data --prefix "*-index" --output table
```

### Download Metadata for Inspection

```powershell
# Azure Blob
az storage blob download --container-name data --name metadata --file metadata.bin

# File System (decompress package)
$compressed = [System.IO.File]::ReadAllBytes("store\packages\{guid}.json.gz")
$input = New-Object System.IO.MemoryStream(, $compressed)
$output = New-Object System.IO.MemoryStream
$gzipStream = New-Object System.IO.Compression.GZipStream($input, [System.IO.Compression.CompressionMode]::Decompress)
$gzipStream.CopyTo($output)
$json = [System.Text.Encoding]::UTF8.GetString($output.ToArray())
$json | ConvertFrom-Json | ConvertTo-Json -Depth 10
```

### Trigger Reindexing

```csharp
if (metadataStore.IsReindexingRequired)
{
    Console.WriteLine("Reindexing...");
    metadataStore.ReIndex();
    metadataStore.Flush();
}
```

---

## Package Store Entry

### Structure

```json
{
  "Identity": "3fa2b8c1-a0d5-e7f9-abc1-23def4567890",
  "MetadataOffset": 4096,
  "MetadataLength": 2048,
  "FileListOffset": 6144,
  "FileListLength": 1024
}
```

### Interpretation

| Field | Description | Example |
|-------|-------------|---------|
| `Identity` | Package GUID | `3fa2b8c1-...` |
| `MetadataOffset` | Byte offset in page blob | 4096 |
| `MetadataLength` | Compressed metadata size | 2048 bytes |
| `FileListOffset` | File list byte offset | 6144 |
| `FileListLength` | Compressed file list size | 1024 bytes |

---

## Compression Ratios

| Metadata Type | Uncompressed | Compressed | Savings |
|---------------|--------------|------------|---------|
| **Small Update** | 5 KB | 1.5 KB | 70% |
| **Medium Update** | 50 KB | 10 KB | 80% |
| **Large Update** | 500 KB | 50 KB | 90% |
| **File List (100 files)** | 100 KB | 20 KB | 80% |

**Example: 10,000 updates**
- Uncompressed: ~500 MB
- Compressed: ~50-100 MB
- **Savings: 400-450 MB**

---

## Cache Strategy

### Upload Cache (32MB)

**Purpose:** Batch writes to reduce Azure API calls

```
Flow:
  1. Add package to upload cache
  2. When cache reaches 32MB ? Flush to page blob
  3. Reset cache and continue
  
Benefit: 
  - 1 upload per 32MB vs. 1 per package
  - Lower Azure costs
  - Higher throughput
```

### Download Cache (4MB)

**Purpose:** Minimize range downloads

```
Flow:
  1. Check if package in cache
  2. If not ? Download 4MB range containing package
  3. Extract package from cache
  
Benefit:
  - 80-90% cache hit rate for sequential access
  - Lower latency
  - Fewer network roundtrips
```

---

## Performance Metrics

### Azure Blob Storage (Azurite)

| Operation | Latency | Throughput |
|-----------|---------|------------|
| **Add Package** | 1-5ms | 100-500/sec |
| **Get Metadata** (cached) | < 1ms | 10,000+/sec |
| **Get Metadata** (uncached) | 10-50ms | 100-200/sec |
| **Flush** | 100-500ms | N/A |
| **Reindex (10K)** | 30-60s | ~200/sec |

### Azure Blob Storage (Azure)

| Operation | Latency | Throughput |
|-----------|---------|------------|
| **Add Package** | 10-50ms | 50-200/sec |
| **Get Metadata** (cached) | < 1ms | 10,000+/sec |
| **Get Metadata** (uncached) | 50-200ms | 50-100/sec |
| **Flush** | 500-2000ms | N/A |
| **Reindex (10K)** | 60-120s | ~100/sec |

### File System (SSD)

| Operation | Latency | Throughput |
|-----------|---------|------------|
| **Add Package** | < 1ms | 1000+/sec |
| **Get Metadata** (cached) | < 1ms | 10,000+/sec |
| **Get Metadata** (uncached) | 1-5ms | 500-1000/sec |
| **Reindex (10K)** | 5-10s | ~1000/sec |

---

## Troubleshooting Checklist

### Problem: "Metadata store not found"

**Solution:**
```csharp
if (!PackageStore.Exists("./store"))
{
    var store = PackageStore.OpenOrCreate("./store");
}
```

---

### Problem: "Reindexing required"

**Solution:**
1. Check `IsReindexingRequired` property
2. Trigger reindex: `metadataStore.ReIndex()`
3. Flush: `metadataStore.Flush()`

**Configuration (auto-reindex on startup):**
```json
{
  "StorageConfiguration": {
    "ReindexOnStartup": true
  }
}
```

---

### Problem: "Package not found by ID"

**Solution:**
```csharp
var packageId = new Guid("3fa2b8c1-...");

if (!metadataStore.ContainsPackage(packageId))
{
    // Package not synced, trigger sync
    await syncService.SyncUpdatesAsync();
}
```

---

### Problem: "Slow metadata queries"

**Solutions:**
1. **Enable caching**
2. **Warm cache on startup**
3. **Check indexes**: `metadataStore.IsMetadataIndexingSupported`
4. **Reindex if needed**: `metadataStore.ReIndex()`

---

### Problem: "Out of memory during reindexing"

**Solution:** Process in batches
```csharp
const int batchSize = 1000;
var identities = metadataStore.GetPackageIdentities();

for (int i = 0; i < identities.Count; i += batchSize)
{
    var batch = identities.Skip(i).Take(batchSize);
    // Process batch
    GC.Collect();
}
```

---

## Code Examples

### Check if store exists

```csharp
if (PackageStore.Exists("./store"))
{
    Console.WriteLine("Store exists");
}
```

### Open or create store

```csharp
using var store = PackageStore.OpenOrCreate("./store");
```

### Get package by ID

```csharp
var packageId = new Guid("3fa2b8c1-a0d5-e7f9-abc1-23def4567890");
var package = store.GetPackage(packageId);
Console.WriteLine($"Title: {package.Title}");
```

### Query packages by category

```csharp
var securityUpdates = store
    .OfType<SoftwareUpdate>()
    .Where(u => u.Categories.Contains(securityCategoryGuid))
    .ToList();
```

### Get supersedence chain

```csharp
var update = store
    .OfType<SoftwareUpdate>()
    .FirstOrDefault(u => u.IsSupersededBy?.Count > 0);

if (update != null)
{
    Console.WriteLine($"Update: {update.Title}");
    Console.WriteLine("Superseded by:");
    
    foreach (var supersededId in update.IsSupersededBy)
    {
        var supersededUpdate = store.GetPackage(supersededId);
        Console.WriteLine($"  - {supersededUpdate.Title}");
    }
}
```

### Get file list

```csharp
var files = store.GetFiles<UpdateFile>(package.Id);
Console.WriteLine($"Package has {files.Count} files");

foreach (var file in files)
{
    Console.WriteLine($"  - {file.FileName} ({file.Size} bytes)");
}
```

### Flush metadata

```csharp
// After adding packages, flush to storage
store.AddPackage(package);
store.Flush();
```

---

## Storage Estimates

| Scenario | Packages | Avg Compressed Size | Total Storage |
|----------|----------|---------------------|---------------|
| **Small (Dev)** | 100-500 | 10 KB | 1-5 MB |
| **Medium (Test)** | 1,000-5,000 | 10 KB | 10-50 MB |
| **Large (Prod)** | 10,000-50,000 | 10 KB | 100-500 MB |
| **Enterprise** | 100,000+ | 10 KB | 1 GB+ |

**Uncompressed equivalent: 10x larger (500 MB ? 5 GB for 50K packages)**

---

## Page Blob Growth

| Packages | Blob Size |
|----------|-----------|
| 0 | 32 MB |
| 1,000 | 64 MB |
| 5,000 | 128 MB |
| 10,000 | 256 MB |
| 50,000 | 1 GB |
| 100,000 | 2 GB |

**Growth:** 32MB increments as needed

---

## Best Practices

? **Use FileSystem for local development** (fast, easy to inspect)  
? **Use Azure Blob for production** (scalable, durable)  
? **Enable caching** (reduce latency and costs)  
? **Reindex after bulk sync** (keep indexes current)  
? **Monitor metrics** (detect issues early)  
? **Warm cache on startup** (improve first-query performance)  
? **Process large datasets in batches** (avoid memory issues)  

---

## Metrics to Monitor

| Metric | Description | Alert Threshold |
|--------|-------------|-----------------|
| `MetadataStoreMetrics.PackagesAdded` | Packages added | N/A |
| `MetadataStoreMetrics.PackagesRetrieved` | Packages retrieved | N/A |
| `MetadataStoreMetrics.QueriesExecuted` | Total queries | N/A |
| `MetadataStoreMetrics.QueryDuration` | Query latency | > 500ms P95 |
| `MetadataStoreMetrics.FlushDuration` | Flush latency | > 2s P95 |

---

**Related Documentation:**
- [METADATA_STORAGE_ARCHITECTURE.md](METADATA_STORAGE_ARCHITECTURE.md) - Full technical details
- [CONTENT_STORAGE_ARCHITECTURE.md](CONTENT_STORAGE_ARCHITECTURE.md) - Binary content storage
- [MetadataStore.cs](../../UpdateEngine.Metadata/src/Storage/AzureBlob/MetadataStore.cs) - Azure Blob implementation
- [MetadataStoreMetrics.cs](../../UpdateEngine.Metadata/src/Metrics/MetadataStoreMetrics.cs) - Metrics definitions

---

**Last Updated:** 2024-01-XX  
**Version:** 1.0
