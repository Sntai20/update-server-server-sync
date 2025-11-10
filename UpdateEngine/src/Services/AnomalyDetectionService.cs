// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using UpdateEngine.Models;
using Microsoft.PackageGraph.MicrosoftUpdate.Metadata;

/// <summary>
/// ML.NET-based anomaly detection for Windows Updates
/// Uses RandomizedPCA for unsupervised anomaly detection
/// </summary>
public class AnomalyDetectionService : IAnomalyDetectionService
{
    private readonly ILogger<AnomalyDetectionService> logger;
    private readonly MLContext mlContext;
    private ITransformer? model;
    private readonly string modelPath;
    private readonly double anomalyThreshold;
    private readonly bool enabled;

    public bool IsModelReady => this.model != null;

    public AnomalyDetectionService(
        ILogger<AnomalyDetectionService> logger,
        IConfiguration configuration)
    {
        this.logger = logger;
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
            UpdateFrequency = metadata.UpdateFrequency
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
            UpdateFrequency = metadata.UpdateFrequency
        };

        var predictionEngine = this.mlContext.Model.CreatePredictionEngine<UpdateFeatures, AnomalyPrediction>(this.model);
        var prediction = predictionEngine.Predict(input);

        return prediction.Score;
    }

    /// <summary>
    /// Scores a SoftwareUpdate for anomaly likelihood
    /// </summary>
    /// <param name="softwareUpdate">Software update to score</param>
    /// <returns>Anomaly score (0.0 = normal, 1.0 = highly anomalous)</returns>
    public double Score(SoftwareUpdate softwareUpdate)
    {
        var metadata = ConvertToUpdateMetadata(softwareUpdate);
        return Score(metadata);
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
    /// Leverages rich Microsoft Update library metadata
    /// </summary>
    private static UpdateMetadata ConvertToUpdateMetadata(SoftwareUpdate softwareUpdate)
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
            Classification = "Unknown", // Would be populated with category resolution
            Product = "Unknown" // Would be populated with category resolution
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
                nameof(UpdateFeatures.IsCumulativeUpdate))
            .Append(this.mlContext.Transforms.NormalizeMinMax("Features"))
            .Append(this.mlContext.AnomalyDetection.Trainers.RandomizedPca(
                featureColumnName: "Features",
                rank: 5, // Increased rank for more features
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
}

public class AnomalyPrediction
{
    [ColumnName("PredictedLabel")]
    public bool PredictedLabel { get; set; }

    [ColumnName("Score")]
    public float Score { get; set; }
}