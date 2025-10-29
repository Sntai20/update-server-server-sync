# Performance Optimization Guide

## Overview

This guide covers performance optimization strategies for the Microsoft Update Server-Server Sync implementation running on Azure Functions Premium plans. The guide focuses on maximizing throughput for metadata synchronization, content downloads, and SOAP endpoint performance.

## Azure Functions Premium Plan Configuration

### Premium P3V3 Plan Specifications

The Premium P3V3 plan provides optimal performance for this workload:

- **vCPUs**: 8 cores per instance
- **Memory**: 32 GB RAM per instance
- **Maximum Instances**: 25 (configurable via `WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT`)
- **Cold Start**: Minimal due to pre-warmed instances
- **Network**: Enhanced networking capabilities

### Scaling Configuration

#### Application Settings
```json
{
  "WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT": "25",
  "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
  "FUNCTIONS_EXTENSION_VERSION": "~4",
  "WEBSITE_RUN_FROM_PACKAGE": "1"
}
```

#### host.json Configuration
```json
{
  "version": "2.0",
  "functionTimeout": "00:10:00",
  "healthMonitor": {
    "enabled": true,
    "healthCheckInterval": "00:00:10",
    "healthCheckWindow": "00:02:00",
    "healthCheckThreshold": 6,
    "counterThreshold": 0.80
  },
  "extensionBundle": {
    "id": "Microsoft.Azure.Functions.ExtensionBundle",
    "version": "[4.*, 5.0.0)"
  }
}
```

## Performance Optimization Strategies

### 1. Parallel Processing

#### UpstreamServerClient Optimization
The `UpstreamServerClient` uses parallel batch processing for metadata synchronization:

```csharp
// Optimal batch configuration for P3V3
public class UpstreamServerClient
{
    private const int OptimalBatchSize = 100;
    private const int MaxConcurrency = 8; // Match vCPU count
    
    public async Task SyncMetadataAsync(IMetadataStore store, CancellationToken cancellationToken)
    {
        var semaphore = new SemaphoreSlim(MaxConcurrency);
        var tasks = new List<Task>();
        
        await foreach (var batch in GetMetadataBatches(OptimalBatchSize))
        {
            await semaphore.WaitAsync(cancellationToken);
            
            tasks.Add(ProcessBatchAsync(batch, store, semaphore, cancellationToken));
        }
        
        await Task.WhenAll(tasks);
    }
}
```

#### Key Performance Patterns
- **Batch Size**: 100 items per batch for optimal memory usage
- **Concurrency**: Match the number of vCPUs (8 for P3V3)
- **Memory Management**: Use streaming for large datasets
- **Connection Pooling**: Leverage HttpClient connection pooling

### 2. Memory Optimization

#### Metadata Store Efficiency
```csharp
// Efficient metadata processing
public async Task<IEnumerable<Update>> ProcessUpdatesAsync(IAsyncEnumerable<Update> updates)
{
    var processedUpdates = new List<Update>();
    
    await foreach (var update in updates.ConfigureAwait(false))
    {
        // Process immediately to avoid memory accumulation
        var processedUpdate = await ProcessSingleUpdateAsync(update);
        processedUpdates.Add(processedUpdate);
        
        // Yield control periodically
        if (processedUpdates.Count % 50 == 0)
        {
            await Task.Yield();
        }
    }
    
    return processedUpdates;
}
```

#### Content Store Optimization
```csharp
// Streaming content downloads
public async Task DownloadContentAsync(string contentHash, Stream destination)
{
    using var httpClient = this.httpClientFactory.CreateClient();
    using var response = await httpClient.GetAsync(this.GetContentUrl(contentHash), HttpCompletionOption.ResponseHeadersRead);
    
    response.EnsureSuccessStatusCode();
    
    using var contentStream = await response.Content.ReadAsStreamAsync();
    await contentStream.CopyToAsync(destination, bufferSize: 81920); // 80KB buffer
}
```

### 3. Database Performance

#### Azure Storage Optimization
```csharp
// Optimized blob storage configuration
public static class StorageConfiguration
{
    public static BlobClientOptions GetOptimizedOptions()
    {
        return new BlobClientOptions
        {
            Transport = new HttpClientTransport(new HttpClient
            {
                MaxResponseHeadersLength = 64 * 1024, // 64KB
                Timeout = TimeSpan.FromMinutes(10)
            }),
            Retry = {
                MaxRetries = 3,
                Delay = TimeSpan.FromSeconds(2),
                MaxDelay = TimeSpan.FromSeconds(16),
                Mode = RetryMode.Exponential
            }
        };
    }
}
```

#### Connection String Optimization
```json
{
  "AzureWebJobsStorage": "DefaultEndpointsProtocol=https;AccountName=<name>;AccountKey=<key>;EndpointSuffix=core.windows.net"
}
```

## Timer Function Performance

### Optimal Timer Schedules

#### TimeSpan Configuration (Recommended)
```json
{
  "Values": {
    "CategorySyncInterval": "06:00:00",
    "UpdateSyncInterval": "01:00:00",
    "HealthCheckInterval": "00:05:00",
    "MetadataCleanupInterval": "1.00:00:00"
  }
}
```

#### Schedule Rationale
- **Category Sync**: Every 6 hours (categories change infrequently)
- **Update Sync**: Every 1 hour (balance between freshness and performance)
- **Health Check**: Every 5 minutes (rapid issue detection)
- **Cleanup**: Daily (maintain storage efficiency)

### Timer Function Implementation
```csharp
[Function("UpdateSync")]
public async Task RunUpdateSync(
    [TimerTrigger("%UpdateSyncInterval%")] TimerInfo timer,
    FunctionContext context)
{
    var logger = context.GetLogger("UpdateSync");
    var cancellationToken = context.CancellationToken;
    
    try
    {
        // Implement cancellation token throughout
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);
        
        await this.syncService.SyncUpdatesAsync(combinedCts.Token);
        
        logger.LogInformation("Update sync completed successfully");
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        logger.LogWarning("Update sync cancelled by host shutdown");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Update sync failed: {Message}", ex.Message);
        throw; // Let Functions runtime handle retry logic
    }
}
```

## SOAP Endpoint Performance

### Request Processing Optimization
```csharp
[Function("ClientWebService")]
public async Task<HttpResponseData> ProcessClientRequest(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
    FunctionContext context)
{
    var logger = context.GetLogger("ClientWebService");
    
    try
    {
        // Pre-allocate response stream
        using var responseStream = new MemoryStream(capacity: 64 * 1024); // 64KB initial
        
        // Process SOAP request with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var result = await this.ProcessSoapRequestAsync(req, cts.Token);
        
        // Return optimized response
        return await this.CreateOptimizedResponse(req, result);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "SOAP request processing failed");
        return await this.CreateErrorResponse(req, ex);
    }
}
```

### Response Compression
```csharp
public async Task<HttpResponseData> CreateOptimizedResponse(HttpRequestData request, string soapResponse)
{
    var response = request.CreateResponse();
    
    // Enable compression for large responses
    if (soapResponse.Length > 1024)
    {
        response.Headers.Add("Content-Encoding", "gzip");
        
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionMode.Compress))
        using (var writer = new StreamWriter(gzip, Encoding.UTF8))
        {
            await writer.WriteAsync(soapResponse);
        }
        
        await response.Body.WriteAsync(compressed.ToArray());
    }
    else
    {
        await response.WriteStringAsync(soapResponse);
    }
    
    response.Headers.Add("Content-Type", "text/xml; charset=utf-8");
    return response;
}
```

## Monitoring and Diagnostics

### Application Insights Configuration
```json
{
  "APPLICATIONINSIGHTS_CONNECTION_STRING": "InstrumentationKey=<key>;IngestionEndpoint=https://<region>.in.applicationinsights.azure.com/",
  "APPLICATIONINSIGHTS_SAMPLING_PERCENTAGE": "10"
}
```

### Custom Performance Metrics
```csharp
public class PerformanceTracker
{
    private readonly TelemetryClient telemetryClient;
    
    public async Task<T> TrackOperationAsync<T>(string operationName, Func<Task<T>> operation)
    {
        using var activity = this.telemetryClient.StartOperation<RequestTelemetry>(operationName);
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await operation();
            
            activity.Telemetry.Success = true;
            activity.Telemetry.Duration = stopwatch.Elapsed;
            
            // Track custom metrics
            this.telemetryClient.TrackMetric($"{operationName}.Duration", stopwatch.ElapsedMilliseconds);
            this.telemetryClient.TrackMetric($"{operationName}.Success", 1);
            
            return result;
        }
        catch (Exception ex)
        {
            activity.Telemetry.Success = false;
            this.telemetryClient.TrackException(ex);
            this.telemetryClient.TrackMetric($"{operationName}.Failure", 1);
            throw;
        }
    }
}
```

## Performance Benchmarks

### Expected Throughput (Premium P3V3)

| Operation | Throughput | Latency (P95) | Resource Usage |
|-----------|------------|---------------|----------------|
| Metadata Sync | 1,000 updates/min | < 2s | 60% CPU, 8GB RAM |
| Category Sync | 100 categories/min | < 1s | 20% CPU, 2GB RAM |
| SOAP Requests | 50 req/sec | < 500ms | 40% CPU, 4GB RAM |
| Content Download | 100 MB/min | < 5s | 30% CPU, 2GB RAM |

### Scaling Behavior
- **Scale Out Trigger**: CPU > 70% for 2+ minutes
- **Scale In Trigger**: CPU < 30% for 10+ minutes
- **Instance Startup**: < 30 seconds (pre-warmed)
- **Max Concurrent**: 25 instances × 50 req/sec = 1,250 req/sec

## Best Practices Summary

### Configuration
1. **Use Premium P3V3** for production workloads
2. **Set appropriate scale limits** (`WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT`)
3. **Configure health monitoring** in host.json
4. **Use TimeSpan schedules** instead of CRON expressions

### Code Optimization
1. **Implement parallel processing** with semaphores
2. **Use streaming** for large data transfers
3. **Apply cancellation tokens** throughout
4. **Enable response compression** for SOAP endpoints

### Monitoring
1. **Track custom performance metrics**
2. **Set up Application Insights** with sampling
3. **Monitor scaling behavior**
4. **Alert on performance degradation**

### Resource Management
1. **Use connection pooling** for HTTP clients
2. **Dispose resources properly** with `using` statements
3. **Implement circuit breakers** for external dependencies
4. **Cache frequently accessed data** appropriately

## Troubleshooting Performance Issues

### Common Issues
1. **High Memory Usage**: Check for memory leaks in streaming operations
2. **Slow SOAP Responses**: Verify database connection pooling
3. **Timer Overlap**: Ensure functions complete before next trigger
4. **Cold Starts**: Use Premium plan with pre-warmed instances

### Diagnostic Commands
```bash
# Check function execution metrics
func azure functionapp logstream <function-app-name>

# Monitor Application Insights
az monitor app-insights query --app <app-name> --analytics-query "requests | where timestamp > ago(1h)"

# Check scaling events
az monitor autoscale show --resource-group <rg> --name <function-app>
```

## Performance Testing

### Load Testing Strategy
```csharp
[Test]
public async Task LoadTest_SoapEndpoint_CanHandle50ConcurrentRequests()
{
    var tasks = new List<Task>();
    var client = new HttpClient();
    
    for (int i = 0; i < 50; i++)
    {
        tasks.Add(SendSoapRequestAsync(client, i));
    }
    
    var results = await Task.WhenAll(tasks);
    
    // Assert all requests completed successfully
    Assert.That(results.All(r => r.IsSuccessStatusCode), Is.True);
}
```

### Continuous Performance Monitoring
```yaml
# Azure DevOps pipeline step
- task: AzureLoadTest@1
  inputs:
    azureSubscription: 'Production'
    loadTestConfigFile: 'tests/load-test-config.yaml'
    resourceGroup: 'rg-msupdate-prod'
```

---

**Last Updated**: October 28, 2025  
**Target Platform**: Azure Functions Premium P3V3  
**Framework**: .NET 9.0 with isolated worker model