# Consolidation Recommendations: Making Functions "Options" of Other Functions

## Your Question
> "Some of these functions feel like they can be options for other functions. How can we consolidate the code?"

## Short Answer

**Yes! You can consolidate from 35+ functions down to ~20 functions** by applying the **Options Pattern** (discriminated unions) where multiple functions perform variations of the same core operation.

## Key Insight: When to Consolidate

Functions should be consolidated when they:
1. ? Perform the **same core operation** with minor variations
2. ? Share **most of the same code** with different parameters
3. ? Have **similar request/response structures**
4. ? Represent **choices** rather than different operations

Functions should stay separate when they:
1. ? Perform **fundamentally different operations**
2. ? Require **different protocols** (SOAP vs HTTP)
3. ? Have **different triggers** (HTTP vs Timer vs Blob)
4. ? Serve **different audiences** or security contexts

## Consolidation Examples from Your Codebase

### Example 1: Sync Operations (11 ? 1 Function) ?

**Current State** (11 separate functions):
```csharp
POST /api/sync/categories/start
POST /api/sync/updates/start  
POST /api/sync/comprehensive/start
POST /api/sync/pause
POST /api/sync/resume
POST /api/sync/cancel
POST /api/sync/categories/background
POST /api/sync/updates/background
POST /api/sync/comprehensive/background
GET /api/sync/status
GET /api/sync/progress
```

**Consolidated** (1 function with options):
```csharp
[Function("Sync")]
public async Task<HttpResponseData> Sync(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
{
    var request = await req.ReadFromJsonAsync<UnifiedSyncRequest>();
    
    // The "options" are in the request body
    return request.Action switch
    {
        SyncAction.Start => await StartSync(request.SyncType, request.Filter),
        SyncAction.Pause => await PauseSync(),
        SyncAction.Resume => await ResumeSync(),
        SyncAction.Cancel => await CancelSync(),
        _ => CreateErrorResponse("Unknown action")
    };
}

// Request model defines the "options"
public class UnifiedSyncRequest
{
    public SyncType SyncType { get; set; }      // categories | updates | comprehensive
    public SyncAction Action { get; set; }      // start | pause | resume | cancel
    public SyncFilter? Filter { get; set; }
}
```

**Usage Examples:**
```bash
# Start category sync (was POST /api/sync/categories/start)
curl -X POST /api/sync -d '{"syncType":"categories","action":"start"}'

# Start update sync (was POST /api/sync/updates/start)
curl -X POST /api/sync -d '{"syncType":"updates","action":"start","filter":{...}}'

# Pause any sync (was POST /api/sync/pause)
curl -X POST /api/sync -d '{"action":"pause"}'
```

**Why This Works:**
- All sync operations share the same core logic (ISyncService)
- The variations are just parameters (type, action, filter)
- Same request/response structure
- Same error handling
- Same security requirements

---

### Example 2: Metadata Export (5 ? 1 Function) ?

**Current State** (5 separate functions):
```csharp
POST /api/metadata/query            // Returns JSON inline
POST /api/metadata/export           // Returns JSON to blob
POST /api/metadata/export/advanced  // Returns JSON to blob with advanced filter
POST /api/metadata/export/csv       // Returns CSV inline
GET /api/metadata/summary           // Returns summary statistics
```

**Consolidated** (1 function with options):
```csharp
[Function("MetadataQuery")]
public async Task<HttpResponseData> Query(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
{
    var request = await req.ReadFromJsonAsync<MetadataQueryRequest>();
    
    // Query the data (same for all)
    var results = await queryService.QueryAsync(request.Filter);
    
    // Format based on options
    return (request.OutputFormat, request.ExportMode) switch
    {
        (OutputFormat.Json, ExportMode.Inline) => ReturnJsonInline(results),
        (OutputFormat.Json, ExportMode.Blob) => ExportJsonToBlob(results),
        (OutputFormat.Csv, ExportMode.Inline) => ReturnCsvInline(results),
        (OutputFormat.Csv, ExportMode.Blob) => ExportCsvToBlob(results),
        (OutputFormat.Summary, _) => ReturnSummary(results),
        _ => CreateErrorResponse("Unknown format/mode combination")
    };
}

public class MetadataQueryRequest
{
    public MetadataFilter Filter { get; set; }
    public OutputFormat OutputFormat { get; set; }  // json | csv | summary
    public ExportMode ExportMode { get; set; }      // inline | blob
}
```

**Usage Examples:**
```bash
# Query and return JSON inline (was POST /api/metadata/query)
curl -X POST /api/metadata/query -d '{"filter":{...},"outputFormat":"json","exportMode":"inline"}'

# Export to CSV in blob storage (was POST /api/metadata/export/csv)
curl -X POST /api/metadata/query -d '{"filter":{...},"outputFormat":"csv","exportMode":"blob"}'

# Get summary statistics (was GET /api/metadata/summary)
curl -X POST /api/metadata/query -d '{"filter":{...},"outputFormat":"summary"}'
```

**Why This Works:**
- All operations query metadata (same IMetadataQueryService)
- Variations are just formatting and output destination
- Can add new formats easily (XML, Parquet, etc.)
- Consistent filtering across all outputs

---

### Example 3: Health Checks (5 ? 1 Function) ?

**Current State** (5 separate functions):
```csharp
GET /api/health/status        // Store status only
GET /api/health/sync          // Sync progress only
GET /api/health/diagnostics   // Full diagnostics
GET /api/health/full          // Everything
POST /api/health/export       // Export to blob
```

**Consolidated** (1 function with query param options):
```csharp
[Function("Health")]
public async Task<HttpResponseData> GetHealth(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
{
    // Option comes from query parameter
    var scope = req.Query["scope"] ?? "status";  // Default to basic status
    var export = req.Query["export"] == "true";
    
    var health = await healthService.GetHealthAsync(scope);
    
    if (export)
    {
        return await ExportToBlob(health);
    }
    
    return ReturnInline(health);
}
```

**Usage Examples:**
```bash
# Basic status (was GET /api/health/status)
curl GET /api/health

# Sync progress (was GET /api/health/sync)
curl GET /api/health?scope=sync

# Full diagnostics (was GET /api/health/diagnostics)
curl GET /api/health?scope=diagnostics

# Export to blob (was POST /api/health/export)
curl GET /api/health?scope=full&export=true
```

**Why This Works:**
- All operations access the same health data
- Variations are just how much data to return
- Query parameters are simpler for GET operations
- Easy to add new scopes (memory, performance, etc.)

---

### Example 4: Content Download (2 ? 1 Function) ?

**Current State** (2 separate functions):
```csharp
POST /api/download/metadata/{updateId}
POST /api/download/content/{updateId}
```

**Consolidated** (1 function with type option):
```csharp
[Function("ContentDownload")]
public async Task<HttpResponseData> Download(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
{
    var request = await req.ReadFromJsonAsync<ContentDownloadRequest>();
    
    return request.Type switch
    {
        DownloadType.Metadata => await DownloadMetadata(request.UpdateIds),
        DownloadType.Content => await DownloadContent(request.UpdateIds),
        DownloadType.Both => await DownloadBoth(request.UpdateIds),
        _ => CreateErrorResponse("Unknown download type")
    };
}

public class ContentDownloadRequest
{
    public DownloadType Type { get; set; }        // metadata | content | both
    public List<Guid> UpdateIds { get; set; }
}
```

**Usage Examples:**
```bash
# Download metadata only (was POST /api/download/metadata/{id})
curl -X POST /api/content/download -d '{"type":"metadata","updateIds":["..."]}'

# Download content only (was POST /api/download/content/{id})
curl -X POST /api/content/download -d '{"type":"content","updateIds":["..."]}'

# Download both (NEW capability!)
curl -X POST /api/content/download -d '{"type":"both","updateIds":["..."]}'
```

**Why This Works:**
- Both operations use IContentStore
- Same security requirements
- Can batch multiple updates
- Opens door for combined downloads

---

## When NOT to Consolidate

### Example 1: SOAP Services (Keep Separate) ?

**These should stay separate:**
```csharp
[Function("ClientWebService")]       // Windows Update client protocol
[Function("ServerWebService")]       // WSUS server protocol  
[Function("SimpleAuthWebService")]   // Simple auth
[Function("DssAuthWebService")]      // DSS auth
[Function("ReportingWebService")]    // Reporting
```

**Why?**
- Different SOAP protocols
- Different authentication mechanisms
- Different client expectations
- WSUS compatibility requirements

### Example 2: Different Triggers (Keep Separate) ?

**These should stay separate:**
```csharp
[Function("Sync")]                          // HTTP trigger
[Function("BackgroundSync")]                // Timer trigger
[Function("ManifestVerificationTrigger")]   // Blob trigger
```

**Why?**
- Different trigger types (HTTP vs Timer vs Blob)
- Different execution contexts
- Different failure handling
- Different scalability requirements

### Example 3: Fundamentally Different Operations (Keep Separate) ?

**These should stay separate:**
```csharp
[Function("MetadataQuery")]    // Querying metadata
[Function("ContentDownload")]  // Downloading files
[Function("DriverMatch")]      // Matching drivers to hardware
```

**Why?**
- Completely different operations
- Different data sources (metadata store vs content store vs driver database)
- Different response types
- Different performance characteristics

---

## Implementation Pattern

Here's the general pattern for consolidation:

### Step 1: Create Unified Request Model

```csharp
public class UnifiedRequest
{
    // Discriminator: What operation to perform
    public OperationType Type { get; set; }
    
    // Action: How to perform it
    public OperationAction Action { get; set; }
    
    // Options: Operation-specific parameters
    public OperationOptions? Options { get; set; }
}

public enum OperationType
{
    CategorySync,
    UpdateSync,
    ComprehensiveSync
}

public enum OperationAction
{
    Start,
    Pause,
    Resume,
    Cancel
}
```

### Step 2: Create Orchestrator Service

```csharp
public interface IOperationOrchestrator
{
    Task<OperationResult> ExecuteAsync(
        OperationType type, 
        OperationAction action, 
        OperationOptions? options,
        CancellationToken cancellationToken);
}

public class OperationOrchestrator : IOperationOrchestrator
{
    public async Task<OperationResult> ExecuteAsync(...)
    {
        // Route to specific handler based on type and action
        return (type, action) switch
        {
            (OperationType.CategorySync, OperationAction.Start) => 
                await StartCategorySync(options, cancellationToken),
            
            (OperationType.UpdateSync, OperationAction.Start) => 
                await StartUpdateSync(options, cancellationToken),
                
            // ... more cases
            
            _ => throw new InvalidOperationException($"Unknown operation: {type}/{action}")
        };
    }
}
```

### Step 3: Create Unified Function

```csharp
public class UnifiedFunction
{
    private readonly IOperationOrchestrator orchestrator;
    
    [Function("UnifiedOperation")]
    public async Task<HttpResponseData> Execute(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        // Parse request
        var request = await req.ReadFromJsonAsync<UnifiedRequest>(cancellationToken);
        
        // Execute via orchestrator
        var result = await orchestrator.ExecuteAsync(
            request.Type, 
            request.Action, 
            request.Options, 
            cancellationToken);
        
        // Return response
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result, cancellationToken);
        return response;
    }
}
```

---

## Benefits of This Approach

### 1. Code Reuse (60% less duplication)
- Shared validation logic
- Shared error handling
- Shared logging
- Shared authentication

### 2. Consistency
- Same request/response patterns
- Same error messages
- Same HTTP status codes
- Same logging format

### 3. Extensibility
- Add new operations without new functions
- Add new actions without new endpoints
- Add new options without breaking changes

### 4. Testing
- Test orchestrator logic once
- Test function routing logic once
- Fewer integration tests needed

### 5. Documentation
- Single comprehensive API doc
- Clear option enumeration
- Consistent examples

---

## Recommended Consolidation Order

1. **Start with Sync Operations** (highest ROI)
   - 11 functions ? 3 functions
   - Most duplication currently
   - Clear option pattern

2. **Then Metadata Operations**
   - 8 functions ? 3 functions
   - Similar patterns to sync
   - High code reuse potential

3. **Then Health/Diagnostics**
   - 8 functions ? 3 functions
   - Query parameter pattern works well

4. **Then Content Operations**
   - 6 functions ? 4 functions
   - Smaller wins but still valuable

5. **Keep SOAP and Triggers As-Is**
   - Different requirements
   - Already well-structured

---

## Final Recommendation

**Yes, consolidate functions that feel like "options" of each other!**

Apply the **Options Pattern** where:
- ? Functions perform variations of the same operation
- ? Request/response structures are similar
- ? Core logic is shared
- ? Security requirements are the same

This will reduce your codebase by **~40%** while improving:
- ?? Maintainability
- ?? Consistency  
- ?? Testability
- ?? Developer experience

**See the detailed guides:**
- [Function Consolidation Guide](./FUNCTION_CONSOLIDATION_GUIDE.md) - Full implementation plan
- [Function Consolidation Comparison](./FUNCTION_CONSOLIDATION_COMPARISON.md) - Visual before/after

---

**Last Updated**: 2025-01-XX  
**Author**: Development Team  
**Status**: Recommended Approach
