using System.Text.Json;
using Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpdateEngine.Core.Models;
using UpdateEngine.Core.Services;

namespace UpdateEngine.Core.Orchestrators;

/// <summary>
/// Host-agnostic sync orchestrator. Contains all sync business logic.
/// Can be used from Azure Functions, Worker Service, Console app, or any other host.
/// Supports automatic cache invalidation after successful sync operations.
/// </summary>
/// <remarks>
/// NO dependencies on:
/// - Microsoft.Azure.Functions.Worker
/// - Microsoft.AspNetCore
/// - HTTP-specific types (HttpRequestData, HttpResponseData, etc.)
/// 
/// This makes the orchestrator fully testable and reusable across hosting models.
/// </remarks>
public class SyncOrchestrator : ISyncOrchestrator
{
    private readonly ISyncService syncService;
    private readonly ILogger<SyncOrchestrator> logger;
    private readonly IOptionsMonitor<AppConfig> config;
    private readonly CacheService? cacheService;

    public SyncOrchestrator(
        ISyncService syncService,
        ILogger<SyncOrchestrator> logger,
        IOptionsMonitor<AppConfig> config,
        CacheService? cacheService = null)
    {
        this.syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.cacheService = cacheService; // Optional for backward compatibility

        // Subscribe to configuration changes
        config.OnChange(newConfig =>
        {
            this.logger.LogInformation(
                "Configuration changed: Emergency sync enabled={EmergencySync}, Scheduled sync enabled={ScheduledSync}",
                newConfig.FeatureFlags.EnableEmergencySync,
                newConfig.SyncConfiguration.EnableScheduledSync);
        });
    }

    /// <inheritdoc/>
    public async Task<SyncOperationResult> ExecuteSyncAsync(
        UnifiedSyncRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        // Get current configuration
        var currentConfig = this.config.CurrentValue;

        // Check if sync type is enabled via feature flags
        if (!this.IsSyncTypeEnabled(request.SyncType, currentConfig))
        {
            var message = $"{request.SyncType} sync is disabled via feature flags";
            this.logger.LogWarning(message);
            return new SyncOperationResult
            {
                Success = false,
                ErrorMessage = message,
                Timestamp = DateTime.UtcNow
            };
        }

        this.logger.LogInformation(
            "Executing sync operation: Type={SyncType}, Action={Action}",
            request.SyncType,
            request.Action);

        try
        {
            // Validate request
            var validationError = this.ValidateRequest(request);
            if (validationError != null)
            {
                this.logger.LogWarning("Invalid sync request: {Error}", validationError);
                return new SyncOperationResult
                {
                    Success = false,
                    ErrorMessage = validationError,
                    Timestamp = DateTime.UtcNow
                };
            }

            // Route to appropriate handler based on action
            return request.Action switch
            {
                SyncAction.Start => await this.HandleStartSyncAsync(request, cancellationToken),
                SyncAction.Pause => await this.HandlePauseSyncAsync(cancellationToken),
                SyncAction.Resume => await this.HandleResumeSyncAsync(cancellationToken),
                SyncAction.Cancel => await this.HandleCancelSyncAsync(cancellationToken),
                _ => throw new ArgumentException($"Unknown sync action: {request.Action}")
            };
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Sync operation failed with exception");
            return new SyncOperationResult
            {
                Success = false,
                ErrorMessage = $"Sync operation failed: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <inheritdoc/>
    public async Task<SyncStatusResult> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            this.logger.LogInformation("Getting sync status");

            var status = await this.syncService.GetSyncStatusAsync(cancellationToken);

            return new SyncStatusResult
            {
                IsRunning = status.IsRunning,
                CurrentSyncType = status.SyncType.HasValue ? (SyncType)status.SyncType.Value : null,
                StartTime = status.StartTime,
                Progress = status.ProgressPercentage,
                CurrentPhase = status.CurrentPhase,
                ItemsProcessed = status.ItemsProcessed,
                TotalItems = status.TotalItems,
                ErrorCount = status.ErrorCount
            };
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to get sync status");
            throw;
        }
    }

    private async Task<SyncOperationResult> HandleStartSyncAsync(
        UnifiedSyncRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.SyncType.HasValue)
        {
            return new SyncOperationResult
            {
                Success = false,
                ErrorMessage = "SyncType is required for start action",
                Timestamp = DateTime.UtcNow
            };
        }

        var startTime = DateTime.UtcNow;

        this.logger.LogInformation("Starting sync: Type={SyncType}", request.SyncType);

        try
        {
            int itemsSynced;

            switch (request.SyncType.Value)
            {
                case SyncType.Categories:
                    itemsSynced = await this.SyncCategoriesAsync(cancellationToken);
                    break;

                case SyncType.Updates:
                    if (request.Filter == null)
                    {
                        return new SyncOperationResult
                        {
                            Success = false,
                            ErrorMessage = "Filter is required for updates sync",
                            Timestamp = DateTime.UtcNow
                        };
                    }
                    itemsSynced = await this.SyncUpdatesAsync(request.Filter, cancellationToken);
                    break;

                case SyncType.Comprehensive:
                    itemsSynced = await this.SyncComprehensiveAsync(request.Filter, cancellationToken);
                    break;

                default:
                    throw new ArgumentException($"Unknown sync type: {request.SyncType}");
            }

            var duration = DateTime.UtcNow - startTime;

            // Invalidate caches after successful sync
            await this.InvalidateCachesAfterSyncAsync(request.SyncType.Value, cancellationToken);

            this.logger.LogInformation(
                "Sync completed: Type={SyncType}, Items={ItemsSynced}, Duration={Duration}",
                request.SyncType,
                itemsSynced,
                duration);

            return new SyncOperationResult
            {
                Success = true,
                Message = $"{request.SyncType} sync completed successfully",
                Timestamp = DateTime.UtcNow,
                Duration = duration,
                ItemsSynced = itemsSynced,
                Statistics = new Dictionary<string, object>
                {
                    { "syncType", request.SyncType.ToString() },
                    { "startTime", startTime },
                    { "endTime", DateTime.UtcNow }
                }
            };
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Sync failed: Type={SyncType}", request.SyncType);
            throw;
        }
    }

    /// <summary>
    /// Invalidates appropriate caches after a successful sync operation.
    /// </summary>
    private async Task InvalidateCachesAfterSyncAsync(SyncType syncType, CancellationToken cancellationToken)
    {
        if (this.cacheService == null)
        {
            return; // No cache service, skip invalidation
        }

        var currentConfig = this.config.CurrentValue;
        if (!currentConfig.CacheConfiguration.EnableDistributedCache)
        {
            return; // Caching not enabled
        }

        if (!currentConfig.CacheConfiguration.InvalidateOnSync)
        {
            this.logger.LogInformation("Cache invalidation disabled via configuration");
            return;
        }

        this.logger.LogInformation("Invalidating caches after {SyncType} sync", syncType);

        try
        {
            switch (syncType)
            {
                case SyncType.Categories:
                    // Categories sync affects metadata statistics
                    await this.cacheService.RemoveAsync("metadata:stats");
                    this.logger.LogInformation("Invalidated metadata statistics cache");
                    break;

                case SyncType.Updates:
                case SyncType.Comprehensive:
                    // Full sync affects all caches
                    await this.cacheService.InvalidateAllCachesAsync();
                    this.logger.LogInformation("Invalidated all caches after comprehensive sync");
                    break;
            }
        }
        catch (Exception ex)
        {
            // Cache invalidation failures should not fail the sync operation
            this.logger.LogWarning(ex, "Failed to invalidate caches after sync, continuing anyway");
        }
    }

    private async Task<SyncOperationResult> HandlePauseSyncAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Pausing sync operation");

        await this.syncService.PauseSyncAsync(cancellationToken);

        return new SyncOperationResult
        {
            Success = true,
            Message = "Sync operation paused successfully",
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task<SyncOperationResult> HandleResumeSyncAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Resuming sync operation");

        await this.syncService.ResumeSyncAsync(cancellationToken);

        return new SyncOperationResult
        {
            Success = true,
            Message = "Sync operation resumed successfully",
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task<SyncOperationResult> HandleCancelSyncAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Cancelling sync operation");

        await this.syncService.CancelSyncAsync(cancellationToken);

        return new SyncOperationResult
        {
            Success = true,
            Message = "Sync operation cancelled successfully",
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task<int> SyncCategoriesAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Syncing categories from upstream");

        // Call the service (which doesn't return a result yet)
        await this.syncService.SyncCategoriesAsync(cancellationToken);

        // TODO: Get actual counts from the service
        // For now, return a placeholder count
        var productCategories = 0;
        var classificationCategories = 0;
        var detectoidCategories = 0;

        this.logger.LogInformation(
            "Categories synced: Products={Products}, Classifications={Classifications}, Detectoids={Detectoids}",
            productCategories,
            classificationCategories,
            detectoidCategories);

        return productCategories + classificationCategories + detectoidCategories;
    }

    private async Task<int> SyncUpdatesAsync(SyncFilter filter, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "Syncing updates from upstream with filter: Products={Products}, Classifications={Classifications}",
            filter.ProductTitles?.Count ?? 0,
            filter.ClassificationIds?.Count ?? 0);

        // Create upstream filter
        var upstreamFilter = this.syncService.CreateCustomFilter(
            filter.ProductTitles,
            filter.ClassificationIds?.Select(g => g.ToString()).ToList());

        // Call the service
        await this.syncService.SyncUpdatesAsync(upstreamFilter, cancellationToken);

        // TODO: Get actual counts from the service
        // For now, return a placeholder count
        var softwareUpdates = 0;
        var driverUpdates = 0;

        this.logger.LogInformation(
            "Updates synced: Software={Software}, Drivers={Drivers}",
            softwareUpdates,
            driverUpdates);

        return softwareUpdates + driverUpdates;
    }

    private async Task<int> SyncComprehensiveAsync(SyncFilter? filter, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Starting comprehensive sync (categories + updates)");

        // First sync categories
        var categoriesCount = await this.SyncCategoriesAsync(cancellationToken);

        // Then sync updates (use filter if provided)
        var updatesCount = filter != null
            ? await this.SyncUpdatesAsync(filter, cancellationToken)
            : 0; // If no filter, skip updates sync (categories only)

        this.logger.LogInformation(
            "Comprehensive sync completed: Categories={Categories}, Updates={Updates}",
            categoriesCount,
            updatesCount);

        return categoriesCount + updatesCount;
    }

    private string? ValidateRequest(UnifiedSyncRequest request)
    {
        if (request.Action == SyncAction.Start && !request.SyncType.HasValue)
        {
            return "SyncType is required when action is 'start'";
        }

        if (request.SyncType == SyncType.Updates && request.Filter == null)
        {
            return "Filter is required for updates sync";
        }

        if (request.Filter != null)
        {
            if (request.Filter.ProductTitles?.Count == 0 && request.Filter.ClassificationIds?.Count == 0)
            {
                return "Filter must specify at least one product or classification";
            }
        }

        return null;
    }

    private bool IsSyncTypeEnabled(SyncType? syncType, AppConfig config)
    {
        if (!syncType.HasValue)
        {
            return true; // Status checks, pause, resume, cancel are always allowed
        }

        return syncType.Value switch
        {
            SyncType.Categories => config.FeatureFlags.EnableComprehensiveSync,
            SyncType.Updates => config.FeatureFlags.EnableComprehensiveSync,
            SyncType.Comprehensive => config.FeatureFlags.EnableComprehensiveSync,
            _ => true
        };
    }
}

/// <summary>
/// Interface for sync orchestrator operations
/// </summary>
public interface ISyncOrchestrator
{
    /// <summary>
    /// Execute a sync operation based on the request parameters
    /// </summary>
    Task<SyncOperationResult> ExecuteSyncAsync(
        UnifiedSyncRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current status of sync operations
    /// </summary>
    Task<SyncStatusResult> GetStatusAsync(
        CancellationToken cancellationToken = default);
}
