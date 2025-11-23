using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using UpdateEngine.Configuration;

namespace UpdateEngine.WorkerService.Workers;

/// <summary>
/// Background service that performs periodic health checks and logs results.
/// Uses IOptionsMonitor for hot-reload support - can react to configuration changes without restart.
/// </summary>
public class HealthCheckWorker : BackgroundService
{
    private readonly HealthCheckService healthCheckService;
    private readonly IOptionsMonitor<AppConfig> config;
    private readonly ILogger<HealthCheckWorker> logger;
    private readonly IDisposable? configChangeListener;
    private TimeSpan currentInterval;

    public HealthCheckWorker(
        HealthCheckService healthCheckService,
        IOptionsMonitor<AppConfig> config,
        ILogger<HealthCheckWorker> logger)
    {
        this.healthCheckService = healthCheckService;
        this.config = config;
        this.logger = logger;

        // Use health check interval from configuration (default 5 minutes)
        this.currentInterval = TimeSpan.FromMinutes(config.CurrentValue.SyncConfiguration.HealthCheckIntervalMinutes);

        // Listen for configuration changes
        this.configChangeListener = config.OnChange(this.OnConfigurationChanged);

        this.logger.LogInformation(
            "HealthCheckWorker initialized with interval: {Interval} minutes",
            this.currentInterval.TotalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.logger.LogInformation("HealthCheckWorker starting");

        // Wait for a short delay on startup to allow services to initialize
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                this.logger.LogInformation("Executing periodic health check");
                await this.ExecuteHealthCheckAsync(stoppingToken);

                // Wait for the configured interval before next check
                await Task.Delay(this.currentInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                this.logger.LogInformation("HealthCheckWorker stopping due to cancellation");
                break;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error in HealthCheckWorker execution");

                // Wait before retrying after error
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        this.logger.LogInformation("HealthCheckWorker stopped");
    }

    private async Task ExecuteHealthCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Execute comprehensive health check
            var healthReport = await this.healthCheckService.CheckHealthAsync(cancellationToken);

            // Log overall status
            this.logger.LogInformation(
                "Health check completed: Status={Status}, TotalDuration={Duration}ms",
                healthReport.Status,
                healthReport.TotalDuration.TotalMilliseconds);

            // Log individual check results
            foreach (var entry in healthReport.Entries)
            {
                var logLevel = entry.Value.Status switch
                {
                    HealthStatus.Healthy => LogLevel.Information,
                    HealthStatus.Degraded => LogLevel.Warning,
                    HealthStatus.Unhealthy => LogLevel.Error,
                    _ => LogLevel.Information
                };

                this.logger.Log(
                    logLevel,
                    "Health check '{CheckName}': Status={Status}, Duration={Duration}ms, Description={Description}",
                    entry.Key,
                    entry.Value.Status,
                    entry.Value.Duration.TotalMilliseconds,
                    entry.Value.Description ?? "N/A");

                // Log exception details if present
                if (entry.Value.Exception != null)
                {
                    this.logger.LogError(
                        entry.Value.Exception,
                        "Health check '{CheckName}' failed with exception",
                        entry.Key);
                }

                // Log data details if present
                if (entry.Value.Data.Any())
                {
                    foreach (var data in entry.Value.Data)
                    {
                        this.logger.LogDebug(
                            "Health check '{CheckName}' data: {Key}={Value}",
                            entry.Key,
                            data.Key,
                            data.Value);
                    }
                }
            }

            // Alert if system is unhealthy
            if (healthReport.Status == HealthStatus.Unhealthy)
            {
                var unhealthyChecks = healthReport.Entries
                    .Where(e => e.Value.Status == HealthStatus.Unhealthy)
                    .Select(e => e.Key);

                this.logger.LogError(
                    "System is unhealthy. Failed checks: {FailedChecks}",
                    string.Join(", ", unhealthyChecks));
            }

            // Warn if system is degraded
            if (healthReport.Status == HealthStatus.Degraded)
            {
                var degradedChecks = healthReport.Entries
                    .Where(e => e.Value.Status == HealthStatus.Degraded)
                    .Select(e => e.Key);

                this.logger.LogWarning(
                    "System is degraded. Degraded checks: {DegradedChecks}",
                    string.Join(", ", degradedChecks));
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error executing health check");
        }
    }

    private void OnConfigurationChanged(AppConfig newConfig, string? name)
    {
        var newInterval = TimeSpan.FromMinutes(newConfig.SyncConfiguration.HealthCheckIntervalMinutes);

        if (newInterval != this.currentInterval)
        {
            this.logger.LogInformation(
                "Configuration changed: Health check interval updated from {OldInterval} to {NewInterval} minutes",
                this.currentInterval.TotalMinutes,
                newInterval.TotalMinutes);

            this.currentInterval = newInterval;
        }
    }

    public override void Dispose()
    {
        this.configChangeListener?.Dispose();
        base.Dispose();
    }
}
