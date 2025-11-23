// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateCli.Commands;

using System.Text.Json;
using Microsoft.PackageGraph.MicrosoftUpdate.Metadata;
using Microsoft.PackageGraph.ObjectModel;
using Microsoft.PackageGraph.Storage;
using UpdateCli.Services;
using UpdateEngine.Core.Models;
using UpdateEngine.Core.Orchestrators;
using UpdateEngine.Core.Services;

/// <summary>
/// Command handlers for the UpdateEngine CLI.
/// Now supports both direct orchestrator access (local mode) and HTTP client (remote mode).
/// </summary>
public class CommandHandlers
{
    private readonly UpdateEngineClient? updateEngineClient;
    private readonly ISyncOrchestrator? syncOrchestrator;
    private readonly IMetadataOrchestrator? metadataOrchestrator;
    private readonly IContentOrchestrator? contentOrchestrator;
    private readonly IHealthService? healthService;
    private readonly IMetadataStore? metadataStore;
    private readonly Server2022DownloadHandler? server2022Handler;
    private readonly Server2025DownloadHandler? server2025Handler;
    private readonly Windows11DownloadHandler? windows11Handler;
    private readonly bool useOrchestrators;

    /// <summary>
    /// Constructor for local mode (uses orchestrators directly).
    /// </summary>
    public CommandHandlers(
        ISyncOrchestrator syncOrchestrator,
        IMetadataOrchestrator metadataOrchestrator,
        IContentOrchestrator contentOrchestrator,
        IHealthService healthService,
        IMetadataStore metadataStore)
    {
        this.syncOrchestrator = syncOrchestrator;
        this.metadataOrchestrator = metadataOrchestrator;
        this.contentOrchestrator = contentOrchestrator;
        this.healthService = healthService;
        this.metadataStore = metadataStore;
        this.useOrchestrators = true;
    }

    /// <summary>
    /// Constructor for remote mode (uses HTTP client).
    /// </summary>
    public CommandHandlers(UpdateEngineClient updateEngineClient)
    {
        this.updateEngineClient = updateEngineClient;
        this.server2022Handler = new Server2022DownloadHandler(updateEngineClient);
        this.server2025Handler = new Server2025DownloadHandler(updateEngineClient);
        this.windows11Handler = new Windows11DownloadHandler(updateEngineClient);
        this.useOrchestrators = false;
    }

    /// <summary>
    /// Handles the health check command with scope support.
    /// </summary>
    public async Task<int> HandleHealthAsync(string scope = "basic")
    {
        try
        {
            Console.WriteLine($"Checking UpdateEngine health (scope: {scope})...");
            
            if (this.useOrchestrators && this.healthService != null && this.metadataStore != null)
            {
                // Local mode: Use IHealthService
                var healthResult = await this.healthService.PerformHealthCheckAsync();
                
                Console.WriteLine("UpdateEngine Health Status:");
                Console.WriteLine($"Status: {healthResult.Status}");
                Console.WriteLine($"Is Healthy: {healthResult.IsHealthy}");
                Console.WriteLine($"Package Count: {healthResult.PackageCount}");
                Console.WriteLine($"Content Store Available: {healthResult.ContentStoreAvailable}");
                Console.WriteLine($"Reindexing Required: {healthResult.ReindexingRequired}");
                Console.WriteLine($"Timestamp: {healthResult.Timestamp}");
                
                return healthResult.IsHealthy ? 0 : 1;
            }
            else if (this.updateEngineClient != null)
            {
                // Remote mode: Use HTTP client
                var status = await this.updateEngineClient.GetHealthStatusAsync(scope);
                Console.WriteLine("UpdateEngine Health Status:");
                Console.WriteLine(status);
                return 0;
            }
            else
            {
                Console.WriteLine("Error: No health service or HTTP client available");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting health status: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the configuration command.
    /// Note: Configuration is managed via appsettings.json in local mode.
    /// This command only works in remote HTTP mode.
    /// </summary>
    public async Task<int> HandleConfigurationAsync()
    {
        try
        {
            if (this.useOrchestrators)
            {
                Console.WriteLine("Configuration in local mode:");
                Console.WriteLine("Configuration is managed via appsettings.json");
                Console.WriteLine("Use 'dotnet run --project update-cli.csproj' with appropriate config files.");
                return 0;
            }
            else if (this.updateEngineClient != null)
            {
                var config = await this.updateEngineClient.GetServerConfigurationAsync();
                Console.WriteLine("UpdateEngine Configuration:");
                Console.WriteLine(config);
                return 0;
            }
            else
            {
                Console.WriteLine("Error: No configuration source available");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting configuration: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the sync metadata command using unified endpoint.
    /// </summary>
    public async Task<int> HandleSyncMetadataAsync()
    {
        try
        {
            Console.WriteLine("Starting comprehensive metadata synchronization...");
            Console.WriteLine("(Syncing categories and updates)");
            
            if (this.useOrchestrators && this.syncOrchestrator != null)
            {
                // Local mode: Use ISyncOrchestrator
                var request = new UnifiedSyncRequest
                {
                    SyncType = SyncType.Comprehensive,
                    Action = SyncAction.Start
                };
                
                var result = await this.syncOrchestrator.ExecuteSyncAsync(request, CancellationToken.None);
                
                Console.WriteLine("Metadata Sync Result:");
                Console.WriteLine($"Success: {result.Success}");
                Console.WriteLine($"Message: {result.Message}");
                if (result.ItemsSynced.HasValue)
                {
                    Console.WriteLine($"Items Synced: {result.ItemsSynced.Value}");
                }
                if (result.Duration.HasValue)
                {
                    Console.WriteLine($"Duration: {result.Duration.Value}");
                }
                
                return result.Success ? 0 : 1;
            }
            else if (this.updateEngineClient != null)
            {
                // Remote mode: Use HTTP client
                var result = await this.updateEngineClient.SyncMetadataAsync();
                Console.WriteLine("Metadata Sync Result:");
                Console.WriteLine(result);
                return 0;
            }
            else
            {
                Console.WriteLine("Error: No sync orchestrator or HTTP client available");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error syncing metadata: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the sync content command using unified endpoint.
    /// </summary>
    public async Task<int> HandleSyncContentAsync(int daysBack = 30)
    {
        try
        {
            Console.WriteLine($"Starting content synchronization (last {daysBack} days)...");
            
            if (this.useOrchestrators && this.contentOrchestrator != null && this.metadataOrchestrator != null)
            {
                // Local mode: Use IContentOrchestrator
                // First, query for recent updates
                var query = new MetadataQuery
                {
                    ReleasedAfter = DateTime.UtcNow.AddDays(-daysBack),
                    MaxResults = 1000
                };
                
                var updateIds = await this.metadataOrchestrator.QueryUpdatesAsync(query, CancellationToken.None);
                Console.WriteLine($"Found {updateIds.Count} updates from last {daysBack} days");
                
                // Download content for those updates
                var result = await this.contentOrchestrator.DownloadContentAsync(updateIds, null, CancellationToken.None);
                
                Console.WriteLine("Content Sync Result:");
                Console.WriteLine($"Success: {result.Success}");
                Console.WriteLine($"Downloaded: {result.DownloadedCount}");
                Console.WriteLine($"Failed: {result.FailedCount}");
                Console.WriteLine($"Duration: {result.Duration}");
                
                return result.Success ? 0 : 1;
            }
            else if (this.updateEngineClient != null)
            {
                // Remote mode: Use HTTP client
                var result = await this.updateEngineClient.SyncContentAsync(daysBack);
                Console.WriteLine("Content Sync Result:");
                Console.WriteLine(result);
                return 0;
            }
            else
            {
                Console.WriteLine("Error: No content orchestrator or HTTP client available");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error syncing content: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the sync critical updates command.
    /// </summary>
    public async Task<int> HandleSyncCriticalAsync()
    {
        try
        {
            Console.WriteLine("Starting critical updates synchronization...");
            Console.WriteLine("(Security and critical updates only)");
            
            if (this.useOrchestrators && this.syncOrchestrator != null)
            {
                // Local mode: Use ISyncOrchestrator with critical classifications filter
                var request = new UnifiedSyncRequest
                {
                    SyncType = SyncType.Updates,
                    Action = SyncAction.Start,
                    Filter = new SyncFilter
                    {
                        ClassificationIds = new List<Guid>
                        {
                            new Guid("0FA1201D-4330-4FA8-8AE9-B877473B6441"), // Security Updates
                            new Guid("E6CF1350-C01B-414D-A61F-263D14D133B4")  // Critical Updates
                        }
                    }
                };
                
                var result = await this.syncOrchestrator.ExecuteSyncAsync(request, CancellationToken.None);
                
                Console.WriteLine("Critical Sync Result:");
                Console.WriteLine($"Success: {result.Success}");
                Console.WriteLine($"Message: {result.Message}");
                if (result.ItemsSynced.HasValue)
                {
                    Console.WriteLine($"Items Synced: {result.ItemsSynced.Value}");
                }
                if (result.Duration.HasValue)
                {
                    Console.WriteLine($"Duration: {result.Duration.Value}");
                }
                
                return result.Success ? 0 : 1;
            }
            else if (this.updateEngineClient != null)
            {
                // Remote mode: Use HTTP client
                var result = await this.updateEngineClient.SyncCriticalUpdatesAsync();
                Console.WriteLine("Critical Sync Result:");
                Console.WriteLine(result);
                return 0;
            }
            else
            {
                Console.WriteLine("Error: No sync orchestrator or HTTP client available");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error syncing critical updates: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the store statistics command.
    /// </summary>
    public async Task<int> HandleStoreStatisticsAsync()
    {
        try
        {
            if (this.useOrchestrators && this.metadataOrchestrator != null)
            {
                // Local mode: Use IMetadataOrchestrator to get statistics
                var stats = await this.metadataOrchestrator.GetStatisticsAsync(CancellationToken.None);
                
                Console.WriteLine("Store Statistics:");
                Console.WriteLine($"Total Updates: {stats.TotalUpdates}");
                Console.WriteLine($"Total Categories: {stats.TotalCategories}");
                Console.WriteLine($"Total Classifications: {stats.TotalClassifications}");
                Console.WriteLine($"Total Products: {stats.TotalProducts}");
                Console.WriteLine($"Reindexing Required: {stats.ReindexingRequired}");
                
                if (stats.LastUpdated.HasValue)
                {
                    Console.WriteLine($"Last Updated: {stats.LastUpdated.Value}");
                }
                
                if (stats.StoreSizeBytes.HasValue)
                {
                    Console.WriteLine($"Store Size: {stats.StoreSizeBytes.Value:N0} bytes");
                }
                
                return 0;
            }
            else if (this.updateEngineClient != null)
            {
                // Remote mode: Use HTTP client
                var stats = await this.updateEngineClient.GetStoreStatisticsAsync();
                Console.WriteLine("Store Statistics:");
                Console.WriteLine(stats);
                return 0;
            }
            else
            {
                Console.WriteLine("Error: No metadata store or HTTP client available");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting store statistics: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the content status command.
    /// </summary>
    public async Task<int> HandleContentStatusAsync()
    {
        try
        {
            if (this.useOrchestrators && this.contentOrchestrator != null)
            {
                // Local mode: Use IContentOrchestrator
                var stats = await this.contentOrchestrator.GetStatisticsAsync(CancellationToken.None);
                
                Console.WriteLine("Content Store Statistics:");
                Console.WriteLine($"Total Files: {stats.TotalFiles}");
                Console.WriteLine($"Total Size: {stats.TotalSizeBytes:N0} bytes ({stats.TotalSizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB)");
                Console.WriteLine($"Updates With Content: {stats.UpdatesWithContent}");
                Console.WriteLine($"Pending Downloads: {stats.PendingDownloads}");
                Console.WriteLine($"Queued Size: {stats.QueuedSizeBytes:N0} bytes");
                Console.WriteLine($"Orphaned Files: {stats.OrphanedFiles}");
                
                return 0;
            }
            else if (this.updateEngineClient != null)
            {
                // Remote mode: Use HTTP client
                var status = await this.updateEngineClient.GetContentStatusAsync();
                Console.WriteLine("Content Sync Status:");
                Console.WriteLine(status);
                return 0;
            }
            else
            {
                Console.WriteLine("Error: No content orchestrator or HTTP client available");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting content status: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the search command.
    /// </summary>
    public async Task<int> HandleSearchAsync(string category)
    {
        try
        {
            Console.WriteLine($"Searching for updates in category: {category}");
            
            if (this.useOrchestrators && this.metadataOrchestrator != null)
            {
                // Local mode: Use IMetadataOrchestrator
                var query = new MetadataQuery
                {
                    Classifications = new List<string> { category },
                    MaxResults = 100
                };
                
                var results = await this.metadataOrchestrator.QueryUpdatesAsync(query, CancellationToken.None);
                
                Console.WriteLine($"Search Results: Found {results.Count} updates");
                foreach (var packageId in results.Take(20)) // Show first 20
                {
                    var guidId = packageId.OpenId != null && packageId.OpenId.Length == 16 
                        ? new Guid(packageId.OpenId).ToString() 
                        : "Unknown";
                    Console.WriteLine($"  - {guidId}");
                }
                
                if (results.Count > 20)
                {
                    Console.WriteLine($"  ... and {results.Count - 20} more");
                }
                
                return 0;
            }
            else if (this.updateEngineClient != null)
            {
                // Remote mode: Use HTTP client
                var results = await this.updateEngineClient.SearchUpdatesByCategoryAsync(category);
                Console.WriteLine("Search Results:");
                Console.WriteLine(results);
                return 0;
            }
            else
            {
                Console.WriteLine("Error: No metadata orchestrator or HTTP client available");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching updates: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the update details command.
    /// </summary>
    public async Task<int> HandleUpdateDetailsAsync(string updateId)
    {
        try
        {
            Console.WriteLine($"Getting details for update: {updateId}");
            
            if (this.useOrchestrators && this.metadataOrchestrator != null && this.metadataStore != null)
            {
                // Local mode: Use IMetadataOrchestrator
                // Parse the update ID
                if (!Guid.TryParse(updateId, out var guid))
                {
                    Console.WriteLine($"Error: Invalid update ID format. Expected GUID, got: {updateId}");
                    return 1;
                }
                
                // Find the package identity
                var identity = this.metadataStore.FirstOrDefault(p => 
                    p.Id?.OpenId != null && 
                    p.Id.OpenId.Length == 16 && 
                    new Guid(p.Id.OpenId) == guid)?.Id;
                
                if (identity == null)
                {
                    Console.WriteLine($"Error: Update not found: {updateId}");
                    return 1;
                }
                
                var package = await this.metadataOrchestrator.GetUpdateDetailsAsync(identity, CancellationToken.None);
                
                if (package == null)
                {
                    Console.WriteLine($"Error: Update not found: {updateId}");
                    return 1;
                }
                
                Console.WriteLine("Update Details:");
                Console.WriteLine($"Title: {package.Title}");
                Console.WriteLine($"Description: {package.Description}");
                Console.WriteLine($"ID: {updateId}");
                if (package is MicrosoftUpdatePackage muPackage)
                {
                    Console.WriteLine($"Type: {muPackage.GetType().Name}");
                }
                
                return 0;
            }
            else if (this.updateEngineClient != null)
            {
                // Remote mode: Use HTTP client
                var details = await this.updateEngineClient.GetUpdateDetailsAsync(updateId);
                Console.WriteLine("Update Details:");
                Console.WriteLine(details);
                return 0;
            }
            else
            {
                Console.WriteLine("Error: No metadata orchestrator or HTTP client available");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting update details: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the reindex command using unified endpoint.
    /// </summary>
    public async Task<int> HandleReindexAsync()
    {
        try
        {
            Console.WriteLine("Checking if reindex is required...");
            
            if (this.useOrchestrators && this.metadataOrchestrator != null)
            {
                // Local mode: Use IMetadataOrchestrator
                var indexStatus = await this.metadataOrchestrator.GetIndexStatusAsync(CancellationToken.None);
                
                if (indexStatus.ReindexingRequired)
                {
                    Console.WriteLine("Reindex is required. Starting store reindex...");
                    var result = await this.metadataOrchestrator.ReindexAsync(null, CancellationToken.None);
                    
                    Console.WriteLine("Reindex Result:");
                    Console.WriteLine($"Success: {result.Success}");
                    Console.WriteLine($"Packages Reindexed: {result.ReindexedCount}");
                    Console.WriteLine($"Duration: {result.Duration}");
                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        Console.WriteLine($"Error: {result.ErrorMessage}");
                    }
                    
                    return result.Success ? 0 : 1;
                }
                else
                {
                    Console.WriteLine("Reindex is not required. Store is up to date.");
                    return 0;
                }
            }
            else if (this.updateEngineClient != null)
            {
                // Remote mode: Use HTTP client
                var isRequired = await this.updateEngineClient.IsReindexRequiredAsync();
                
                if (isRequired)
                {
                    Console.WriteLine("Reindex is required. Starting store reindex...");
                    var result = await this.updateEngineClient.ReindexStoreAsync();
                    Console.WriteLine("Reindex Result:");
                    Console.WriteLine(result);
                }
                else
                {
                    Console.WriteLine("Reindex is not required. Store is up to date.");
                }
                
                return 0;
            }
            else
            {
                Console.WriteLine("Error: No metadata orchestrator or HTTP client available");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reindexing store: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the categories command.
    /// </summary>
    public async Task<int> HandleCategoriesAsync()
    {
        try
        {
            if (this.useOrchestrators && this.metadataStore != null)
            {
                // Local mode: Query metadata store directly using OfType<T>()
                Console.WriteLine("Available Categories:");
                Console.WriteLine("\nProducts:");
                var products = this.metadataStore.OfType<ProductCategory>().Take(25);
                foreach (var product in products)
                {
                    Console.WriteLine($"  - {product.Title}");
                }
                
                Console.WriteLine("\nClassifications:");
                var classifications = this.metadataStore.OfType<ClassificationCategory>().Take(25);
                foreach (var classification in classifications)
                {
                    Console.WriteLine($"  - {classification.Title}");
                }
                
                return 0;
            }
            else if (this.updateEngineClient != null)
            {
                // Remote mode: Use HTTP client
                var categories = await this.updateEngineClient.GetCategoriesAsync();
                Console.WriteLine("Available Categories:");
                Console.WriteLine(categories);
                return 0;
            }
            else
            {
                Console.WriteLine("Error: No metadata store or HTTP client available");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting categories: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the download list command.
    /// </summary>
    public async Task<int> HandleDownloadListAsync(string updateId)
    {
        try
        {
            Console.WriteLine($"Listing downloads for update: {updateId}");
            var downloads = await this.updateEngineClient.ListUpdateDownloadsAsync(updateId);
            Console.WriteLine("Available Downloads:");
            Console.WriteLine(downloads);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error listing downloads: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the download metadata command.
    /// </summary>
    public async Task<int> HandleDownloadMetadataAsync(string updateId, string? outputPath = null)
    {
        try
        {
            var fileName = outputPath ?? $"{updateId}_metadata.json";
            Console.WriteLine($"Downloading metadata for update: {updateId}");
            Console.WriteLine($"Output file: {fileName}");
            
            var success = await this.updateEngineClient.DownloadUpdateMetadataAsync(updateId, fileName);
            if (success)
            {
                var fileInfo = new FileInfo(fileName);
                Console.WriteLine($"✓ Metadata downloaded successfully!");
                Console.WriteLine($"  File: {fileInfo.FullName}");
                Console.WriteLine($"  Size: {fileInfo.Length:N0} bytes");
                return 0;
            }
            else
            {
                Console.WriteLine("✗ Download failed");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading metadata: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the download content command.
    /// </summary>
    public async Task<int> HandleDownloadContentAsync(string updateId, string? outputPath = null, bool showProgress = false)
    {
        try
        {
            var fileName = outputPath ?? $"{updateId}_content";
            Console.WriteLine($"Downloading content for update: {updateId}");
            Console.WriteLine($"Output file: {fileName}");
            
            bool success;
            
            if (showProgress)
            {
                var progress = new Progress<long>(bytes =>
                {
                    Console.Write($"\rDownloaded: {bytes:N0} bytes");
                });
                
                success = await this.updateEngineClient.DownloadUpdateContentWithProgressAsync(updateId, fileName, progress);
                Console.WriteLine(); // New line after progress
            }
            else
            {
                success = await this.updateEngineClient.DownloadUpdateContentAsync(updateId, fileName);
            }
            
            if (success)
            {
                var fileInfo = new FileInfo(fileName);
                Console.WriteLine($"✓ Content downloaded successfully!");
                Console.WriteLine($"  File: {fileInfo.FullName}");
                Console.WriteLine($"  Size: {fileInfo.Length:N0} bytes");
                return 0;
            }
            else
            {
                Console.WriteLine("✗ Download failed");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading content: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the Windows Server 2022 bulk download command.
    /// </summary>
    public async Task<int> HandleDownloadServer2022Async(string downloadPath, bool securityOnly = false, int maxUpdates = 50, bool skipSync = false, bool ipakCompatible = false)
    {
        return await this.server2022Handler.HandleBulkDownloadAsync(downloadPath, securityOnly, maxUpdates, skipSync, ipakCompatible);
    }

    /// <summary>
    /// Handles the Windows Server 2025 bulk download command.
    /// </summary>
    public async Task<int> HandleDownloadServer2025Async(string downloadPath, bool securityOnly = false, int maxUpdates = 50, bool skipSync = false, bool ipakCompatible = false)
    {
        return await this.server2025Handler.HandleBulkDownloadAsync(downloadPath, securityOnly, maxUpdates, skipSync, ipakCompatible);
    }

    /// <summary>
    /// Handles the Windows 11 bulk download command.
    /// </summary>
    public async Task<int> HandleDownloadWindows11Async(string downloadPath, bool securityOnly = false, int maxUpdates = 50, bool skipSync = false, bool ipakCompatible = false)
    {
        return await this.windows11Handler.HandleBulkDownloadAsync(downloadPath, securityOnly, maxUpdates, skipSync, ipakCompatible);
    }

}