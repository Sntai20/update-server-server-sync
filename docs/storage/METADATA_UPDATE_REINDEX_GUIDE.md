# Metadata Update and Reindexing Guide
**Complete guide to updating and reindexing Windows Update metadata**

## Table of Contents
1. [Overview](#overview)
2. [Updating Metadata](#updating-metadata)
3. [Understanding Reindexing](#understanding-reindexing)
4. [Triggering Reindexing](#triggering-reindexing)
5. [Reindexing Process Details](#reindexing-process-details)
6. [Performance and Optimization](#performance-and-optimization)
7. [Monitoring and Troubleshooting](#monitoring-and-troubleshooting)
8. [Best Practices](#best-practices)

---

## Overview

The UpdateEngine system uses a **metadata store** to manage Windows Update information (titles, descriptions, categories, supersedence relationships). This guide covers:

- How metadata is added/updated in the store
- When and why reindexing is required
- Different methods to trigger reindexing
- Performance characteristics and optimization strategies

### Key Concepts

| Concept | Description |
|---------|-------------|
| **Metadata** | Update information (JSON) - NOT binary content |
| **Package Store Entry** | Index entry mapping package GUID to storage offset |
| **Identity Index** | Primary index for O(1) package lookup by GUID |
| **Secondary Indexes** | Category, classification, product indexes for filtering |
| **Reindexing** | Rebuilding all indexes from stored metadata |

---

## Updating Metadata

### 1. Adding Metadata via Sync

The most common way to update metadata is through **upstream synchronization**:

```csharp
// From SyncService.cs
public async Task SyncUpdatesAsync(CancellationToken cancellationToken)
{
    this.logger.LogInformation("Starting metadata sync from upstream server");
    
    // 1. Create upstream source
    var updatesSource = new UpstreamUpdatesSource(this.endpoint);
    
    // 2. Copy metadata to local store (with optional filter)
    await updatesSource.CopyTo(
        this.metadataStore, 
        this.metadataFilter, 
        cancellationToken
    );
    
    // 3. Check if reindexing is required
    if (this.metadataStore.IsReindexingRequired)
    {
        this.logger.LogInformation("Reindexing required after bulk sync");
        this.metadataStore.ReIndex();
        this.metadataStore.Flush();
    }
    
    this.logger.LogInformation("Metadata sync complete. Package count: {Count}", 
        this.metadataStore.GetPackageIdentities().Count);
}
```

**What happens during sync:**
1. Connects to upstream Microsoft Update servers
2. Downloads update metadata in batches
3. Compresses with GZip (70-90% reduction)
4. Appends to metadata Page Blob (Azure) or files (FileSystem)
5. Updates identity index with new package entries
6. Sets `IsReindexingRequired` flag if many packages added

---

### 2. Adding Individual Packages

For programmatic metadata addition:

```csharp
// From MetadataStore.cs
public PackageStoreEntry AddPackage(IPackage package)
{
    // 1. Serialize package to JSON
    var packageJson = JsonSerializer.Serialize(package);
    
    // 2. Compress with GZip
    using var compressedStream = new MemoryStream();
    using var gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal);
    var bytes = Encoding.UTF8.GetBytes(packageJson);
    gzipStream.Write(bytes);
    gzipStream.Flush();
    
    // 3. Add to upload cache (32MB batch)
    var metadataOffset = this.NextAvailableOffset;
    var metadataLength = compressedStream.Length;
    this.UploadCache.Write(compressedStream.ToArray());
    
    // 4. Create package store entry
    var entry = new PackageStoreEntry
    {
        Identity = package.Id.ToString(),
        MetadataOffset = metadataOffset,
        MetadataLength = metadataLength,
        FileListOffset = 0,  // Set if package has files
        FileListLength = 0
    };
    
    // 5. Update identity index
    this.identityIndex[package.Id] = entry;
    
    // 6. Auto-flush when cache reaches 32MB
    if (this.UploadCache.Position >= UploadCacheSize)
    {
        this.FlushUploadCache();
    }
    
    // 7. Mark reindexing as required
    this.isReindexingRequired = true;
    
    return entry;
}
```

**Package Store Entry Structure:**
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

### 3. Metadata Storage Architecture

**Azure Page Blob Layout:**
```
data/metadata (Page Blob)
  ??? Package 1 Metadata (GZip)  ? Offset: 0,    Length: 2048
  ??? Package 1 File List (GZip) ? Offset: 2048, Length: 1024
  ??? Package 2 Metadata (GZip)  ? Offset: 4096, Length: 2048
  ??? Package 2 File List (GZip) ? Offset: 6144, Length: 1024
  ??? ...

data/identities-index (Block Blob)
  ??? Dictionary<Guid, PackageStoreEntry>

data/categories-index (Block Blob)
  ??? Dictionary<Guid, HashSet<Guid>>
```

**FileSystem Layout:**
```
store/
  ??? metadata-index.json        ? Identity index
  ??? categories-index.json      ? Category ? Package IDs
  ??? classifications-index.json ? Classification ? Package IDs
  ??? packages/
  ?   ??? {guid1}.json.gz        ? Compressed metadata
  ?   ??? {guid2}.json.gz
  ?   ??? ...
  ??? file-lists/
      ??? {guid1}.json.gz        ? Compressed file lists
      ??? ...
```

---

## Understanding Reindexing

### What is Reindexing?

**Reindexing** is the process of rebuilding all secondary indexes (categories, classifications, products) from the stored metadata. It's required because:

1. **After bulk sync**: Thousands of packages added, indexes incomplete
2. **Index corruption**: Detected corruption in index files
3. **New index types**: New secondary indexes added to system
4. **Manual cleanup**: After removing or modifying packages

### When is Reindexing Required?

Check the `IsReindexingRequired` property:

```csharp
if (metadataStore.IsReindexingRequired)
{
    logger.LogWarning("Metadata store requires reindexing");
    // Trigger reindex...
}
```

**Automatic triggers:**
- Bulk sync (500+ packages added)
- Store opened with missing/corrupted indexes
- After metadata cleanup operations

### What Happens During Reindexing?

```csharp
public void ReIndex()
{
    logger.LogInformation("Starting metadata store reindexing");
    
    // 1. Clear existing indexes
    this.identityIndex.Clear();
    this.categoriesIndex.Clear();
    this.classificationsIndex.Clear();
    this.productsIndex.Clear();
    
    // 2. Get all package identities
    var packageIdentities = this.GetAllPackageEntries();
    logger.LogInformation("Reindexing {Count} packages", packageIdentities.Count);
    
    // 3. Iterate all packages
    foreach (var entry in packageIdentities)
    {
        // Load metadata from storage
        var package = this.GetPackageFromEntry(entry);
        
        // 4. Rebuild identity index
        this.identityIndex[package.Id] = entry;
        
        // 5. Rebuild category indexes
        if (package is IUpdate update)
        {
            foreach (var categoryId in update.Categories)
            {
                if (!this.categoriesIndex.ContainsKey(categoryId))
                {
                    this.categoriesIndex[categoryId] = new HashSet<Guid>();
                }
                this.categoriesIndex[categoryId].Add(package.Id);
            }
            
            // 6. Rebuild classification indexes
            foreach (var classificationId in update.Classifications)
            {
                if (!this.classificationsIndex.ContainsKey(classificationId))
                {
                    this.classificationsIndex[classificationId] = new HashSet<Guid>();
                }
                this.classificationsIndex[classificationId].Add(package.Id);
            }
            
            // 7. Rebuild product indexes
            foreach (var productId in update.Products)
            {
                if (!this.productsIndex.ContainsKey(productId))
                {
                    this.productsIndex[productId] = new HashSet<Guid>();
                }
                this.productsIndex[productId].Add(package.Id);
            }
        }
    }
    
    // 8. Save indexes to storage
    this.SaveIndexes();
    
    // 9. Clear reindexing flag
    this.isReindexingRequired = false;
    
    logger.LogInformation("Metadata store reindexing complete");
}
```

**Index Types Rebuilt:**

| Index | Purpose | Example |
|-------|---------|---------|
| **Identity Index** | Package GUID ? Storage offset | `{guid} ? {offset: 4096, length: 2048}` |
| **Categories Index** | Category GUID ? Package GUIDs | `{security-updates} ? [{guid1}, {guid2}, ...]` |
| **Classifications Index** | Classification ? Packages | `{critical-updates} ? [{guid1}, ...]` |
| **Products Index** | Product ? Packages | `{windows-10} ? [{guid1}, {guid2}, ...]` |

---

## Triggering Reindexing

### Method 1: Automatic on Startup

**Configuration** (`appsettings.Development.json`):
```json
{
  "UpdateEngine": {
    "StorageConfiguration": {
      "UseAzureStorageForMetadata": true,
      "MetadataContainerName": "data",
      "ReindexOnStartup": true  // ? Enable auto-reindex
    }
  }
}
```

**Implementation** (`Program.cs`):
```csharp
public static async Task Main(string[] args)
{
    var host = CreateHost(args);
    
    // Get metadata store and configuration
    var metadataStore = host.Services.GetRequiredService<IMetadataStore>();
    var config = host.Services.GetRequiredService<StorageConfiguration>();
    
    // Check if reindex on startup is enabled
    if (config.ReindexOnStartup && metadataStore.IsReindexingRequired)
    {
        logger.LogInformation("Reindexing metadata store on startup");
        metadataStore.ReIndex();
        metadataStore.Flush();
    }
    
    await host.RunAsync();
}
```

---

### Method 2: Manual via Code

**In application code:**
```csharp
// After bulk operations
await syncService.SyncUpdatesAsync(cancellationToken);

if (metadataStore.IsReindexingRequired)
{
    logger.LogInformation("Triggering manual reindex");
    metadataStore.ReIndex();
    metadataStore.Flush();
}
```

---

### Method 3: Via CLI Tool

**Using `update-cli`:**
```powershell
# Trigger reindex via CLI
update-cli cleanup --reindex

# With specific metadata path
update-cli cleanup --reindex --metadata-path "./store"

# With verbose logging
update-cli cleanup --reindex --verbose
```

**CLI Implementation** (`CleanupCommand.cs`):
```csharp
[Command("cleanup", Description = "Cleanup and maintenance operations")]
public class CleanupCommand
{
    [Option("--reindex", Description = "Reindex the metadata store")]
    public bool ReindexMetadata { get; set; }
    
    [Option("--metadata-path", Description = "Path to metadata store")]
    public string? MetadataPath { get; set; }
    
    public async Task<int> OnExecuteAsync()
    {
        if (this.ReindexMetadata)
        {
            logger.LogInformation("Opening metadata store: {Path}", MetadataPath);
            using var store = PackageStore.Open(MetadataPath ?? "./store");
            
            logger.LogInformation("Reindexing metadata store");
            store.ReIndex();
            store.Flush();
            
            logger.LogInformation("Reindexing complete");
        }
        
        return 0;
    }
}
```

---

### Method 4: Via Azure Functions HTTP Endpoint

**HTTP POST to maintenance function:**
```powershell
# Trigger reindex via HTTP
curl -X POST http://localhost:7071/api/ReindexMetadata

# In production (with function key)
curl -X POST https://my-update-engine.azurewebsites.net/api/ReindexMetadata?code={function-key}
```

**Function Implementation** (`MaintenanceFunction.cs`):
```csharp
[Function("ReindexMetadata")]
public async Task<HttpResponseData> ReindexMetadata(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
{
    this.logger.LogInformation("Reindex metadata request received");
    
    try
    {
        // Get metadata store from DI
        var metadataStore = this.serviceProvider.GetRequiredService<IMetadataStore>();
        
        // Check if reindexing is required
        if (!metadataStore.IsReindexingRequired)
        {
            this.logger.LogInformation("Metadata store does not require reindexing");
            var notRequiredResponse = req.CreateResponse(HttpStatusCode.OK);
            await notRequiredResponse.WriteStringAsync("Reindexing not required");
            return notRequiredResponse;
        }
        
        // Perform reindex
        this.logger.LogInformation("Starting metadata store reindexing");
        var sw = Stopwatch.StartNew();
        
        metadataStore.ReIndex();
        metadataStore.Flush();
        
        sw.Stop();
        this.logger.LogInformation("Reindexing complete in {Duration}ms", sw.ElapsedMilliseconds);
        
        // Record metrics
        MetadataStoreMetrics.ReindexOperations.Add(1);
        MetadataStoreMetrics.ReindexDuration.Record(sw.ElapsedMilliseconds);
        
        // Return success response
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            success = true,
            durationMs = sw.ElapsedMilliseconds,
            message = "Metadata store reindexing complete"
        });
        
        return response;
    }
    catch (Exception ex)
    {
        this.logger.LogError(ex, "Error during metadata reindexing");
        
        var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
        await errorResponse.WriteAsJsonAsync(new
        {
            success = false,
            error = ex.Message
        });
        
        return errorResponse;
    }
}
```

---

### Method 5: Via Timer Trigger (Scheduled)

**Scheduled reindexing** (`appsettings.json`):
```json
{
  "UpdateEngine": {
    "MaintenanceSchedule": {
      "ReindexCronExpression": "0 0 2 * * *",  // 2 AM daily
      "EnableScheduledReindex": true
    }
  }
}
```

**Timer Function** (`MetadataMaintenanceTimer.cs`):
```csharp
[Function("ScheduledReindex")]
public async Task ScheduledReindex(
    [TimerTrigger("0 0 2 * * *")] TimerInfo timer,  // 2 AM daily
    FunctionContext context)
{
    this.logger.LogInformation("Scheduled reindex triggered at {Time}", DateTime.UtcNow);
    
    // Check if scheduled reindex is enabled
    if (!this.maintenanceConfig.EnableScheduledReindex)
    {
        this.logger.LogInformation("Scheduled reindex is disabled");
        return;
    }
    
    // Perform reindex
    var metadataStore = this.serviceProvider.GetRequiredService<IMetadataStore>();
    
    if (metadataStore.IsReindexingRequired)
    {
        this.logger.LogInformation("Performing scheduled reindex");
        metadataStore.ReIndex();
        metadataStore.Flush();
    }
    else
    {
        this.logger.LogInformation("Reindexing not required");
    }
}
```

---

## Reindexing Process Details

### Memory Management

For large metadata stores (50K+ packages), use **batch processing** to avoid memory issues:

```csharp
public void ReIndexIncrementally()
{
    const int batchSize = 1000;
    var identities = this.metadataStore.GetPackageIdentities();
    
    this.logger.LogInformation("Reindexing {Count} packages in batches of {BatchSize}", 
        identities.Count, batchSize);
    
    for (int i = 0; i < identities.Count; i += batchSize)
    {
        var batch = identities.Skip(i).Take(batchSize);
        
        foreach (var packageId in batch)
        {
            var package = this.metadataStore.GetPackage(packageId);
            // Rebuild indexes for this package
            this.RebuildIndexesForPackage(package);
        }
        
        // Force garbage collection every batch
        GC.Collect();
        GC.WaitForPendingFinalizers();
        
        this.logger.LogDebug("Processed batch {Current}/{Total}", 
            Math.Min(i + batchSize, identities.Count), identities.Count);
    }
    
    // Save indexes to storage
    this.SaveIndexes();
}
```

---

### Progress Reporting

Monitor reindexing progress with events:

```csharp
// Subscribe to progress events
metadataStore.PackageIndexingProgress += (sender, e) =>
{
    logger.LogInformation("Reindexing progress: {Current}/{Total} ({Percent}%)",
        e.Current, e.Total, (e.Current * 100 / e.Total));
};

// Perform reindex
metadataStore.ReIndex();
```

**Event Args:**
```csharp
public class PackageStoreEventArgs : EventArgs
{
    public int Current { get; set; }
    public int Total { get; set; }
    public string? Message { get; set; }
}
```

---

## Performance and Optimization

### Performance Characteristics

| Packages | Reindex Time (Azurite) | Reindex Time (Azure) | Memory Usage |
|----------|------------------------|----------------------|--------------|
| 100 | ~1 second | ~2 seconds | ~10 MB |
| 1,000 | ~5 seconds | ~10 seconds | ~50 MB |
| 10,000 | 30-60 seconds | 60-120 seconds | ~200 MB |
| 50,000 | 3-5 minutes | 6-10 minutes | ~1 GB |
| 100,000 | 10-15 minutes | 20-30 minutes | ~2 GB |

**Factors affecting performance:**
- **Storage backend**: Azure Blob vs. FileSystem (FileSystem is faster)
- **Network latency**: Azurite (local) vs. Azure (network)
- **Package complexity**: Number of files, categories, classifications
- **Batch size**: Larger batches = better throughput, higher memory
- **Compression level**: GZip decompression overhead

---

### Optimization Strategies

#### 1. Use Appropriate Configuration

**Development** (`appsettings.Development.json`):
```json
{
  "StorageConfiguration": {
    "UseAzureStorageForMetadata": false,  // Use FileSystem for speed
    "ReindexOnStartup": true  // Auto-reindex on startup
  }
}
```

**Production** (`appsettings.Production.json`):
```json
{
  "StorageConfiguration": {
    "UseAzureStorageForMetadata": true,  // Use Azure Blob for durability
    "ReindexOnStartup": false  // Manual reindex only
  }
}
```

---

#### 2. Schedule During Low-Traffic Periods

```json
{
  "MaintenanceSchedule": {
    "ReindexCronExpression": "0 0 2 * * 0",  // 2 AM every Sunday
    "EnableScheduledReindex": true
  }
}
```

---

#### 3. Enable Caching

Reduce query load after reindexing:

```json
{
  "CacheConfiguration": {
    "EnableDistributedCache": true,
    "StatisticsCacheMinutes": 5,
    "UpdateDetailsCacheMinutes": 60
  }
}
```

---

#### 4. Monitor Metrics

Track reindexing operations:

```csharp
// Metrics tracked automatically
MetadataStoreMetrics.ReindexOperations.Add(1);
MetadataStoreMetrics.ReindexDuration.Record(durationMs);
MetadataStoreMetrics.PackagesIndexed.Add(packageCount);
```

**Alert thresholds:**
- Reindex duration > 5 minutes for 10K packages
- Reindex failures
- High memory usage during reindexing

---

## Monitoring and Troubleshooting

### Monitoring Reindexing

**Check reindex status:**
```csharp
if (metadataStore.IsReindexingRequired)
{
    logger.LogWarning("Metadata store requires reindexing");
}
```

**Monitor pending packages:**
```csharp
var pendingPackages = metadataStore.GetPendingPackages();
logger.LogInformation("{Count} packages pending indexing", pendingPackages.Count);
```

---

### Troubleshooting Common Issues

#### Issue 1: "Reindexing takes too long"

**Symptoms:**
- Reindexing > 5 minutes for 10K packages
- High CPU/memory usage
- Application unresponsive

**Solutions:**
1. **Use batch processing** (see Memory Management section)
2. **Schedule during off-hours**
3. **Increase Azure Function timeout**:
   ```json
   {
     "functionTimeout": "00:30:00"  // 30 minutes
   }
   ```
4. **Use FileSystem for local development**

---

#### Issue 2: "Out of memory during reindexing"

**Symptoms:**
- `OutOfMemoryException`
- Application crashes during reindex

**Solutions:**
1. **Use incremental reindexing** (batch processing)
2. **Force GC between batches**:
   ```csharp
   GC.Collect();
   GC.WaitForPendingFinalizers();
   ```
3. **Reduce batch size** (default 1000 ? 500)
4. **Increase function memory allocation** (Azure Functions Premium)

---

#### Issue 3: "Indexes corrupted after reindex"

**Symptoms:**
- Queries return no results
- `IsReindexingRequired` always true
- Index files missing/empty

**Solutions:**
1. **Delete index files and reindex**:
   ```powershell
   # Azure Blob
   az storage blob delete --container-name data --name identities-index
   az storage blob delete --container-name data --name categories-index
   
   # FileSystem
   Remove-Item "store\metadata-index.json"
   Remove-Item "store\categories-index.json"
   ```
2. **Trigger reindex**:
   ```csharp
   metadataStore.ReIndex();
   metadataStore.Flush();
   ```
3. **Check logs for errors during save**

---

#### Issue 4: "Reindex completes but IsReindexingRequired still true"

**Symptoms:**
- Reindex completes successfully
- Flag not cleared

**Cause:** Flag not reset after successful reindex

**Solution:**
```csharp
// Ensure flag is cleared after reindex
metadataStore.ReIndex();
metadataStore.Flush();

// Verify flag cleared
if (metadataStore.IsReindexingRequired)
{
    logger.LogError("Reindexing flag not cleared after successful reindex");
}
```

---

## Best Practices

### Development Environment

? **Use FileSystem storage** (faster than Azurite)  
? **Enable ReindexOnStartup: true** (auto-heal)  
? **Monitor reindex duration** (detect performance issues early)  
? **Test with realistic data volumes** (10K+ packages)  

### Production Environment

? **Use Azure Blob Storage** (durable, scalable)  
? **Disable ReindexOnStartup** (manual control)  
? **Schedule periodic reindexing** (weekly/monthly)  
? **Monitor metrics and alerts** (detect failures)  
? **Use incremental reindexing for large stores** (50K+ packages)  
? **Enable caching** (reduce query load after reindex)  

### Operational Guidelines

| Scenario | Recommendation |
|----------|----------------|
| **After bulk sync** | Automatic reindex (built-in) |
| **Production deployments** | Schedule during maintenance window |
| **Large stores (50K+)** | Use incremental/batch reindexing |
| **High-traffic systems** | Schedule during low-traffic periods |
| **Development** | Auto-reindex on startup |
| **Monitoring** | Alert on duration > 5 min per 10K packages |

---

## Summary

**Updating Metadata:**
- Metadata added via sync, programmatic API, or bulk import
- Compressed with GZip (70-90% reduction)
- Stored in Page Blob (Azure) or file system (local)
- Triggers `IsReindexingRequired` flag

**Reindexing:**
- Rebuilds all secondary indexes from stored metadata
- Required after bulk sync, index corruption, or cleanup
- Can be triggered automatically, manually, via CLI, HTTP, or scheduled
- Performance: ~1 minute per 10,000 packages (Azurite)

**Key Takeaways:**
- Enable **ReindexOnStartup** in development
- Disable **ReindexOnStartup** in production
- Schedule periodic reindexing during **low-traffic periods**
- Use **batch processing** for large stores (50K+ packages)
- Monitor **metrics** and set up **alerts**
- Use **caching** to reduce query load after reindexing

---

**Related Documentation:**
- [METADATA_STORAGE_ARCHITECTURE.md](METADATA_STORAGE_ARCHITECTURE.md) - Metadata storage architecture
- [METADATA_STORAGE_QUICK_REF.md](METADATA_STORAGE_QUICK_REF.md) - Quick reference
- [CONTENT_STORAGE_ARCHITECTURE.md](CONTENT_STORAGE_ARCHITECTURE.md) - Content storage
- [README.md](README.md) - Storage documentation index

**Implementation References:**
- [MetadataStore.cs](../../UpdateEngine.Metadata/src/Storage/AzureBlob/MetadataStore.cs) - Core implementation
- [SyncService.cs](../../UpdateEngine.Core/src/Services/SyncService.cs) - Sync service
- [MetadataStoreCleanup.cs](../../UpdateEngine.Core/src/Maintenance/MetadataStoreCleanup.cs) - Maintenance operations
- [MaintenanceFunction.cs](../../UpdateEngine.Functions/src/Functions/MaintenanceFunction.cs) - HTTP endpoints

---

**Last Updated:** 2024-11-30  
**Version:** 1.0  
**Authors:** UpdateEngine Team
