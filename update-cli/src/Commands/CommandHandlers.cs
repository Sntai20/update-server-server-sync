// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using UpdateCli.Services;

namespace UpdateCli.Commands;

/// <summary>
/// Command handlers for the UpdateEngine CLI.
/// Updated to work with unified function endpoints.
/// </summary>
public class CommandHandlers
{
    private readonly UpdateEngineClient updateEngineClient;
    private readonly Server2022DownloadHandler server2022Handler;
    private readonly Server2025DownloadHandler server2025Handler;
    private readonly Windows11DownloadHandler windows11Handler;

    public CommandHandlers(UpdateEngineClient updateEngineClient)
    {
        this.updateEngineClient = updateEngineClient;
        this.server2022Handler = new Server2022DownloadHandler(updateEngineClient);
        this.server2025Handler = new Server2025DownloadHandler(updateEngineClient);
        this.windows11Handler = new Windows11DownloadHandler(updateEngineClient);
    }

    /// <summary>
    /// Handles the health check command with scope support.
    /// </summary>
    public async Task<int> HandleHealthAsync(string scope = "basic")
    {
        try
        {
            Console.WriteLine($"Checking UpdateEngine health (scope: {scope})...");
            var status = await this.updateEngineClient.GetHealthStatusAsync(scope);
            Console.WriteLine("UpdateEngine Health Status:");
            Console.WriteLine(status);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting health status: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the configuration command.
    /// </summary>
    public async Task<int> HandleConfigurationAsync()
    {
        try
        {
            var config = await this.updateEngineClient.GetServerConfigurationAsync();
            Console.WriteLine("UpdateEngine Configuration:");
            Console.WriteLine(config);
            return 0;
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
            var result = await this.updateEngineClient.SyncMetadataAsync();
            Console.WriteLine("Metadata Sync Result:");
            Console.WriteLine(result);
            return 0;
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
            var result = await this.updateEngineClient.SyncContentAsync(daysBack);
            Console.WriteLine("Content Sync Result:");
            Console.WriteLine(result);
            return 0;
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
            var result = await this.updateEngineClient.SyncCriticalUpdatesAsync();
            Console.WriteLine("Critical Sync Result:");
            Console.WriteLine(result);
            return 0;
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
            var stats = await this.updateEngineClient.GetStoreStatisticsAsync();
            Console.WriteLine("Store Statistics:");
            Console.WriteLine(stats);
            return 0;
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
            var status = await this.updateEngineClient.GetContentStatusAsync();
            Console.WriteLine("Content Sync Status:");
            Console.WriteLine(status);
            return 0;
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
            var results = await this.updateEngineClient.SearchUpdatesByCategoryAsync(category);
            Console.WriteLine("Search Results:");
            Console.WriteLine(results);
            return 0;
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
            var details = await this.updateEngineClient.GetUpdateDetailsAsync(updateId);
            Console.WriteLine("Update Details:");
            Console.WriteLine(details);
            return 0;
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
            var categories = await this.updateEngineClient.GetCategoriesAsync();
            Console.WriteLine("Available Categories:");
            Console.WriteLine(categories);
            return 0;
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