using Microsoft.Extensions.Options;
using Configuration;
using UpdateEngine.Core.Models;
using UpdateEngine.Core.Orchestrators;

namespace Microsoft.UpdateServices.WorkerService.Workers;

/// <summary>
/// Background service that executes scheduled sync operations.
/// Uses IOptionsMonitor for hot-reload support - can react to configuration changes without restart.
/// </summary>
public class SyncWorker : BackgroundService
{
    private readonly ISyncOrchestrator orchestrator;
    private readonly IOptionsMonitor<AppConfig> config;
    private readonly ILogger<SyncWorker> logger;
    private readonly IDisposable? configChangeListener;
    private TimeSpan currentInterval;

    public SyncWorker(
        ISyncOrchestrator orchestrator,
        IOptionsMonitor<AppConfig> config,
        ILogger<SyncWorker> logger)
    {
        this.orchestrator = orchestrator;
        this.config = config;
        this.logger = logger;
        this.currentInterval = TimeSpan.FromMinutes(config.CurrentValue.SyncConfiguration.SyncIntervalMinutes);

        // Listen for configuration changes
        this.configChangeListener = config.OnChange(this.OnConfigurationChanged);

        this.logger.LogInformation(
            "SyncWorker initialized with interval: {Interval} minutes",
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
                    this.logger.LogInformation("Executing scheduled sync");
                    await this.ExecuteSyncAsync(stoppingToken);
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

    private async Task ExecuteSyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            this.logger.LogInformation("Starting comprehensive sync");

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
            this.logger.LogError(ex, "Error executing sync operation");
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
