// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Cli.Services;

using Microsoft.Extensions.Options;
using System.Text.Json;
using UpdateEngine.Cli.Configuration;

/// <summary>
/// Service for communicating with the UpdateEngine API.
/// Updated to work with unified function endpoints.
/// </summary>
public class UpdateEngineClient
{
    private readonly HttpClient httpClient;
    private readonly JsonSerializerOptions jsonOptions;

    public UpdateEngineClient(HttpClient httpClient, IOptions<UpdateEngineConfiguration> configuration)
    {
        this.httpClient = httpClient;
        var config = configuration.Value;
        this.httpClient.BaseAddress = new Uri(config.BaseUrl);
        this.httpClient.Timeout = config.Timeout;
        
        this.jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
    }

    /// <summary>
    /// Gets the health status of the UpdateEngine.
    /// Uses the unified UniversalHealth endpoint.
    /// </summary>
    /// <param name="scope">Health check scope: basic, full, sync, or store (default: basic)</param>
    /// <returns>Health status information.</returns>
    public async Task<string> GetHealthStatusAsync(string scope = "basic")
    {
        var response = await this.httpClient.GetAsync($"/api/UniversalHealth?scope={scope}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Gets the server configuration from UpdateEngine.
    /// </summary>
    /// <returns>Server configuration information.</returns>
    public async Task<string> GetServerConfigurationAsync()
    {
        var response = await this.httpClient.GetAsync("/api/GetServerConfiguration");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Triggers a manual metadata synchronization using the unified sync endpoint.
    /// </summary>
    /// <returns>Synchronization result.</returns>
    public async Task<string> SyncMetadataAsync()
    {
        var request = new
        {
            syncType = "comprehensive",
            syncUpdates = true,
            syncCategories = true,
            syncContent = false
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, this.jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await this.httpClient.PostAsync("/api/UniversalSync", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Triggers a manual content synchronization using the unified sync endpoint.
    /// </summary>
    /// <param name="daysBack">Number of days back to sync content (default: 30)</param>
    /// <returns>Synchronization result.</returns>
    public async Task<string> SyncContentAsync(int daysBack = 30)
    {
        var request = new
        {
            syncType = "content",
            syncUpdates = false,
            syncCategories = false,
            syncContent = true,
            contentDaysBack = daysBack
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, this.jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await this.httpClient.PostAsync("/api/UniversalSync", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Triggers a critical updates sync using the unified sync endpoint.
    /// </summary>
    /// <returns>Synchronization result.</returns>
    public async Task<string> SyncCriticalUpdatesAsync()
    {
        var request = new
        {
            syncType = "critical",
            syncUpdates = true,
            syncCategories = false,
            syncContent = false
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, this.jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await this.httpClient.PostAsync("/api/UniversalSync", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Gets statistics about the metadata store.
    /// </summary>
    /// <returns>Store statistics.</returns>
    public async Task<string> GetStoreStatisticsAsync()
    {
        var response = await this.httpClient.GetAsync("/api/GetStoreStatus");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Checks if store reindexing is required.
    /// Uses the unified CheckReindexRequired endpoint.
    /// </summary>
    /// <returns>Reindex status information.</returns>
    public async Task<bool> IsReindexRequiredAsync()
    {
        var response = await this.httpClient.GetAsync("/api/CheckReindexRequired");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("reindexRequired").GetBoolean();
    }

    /// <summary>
    /// Triggers a reindex of the metadata store.
    /// Uses the unified StoreManagement endpoint.
    /// </summary>
    /// <returns>Reindex result.</returns>
    public async Task<string> ReindexStoreAsync()
    {
        var request = new
        {
            reindex = true,
            clearCache = false,
            cleanup = false
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, this.jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await this.httpClient.PostAsync("/api/StoreManagement", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Gets the content sync status.
    /// Uses the unified QueryContentStatus endpoint.
    /// </summary>
    /// <returns>Content sync status.</returns>
    public async Task<string> GetContentStatusAsync()
    {
        var response = await this.httpClient.GetAsync("/api/QueryContentStatus");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Searches for updates by category.
    /// </summary>
    /// <param name="category">The update category to search for.</param>
    /// <returns>Search results.</returns>
    public async Task<string> SearchUpdatesByCategoryAsync(string category)
    {
        var response = await this.httpClient.GetAsync($"/api/SearchUpdates?category={Uri.EscapeDataString(category)}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Searches for updates by search term.
    /// </summary>
    /// <param name="searchTerm">The search term to look for.</param>
    /// <returns>Search results.</returns>
    public async Task<string> SearchUpdatesAsync(string searchTerm)
    {
        var response = await this.httpClient.GetAsync($"/api/SearchUpdates?term={Uri.EscapeDataString(searchTerm)}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Gets details about a specific update by ID.
    /// </summary>
    /// <param name="updateId">The update ID.</param>
    /// <returns>Update details.</returns>
    public async Task<string> GetUpdateDetailsAsync(string updateId)
    {
        var response = await this.httpClient.GetAsync($"/api/GetUpdateDetails?updateId={Uri.EscapeDataString(updateId)}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Gets the list of available categories.
    /// </summary>
    /// <returns>Available categories.</returns>
    public async Task<string> GetCategoriesAsync()
    {
        var response = await this.httpClient.GetAsync("/api/GetCategories");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Lists available downloads for a specific update.
    /// </summary>
    /// <param name="updateId">The update ID.</param>
    /// <returns>Download information.</returns>
    public async Task<string> ListUpdateDownloadsAsync(string updateId)
    {
        var response = await this.httpClient.GetAsync($"/api/download/list/{Uri.EscapeDataString(updateId)}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Downloads update metadata to a file.
    /// </summary>
    /// <param name="updateId">The update ID.</param>
    /// <param name="outputPath">The output file path.</param>
    /// <returns>True if successful.</returns>
    public async Task<bool> DownloadUpdateMetadataAsync(string updateId, string outputPath)
    {
        var response = await this.httpClient.GetAsync($"/api/download/metadata/{Uri.EscapeDataString(updateId)}");
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsByteArrayAsync();
        await File.WriteAllBytesAsync(outputPath, content);
        return true;
    }

    /// <summary>
    /// Downloads update content files to a file or directory.
    /// </summary>
    /// <param name="updateId">The update ID.</param>
    /// <param name="outputPath">The output file or directory path.</param>
    /// <returns>True if successful.</returns>
    public async Task<bool> DownloadUpdateContentAsync(string updateId, string outputPath)
    {
        var response = await this.httpClient.GetAsync($"/api/download/content/{Uri.EscapeDataString(updateId)}");
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsByteArrayAsync();
        
        // Create directory if it doesn't exist
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        await File.WriteAllBytesAsync(outputPath, content);
        return true;
    }

    /// <summary>
    /// Downloads update content with progress reporting.
    /// </summary>
    /// <param name="updateId">The update ID.</param>
    /// <param name="outputPath">The output file path.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successful.</returns>
    public async Task<bool> DownloadUpdateContentWithProgressAsync(string updateId, string outputPath, 
        IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        var response = await this.httpClient.GetAsync($"/api/download/content/{Uri.EscapeDataString(updateId)}", 
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var downloadedBytes = 0L;
        
        // Create directory if it doesn't exist
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        
        var buffer = new byte[8192];
        int bytesRead;
        
        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            downloadedBytes += bytesRead;
            progress?.Report(downloadedBytes);
        }
        
        return true;
    }
}