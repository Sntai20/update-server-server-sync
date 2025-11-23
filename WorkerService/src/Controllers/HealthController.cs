using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Configuration;

namespace Microsoft.UpdateServices.WorkerService.Controllers;

/// <summary>
/// ASP.NET Core controller for health check operations.
/// Provides programmatic access to health check results.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly HealthCheckService healthCheckService;
    private readonly IOptionsSnapshot<AppConfig> config;
    private readonly ILogger<HealthController> logger;

    public HealthController(
        HealthCheckService healthCheckService,
        IOptionsSnapshot<AppConfig> config,
        ILogger<HealthController> logger)
    {
        this.healthCheckService = healthCheckService;
        this.config = config;
        this.logger = logger;
    }

    /// <summary>
    /// Get comprehensive health status.
    /// GET /api/health
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthReport), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthReport>> GetHealth(
        CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Executing comprehensive health check");

        var healthReport = await this.healthCheckService.CheckHealthAsync(cancellationToken);

        var statusCode = healthReport.Status == HealthStatus.Healthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        return this.StatusCode(statusCode, healthReport);
    }

    /// <summary>
    /// Get liveness probe status (critical checks only).
    /// GET /api/health/live
    /// </summary>
    [HttpGet("live")]
    [ProducesResponseType(typeof(HealthReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthReport), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthReport>> GetLiveness(
        CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Executing liveness health check");

        var healthReport = await this.healthCheckService.CheckHealthAsync(
            check => check.Tags.Contains("critical"),
            cancellationToken);

        var statusCode = healthReport.Status == HealthStatus.Healthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        return this.StatusCode(statusCode, healthReport);
    }

    /// <summary>
    /// Get readiness probe status (all checks).
    /// GET /api/health/ready
    /// </summary>
    [HttpGet("ready")]
    [ProducesResponseType(typeof(HealthReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthReport), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthReport>> GetReadiness(
        CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Executing readiness health check");

        var healthReport = await this.healthCheckService.CheckHealthAsync(cancellationToken);

        // For readiness, we can tolerate degraded status
        var statusCode = healthReport.Status == HealthStatus.Unhealthy
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK;

        return this.StatusCode(statusCode, healthReport);
    }

    /// <summary>
    /// Get health status by tag.
    /// GET /api/health/tags/{tag}
    /// </summary>
    [HttpGet("tags/{tag}")]
    [ProducesResponseType(typeof(HealthReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthReport), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthReport>> GetHealthByTag(
        string tag,
        CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Executing health check for tag: {Tag}", tag);

        var healthReport = await this.healthCheckService.CheckHealthAsync(
            check => check.Tags.Contains(tag),
            cancellationToken);

        var statusCode = healthReport.Status == HealthStatus.Healthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        return this.StatusCode(statusCode, healthReport);
    }
}
