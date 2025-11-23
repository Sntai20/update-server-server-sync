using Configuration;
using UpdateEngine.Core;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add shared configuration from Configuration project
builder.Configuration.AddSharedAppConfiguration();

// Add service defaults (Aspire telemetry, health checks, resilience)
builder.AddServiceDefaults();

// Add Redis distributed cache using connection string from Aspire
// This registers IDistributedCache which is required by CacheService
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "updateengine:";
    });
}

// Add UpdateEngine core services (orchestrators, stores, health checks, caching)
// This single line registers all shared services
builder.Services.AddUpdateEngineCore(builder.Configuration);

// Add ASP.NET Core controllers for HTTP APIs
builder.Services.AddControllers();

// Add background workers
builder.Services.AddHostedService<Microsoft.UpdateServices.WorkerService.Workers.SyncWorker>();
builder.Services.AddHostedService<Microsoft.UpdateServices.WorkerService.Workers.HealthCheckWorker>();

// Add OpenAPI/Swagger for development
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "UpdateEngine Worker Service API",
        Version = "v1",
        Description = "ASP.NET Core Worker Service for Microsoft Update Server-Server Sync"
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "UpdateEngine Worker Service v1");
    });
}

// Map default Aspire endpoints (health, metrics, etc.)
app.MapDefaultEndpoints();

// Map ASP.NET Core health check endpoints (Kubernetes/Docker compatible)
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("critical")
});
app.MapHealthChecks("/health/ready");

// Map controllers
app.MapControllers();

app.Run();

// Make Program class accessible to WebApplicationFactory for testing
public partial class Program { }
