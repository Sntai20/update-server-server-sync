// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using UpdateEngine.Models;

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
            DomainReputation = metadata.DomainReputation,
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
            DomainReputation = m.DomainReputation,
            HashMatchScore = m.HashMatch ? 1.0f : 0.0f,
            UpdateFrequency = m.UpdateFrequency
        });

        var dataView = this.mlContext.Data.LoadFromEnumerable(features);

        // Build pipeline using RandomizedPCA for anomaly detection
        var pipeline = this.mlContext.Transforms.Concatenate("Features", 
                nameof(UpdateFeatures.FileSize),
                nameof(UpdateFeatures.IsSigned),
                nameof(UpdateFeatures.DomainReputation),
                nameof(UpdateFeatures.HashMatchScore),
                nameof(UpdateFeatures.UpdateFrequency))
            .Append(this.mlContext.Transforms.NormalizeMinMax("Features"))
            .Append(this.mlContext.AnomalyDetection.Trainers.RandomizedPca(
                featureColumnName: "Features",
                rank: 3,
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

// ML.NET data structures
public class UpdateFeatures
{
    public float FileSize { get; set; }
    public float IsSigned { get; set; }
    public float DomainReputation { get; set; }
    public float HashMatchScore { get; set; }
    public float UpdateFrequency { get; set; }
}

public class AnomalyPrediction
{
    [ColumnName("PredictedLabel")]
    public bool PredictedLabel { get; set; }

    [ColumnName("Score")]
    public float Score { get; set; }
}