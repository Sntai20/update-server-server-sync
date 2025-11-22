# Configuration & Health Checks: Quick Reference

## ?? Configuration Pattern

**Use `IOptionsMonitor<AppConfig>` for hot-reload support**

### Registration
```csharp
// ServiceCollectionExtensions.cs
services.Configure<AppConfig>(
    configuration.GetSection(AppConfig.SectionName));
```

### Usage
```csharp
// For hot-reload (singleton services)
public SyncOrchestrator(IOptionsMonitor<AppConfig> config)
{
    var current = config.CurrentValue; // Always up-to-date
    config.OnChange(newConfig => { /* react */ });
}

// For per-request (scoped services)
public SyncController(IOptionsSnapshot<AppConfig> config)
{
    var current = config.Value; // Updated per request
}

// For startup-only (stores)
public StoreFactory(IOptions<AppConfig> config)
{
    var current = config.Value; // Fixed at startup
}
```

### appsettings.json
```json
{
  "UpdateEngine": {
    "SyncConfiguration": {
      "SyncIntervalMinutes": 60,
      "EnableScheduledSync": true
    },
    "FeatureFlags": {
      "EnableEmergencySync": true
    }
  }
}
```

---

## ?? Health Checks

**Use ASP.NET Core Health Checks (industry standard)**

### Registration
```csharp
services.AddHealthChecks()
    .AddCheck<MetadataStoreHealthCheck>("metadata-store", 
        tags: new[] { "storage", "critical" })
    .AddCheck<UpstreamConnectionHealthCheck>("upstream", 
        tags: new[] { "network" });
```

### Azure Functions Endpoint
```csharp
[Function("Health")]
public async Task<HttpResponseData> GetHealth(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
{
    var report = await healthCheckService.CheckHealthAsync();
    return CreateHealthResponse(report);
}
```

### Worker Service Endpoints
```csharp
// Comprehensive health
app.MapHealthChecks("/health");

// Kubernetes liveness probe
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("critical")
});

// Kubernetes readiness probe
app.MapHealthChecks("/health/ready");
```

### Pre-Operation Validation
```csharp
public async Task<SyncOperationResult> ExecuteSyncAsync(request)
{
    // Check health before critical operations
    var health = await healthCheckService.CheckHealthAsync(
        predicate: check => check.Tags.Contains("critical"));
    
    if (health.Status != HealthStatus.Healthy)
        return CreateErrorResult("System unhealthy");
    
    // Proceed...
}
```

---

## ?? Quick Comparison

| Pattern | Configuration | Health Checks |
|---------|--------------|---------------|
| **Standard** | .NET Options Pattern | ASP.NET Core Health Checks |
| **Hot-Reload** | ? IOptionsMonitor | N/A |
| **Kubernetes** | N/A | ? /health/live, /health/ready |
| **Monitoring** | N/A | ? Prometheus, Grafana compatible |
| **Testing** | ? Easy to mock | ? Easy to test |

---

## ?? Key Benefits

### Configuration
- ? Hot-reload without restart
- ? Thread-safe (immutable)
- ? Industry standard
- ? Easy testing

### Health Checks
- ? Kubernetes/Docker ready
- ? Pre-operation validation
- ? Monitoring tool compatible
- ? Extensible

---

?? **[Full Documentation ?](./ARCHITECTURE_DECISIONS.md)**
