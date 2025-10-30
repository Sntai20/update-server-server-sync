// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using UpdateCli.Services;
using System.Text.Json;

namespace UpdateCli.Commands;

/// <summary>
/// Base class for Windows/Server specific bulk download operations.
/// Provides common functionality following DRY principles.
/// </summary>
public abstract class BaseWindowsDownloadHandler
{
    protected readonly UpdateEngineClient updateEngineClient;

    protected BaseWindowsDownloadHandler(UpdateEngineClient updateEngineClient)
    {
        this.updateEngineClient = updateEngineClient;
    }

    /// <summary>
    /// Gets the search terms for the specific Windows/Server version.
    /// </summary>
    protected abstract string[] GetSearchTerms(bool securityOnly);

    /// <summary>
    /// Gets the display name for the Windows/Server version.
    /// </summary>
    protected abstract string GetDisplayName();

    /// <summary>
    /// Handles bulk download for the specific Windows/Server version.
    /// </summary>
    public async Task<int> HandleBulkDownloadAsync(string downloadPath, bool securityOnly = false, int maxUpdates = 50, bool skipSync = false)
    {
        try
        {
            var displayName = this.GetDisplayName();
            Console.WriteLine($"=== {displayName} Update Download ===");
            Console.WriteLine($"Download Path: {downloadPath}");
            Console.WriteLine($"Security Only: {securityOnly}");
            Console.WriteLine($"Max Updates: {maxUpdates}");
            Console.WriteLine($"Skip Sync: {skipSync}");
            Console.WriteLine();

            // Step 1: Create directories
            var (metadataPath, contentPath, logsPath) = await CreateDirectoriesAsync(downloadPath);

            // Step 2: Check connectivity
            var connected = await this.CheckConnectivityAsync();
            if (!connected) return 1;

            // Step 3: Sync metadata
            if (!skipSync)
            {
                await this.SyncMetadataAsync();
            }

            // Step 4: Search and collect update IDs
            var updateIds = await this.SearchAndCollectUpdatesAsync(securityOnly, maxUpdates, logsPath);
            if (!updateIds.Any())
            {
                Console.WriteLine("⚠ No updates found");
                return 0;
            }

            // Step 5: Download metadata
            var (metadataSuccess, metadataFailed) = await this.DownloadMetadataAsync(updateIds, metadataPath);

            // Step 6: Analyze and download content
            var (contentSuccess, contentFailed, totalSize) = await this.DownloadContentAsync(metadataSuccess, metadataPath, contentPath);

            // Step 7: Generate report
            var reportData = new DownloadReportData(downloadPath, securityOnly, maxUpdates, skipSync, 
                updateIds, metadataSuccess, metadataFailed, contentSuccess, contentFailed, totalSize);
            await this.GenerateReportAsync(reportData, logsPath);

            return contentFailed.Any() ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in {this.GetDisplayName()} download: {ex.Message}");
            return 1;
        }
    }

    private static async Task<(string metadata, string content, string logs)> CreateDirectoriesAsync(string downloadPath)
    {
        Console.WriteLine("📁 Creating download directories...");
        var metadataPath = Path.Combine(downloadPath, "metadata");
        var contentPath = Path.Combine(downloadPath, "content");
        var logsPath = Path.Combine(downloadPath, "logs");
        
        await Task.Run(() =>
        {
            Directory.CreateDirectory(metadataPath);
            Directory.CreateDirectory(contentPath);
            Directory.CreateDirectory(logsPath);
        });
        
        Console.WriteLine("✓ Directories created");
        return (metadataPath, contentPath, logsPath);
    }

    private async Task<bool> CheckConnectivityAsync()
    {
        Console.WriteLine("🔍 Checking UpdateEngine connectivity...");
        try
        {
            await this.updateEngineClient.GetHealthStatusAsync();
            Console.WriteLine("✓ UpdateEngine is accessible");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Cannot connect to UpdateEngine: {ex.Message}");
            Console.WriteLine("Please ensure UpdateEngine is running: cd AppHost/src && dotnet run");
            return false;
        }
    }

    private async Task SyncMetadataAsync()
    {
        Console.WriteLine("📡 Syncing metadata from Microsoft Update...");
        try
        {
            await this.updateEngineClient.SyncMetadataAsync();
            Console.WriteLine("✓ Metadata sync completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Metadata sync failed: {ex.Message}");
            Console.WriteLine("Continuing with existing metadata...");
        }
    }

    private async Task<List<string>> SearchAndCollectUpdatesAsync(bool securityOnly, int maxUpdates, string logsPath)
    {
        Console.WriteLine($"🔍 Searching for {this.GetDisplayName()} updates...");
        var searchTerms = this.GetSearchTerms(securityOnly);

        var allUpdateIds = new HashSet<string>();
        var searchResults = new Dictionary<string, string>();

        foreach (var term in searchTerms)
        {
            Console.WriteLine($"  Searching: {term}");
            try
            {
                var searchResult = await this.updateEngineClient.SearchUpdatesAsync(term);
                searchResults[term] = searchResult;
                
                var updateIds = ExtractUpdateIdsFromSearchResult(searchResult);
                foreach (var id in updateIds)
                {
                    allUpdateIds.Add(id);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Search failed for '{term}': {ex.Message}");
            }
        }

        var selectedUpdateIds = allUpdateIds.Take(maxUpdates).ToList();
        Console.WriteLine($"📊 Found {allUpdateIds.Count} total updates, processing {selectedUpdateIds.Count}");

        // Save search results
        var searchResultsPath = Path.Combine(logsPath, "search-results.json");
        await File.WriteAllTextAsync(searchResultsPath, JsonSerializer.Serialize(searchResults, new JsonSerializerOptions { WriteIndented = true }));
        await File.WriteAllLinesAsync(Path.Combine(logsPath, "update-ids.txt"), selectedUpdateIds);

        return selectedUpdateIds;
    }

    private async Task<(List<string> success, List<string> failed)> DownloadMetadataAsync(List<string> updateIds, string metadataPath)
    {
        Console.WriteLine($"📥 Downloading metadata for {updateIds.Count} updates...");
        var metadataSuccess = new List<string>();
        var metadataFailed = new List<string>();

        var current = 1;
        foreach (var updateId in updateIds)
        {
            Console.Write($"\r  Progress: {current}/{updateIds.Count} - {updateId}");
            
            try
            {
                var metadataFile = Path.Combine(metadataPath, $"{updateId}.json");
                var success = await this.updateEngineClient.DownloadUpdateMetadataAsync(updateId, metadataFile);
                if (success)
                {
                    metadataSuccess.Add(updateId);
                }
                else
                {
                    metadataFailed.Add(updateId);
                }
            }
            catch (Exception)
            {
                metadataFailed.Add(updateId);
            }
            current++;
        }
        Console.WriteLine();
        Console.WriteLine($"✓ Metadata downloaded: {metadataSuccess.Count}, Failed: {metadataFailed.Count}");

        return (metadataSuccess, metadataFailed);
    }

    private async Task<(List<string> success, List<string> failed, long totalSize)> DownloadContentAsync(
        List<string> metadataSuccess, string metadataPath, string contentPath)
    {
        Console.WriteLine("🔍 Analyzing metadata for content download...");
        var contentDownloadCandidates = await this.AnalyzeMetadataForDownloadAsync(metadataSuccess, metadataPath);

        Console.WriteLine($"📋 Selected {contentDownloadCandidates.Count} updates for content download");

        var contentSuccess = new List<string>();
        var contentFailed = new List<string>();
        long totalDownloadSize = 0;

        if (contentDownloadCandidates.Any())
        {
            Console.WriteLine("📦 Downloading update content...");
            var current = 1;
            
            foreach (var (updateId, title, size) in contentDownloadCandidates)
            {
                Console.WriteLine($"  [{current}/{contentDownloadCandidates.Count}] {updateId}");
                Console.WriteLine($"    Title: {title}");
                Console.WriteLine($"    Size: {size:N0} bytes");
                
                try
                {
                    var contentFile = Path.Combine(contentPath, updateId);
                    var progress = new Progress<long>(bytes => 
                    {
                        var percent = size > 0 ? (bytes * 100 / size) : 0;
                        Console.Write($"\r    Progress: {bytes:N0}/{size:N0} bytes ({percent}%)");
                    });
                    
                    var success = await this.updateEngineClient.DownloadUpdateContentWithProgressAsync(updateId, contentFile, progress);
                    Console.WriteLine(); // New line after progress
                    
                    if (success && File.Exists(contentFile))
                    {
                        var actualSize = new FileInfo(contentFile).Length;
                        totalDownloadSize += actualSize;
                        contentSuccess.Add(updateId);
                        Console.WriteLine($"    ✓ Downloaded: {actualSize:N0} bytes");
                    }
                    else
                    {
                        contentFailed.Add(updateId);
                        Console.WriteLine($"    ✗ Download failed");
                    }
                }
                catch (Exception ex)
                {
                    contentFailed.Add(updateId);
                    Console.WriteLine($"    ✗ Error: {ex.Message}");
                }
                current++;
            }
        }

        return (contentSuccess, contentFailed, totalDownloadSize);
    }

    private async Task<List<(string UpdateId, string Title, long Size)>> AnalyzeMetadataForDownloadAsync(
        List<string> metadataSuccess, string metadataPath)
    {
        var contentDownloadCandidates = new List<(string UpdateId, string Title, long Size)>();
        
        foreach (var updateId in metadataSuccess)
        {
            try
            {
                var metadataFile = Path.Combine(metadataPath, $"{updateId}.json");
                if (File.Exists(metadataFile))
                {
                    var jsonContent = await File.ReadAllTextAsync(metadataFile);
                    using var document = JsonDocument.Parse(jsonContent);
                    
                    var title = document.RootElement.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "Unknown" : "Unknown";
                    var size = document.RootElement.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;
                    
                    // Filter criteria - less than 1GB
                    if (size < 1_000_000_000)
                    {
                        contentDownloadCandidates.Add((updateId, title, size));
                    }
                }
            }
            catch (Exception)
            {
                // Skip updates we can't parse
            }
        }

        return contentDownloadCandidates;
    }

    private async Task GenerateReportAsync(DownloadReportData reportData, string logsPath)
    {
        var report = new
        {
            Timestamp = DateTime.UtcNow,
            Platform = this.GetDisplayName(),
            DownloadPath = reportData.DownloadPath,
            SecurityOnly = reportData.SecurityOnly,
            MaxUpdates = reportData.MaxUpdates,
            SkippedSync = reportData.SkippedSync,
            UpdatesFound = reportData.UpdateIds.Count,
            MetadataSuccess = reportData.MetadataSuccess.Count,
            MetadataFailed = reportData.MetadataFailed.Count,
            ContentSuccess = reportData.ContentSuccess.Count,
            ContentFailed = reportData.ContentFailed.Count,
            TotalDownloadBytes = reportData.TotalSize,
            TotalDownloadMB = Math.Round(reportData.TotalSize / 1024.0 / 1024.0, 2),
            TotalDownloadGB = Math.Round(reportData.TotalSize / 1024.0 / 1024.0 / 1024.0, 2),
            SuccessfulDownloads = reportData.ContentSuccess,
            FailedDownloads = reportData.ContentFailed
        };

        var reportPath = Path.Combine(logsPath, "download-report.json");
        var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(reportPath, reportJson);

        // Display summary
        Console.WriteLine();
        Console.WriteLine("=== Download Summary ===");
        Console.WriteLine($"✓ Metadata downloaded: {reportData.MetadataSuccess.Count} updates");
        Console.WriteLine($"✓ Content downloaded: {reportData.ContentSuccess.Count} updates");
        Console.WriteLine($"📊 Total size: {report.TotalDownloadMB} MB ({report.TotalDownloadGB} GB)");
        Console.WriteLine($"📁 Location: {reportData.DownloadPath}");
        
        if (reportData.ContentFailed.Any())
        {
            Console.WriteLine($"⚠ Content download failed: {reportData.ContentFailed.Count} updates");
        }

        Console.WriteLine();
        Console.WriteLine("📄 Generated files:");
        Console.WriteLine($"  📊 {reportPath}");
        Console.WriteLine($"  📁 {Path.Combine(reportData.DownloadPath, "metadata")}\\ - Update metadata (JSON)");
        Console.WriteLine($"  📁 {Path.Combine(reportData.DownloadPath, "content")}\\ - Update content files");
        
        Console.WriteLine();
        Console.WriteLine("🎯 Next steps:");
        Console.WriteLine("  1. Review download-report.json for detailed results");
        Console.WriteLine("  2. Test updates in lab environment");
        Console.WriteLine("  3. Deploy via WSUS, ConfigMgr, or direct installation");
    }

    /// <summary>
    /// Extracts update IDs from search result text.
    /// </summary>
    private static List<string> ExtractUpdateIdsFromSearchResult(string searchResult)
    {
        var updateIds = new List<string>();
        
        // Look for GUID patterns
        var guidPattern = @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";
        var guidMatches = System.Text.RegularExpressions.Regex.Matches(searchResult, guidPattern);
        foreach (System.Text.RegularExpressions.Match match in guidMatches)
        {
            updateIds.Add(match.Value);
        }
        
        // Look for KB numbers
        var kbPattern = @"KB\d{6,7}";
        var kbMatches = System.Text.RegularExpressions.Regex.Matches(searchResult, kbPattern);
        foreach (System.Text.RegularExpressions.Match match in kbMatches)
        {
            updateIds.Add(match.Value);
        }
        
        return updateIds.Distinct().ToList();
    }
}

/// <summary>
/// Data structure for download report generation.
/// </summary>
public record DownloadReportData(
    string DownloadPath,
    bool SecurityOnly,
    int MaxUpdates,
    bool SkippedSync,
    List<string> UpdateIds,
    List<string> MetadataSuccess,
    List<string> MetadataFailed,
    List<string> ContentSuccess,
    List<string> ContentFailed,
    long TotalSize
);