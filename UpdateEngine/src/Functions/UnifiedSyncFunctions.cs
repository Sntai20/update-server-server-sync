// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Functions;

using System.Text.Json;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.PackageGraph.Storage;
using Microsoft.PackageGraph.MicrosoftUpdate.Source;
using Microsoft.PackageGraph.MicrosoftUpdate.Metadata;
using UpdateEngine.Services;
using UpdateEngine.Models;
using System.Net;
using Azure.Storage.Blobs;

/// <summary>
/// Unified sync functions consolidating all synchronization operations.
/// Replaces 10+ separate functions with configurable sync type operations.
/// Supports HTTP triggers and scheduled timer triggers.
/// </summary>
public class UnifiedSyncFunctions
{
    private readonly ILogger<UnifiedSyncFunctions> logger;
    private readonly ISyncService syncService;
    private readonly IContentStore? contentStore;
    private readonly IAnomalyDetectionService? anomalyDetectionService;
    private readonly IMetadataStore? metadataStore;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly BlobServiceClient? blobServiceClient;
    private readonly IConfiguration configuration;

    public UnifiedSyncFunctions(
        ILogger<UnifiedSyncFunctions> logger,
        ISyncService syncService,
        JsonSerializerOptions jsonOptions,
        IConfiguration configuration,
        IContentStore? contentStore = null,
        IAnomalyDetectionService? anomalyDetectionService = null,
        IMetadataStore? metadataStore = null,
        BlobServiceClient? blobServiceClient = null)
    {
        this.logger = logger;
        this.syncService = syncService;
        this.contentStore = contentStore;
        this.anomalyDetectionService = anomalyDetectionService;
        this.metadataStore = metadataStore;
        this.jsonOptions = jsonOptions;
        this.blobServiceClient = blobServiceClient;
        this.configuration = configuration;
    }

    /// <summary>
    /// Universal sync endpoint supporting all sync types via HTTP.
    /// POST /api/UniversalSync
    /// Replaces: SyncMetadata, SyncContent, EmergencySync
    /// </summary>
    [Function("UniversalSync")]
    public async Task<HttpResponseData> UniversalSync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<UniversalSyncRequest>(requestBody ?? "{}", this.jsonOptions);

            var result = await this.ExecuteSyncOperation(request);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(result, this.jsonOptions));
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during universal sync");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }, this.jsonOptions), System.Text.Encoding.UTF8);
            return errorResponse;
        }
    }

    /// <summary>
    /// Scheduled comprehensive metadata sync (categories + updates).
    /// Default: Every 24 hours.
    /// Configure with SyncComprehensiveSchedule app setting (TimeSpan format).
    /// Timer trigger requires AzureWebJobsStorage to be configured.
    /// </summary>
    [Function("SyncComprehensive")]
    public async Task SyncComprehensive(
        [TimerTrigger("%SyncComprehensiveSchedule%")] TimerInfo timer)
    {
        this.logger.LogInformation("Starting scheduled comprehensive sync at {Time}", DateTime.UtcNow);

        try
        {
            var request = new UniversalSyncRequest
            {
                SyncType = "comprehensive",
                SyncContent = false // Critical content will be downloaded automatically
            };

            await this.ExecuteSyncOperation(request);

            this.logger.LogInformation("Scheduled comprehensive sync completed. Next run: {NextRun}", timer.ScheduleStatus?.Next);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during scheduled comprehensive sync");
            throw;
        }
    }

    /// <summary>
    /// Critical updates sync (security/critical classification).
    /// Default: Every 4 hours.
    /// Configure with SyncCriticalSchedule app setting (TimeSpan format).
    /// </summary>
    [Function("SyncCritical")]
    public async Task SyncCritical(
        [TimerTrigger("%SyncCriticalSchedule%")] TimerInfo timer)
    {
        this.logger.LogInformation("Starting scheduled critical updates sync at {Time}", DateTime.UtcNow);

        try
        {
            var request = new UniversalSyncRequest
            {
                SyncType = "critical"
            };

            await this.ExecuteSyncOperation(request);

            this.logger.LogInformation("Scheduled critical sync completed (with automatic content downloads). Next run: {NextRun}", timer.ScheduleStatus?.Next);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during scheduled critical sync");
            throw;
        }
    }

    /// <summary>
    /// Scheduled content sync for recently updated packages.
    /// Default: Every 24 hours.
    /// Configure with SyncContentSchedule app setting (TimeSpan format).
    /// Only runs if content store is configured.
    /// </summary>
    [Function("SyncContent")]
    public async Task SyncContent(
        [TimerTrigger("%SyncContentSchedule%")] TimerInfo timer)
    {
        if (this.contentStore == null)
        {
            this.logger.LogWarning("Content store not configured, skipping scheduled content sync");
            return;
        }

        this.logger.LogInformation("Starting scheduled content sync at {Time}", DateTime.UtcNow);

        try
        {
            var request = new UniversalSyncRequest
            {
                SyncType = "content",
                ContentDaysBack = 30  // Changed from 7 to 30 to match original
            };

            await this.ExecuteSyncOperation(request);

            this.logger.LogInformation("Scheduled content sync completed. Next run: {NextRun}", timer.ScheduleStatus?.Next);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during scheduled content sync");
            throw;
        }
    }

    /// <summary>
    /// Query content synchronization status.
    /// GET /api/QueryContentStatus
    /// Replaces: QueryContentStatus from ContentSyncFunctions
    /// </summary>
    [Function("QueryContentStatus")]
    public async Task<HttpResponseData> QueryContentStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        try
        {
            if (this.contentStore == null)
            {
                var noStoreResponse = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
                noStoreResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await noStoreResponse.WriteStringAsync(JsonSerializer.Serialize(new { configured = false, message = "Content store not configured" }, this.jsonOptions), System.Text.Encoding.UTF8);
                return noStoreResponse;
            }

            // Get content store statistics using available properties
            var stats = new
            {
                configured = true,
                queuedCount = this.contentStore.QueuedCount,
                downloadedSize = this.contentStore.DownloadedSize
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(stats, this.jsonOptions), System.Text.Encoding.UTF8);
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error querying content status");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }, this.jsonOptions), System.Text.Encoding.UTF8);
            return errorResponse;
        }
    }

    /// <summary>
    /// Sync Windows 11 critical and security updates with automatic content downloads.
    /// POST /api/SyncWindows11Critical
    /// Specialized endpoint for Windows 11 environments.
    /// </summary>
    [Function("SyncWindows11Critical")]
    public async Task<HttpResponseData> SyncWindows11Critical(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        try
        {
            this.logger.LogInformation("Starting Windows 11 critical updates sync with content downloads");

            var request = new UniversalSyncRequest
            {
                SyncType = "critical"
            };

            var result = await this.ExecuteSyncOperation(request);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(new 
            { 
                Success = result.Success,
                Message = "Windows 11 critical updates sync completed with automatic content downloads",
                UpdatesSynced = result.UpdatesSynced,
                ContentSynced = result.ContentSynced,
                StartTime = result.StartTime,
                EndTime = result.EndTime,
                Duration = result.EndTime - result.StartTime
            }, this.jsonOptions));
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during Windows 11 critical updates sync");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }, this.jsonOptions), System.Text.Encoding.UTF8);
            return errorResponse;
        }
    }

    /// <summary>
    /// Configure sync with custom language filters.
    /// POST /api/SyncWithLanguageFilter
    /// Body: { "syncType": "critical", "languageFilters": ["en", "en-US", "neutral"], "products": ["Windows 11"], "classifications": ["Security Updates"] }
    /// </summary>
    [Function("SyncWithLanguageFilter")]
    public async Task<HttpResponseData> SyncWithLanguageFilter(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = JsonSerializer.Deserialize<LanguageFilteredSyncRequest>(requestBody ?? "{}", this.jsonOptions);

            this.logger.LogInformation("Starting sync with language filters: Languages=[{Languages}], Products=[{Products}], Classifications=[{Classifications}]",
                string.Join(", ", request?.LanguageFilters ?? new List<string>()),
                string.Join(", ", request?.ProductFilters ?? new List<string>()),
                string.Join(", ", request?.ClassificationFilters ?? new List<string>()));

            var syncRequest = new UniversalSyncRequest
            {
                SyncType = request?.SyncType ?? "critical",
                SyncContent = request?.SyncContent ?? true,
                ContentDaysBack = request?.ContentDaysBack
            };

            var result = await this.ExecuteSyncOperationWithLanguageFilter(syncRequest, request);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(result, this.jsonOptions));
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during language-filtered sync");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }, this.jsonOptions), System.Text.Encoding.UTF8);
            return errorResponse;
        }
    }

    /// <summary>
    /// Core sync execution logic shared by HTTP and timer triggers.
    /// </summary>
    private async Task<SyncResult> ExecuteSyncOperation(UniversalSyncRequest? request)
    {
        var result = new SyncResult
        {
            StartTime = DateTime.UtcNow
        };

        try
        {
            switch (request?.SyncType?.ToLowerInvariant())
            {
                case "content":
                    await this.PerformContentSync(request.ContentDaysBack ?? 30);
                    result.ContentSynced = true;
                    break;

                case "emergency":
                case "critical":
                    await this.PerformCriticalSync();
                    // Automatically download content for critical updates
                    if (this.contentStore != null)
                    {
                        this.logger.LogInformation("Critical sync: automatically downloading content for critical updates");
                        await this.PerformCriticalContentSync();
                        result.ContentSynced = true;
                    }
                    result.UpdatesSynced = true;
                    break;

                case "comprehensive":
                    await this.PerformComprehensiveSync();
                    // Include content downloads by default for comprehensive sync
                    if (this.contentStore != null)
                    {
                        if (request.SyncContent == true) 
                        {
                            this.logger.LogInformation("Comprehensive sync with content explicitly enabled");
                            await this.PerformContentSync();
                        }
                        else
                        {
                            this.logger.LogInformation("Comprehensive sync: automatically downloading critical content");
                            await this.PerformCriticalContentSync();
                        }
                        result.ContentSynced = true;
                    }
                    else
                    {
                        this.logger.LogInformation("Comprehensive sync without content (content store not configured)");
                    }
                    result.CategoriesSynced = true;
                    result.UpdatesSynced = true;
                    break;

                case "full":
                    await this.PerformFullSync();
                    result.CategoriesSynced = true;
                    result.UpdatesSynced = true;
                    result.ContentSynced = true;
                    break;

                default:
                    throw new ArgumentException($"Unknown sync type: {request?.SyncType}");
            }

            result.EndTime = DateTime.UtcNow;
            result.Success = true;

            // Perform post-sync anomaly detection if services are available
            if (this.anomalyDetectionService != null && this.metadataStore != null)
            {
                await this.PerformPostSyncAnomalyDetection(request?.SyncType ?? "unknown", result.StartTime);
            }

            // Generate CSV export for sync audit trail
            try
            {
                await this.ExportSyncSummaryToCsv(request?.SyncType ?? "unknown");
            }
            catch (Exception csvEx)
            {
                this.logger.LogWarning(csvEx, "Failed to export sync summary CSV, but sync operation completed successfully");
            }
        }
        catch (Exception ex)
        {
            result.EndTime = DateTime.UtcNow;
            result.Success = false;
            result.ErrorMessage = ex.Message;
            this.logger.LogError(ex, "Sync operation failed: {SyncType}", request?.SyncType);
        }

        return result;
    }

    /// <summary>
    /// Sync execution with custom language filtering support.
    /// </summary>
    private async Task<SyncResult> ExecuteSyncOperationWithLanguageFilter(UniversalSyncRequest? request, LanguageFilteredSyncRequest? languageRequest)
    {
        var result = new SyncResult
        {
            StartTime = DateTime.UtcNow
        };

        try
        {
            switch (request?.SyncType?.ToLowerInvariant())
            {
                case "content":
                    await this.PerformContentSyncWithLanguageFilter(languageRequest);
                    result.ContentSynced = true;
                    break;

                case "emergency":
                case "critical":
                    await this.PerformCriticalSync();
                    // Automatically download content with language filtering
                    if (this.contentStore != null)
                    {
                        this.logger.LogInformation("Critical sync: downloading content with language filters");
                        await this.PerformCriticalContentSyncWithLanguageFilter(languageRequest);
                        result.ContentSynced = true;
                    }
                    result.UpdatesSynced = true;
                    break;

                case "comprehensive":
                    await this.PerformComprehensiveSync();
                    if (this.contentStore != null)
                    {
                        if (request.SyncContent == true) 
                        {
                            this.logger.LogInformation("Comprehensive sync with language-filtered content");
                            await this.PerformContentSyncWithLanguageFilter(languageRequest);
                        }
                        else
                        {
                            this.logger.LogInformation("Comprehensive sync with language-filtered critical content");
                            await this.PerformCriticalContentSyncWithLanguageFilter(languageRequest);
                        }
                        result.ContentSynced = true;
                    }
                    result.CategoriesSynced = true;
                    result.UpdatesSynced = true;
                    break;

                case "full":
                    await this.PerformComprehensiveSync();
                    await this.PerformContentSyncWithLanguageFilter(languageRequest);
                    result.CategoriesSynced = true;
                    result.UpdatesSynced = true;
                    result.ContentSynced = true;
                    break;

                default:
                    throw new ArgumentException($"Unknown sync type: {request?.SyncType}");
            }

            result.EndTime = DateTime.UtcNow;
            result.Success = true;

            // Generate CSV export for sync audit trail
            try
            {
                await this.ExportSyncSummaryToCsv(request?.SyncType ?? "unknown");
            }
            catch (Exception csvEx)
            {
                this.logger.LogWarning(csvEx, "Failed to export sync summary CSV, but sync operation completed successfully");
            }
        }
        catch (Exception ex)
        {
            result.EndTime = DateTime.UtcNow;
            result.Success = false;
            result.ErrorMessage = ex.Message;
            this.logger.LogError(ex, "Language-filtered sync operation failed: {SyncType}", request?.SyncType);
        }

        return result;
    }

    private async Task PerformCriticalSync()
    {
        var filter = this.syncService.CreateCriticalUpdatesFilter();
        await this.syncService.SyncUpdatesAsync(filter);
    }

    private async Task PerformComprehensiveSync()
    {
        if (await this.syncService.IsReindexingRequired())
        {
            this.logger.LogInformation("Store reindexing required, performing reindex");
            await this.syncService.ReindexStoreAsync();
        }

        await this.syncService.SyncCategoriesAsync();
        var filter = this.syncService.CreateComprehensiveUpdatesFilter();
        await this.syncService.SyncUpdatesAsync(filter);
    }

    private async Task PerformContentSync(int daysBack = 30)
    {
        if (this.contentStore == null)
        {
            this.logger.LogWarning("Content store not configured, skipping content sync");
            return;
        }

        this.logger.LogInformation("Starting content sync for updates from last {DaysBack} days (English and neutral languages)", daysBack);

        var filter = new ServiceMetadataFilter
        {
            LanguageFilters = new List<string> { "en", "en-US", "neutral", "" }, // English and language-neutral updates
            UpdatedAfter = DateTime.UtcNow.AddDays(-daysBack)
        };
        
        this.logger.LogInformation("Content sync filter: Languages=[{Languages}], UpdatedAfter = {UpdatedAfter}", 
            string.Join(", ", filter.LanguageFilters), 
            filter.UpdatedAfter);
        
        await this.syncService.SyncContentAsync(filter, this.contentStore);
        
        this.logger.LogInformation("Content sync completed");
    }

    private async Task PerformCriticalContentSync()
    {
        if (this.contentStore == null)
        {
            this.logger.LogWarning("Content store not configured, skipping critical content sync");
            return;
        }

        this.logger.LogInformation("Starting critical content sync for Windows 11 security and critical updates (English and neutral languages)");

        // Create filter for Windows 11 critical and security updates with English and neutral language support
        var filter = new ServiceMetadataFilter
        {
            ProductFilters = new List<string> { "Windows 11" },
            ClassificationFilters = new List<string> { "Security Updates", "Critical Updates", "Definition Updates" },
            LanguageFilters = new List<string> { "en", "en-US", "neutral", "" }, // English and language-neutral updates
            UpdatedAfter = DateTime.UtcNow.AddDays(-90) // Last 90 days for critical updates
        };
        
        this.logger.LogInformation("Critical content sync filter: Products=[{Products}], Classifications=[{Classifications}], Languages=[{Languages}], UpdatedAfter={UpdatedAfter}", 
            string.Join(", ", filter.ProductFilters), 
            string.Join(", ", filter.ClassificationFilters), 
            string.Join(", ", filter.LanguageFilters), 
            filter.UpdatedAfter);
        
        await this.syncService.SyncContentAsync(filter, this.contentStore);
        
        this.logger.LogInformation("Critical content sync completed");
    }

    private async Task PerformContentSyncWithLanguageFilter(LanguageFilteredSyncRequest? languageRequest)
    {
        if (this.contentStore == null)
        {
            this.logger.LogWarning("Content store not configured, skipping language-filtered content sync");
            return;
        }

        var daysBack = languageRequest?.ContentDaysBack ?? 30;
        var languages = languageRequest?.LanguageFilters ?? new List<string> { "en", "en-US", "neutral", "" };
        
        this.logger.LogInformation("Starting language-filtered content sync for updates from last {DaysBack} days", daysBack);

        var filter = new ServiceMetadataFilter
        {
            ProductFilters = languageRequest?.ProductFilters,
            ClassificationFilters = languageRequest?.ClassificationFilters,
            LanguageFilters = languages,
            UpdatedAfter = languageRequest?.UpdatedAfter ?? DateTime.UtcNow.AddDays(-daysBack),
            UpdatedBefore = languageRequest?.UpdatedBefore
        };
        
        this.logger.LogInformation("Language-filtered content sync filter: Products=[{Products}], Classifications=[{Classifications}], Languages=[{Languages}], UpdatedAfter={UpdatedAfter}", 
            string.Join(", ", filter.ProductFilters ?? new List<string>()),
            string.Join(", ", filter.ClassificationFilters ?? new List<string>()),
            string.Join(", ", filter.LanguageFilters), 
            filter.UpdatedAfter);
        
        await this.syncService.SyncContentAsync(filter, this.contentStore);
        
        this.logger.LogInformation("Language-filtered content sync completed");
    }

    private async Task PerformCriticalContentSyncWithLanguageFilter(LanguageFilteredSyncRequest? languageRequest)
    {
        if (this.contentStore == null)
        {
            this.logger.LogWarning("Content store not configured, skipping language-filtered critical content sync");
            return;
        }

        var languages = languageRequest?.LanguageFilters ?? new List<string> { "en", "en-US", "neutral", "" };
        var products = languageRequest?.ProductFilters ?? new List<string> { "Windows 11" };
        var classifications = languageRequest?.ClassificationFilters ?? new List<string> { "Security Updates", "Critical Updates", "Definition Updates" };
        
        this.logger.LogInformation("Starting language-filtered critical content sync");

        var filter = new ServiceMetadataFilter
        {
            ProductFilters = products,
            ClassificationFilters = classifications,
            LanguageFilters = languages,
            UpdatedAfter = languageRequest?.UpdatedAfter ?? DateTime.UtcNow.AddDays(-90),
            UpdatedBefore = languageRequest?.UpdatedBefore
        };
        
        this.logger.LogInformation("Language-filtered critical content sync filter: Products=[{Products}], Classifications=[{Classifications}], Languages=[{Languages}], UpdatedAfter={UpdatedAfter}", 
            string.Join(", ", filter.ProductFilters), 
            string.Join(", ", filter.ClassificationFilters), 
            string.Join(", ", filter.LanguageFilters), 
            filter.UpdatedAfter);
        
        await this.syncService.SyncContentAsync(filter, this.contentStore);
        
        this.logger.LogInformation("Language-filtered critical content sync completed");
    }

    private async Task PerformFullSync()
    {
        await this.PerformComprehensiveSync();
        await this.PerformContentSync();
    }

    private async Task PerformEmergencySync(string? reason, IEnumerable<string>? specificUpdateIds)
    {
        this.logger.LogWarning("Emergency sync reason: {Reason}", reason ?? "Not specified");

        if (specificUpdateIds?.Any() == true)
        {
            this.logger.LogInformation("Emergency sync for specific updates: {UpdateIds}",
                string.Join(", ", specificUpdateIds));
        }

        // Always perform critical updates in emergency
        var filter = this.syncService.CreateCriticalUpdatesFilter();
        await this.syncService.SyncUpdatesAsync(filter);
    }

    /// <summary>
    /// Perform anomaly detection on recently synchronized updates using Microsoft Update library capabilities.
    /// Leverages comprehensive metadata analysis including category resolution and applicability rules.
    /// </summary>
    private async Task PerformPostSyncAnomalyDetection(string syncType, DateTime syncStartTime)
    {
        if (this.anomalyDetectionService == null || this.metadataStore == null)
        {
            this.logger.LogDebug("Anomaly detection service or metadata store not available, skipping post-sync analysis");
            return;
        }

        this.logger.LogInformation("Performing post-sync anomaly detection for {SyncType} sync", syncType);

        try
        {
            // Get all packages from the metadata store and filter for software updates
            var allPackages = this.metadataStore.OfType<SoftwareUpdate>().ToList();
            
            // Filter for recent updates (would need metadata to determine recency)
            // For now, analyze recent packages based on available data
            var recentUpdates = allPackages.Take(100).ToList(); // Limit for performance

            if (!recentUpdates.Any())
            {
                this.logger.LogInformation("No software updates found in metadata store for post-sync anomaly detection");
                return;
            }

            this.logger.LogInformation("Analyzing {Count} software updates for anomalies", recentUpdates.Count);

            var anomalies = new List<(SoftwareUpdate Update, AnomalyDetectionResult Result)>();
            var normalCount = 0;
            
            foreach (var softwareUpdate in recentUpdates)
            {
                try
                {
                    // Use the SoftwareUpdate scoring method directly
                    var anomalyScore = this.anomalyDetectionService.Score(softwareUpdate);
                    
                    // Create detection result
                    var anomalyResult = new AnomalyDetectionResult
                    {
                        IsAnomaly = anomalyScore > 0.5, // Threshold for anomaly detection
                        Score = anomalyScore,
                        Message = anomalyScore > 0.8 ? "High-risk anomaly detected" : 
                                 anomalyScore > 0.5 ? "Medium-risk anomaly detected" : "Normal update"
                    };
                    
                    if (anomalyResult.IsAnomaly)
                    {
                        anomalies.Add((softwareUpdate, anomalyResult));
                    }
                    else
                    {
                        normalCount++;
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Error analyzing update {UpdateId} for anomalies", softwareUpdate.Id.ID);
                }
            }

            // Report findings
            if (anomalies.Any())
            {
                foreach (var (update, anomaly) in anomalies)
                {
                    this.logger.LogWarning("POST-SYNC ANOMALY DETECTED: {UpdateId} - {Title} (Score: {Score:F2}) - {Message}",
                        update.Id.ID, update.Title, anomaly.Score, anomaly.Message);
                }

                // Log summary
                var highRiskCount = anomalies.Count(a => a.Result.Score > 0.8);
                var mediumRiskCount = anomalies.Count(a => a.Result.Score > 0.5 && a.Result.Score <= 0.8);
                
                this.logger.LogWarning("Post-sync anomaly summary for {SyncType}: {HighRisk} high-risk, {MediumRisk} medium-risk anomalies detected from {Total} updates analyzed",
                    syncType, highRiskCount, mediumRiskCount, recentUpdates.Count);
            }
            else
            {
                this.logger.LogInformation("Post-sync anomaly detection completed for {SyncType}: No anomalies detected in {AnalyzedCount} updates ({NormalCount} normal)",
                    syncType, recentUpdates.Count, normalCount);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during post-sync anomaly detection for {SyncType}", syncType);
        }
    }

    /// <summary>
    /// Automatically export sync summary to CSV in blob storage.
    /// Called after successful sync operations to maintain audit trail.
    /// </summary>
    private async Task ExportSyncSummaryToCsv(string syncType)
    {
        if (this.blobServiceClient == null || this.metadataStore == null)
        {
            this.logger.LogDebug("Blob storage or metadata store not available, skipping CSV export");
            return;
        }

        try
        {
            this.logger.LogInformation("Exporting sync summary to CSV for sync type: {SyncType}", syncType);

            // Get container configuration
            var containerName = this.configuration["MetadataContainerName"] ?? "data";
            var pathPrefix = this.configuration["ContentPathPrefix"] ?? "";
            
            // Create CSV export
            var csvData = this.GenerateSyncSummaryCsv();
            var fileName = $"sync-summary-{syncType}-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}.csv";
            var blobPath = string.IsNullOrEmpty(pathPrefix) ? $"reports/{fileName}" : $"{pathPrefix.TrimEnd('/')}/reports/{fileName}";

            // Upload to blob storage
            var containerClient = this.blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();
            
            var blobClient = containerClient.GetBlobClient(blobPath);
            var csvBytes = Encoding.UTF8.GetBytes(csvData);
            await blobClient.UploadAsync(new BinaryData(csvBytes), overwrite: true);

            this.logger.LogInformation("Sync summary exported to: {BlobPath} ({FileSize} bytes)", blobPath, csvBytes.Length);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error exporting sync summary to CSV");
            throw;
        }
    }

    private string GenerateSyncSummaryCsv()
    {
        if (this.metadataStore == null)
        {
            throw new InvalidOperationException("Metadata store is not available");
        }

        // Create a filter that returns all packages
        var filter = new MetadataFilter
        {
            TitleFilter = string.Empty,
            HardwareIdFilter = string.Empty,
            SkipSuperseded = false,
            FirstX = 0
        };

        var packages = filter.Apply(this.metadataStore).ToList();
        var csv = new StringBuilder();
        
        // CSV Header
        csv.AppendLine("Id,Title,Type,HasContent,IsSuperseded,FileSize,FileCount");

        // CSV Data
        foreach (var package in packages)
        {
            var hasContent = false;
            var fileSize = 0L;
            var fileCount = 0;
            var isSuperseded = false;

            // Get additional metadata based on package type
            if (package is SoftwareUpdate softwareUpdate)
            {
                hasContent = softwareUpdate.Files?.Any() == true;
                fileSize = softwareUpdate.Files?.Sum(f => (long)f.Size) ?? 0;
                fileCount = softwareUpdate.Files?.Count() ?? 0;
                isSuperseded = softwareUpdate.IsSupersededBy?.Any() == true;
            }

            // Escape CSV fields
            var title = (package.Title ?? "").Replace("\"", "\"\"");
            var type = package.GetType().Name;

            csv.AppendLine($"\"{package.Id.OpenId}\",\"{title}\",\"{type}\",{hasContent},{isSuperseded},{fileSize},{fileCount}");
        }

        return csv.ToString();
    }
}