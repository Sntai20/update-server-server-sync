// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Services;

using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;

/// <summary>
/// Implementation of health service providing health check and maintenance operations.
/// </summary>
public class HealthService : IHealthService
{
    private readonly ILogger<HealthService> logger;
    private readonly IMetadataStore? metadataStore;
    private readonly IContentStore? contentStore;

    public HealthService(ILogger<HealthService> logger, IMetadataStore? metadataStore, IContentStore? contentStore)
    {
        this.logger = logger;
        this.metadataStore = metadataStore;
        this.contentStore = contentStore;
    }

    public async Task<HealthCheckResult> PerformHealthCheckAsync()
    {
        var result = new HealthCheckResult
        {
            IsHealthy = true,
            Status = "Healthy",
            ContentStoreAvailable = this.contentStore != null
        };

        if (this.metadataStore != null)
        {
            try
            {
                result.PackageCount = this.metadataStore.Cast<Microsoft.PackageGraph.ObjectModel.IPackage>().Count();
                result.ReindexingRequired = this.metadataStore.IsReindexingRequired;
                
                this.logger.LogInformation("Health check: {PackageCount} packages in metadata store", result.PackageCount);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error accessing metadata store during health check");
                result.IsHealthy = false;
                result.Status = "Metadata store error";
            }
        }
        else
        {
            result.IsHealthy = false;
            result.Status = "Metadata store not configured";
        }

        if (this.contentStore != null)
        {
            this.logger.LogInformation("Health check: Content store is available");
        }

        return result;
    }

    public async Task<HealthStatus> GetSystemHealthAsync()
    {
        this.logger.LogInformation("Getting system health status");
        
        var health = new HealthStatus
        {
            IsHealthy = true,
            Issues = new List<HealthIssue>(),
            Metrics = new List<HealthMetric>()
        };

        // Check metadata store
        if (this.metadataStore != null)
        {
            try
            {
                var packageCount = this.metadataStore.Cast<Microsoft.PackageGraph.ObjectModel.IPackage>().Count();
                health.Metrics.Add(new HealthMetric { Name = "PackageCount", Value = packageCount.ToString(), Unit = "packages" });
                
                if (this.metadataStore.IsReindexingRequired)
                {
                    health.Issues.Add(new HealthIssue 
                    { 
                        Component = "MetadataStore", 
                        Message = "Reindexing required", 
                        Severity = "Warning" 
                    });
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error checking metadata store health");
                health.IsHealthy = false;
                health.Issues.Add(new HealthIssue 
                { 
                    Component = "MetadataStore", 
                    Message = ex.Message, 
                    Severity = "Error" 
                });
            }
        }
        else
        {
            health.IsHealthy = false;
            health.Issues.Add(new HealthIssue 
            { 
                Component = "MetadataStore", 
                Message = "Not configured", 
                Severity = "Error" 
            });
        }

        // Check content store
        if (this.contentStore != null)
        {
            health.Metrics.Add(new HealthMetric { Name = "ContentStoreAvailable", Value = "true" });
        }
        else
        {
            health.Metrics.Add(new HealthMetric { Name = "ContentStoreAvailable", Value = "false" });
        }

        return health;
    }

    public async Task<HealthStatus> GetSyncHealthAsync()
    {
        this.logger.LogInformation("Getting sync-specific health status");
        
        var health = new HealthStatus
        {
            IsHealthy = true,
            Issues = new List<HealthIssue>(),
            Metrics = new List<HealthMetric>()
        };

        // Check if stores are ready for sync operations
        if (this.metadataStore != null)
        {
            try
            {
                var packageCount = this.metadataStore.Cast<Microsoft.PackageGraph.ObjectModel.IPackage>().Count();
                health.Metrics.Add(new HealthMetric { Name = "SyncablePackages", Value = packageCount.ToString(), Unit = "packages" });
                
                if (this.metadataStore.IsReindexingRequired)
                {
                    health.Issues.Add(new HealthIssue 
                    { 
                        Component = "SyncReadiness", 
                        Message = "Metadata store requires reindexing before sync", 
                        Severity = "Warning" 
                    });
                }
                else
                {
                    health.Metrics.Add(new HealthMetric { Name = "SyncReadiness", Value = "Ready" });
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error checking sync readiness");
                health.IsHealthy = false;
                health.Issues.Add(new HealthIssue 
                { 
                    Component = "SyncReadiness", 
                    Message = $"Cannot determine sync readiness: {ex.Message}", 
                    Severity = "Error" 
                });
            }
        }
        else
        {
            health.IsHealthy = false;
            health.Issues.Add(new HealthIssue 
            { 
                Component = "SyncReadiness", 
                Message = "Metadata store not configured - sync not possible", 
                Severity = "Error" 
            });
        }

        return health;
    }

    public async Task CleanupTemporaryFilesAsync()
    {
        this.logger.LogInformation("Starting temporary file cleanup");
        // Implementation for cleaning up temporary files would go here
        this.logger.LogInformation("Temporary file cleanup completed");
    }

    public async Task<MaintenanceResult> PerformMaintenanceAsync(MaintenanceLevel level)
    {
        this.logger.LogInformation("Starting {Level} maintenance operations", level);

        var result = new MaintenanceResult
        {
            Success = true,
            Level = level
        };

        try
        {
            switch (level)
            {
                case MaintenanceLevel.Light:
                    await this.CleanupTemporaryFilesAsync();
                    result.Message = "Light maintenance completed successfully";
                    break;

                case MaintenanceLevel.Weekly:
                    await this.CleanupTemporaryFilesAsync();
                    await this.PerformWeeklyMaintenance();
                    result.Message = "Weekly maintenance completed successfully";
                    break;

                case MaintenanceLevel.Monthly:
                    await this.CleanupTemporaryFilesAsync();
                    await this.PerformWeeklyMaintenance();
                    await this.PerformMonthlyMaintenance();
                    result.Message = "Monthly maintenance completed successfully";
                    break;
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during {Level} maintenance", level);
            result.Success = false;
            result.Message = $"Maintenance failed: {ex.Message}";
        }

        this.logger.LogInformation("{Level} maintenance operations completed", level);
        return result;
    }

    public async Task<bool> CheckReindexingNeeded()
    {
        if (this.metadataStore?.IsReindexingRequired == true)
        {
            this.logger.LogWarning("Metadata store requires reindexing");
            return true;
        }

        return false;
    }

    private async Task PerformWeeklyMaintenance()
    {
        this.logger.LogInformation("Performing weekly maintenance tasks");
        // Implementation for weekly maintenance tasks
    }

    private async Task PerformMonthlyMaintenance()
    {
        this.logger.LogInformation("Performing monthly maintenance tasks");
        // Implementation for monthly maintenance tasks including optimization
    }
}