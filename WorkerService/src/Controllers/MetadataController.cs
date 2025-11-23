using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.PackageGraph.ObjectModel;
using Configuration;
using UpdateEngine.Core.Orchestrators;

namespace Microsoft.UpdateServices.WorkerService.Controllers;

/// <summary>
/// ASP.NET Core controller for metadata operations.
/// Thin adapter over IMetadataOrchestrator (host-agnostic business logic).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MetadataController : ControllerBase
{
    private readonly IMetadataOrchestrator orchestrator;
    private readonly IOptionsSnapshot<AppConfig> config;
    private readonly ILogger<MetadataController> logger;

    public MetadataController(
        IMetadataOrchestrator orchestrator,
        IOptionsSnapshot<AppConfig> config,
        ILogger<MetadataController> logger)
    {
        this.orchestrator = orchestrator;
        this.config = config;
        this.logger = logger;
    }

    /// <summary>
    /// Get metadata store statistics.
    /// GET /api/metadata/statistics
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(MetadataStatistics), StatusCodes.Status200OK)]
    public async Task<ActionResult<MetadataStatistics>> GetStatistics(
        CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Getting metadata statistics");

        var statistics = await this.orchestrator.GetStatisticsAsync(cancellationToken);
        return this.Ok(statistics);
    }

    /// <summary>
    /// Query updates by filter.
    /// POST /api/metadata/query
    /// </summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IReadOnlyList<IPackageIdentity>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<IPackageIdentity>>> QueryUpdates(
        [FromBody] MetadataQuery query,
        CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation(
            "Querying updates: Products={Products}, Classifications={Classifications}",
            query.Products?.Count ?? 0,
            query.Classifications?.Count ?? 0);

        var results = await this.orchestrator.QueryUpdatesAsync(query, cancellationToken);
        return this.Ok(results);
    }

    /// <summary>
    /// Get index status.
    /// GET /api/metadata/index/status
    /// </summary>
    [HttpGet("index/status")]
    [ProducesResponseType(typeof(IndexStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<IndexStatus>> GetIndexStatus(
        CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Getting index status");

        var status = await this.orchestrator.GetIndexStatusAsync(cancellationToken);
        return this.Ok(status);
    }

    /// <summary>
    /// Reindex metadata store.
    /// POST /api/metadata/index/reindex
    /// </summary>
    [HttpPost("index/reindex")]
    [ProducesResponseType(typeof(ReindexResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReindexResult>> Reindex(
        CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Starting metadata reindex");

        var result = await this.orchestrator.ReindexAsync(cancellationToken: cancellationToken);
        return this.Ok(result);
    }
}
