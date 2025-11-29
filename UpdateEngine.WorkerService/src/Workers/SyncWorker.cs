using Microsoft.Extensions.Options;
using UpdateEngine.Configuration;
using UpdateEngine.Core.Models;
using UpdateEngine.Core.Orchestrators;
using UpdateEngine.Core.Services;

namespace UpdateEngine.WorkerService.Workers;

/// <summary>
/// Background service that executes scheduled sync operations.
/// Uses IOptionsMonitor for hot-reload support - can react to configuration changes without restart.
/// Supports both upstream (Microsoft Update) and downstream (Functions) sync modes.
/// </summary>
public class SyncWorker : BackgroundService
{
    private readonly ISyncOrchestrator orchestrator;
    private readonly IDownstreamSyncService downstreamSyncService;
    private readonly IOptionsMonitor<AppConfig> config;
    private readonly ILogger<SyncWorker> logger;
    private readonly IDisposable? configChangeListener;
    private TimeSpan currentInterval;

    public SyncWorker(
        ISyncOrchestrator orchestrator,
        IDownstreamSyncService downstreamSyncService,
        IOptionsMonitor<AppConfig> config,
        ILogger<SyncWorker> logger)
    {
        this.orchestrator = orchestrator;
        this.downstreamSyncService = downstreamSyncService;
        this.config = config;
        this.logger = logger;
        this.currentInterval = TimeSpan.FromMinutes(config.CurrentValue.SyncConfiguration.SyncIntervalMinutes);

        // Listen for configuration changes
        this.configChangeListener = config.OnChange(this.OnConfigurationChanged);

        var downstreamMode = config.CurrentValue.DownstreamConfiguration.SyncFromUpstream ? "DOWNSTREAM (from Functions)" : "UPSTREAM (from Microsoft Update)";
        this.logger.LogInformation(
            "SyncWorker initialized - Mode: {Mode}, Interval: {Interval} minutes",
            downstreamMode,
            this.currentInterval.TotalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.logger.LogInformation("SyncWorker starting");

        // Wait for a short delay on startup to allow services to initialize
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var currentConfig = this.config.CurrentValue;

                if (currentConfig.SyncConfiguration.EnableScheduledSync)
                {
                    // Check if downstream sync is enabled
                    if (currentConfig.DownstreamConfiguration.SyncFromUpstream)
                    {
                        this.logger.LogInformation("Executing scheduled DOWNSTREAM sync from Functions");
                        await this.ExecuteDownstreamSyncAsync(stoppingToken);
                    }
                    else
                    {
                        this.logger.LogInformation("Executing scheduled UPSTREAM sync from Microsoft Update");
                        await this.ExecuteUpstreamSyncAsync(stoppingToken);
                    }
                }
                else
                {
                    this.logger.LogDebug("Scheduled sync is disabled via configuration");
                }

                // Wait for the configured interval before next sync
                await Task.Delay(this.currentInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                this.logger.LogInformation("SyncWorker stopping due to cancellation");
                break;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error in SyncWorker execution");

                // Wait before retrying after error
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        this.logger.LogInformation("SyncWorker stopped");
    }

    /// <summary>
    /// Executes downstream sync - pulls metadata and content from upstream Functions.
    /// </summary>
    private async Task ExecuteDownstreamSyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            var currentConfig = this.config.CurrentValue;
            
            this.logger.LogInformation("Starting downstream metadata sync from Functions");
            
            // Create filter from configuration (reuse existing filter logic)
            var filter = new ServiceMetadataFilter
            {
                ProductFilters = currentConfig.ServiceConfiguration.SupportedCategories?.ToList(),
                // Add more filters as needed
            };

            // Sync metadata from Functions
            var result = await this.downstreamSyncService.SyncMetadataFromUpstreamAsync(filter, cancellationToken);
            
            if (result.Success)
            {
                this.logger.LogInformation(
                    "Downstream metadata sync completed: {ItemsProcessed} items processed",
                    result.ItemsProcessed);
            }
            else
            {
                this.logger.LogWarning(
                    "Downstream metadata sync failed: {ErrorMessage}",
                    result.ErrorMessage);
                return; // Don't proceed to content sync if metadata sync failed
            }

            // Sync content if enabled
            if (currentConfig.DownstreamConfiguration.EnableContentSync)
            {
                this.logger.LogInformation("Starting downstream content sync from Functions");
                await this.downstreamSyncService.SyncContentFromUpstreamAsync(filter, cancellationToken);
                this.logger.LogInformation("Downstream content sync completed");
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error executing downstream sync operation");
        }
    }

    /// <summary>
    /// Executes upstream sync - pulls directly from Microsoft Update (original behavior).
    /// </summary>
    private async Task ExecuteUpstreamSyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            this.logger.LogInformation("Starting comprehensive sync from Microsoft Update");

            var comprehensiveRequest = new UnifiedSyncRequest
            {
                SyncType = SyncType.Comprehensive,
                Action = SyncAction.Start
            };

            var result = await this.orchestrator.ExecuteSyncAsync(comprehensiveRequest, cancellationToken);

            if (result.Success)
            {
                this.logger.LogInformation(
                    "Comprehensive sync completed: {UpdatesProcessed} items processed",
                    result.ItemsSynced);
            }
            else
            {
                this.logger.LogWarning(
                    "Comprehensive sync failed: {ErrorMessage}",
                    result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error executing upstream sync operation");
        }
    }

    private void OnConfigurationChanged(AppConfig newConfig, string? name)
    {
        var newInterval = TimeSpan.FromMinutes(newConfig.SyncConfiguration.SyncIntervalMinutes);

        if (newInterval != this.currentInterval)
        {
            this.logger.LogInformation(
                "Configuration changed: Sync interval updated from {OldInterval} to {NewInterval} minutes",
                this.currentInterval.TotalMinutes,
                newInterval.TotalMinutes);

            this.currentInterval = newInterval;
        }

        var downstreamMode = newConfig.DownstreamConfiguration.SyncFromUpstream ? "DOWNSTREAM (from Functions)" : "UPSTREAM (from Microsoft Update)";
        this.logger.LogInformation("Sync mode: {Mode}", downstreamMode);

        if (newConfig.SyncConfiguration.EnableScheduledSync)
        {
            this.logger.LogInformation("Scheduled sync enabled via configuration");
        }
        else
        {
            this.logger.LogInformation("Scheduled sync disabled via configuration");
        }
    }

    public override void Dispose()
    {
        this.configChangeListener?.Dispose();
        base.Dispose();
    }
}
