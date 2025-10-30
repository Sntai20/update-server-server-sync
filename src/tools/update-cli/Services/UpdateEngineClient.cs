// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Options;
using System.Text.Json;
using UpdateCli.Configuration;

namespace UpdateCli.Services;

/// <summary>
/// Service for communicating with the UpdateEngine API.
/// </summary>
public class UpdateEngineClient
{
    private readonly HttpClient httpClient;

    public UpdateEngineClient(HttpClient httpClient, IOptions<UpdateEngineConfiguration> configuration)
    {
        this.httpClient = httpClient;
        var config = configuration.Value;
        this.httpClient.BaseAddress = new Uri(config.BaseUrl);
        this.httpClient.Timeout = config.Timeout;
    }

    /// <summary>
    /// Gets the health status of the UpdateEngine.
    /// </summary>
    /// <returns>Health status information.</returns>
    public async Task<string> GetHealthStatusAsync()
    {
        var response = await this.httpClient.GetAsync("/api/GetStoreStatus");
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
    /// Triggers a manual metadata synchronization.
    /// </summary>
    /// <returns>Synchronization result.</returns>
    public async Task<string> SyncMetadataAsync()
    {
        var response = await this.httpClient.PostAsync("/api/SyncMetadata", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Triggers a manual content synchronization.
    /// </summary>
    /// <returns>Synchronization result.</returns>
    public async Task<string> SyncContentAsync()
    {
        var response = await this.httpClient.PostAsync("/api/SyncContent", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Gets statistics about the metadata store.
    /// </summary>
    /// <returns>Store statistics.</returns>
    public async Task<string> GetStoreStatisticsAsync()
    {
        var response = await this.httpClient.GetAsync("/api/GetStoreStatistics");
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
    /// Triggers a reindex of the metadata store.
    /// </summary>
    /// <returns>Reindex result.</returns>
    public async Task<string> ReindexStoreAsync()
    {
        var response = await this.httpClient.PostAsync("/api/ReindexStore", null);
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
        
        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            downloadedBytes += bytesRead;
            progress?.Report(downloadedBytes);
        }
        
        return true;
    }
}