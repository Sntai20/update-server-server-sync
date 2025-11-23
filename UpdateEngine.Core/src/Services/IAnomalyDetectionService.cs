// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core.Services;

using UpdateEngine.Metadata.Metadata;
using UpdateEngine.Core.Models;

/// <summary>
/// Service for detecting anomalies in Windows Update metadata using ML.NET
/// Leverages rich Microsoft Update library capabilities for enhanced feature engineering
/// </summary>
public interface IAnomalyDetectionService
{
    /// <summary>
    /// Analyzes update metadata for anomalies
    /// </summary>
    /// <param name="metadata">Update metadata to analyze</param>
    /// <returns>Anomaly detection result with score and classification</returns>
    Task<AnomalyDetectionResult> DetectAnomalyAsync(UpdateMetadata metadata);
    
    /// <summary>
    /// Scores update metadata for anomaly likelihood
    /// </summary>
    /// <param name="metadata">Update metadata to score</param>
    /// <returns>Anomaly score (0.0 = normal, 1.0 = highly anomalous)</returns>
    double Score(UpdateMetadata metadata);
    
    /// <summary>
    /// Scores a SoftwareUpdate for anomaly likelihood
    /// Leverages Microsoft Update library metadata including supersedence, bundling, and categorization
    /// </summary>
    /// <param name="softwareUpdate">Software update to score</param>
    /// <returns>Anomaly score (0.0 = normal, 1.0 = highly anomalous)</returns>
    double Score(SoftwareUpdate softwareUpdate);
    
    /// <summary>
    /// Trains the anomaly detection model from historical data
    /// </summary>
    /// <param name="trainingData">Historical update metadata for training</param>
    Task TrainModelAsync(IEnumerable<UpdateMetadata> trainingData);
    
    /// <summary>
    /// Checks if the model is loaded and ready
    /// </summary>
    bool IsModelReady { get; }
}