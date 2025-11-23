using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UpdateEngine.Configuration;
using UpdateEngine.Core.Models;
using UpdateEngine.Core.Orchestrators;

namespace UpdateEngine.WorkerService.Controllers;

/// <summary>
/// ASP.NET Core controller for sync operations.
/// Thin adapter over ISyncOrchestrator (host-agnostic business logic).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly ISyncOrchestrator orchestrator;
    private readonly IOptionsSnapshot<AppConfig> config;
    private readonly ILogger<SyncController> logger;

    public SyncController(
        ISyncOrchestrator orchestrator,
        IOptionsSnapshot<AppConfig> config,
        ILogger<SyncController> logger)
    {
        this.orchestrator = orchestrator;
        this.config = config;
        this.logger = logger;
    }

    /// <summary>
    /// Start a sync operation.
    /// POST /api/sync
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SyncOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SyncOperationResult), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SyncOperationResult>> StartSync(
        [FromBody] UnifiedSyncRequest request,
        CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation(
            "Starting sync: Type={SyncType}, Action={Action}",
            request.SyncType,
            request.Action);

        var result = await this.orchestrator.ExecuteSyncAsync(request, cancellationToken);

        if (result.Success)
        {
            return this.Ok(result);
        }

        this.logger.LogWarning(
            "Sync failed: {ErrorMessage}",
            result.ErrorMessage);

        return this.BadRequest(result);
    }

    /// <summary>
    /// Get sync status.
    /// GET /api/sync/status
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(SyncStatusResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<SyncStatusResult>> GetStatus(
        CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Getting sync status");

        var status = await this.orchestrator.GetStatusAsync(cancellationToken);
        return this.Ok(status);
    }
}
