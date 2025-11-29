// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core.Services;

using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using UpdateEngine.Metadata.Storage;
using UpdateEngine.Metadata.Metadata;

/// <summary>
/// Service for syncing metadata and content from an upstream UpdateEngine Functions instance.
/// This enables a downstream WorkerService to pull updates from a cloud-hosted Functions instance
/// rather than syncing directly from Microsoft Update.
/// </summary>
public class DownstreamSyncService : IDownstreamSyncService
{
    private readonly ILogger<DownstreamSyncService> logger;
    private readonly HttpClient httpClient;
    private readonly IMetadataStore metadataStore;
    private readonly IContentStore? contentStore;

    public DownstreamSyncService(
        ILogger<DownstreamSyncService> logger,
        HttpClient httpClient,
        IMetadataStore metadataStore,
        IContentStore? contentStore = null)
    {
        this.logger = logger;
        this.httpClient = httpClient;
        this.metadataStore = metadataStore;
        this.contentStore = contentStore;
    }

    /// <summary>
    /// Syncs metadata from upstream Functions API to local metadata store.
    /// </summary>
    public async Task<SyncResult> SyncMetadataFromUpstreamAsync(
        ServiceMetadataFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        this.logger.LogInformation("Starting downstream metadata sync from upstream Functions API");

        try
        {
            // Build export request
            var exportRequest = new MetadataExportRequest
            {
                ProductsFilter = filter?.ProductFilters,
                ClassificationsFilter = filter?.ClassificationFilters,
                Format = "json",
                IncludeSuperseded = false,
                IncludeContent = false
            };

            // Call upstream Functions API to export metadata
            var response = await this.httpClient.PostAsJsonAsync(
                "/api/metadata/export",
                exportRequest,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            // Deserialize the MetadataExportResult envelope
            var exportResult = await response.Content.ReadFromJsonAsync<MetadataExportResult>(cancellationToken);
            
            if (exportResult == null || !exportResult.Success)
            {
                var errorMessage = exportResult?.ErrorMessage ?? "Unknown error during metadata export";
                this.logger.LogError("Metadata export failed: {ErrorMessage}", errorMessage);
                
                return new SyncResult
                {
                    Success = false,
                    StartTime = startTime,
                    EndTime = DateTime.UtcNow,
                    ErrorMessage = errorMessage,
                    UpdatesSynced = false
                };
            }

            // Extract and deserialize the actual metadata from ExportData
            List<Dictionary<string, object>>? packages = null;
            
            if (!string.IsNullOrEmpty(exportResult.ExportData))
            {
                packages = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(exportResult.ExportData);
            }

            if (packages == null || packages.Count == 0)
            {
                this.logger.LogWarning("No metadata packages received from upstream");
                
                return new SyncResult
                {
                    Success = true,
                    StartTime = startTime,
                    EndTime = DateTime.UtcNow,
                    UpdatesSynced = false,
                    ItemsProcessed = 0
                };
            }

            this.logger.LogInformation("Received {Count} metadata packages from upstream", packages.Count);

            // TODO: Import metadata into local store
            // For now, just log what we received
            this.logger.LogWarning(
                "Metadata import not yet implemented. Received {Count} packages but did not import them",
                packages.Count);

            return new SyncResult
            {
                Success = true,
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                UpdatesSynced = true,
                ItemsProcessed = packages.Count
            };
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error syncing metadata from upstream: {Message}", ex.Message);
            
            return new SyncResult
            {
                Success = false,
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                ErrorMessage = ex.Message,
                UpdatesSynced = false
            };
        }
    }

    /// <summary>
    /// Syncs content files from upstream Functions API to local content store.
    /// </summary>
    public async Task SyncContentFromUpstreamAsync(
        ServiceMetadataFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        if (this.contentStore == null)
        {
            this.logger.LogWarning("Content store not available, skipping content sync");
            return;
        }

        this.logger.LogInformation("Starting downstream content sync from upstream Functions API");

        try
        {
            // First, query upstream for list of updates matching filter
            var queryRequest = new
            {
                Filter = filter,
                IncludeFiles = true
            };

            var queryResponse = await this.httpClient.PostAsJsonAsync(
                "/api/metadata/query",
                queryRequest,
                cancellationToken);

            queryResponse.EnsureSuccessStatusCode();

            var queryResult = await queryResponse.Content.ReadFromJsonAsync<DownstreamMetadataQueryResult>(cancellationToken);
            
            if (queryResult?.Updates == null || !queryResult.Updates.Any())
            {
                this.logger.LogInformation("No updates found matching filter for content sync");
                return;
            }

            this.logger.LogInformation("Found {Count} updates with content to sync", queryResult.Updates.Count);

            // Download each content file
            var totalFiles = 0;
            var downloadedFiles = 0;

            foreach (var update in queryResult.Updates)
            {
                if (update.Files == null || !update.Files.Any())
                {
                    continue;
                }

                foreach (var file in update.Files)
                {
                    totalFiles++;

                    // Download from upstream
                    try
                    {
                        this.logger.LogInformation("Downloading content file: {FileName} ({Size} bytes)", 
                            file.FileName, file.Size);

                        var contentResponse = await this.httpClient.GetAsync(
                            $"/api/content/{file.Digests?.Sha256Digest}",
                            HttpCompletionOption.ResponseHeadersRead,
                            cancellationToken);

                        contentResponse.EnsureSuccessStatusCode();

                        // Stream the content to local storage
                        using var contentStream = await contentResponse.Content.ReadAsStreamAsync(cancellationToken);
                        
                        // TODO: Implement content store write/stream API
                        // For now, this is a placeholder showing the pattern
                        this.logger.LogWarning("Content streaming to local store not yet implemented for {FileName}", file.FileName);
                        
                        downloadedFiles++;
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, "Error downloading content file {FileName}: {Message}", 
                            file.FileName, ex.Message);
                    }
                }
            }

            this.logger.LogInformation(
                "Downstream content sync completed: {Downloaded}/{Total} files downloaded",
                downloadedFiles,
                totalFiles);
        }
        catch (HttpRequestException ex)
        {
            this.logger.LogError(ex, "HTTP error syncing content from upstream: {Message}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error syncing content from upstream: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets the sync status from the upstream server.
    /// </summary>
    public async Task<UpstreamSyncStatus> GetUpstreamSyncStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await this.httpClient.GetAsync("/api/sync/status", cancellationToken);
            response.EnsureSuccessStatusCode();

            var status = await response.Content.ReadFromJsonAsync<UpstreamSyncStatus>(cancellationToken);
            return status ?? new UpstreamSyncStatus { IsHealthy = false };
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error getting upstream sync status: {Message}", ex.Message);
            return new UpstreamSyncStatus
            {
                IsHealthy = false,
                ErrorMessage = ex.Message
            };
        }
    }
}

/// <summary>
/// Interface for downstream synchronization from upstream UpdateEngine server.
/// </summary>
public interface IDownstreamSyncService
{
    Task<SyncResult> SyncMetadataFromUpstreamAsync(ServiceMetadataFilter? filter = null, CancellationToken cancellationToken = default);
    Task SyncContentFromUpstreamAsync(ServiceMetadataFilter? filter = null, CancellationToken cancellationToken = default);
    Task<UpstreamSyncStatus> GetUpstreamSyncStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Model for metadata query results from upstream (downstream-specific model to avoid conflicts).
/// </summary>
public class DownstreamMetadataQueryResult
{
    public List<UpdateInfo>? Updates { get; set; }
}

/// <summary>
/// Model for update information from upstream.
/// </summary>
public class UpdateInfo
{
    public string? UpdateId { get; set; }
    public string? Title { get; set; }
    public List<DownstreamFileInfo>? Files { get; set; }
}

/// <summary>
/// Model for file information from upstream (downstream-specific to avoid conflicts).
/// </summary>
public class DownstreamFileInfo
{
    public string? FileName { get; set; }
    public long Size { get; set; }
    public FileDigests? Digests { get; set; }
}

/// <summary>
/// Model for file digests.
/// </summary>
public class FileDigests
{
    public string? Sha256Digest { get; set; }
    public string? Sha1Digest { get; set; }
}

/// <summary>
/// Model for upstream sync status.
/// </summary>
public class UpstreamSyncStatus
{
    public bool IsHealthy { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? LastSyncTime { get; set; }
    public int? PackageCount { get; set; }
}

/// <summary>
/// No-op implementation of IDownstreamSyncService used when downstream sync is disabled.
/// </summary>
internal class NoOpDownstreamSyncService : IDownstreamSyncService
{
    public Task<SyncResult> SyncMetadataFromUpstreamAsync(ServiceMetadataFilter? filter = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SyncResult
        {
            Success = false,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow,
            ErrorMessage = "Downstream sync is disabled"
        });
    }

    public Task SyncContentFromUpstreamAsync(ServiceMetadataFilter? filter = null, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<UpstreamSyncStatus> GetUpstreamSyncStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new UpstreamSyncStatus 
        { 
            IsHealthy = false,
            ErrorMessage = "Downstream sync is disabled"
        });
    }
}
