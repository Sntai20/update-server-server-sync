// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using UpdateEngine.Models;
using Microsoft.PackageGraph.MicrosoftUpdate.Metadata;
using Microsoft.PackageGraph.MicrosoftUpdate.Metadata.Applicability;
using Microsoft.PackageGraph.Storage;

/// <summary>
/// ML.NET-based anomaly detection for Windows Updates
/// Uses RandomizedPCA for unsupervised anomaly detection
/// </summary>
public class AnomalyDetectionService : IAnomalyDetectionService
{
    private readonly ILogger<AnomalyDetectionService> logger;
    private readonly IMetadataStore metadataStore;
    private readonly MLContext mlContext;
    private ITransformer? model;
    private readonly string modelPath;
    private readonly double anomalyThreshold;
    private readonly bool enabled;
    private ILookup<Guid, MicrosoftUpdatePackage>? categoriesLookup;

    public bool IsModelReady => this.model != null;

    public AnomalyDetectionService(
        ILogger<AnomalyDetectionService> logger,
        IMetadataStore metadataStore,
        IConfiguration configuration)
    {
        this.logger = logger;
        this.metadataStore = metadataStore;
        this.mlContext = new MLContext(seed: 0);
        
        // Read configuration from appsettings
        this.enabled = configuration.GetValue<bool>("Features:EnableAnomalyDetection", false);
        this.modelPath = configuration["AnomalyDetection:ModelPath"] ?? "./anomaly-model.zip";
        this.anomalyThreshold = configuration.GetValue<double>("AnomalyDetection:AnomalyScoreThreshold", 0.85);
        
        if (!this.enabled)
        {
            this.logger.LogInformation("Anomaly detection is disabled");
            return;
        }

        // Try to load existing model
        if (File.Exists(this.modelPath))
        {
            try
            {
                this.model = this.mlContext.Model.Load(this.modelPath, out var _);
                this.logger.LogInformation("Loaded anomaly detection model from {Path}", this.modelPath);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to load anomaly detection model");
            }
        }
        else
        {
            this.logger.LogWarning("Anomaly detection model not found at {Path}", this.modelPath);
        }
    }

    /// <summary>
    /// Builds categories lookup from metadata store for enhanced category resolution
    /// Leverages Microsoft Update library OfType<> capabilities
    /// </summary>
    private ILookup<Guid, MicrosoftUpdatePackage> GetCategoriesLookup()
    {
        if (this.categoriesLookup == null)
        {
            this.logger.LogInformation("Building categories lookup from metadata store");
            
            // Use Microsoft Update library to efficiently query categories
            this.categoriesLookup = this.metadataStore
                .OfType<MicrosoftUpdatePackage>()
                .Where(p => p is ClassificationCategory || p is ProductCategory)
                .Where(p => p.Id?.OpenId != null && p.Id.OpenId.Length == 16) // Valid GUID IDs
                .ToLookup(p => new Guid(p.Id.OpenId));
                
            this.logger.LogInformation("Categories lookup built with {Count} categories", 
                this.categoriesLookup.Count());
        }
        
        return this.categoriesLookup;
    }

    public async Task<AnomalyDetectionResult> DetectAnomalyAsync(UpdateMetadata metadata)
    {
        if (!this.enabled)
        {
            return new AnomalyDetectionResult
            {
                IsAnomaly = false,
                Score = 0.0,
                Message = "Anomaly detection disabled"
            };
        }

        if (this.model == null)
        {
            this.logger.LogWarning("Anomaly detection model not trained yet");
            return new AnomalyDetectionResult
            {
                IsAnomaly = false,
                Score = 0.0,
                Message = "Model not trained"
            };
        }

        var input = new UpdateFeatures
        {
            FileSize = metadata.FileSize,
            IsSigned = metadata.IsSigned ? 1.0f : 0.0f,
            DomainReputation = ConvertDomainReputationToScore(metadata.DomainReputation),
            HashMatchScore = metadata.HashMatch ? 1.0f : 0.0f,
            UpdateFrequency = metadata.UpdateFrequency,
            
            // Enhanced features using Microsoft Update library data
            SupersededCount = metadata.SupersededCount,
            SupersededByCount = metadata.SupersededByCount,
            BundledUpdatesCount = metadata.BundledUpdatesCount,
            IsSecurityUpdate = metadata.IsSecurityUpdate ? 1.0f : 0.0f,
            IsCriticalUpdate = metadata.IsCriticalUpdate ? 1.0f : 0.0f,
            IsCumulativeUpdate = metadata.IsCumulativeUpdate ? 1.0f : 0.0f,
            ApplicabilityRulesCount = metadata.ApplicabilityRulesCount,
            HasComplexApplicability = metadata.HasComplexApplicability ? 1.0f : 0.0f
        };

        var predictionEngine = this.mlContext.Model.CreatePredictionEngine<UpdateFeatures, AnomalyPrediction>(this.model);
        var prediction = predictionEngine.Predict(input);

        var isAnomaly = prediction.Score > this.anomalyThreshold;
        
        return await Task.FromResult(new AnomalyDetectionResult
        {
            IsAnomaly = isAnomaly,
            Score = prediction.Score,
            Message = isAnomaly 
                ? $"Anomaly detected with score {prediction.Score:F3} (threshold: {this.anomalyThreshold:F3})" 
                : $"Normal update (score: {prediction.Score:F3})"
        });
    }

    public double Score(UpdateMetadata metadata)
    {
        if (!this.enabled || this.model == null)
        {
            return 0.0;
        }

        var input = new UpdateFeatures
        {
            FileSize = metadata.FileSize,
            IsSigned = metadata.IsSigned ? 1.0f : 0.0f,
            DomainReputation = ConvertDomainReputationToScore(metadata.DomainReputation),
            HashMatchScore = metadata.HashMatch ? 1.0f : 0.0f,
            UpdateFrequency = metadata.UpdateFrequency,
            
            // Enhanced features using Microsoft Update library data
            SupersededCount = metadata.SupersededCount,
            SupersededByCount = metadata.SupersededByCount,
            BundledUpdatesCount = metadata.BundledUpdatesCount,
            IsSecurityUpdate = metadata.IsSecurityUpdate ? 1.0f : 0.0f,
            IsCriticalUpdate = metadata.IsCriticalUpdate ? 1.0f : 0.0f,
            IsCumulativeUpdate = metadata.IsCumulativeUpdate ? 1.0f : 0.0f,
            ApplicabilityRulesCount = metadata.ApplicabilityRulesCount,
            HasComplexApplicability = metadata.HasComplexApplicability ? 1.0f : 0.0f
        };

        var predictionEngine = this.mlContext.Model.CreatePredictionEngine<UpdateFeatures, AnomalyPrediction>(this.model);
        var prediction = predictionEngine.Predict(input);

        return prediction.Score;
    }

    /// <summary>
    /// Scores a SoftwareUpdate for anomaly likelihood using rich Microsoft Update library metadata
    /// </summary>
    /// <param name="softwareUpdate">Software update to score</param>
    /// <returns>Anomaly score (0.0 = normal, 1.0 = highly anomalous)</returns>
    public double Score(SoftwareUpdate softwareUpdate)
    {
        try
        {
            var categoriesLookup = GetCategoriesLookup();
            var metadata = ConvertToUpdateMetadata(softwareUpdate, categoriesLookup);
            return Score(metadata);
        }
        catch (Exception ex) when (ex.Message.Contains("Unknown expression type"))
        {
            // Some updates have newer expression types that aren't supported yet
            // Return a neutral score for anomaly detection
            this.logger.LogDebug("Returning neutral anomaly score for update {UpdateId} due to unsupported expression type: {Error}", 
                softwareUpdate.Id?.ID, ex.Message);
            return 0.0; // Normal score
        }
        catch (Exception ex) when (ex.Message.Contains("not found"))
        {
            // Package metadata is missing from storage - this can happen during partial sync
            // Return a neutral score for anomaly detection
            this.logger.LogDebug("Returning neutral anomaly score for update {UpdateId} due to missing package metadata: {Error}", 
                softwareUpdate.Id?.ID, ex.Message);
            return 0.0; // Normal score
        }
        catch (Exception ex)
        {
            // Log other errors but don't fail the entire sync process
            this.logger.LogWarning(ex, "Error scoring update {UpdateId} for anomalies, returning neutral score", 
                softwareUpdate.Id?.ID);
            return 0.0; // Normal score
        }
    }

    /// <summary>
    /// Converts string domain reputation to a numeric score for ML processing
    /// </summary>
    private static float ConvertDomainReputationToScore(string domainReputation)
    {
        return domainReputation?.ToLowerInvariant() switch
        {
            "trusted" => 1.0f,
            "good" => 0.8f,
            "neutral" => 0.5f,
            "suspicious" => 0.2f,
            "malicious" => 0.0f,
            _ => 0.5f // Default to neutral for unknown values
        };
    }

    /// <summary>
    /// Converts a SoftwareUpdate to UpdateMetadata for anomaly detection
    /// Leverages rich Microsoft Update library metadata including category resolution
    /// </summary>
    private UpdateMetadata ConvertToUpdateMetadata(SoftwareUpdate softwareUpdate, ILookup<Guid, MicrosoftUpdatePackage>? categoriesLookup = null)
    {
        // Calculate update frequency based on supersedence relationships
        var updateFrequency = CalculateUpdateFrequency(softwareUpdate);
        
        // Use rich file metadata - check if files have cryptographic signatures
        var hasSignedFiles = softwareUpdate.Files?.Any(f => 
            f.Digest?.Algorithm?.Contains("SHA", StringComparison.OrdinalIgnoreCase) == true) ?? true;
        
        // Analyze title for update characteristics
        var title = softwareUpdate.Title?.ToLowerInvariant() ?? "";
        var isSecurityUpdate = title.Contains("security") || title.Contains("vulnerability");
        var isCriticalUpdate = title.Contains("critical") || title.Contains("important");
        var isCumulativeUpdate = title.Contains("cumulative");
        
        // Get supersedence counts from Microsoft Update library
        var supersededCount = softwareUpdate.SupersededUpdates?.Count ?? 0;
        var supersededByCount = softwareUpdate.IsSupersededBy?.Count ?? 0;
        var bundledCount = softwareUpdate.BundledUpdates?.Count ?? 0;
        
        // Analyze applicability rules for complexity (anomaly indicator)
        var applicabilityRulesCount = 0;
        var hasComplexApplicability = false;
        
        try
        {
            applicabilityRulesCount = softwareUpdate.ApplicabilityRules?.Count ?? 0;
            hasComplexApplicability = applicabilityRulesCount > 5 || 
                softwareUpdate.ApplicabilityRules?.Any(rule => 
                    rule.RuleType == ApplicabilityRuleType.WindowsDriver ||
                    rule.RuleType == ApplicabilityRuleType.MsiApplicationMetadata) == true;
        }
        catch (Exception ex) when (ex.Message.Contains("Unknown expression type"))
        {
            // Some updates have newer expression types that aren't supported yet
            // Default to safe values for anomaly detection
            this.logger.LogDebug("Skipping applicability analysis for update {UpdateId} due to unsupported expression type: {Error}", 
                softwareUpdate.Id?.ID, ex.Message);
            applicabilityRulesCount = 0;
            hasComplexApplicability = false;
        }
        catch (Exception ex) when (ex.Message.Contains("not found"))
        {
            // Package metadata is missing from storage - this can happen during partial sync
            // Default to safe values for anomaly detection
            this.logger.LogDebug("Skipping applicability analysis for update {UpdateId} due to missing package metadata: {Error}", 
                softwareUpdate.Id?.ID, ex.Message);
            applicabilityRulesCount = 0;
            hasComplexApplicability = false;
        }
        
        // Leverage Microsoft Update library category resolution
        string classification = "Unknown";
        string product = "Unknown";
        
        if (categoriesLookup != null)
        {
            var categories = softwareUpdate.GetCategories(categoriesLookup);
            if (categories != null)
            {
                var classificationCategory = categories.OfType<ClassificationCategory>().FirstOrDefault();
                var productCategory = categories.OfType<ProductCategory>().FirstOrDefault();
                
                classification = classificationCategory?.Title ?? "Unknown";
                product = productCategory?.Title ?? "Unknown";
                
                // Enhance security/critical detection using actual classifications
                if (classification.Contains("Security", StringComparison.OrdinalIgnoreCase))
                    isSecurityUpdate = true;
                if (classification.Contains("Critical", StringComparison.OrdinalIgnoreCase))
                    isCriticalUpdate = true;
            }
        }
        
        return new UpdateMetadata
        {
            KB_ID = !string.IsNullOrEmpty(softwareUpdate.KBArticleId) 
                ? $"KB{softwareUpdate.KBArticleId}" 
                : "KB_NoArticle",
            Publisher = "Microsoft", // All updates in this store are from Microsoft
            HashMatch = true, // Assume true for existing updates in store
            IsSigned = hasSignedFiles, // Use digest information to infer signing
            FileSize = softwareUpdate.Files?.Sum(f => (long)f.Size) ?? 0,
            DomainReputation = "Trusted", // Microsoft is trusted
            UpdateFrequency = updateFrequency,
            
            // Enhanced fields using Microsoft Update library data
            SupersededCount = supersededCount,
            SupersededByCount = supersededByCount,
            BundledUpdatesCount = bundledCount,
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
    /// Calculates update frequency based on supersedence relationships and metadata
    /// Uses Microsoft Update library supersedence data
    /// </summary>
    private static float CalculateUpdateFrequency(SoftwareUpdate softwareUpdate)
    {
        // Base frequency
        float frequency = 0.5f;
        
        // Higher frequency if this update supersedes many others (major update)
        var supersededCount = softwareUpdate.SupersededUpdates?.Count ?? 0;
        if (supersededCount > 10) frequency += 0.3f;
        else if (supersededCount > 5) frequency += 0.2f;
        else if (supersededCount > 0) frequency += 0.1f;
        
        // Lower frequency if superseded by many updates (older/less relevant)
        var supersededByCount = softwareUpdate.IsSupersededBy?.Count ?? 0;
        if (supersededByCount > 3) frequency -= 0.2f;
        else if (supersededByCount > 1) frequency -= 0.1f;
        
        // Higher frequency for bundled updates (typically important)
        var bundledCount = softwareUpdate.BundledUpdates?.Count ?? 0;
        if (bundledCount > 0) frequency += 0.15f;
        
        // Analyze title for importance indicators
        var title = softwareUpdate.Title?.ToLowerInvariant() ?? "";
        if (title.Contains("security") || title.Contains("critical")) frequency += 0.2f;
        if (title.Contains("cumulative")) frequency += 0.15f;
        if (title.Contains("preview") || title.Contains("beta")) frequency -= 0.1f;
        
        return Math.Max(0.0f, Math.Min(1.0f, frequency)); // Clamp to [0,1]
    }

    public async Task TrainModelAsync(IEnumerable<UpdateMetadata> trainingData)
    {
        if (!this.enabled)
        {
            this.logger.LogWarning("Cannot train model - anomaly detection is disabled");
            return;
        }

        var dataList = trainingData.ToList();
        this.logger.LogInformation("Training anomaly detection model with {Count} samples", dataList.Count);

        if (dataList.Count < 100)
        {
            this.logger.LogWarning("Training data size ({Count}) is below recommended minimum (100)", dataList.Count);
        }

        var features = dataList.Select(m => new UpdateFeatures
        {
            FileSize = m.FileSize,
            IsSigned = m.IsSigned ? 1.0f : 0.0f,
            DomainReputation = ConvertDomainReputationToScore(m.DomainReputation),
            HashMatchScore = m.HashMatch ? 1.0f : 0.0f,
            UpdateFrequency = m.UpdateFrequency,
            
            // Enhanced features leveraging Microsoft Update library
            SupersededCount = m.SupersededCount,
            SupersededByCount = m.SupersededByCount,
            BundledUpdatesCount = m.BundledUpdatesCount,
            IsSecurityUpdate = m.IsSecurityUpdate ? 1.0f : 0.0f,
            IsCriticalUpdate = m.IsCriticalUpdate ? 1.0f : 0.0f,
            IsCumulativeUpdate = m.IsCumulativeUpdate ? 1.0f : 0.0f
        });

        var dataView = this.mlContext.Data.LoadFromEnumerable(features);

        // Enhanced pipeline using more Microsoft Update library features
        var pipeline = this.mlContext.Transforms.Concatenate("Features", 
                nameof(UpdateFeatures.FileSize),
                nameof(UpdateFeatures.IsSigned),
                nameof(UpdateFeatures.DomainReputation),
                nameof(UpdateFeatures.HashMatchScore),
                nameof(UpdateFeatures.UpdateFrequency),
                nameof(UpdateFeatures.SupersededCount),
                nameof(UpdateFeatures.SupersededByCount),
                nameof(UpdateFeatures.BundledUpdatesCount),
                nameof(UpdateFeatures.IsSecurityUpdate),
                nameof(UpdateFeatures.IsCriticalUpdate),
                nameof(UpdateFeatures.IsCumulativeUpdate),
                nameof(UpdateFeatures.ApplicabilityRulesCount),
                nameof(UpdateFeatures.HasComplexApplicability))
            .Append(this.mlContext.Transforms.NormalizeMinMax("Features"))
            .Append(this.mlContext.AnomalyDetection.Trainers.RandomizedPca(
                featureColumnName: "Features",
                rank: 6, // Increased rank for 13 features (roughly half)
                ensureZeroMean: true,
                oversampling: 20));

        this.model = pipeline.Fit(dataView);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(this.modelPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Save model to disk
        this.mlContext.Model.Save(this.model, dataView.Schema, this.modelPath);
        this.logger.LogInformation("Anomaly detection model trained and saved to {Path}", this.modelPath);

        await Task.CompletedTask;
    }
}

// Enhanced ML.NET data structures leveraging Microsoft Update library
public class UpdateFeatures
{
    public float FileSize { get; set; }
    public float IsSigned { get; set; }
    public float DomainReputation { get; set; }
    public float HashMatchScore { get; set; }
    public float UpdateFrequency { get; set; }
    
    // Additional features from Microsoft Update library
    public float SupersededCount { get; set; }
    public float SupersededByCount { get; set; }
    public float BundledUpdatesCount { get; set; }
    public float IsSecurityUpdate { get; set; }
    public float IsCriticalUpdate { get; set; }
    public float IsCumulativeUpdate { get; set; }
    public float ApplicabilityRulesCount { get; set; }
    public float HasComplexApplicability { get; set; }
}

public class AnomalyPrediction
{
    [ColumnName("PredictedLabel")]
    public bool PredictedLabel { get; set; }

    [ColumnName("Score")]
    public float Score { get; set; }
}