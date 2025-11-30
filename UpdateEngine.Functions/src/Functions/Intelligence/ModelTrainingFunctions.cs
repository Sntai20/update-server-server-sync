// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Functions.Intelligence;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using UpdateEngine.Core.Services;
using UpdateEngine.Core.Models;
using UpdateEngine.Metadata.Metadata;
using UpdateEngine.Metadata.Storage;

/// <summary>
/// Azure Function for training the ML.NET anomaly detection model.
/// Extracts features from metadata store and trains the model.
/// </summary>
public class ModelTrainingFunctions
{
    private readonly ILogger<ModelTrainingFunctions> logger;
    private readonly IAnomalyDetectionService anomalyService;
    private readonly IMetadataStore metadataStore;

    public ModelTrainingFunctions(
        ILogger<ModelTrainingFunctions> logger,
        IAnomalyDetectionService anomalyService,
        IMetadataStore metadataStore)
    {
        this.logger = logger;
        this.anomalyService = anomalyService;
        this.metadataStore = metadataStore;
    }

    /// <summary>
    /// HTTP endpoint for training the anomaly detection model.
    /// POST /api/train-model?sampleSize=1000
    /// Extracts features from metadata store and trains ML.NET model.
    /// </summary>
    [Function("TrainModel")]
    public async Task<HttpResponseData> TrainModel(
        [HttpTrigger(AuthorizationLevel.Admin, "post")] HttpRequestData req)
    {
        this.logger.LogInformation("Model training initiated via HTTP trigger");

        try
        {
            // Parse sample size from query string (default: 1000)
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var sampleSizeStr = query["sampleSize"] ?? "1000";
            if (!int.TryParse(sampleSizeStr, out int sampleSize) || sampleSize < 100)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid sampleSize. Must be >= 100");
                return badRequest;
            }

            // Extract training data from metadata store
            this.logger.LogInformation("Extracting {SampleSize} samples from metadata store", sampleSize);
            
            var trainingData = new List<UpdateMetadata>();
            var categoriesLookup = this.metadataStore
                .OfType<MicrosoftUpdatePackage>()
                .Where(p => p is ClassificationCategory || p is ProductCategory)
                .Where(p => p.Id?.OpenId != null && p.Id.OpenId.Length == 16)
                .ToLookup(p => new Guid(p.Id.OpenId));

            var softwareUpdates = this.metadataStore
                .OfType<SoftwareUpdate>()
                .Take(sampleSize)
                .ToList();

            this.logger.LogInformation("Found {Count} software updates in metadata store", softwareUpdates.Count);

            foreach (var update in softwareUpdates)
            {
                try
                {
                    var metadata = ConvertToUpdateMetadata(update, categoriesLookup);
                    trainingData.Add(metadata);
                }
                catch (Exception ex)
                {
                    this.logger.LogDebug("Skipping update {UpdateId} due to conversion error: {Error}",
                        update.Id?.ID, ex.Message);
                }
            }

            if (trainingData.Count < 100)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync($"Insufficient training data. Found {trainingData.Count} samples, need at least 100");
                return badRequest;
            }

            // Train the model
            this.logger.LogInformation("Training model with {Count} samples", trainingData.Count);
            var startTime = DateTime.UtcNow;
            
            await this.anomalyService.TrainModelAsync(trainingData);
            
            var duration = DateTime.UtcNow - startTime;
            this.logger.LogInformation("Model training completed in {Duration:F2}s", duration.TotalSeconds);

            // Return success response
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(System.Text.Json.JsonSerializer.Serialize(new
            {
                status = "success",
                message = "Model trained successfully",
                samplesUsed = trainingData.Count,
                durationSeconds = duration.TotalSeconds,
                timestamp = DateTime.UtcNow
            }));
            return response;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error training anomaly detection model");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Internal error: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Timer-triggered function for automatic model retraining.
    /// Runs on a schedule (e.g., weekly) to keep model up-to-date.
    /// Configure with ModelTrainingSchedule app setting.
    /// </summary>
    [Function("ScheduledModelTraining")]
    public async Task RunScheduledModelTraining(
        [TimerTrigger("%ModelTrainingSchedule%")] TimerInfo timer)
    {
        this.logger.LogInformation("Scheduled model training triggered at {Time}", DateTime.UtcNow);

        try
        {
            // Extract training data from metadata store (last 7 days of updates)
            var trainingData = new List<UpdateMetadata>();
            var categoriesLookup = this.metadataStore
                .OfType<MicrosoftUpdatePackage>()
                .Where(p => p is ClassificationCategory || p is ProductCategory)
                .Where(p => p.Id?.OpenId != null && p.Id.OpenId.Length == 16)
                .ToLookup(p => new Guid(p.Id.OpenId));

            // Get recent software updates for training
            var softwareUpdates = this.metadataStore
                .OfType<SoftwareUpdate>()
                .Take(2000) // Use more samples for scheduled training
                .ToList();

            this.logger.LogInformation("Extracting features from {Count} updates", softwareUpdates.Count);

            foreach (var update in softwareUpdates)
            {
                try
                {
                    var metadata = ConvertToUpdateMetadata(update, categoriesLookup);
                    trainingData.Add(metadata);
                }
                catch (Exception ex)
                {
                    this.logger.LogDebug("Skipping update {UpdateId}: {Error}", 
                        update.Id?.ID, ex.Message);
                }
            }

            if (trainingData.Count >= 100)
            {
                this.logger.LogInformation("Training model with {Count} samples", trainingData.Count);
                await this.anomalyService.TrainModelAsync(trainingData);
                this.logger.LogInformation("Scheduled model training completed successfully");
            }
            else
            {
                this.logger.LogWarning("Insufficient training data: {Count} samples (need 100+)", 
                    trainingData.Count);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during scheduled model training");
        }
    }

    /// <summary>
    /// Converts a SoftwareUpdate to UpdateMetadata for training.
    /// </summary>
    private UpdateMetadata ConvertToUpdateMetadata(
        SoftwareUpdate update, 
        ILookup<Guid, MicrosoftUpdatePackage> categoriesLookup)
    {
        var title = update.Title?.ToLowerInvariant() ?? "";
        
        // Extract categories
        var categories = update.GetCategories(categoriesLookup);
        var classification = categories?.OfType<ClassificationCategory>().FirstOrDefault()?.Title ?? "Unknown";
        var product = categories?.OfType<ProductCategory>().FirstOrDefault()?.Title ?? "Unknown";
        
        // Analyze update characteristics
        var isSecurityUpdate = title.Contains("security") || 
            classification.Contains("Security", StringComparison.OrdinalIgnoreCase);
        var isCriticalUpdate = title.Contains("critical") || 
            classification.Contains("Critical", StringComparison.OrdinalIgnoreCase);
        var isCumulativeUpdate = title.Contains("cumulative");
        
        // Get applicability rules count (with error handling)
        int applicabilityRulesCount = 0;
        bool hasComplexApplicability = false;
        try
        {
            applicabilityRulesCount = update.ApplicabilityRules?.Count ?? 0;
            hasComplexApplicability = applicabilityRulesCount > 5;
        }
        catch
        {
            // Ignore errors accessing applicability rules
        }
        
        return new UpdateMetadata
        {
            KB_ID = !string.IsNullOrEmpty(update.KBArticleId) ? $"KB{update.KBArticleId}" : "KB_NoArticle",
            Publisher = "Microsoft",
            HashMatch = true,
            IsSigned = update.Files?.Any(f => f.Digest?.Algorithm?.Contains("SHA") == true) ?? true,
            FileSize = update.Files?.Sum(f => (long)f.Size) ?? 0,
            DomainReputation = "Trusted",
            UpdateFrequency = CalculateUpdateFrequency(update),
            SupersededCount = update.SupersededUpdates?.Count ?? 0,
            SupersededByCount = update.IsSupersededBy?.Count ?? 0,
            BundledUpdatesCount = update.BundledUpdates?.Count ?? 0,
            IsSecurityUpdate = isSecurityUpdate,
            IsCriticalUpdate = isCriticalUpdate,
            IsCumulativeUpdate = isCumulativeUpdate,
            Classification = classification,
            Product = product,
            ApplicabilityRulesCount = applicabilityRulesCount,
            HasComplexApplicability = hasComplexApplicability
        };
    }

    /// <summary>
    /// Calculates update frequency score based on supersedence and metadata.
    /// </summary>
    private static float CalculateUpdateFrequency(SoftwareUpdate update)
    {
        float frequency = 0.5f;
        
        var supersededCount = update.SupersededUpdates?.Count ?? 0;
        if (supersededCount > 10) frequency += 0.3f;
        else if (supersededCount > 5) frequency += 0.2f;
        else if (supersededCount > 0) frequency += 0.1f;
        
        var supersededByCount = update.IsSupersededBy?.Count ?? 0;
        if (supersededByCount > 3) frequency -= 0.2f;
        else if (supersededByCount > 1) frequency -= 0.1f;
        
        var bundledCount = update.BundledUpdates?.Count ?? 0;
        if (bundledCount > 0) frequency += 0.15f;
        
        var title = update.Title?.ToLowerInvariant() ?? "";
        if (title.Contains("security") || title.Contains("critical")) frequency += 0.2f;
        if (title.Contains("cumulative")) frequency += 0.15f;
        if (title.Contains("preview") || title.Contains("beta")) frequency -= 0.1f;
        
        return Math.Max(0.0f, Math.Min(1.0f, frequency));
    }
}
