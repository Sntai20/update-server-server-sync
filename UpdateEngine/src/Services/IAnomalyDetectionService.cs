// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Services;

using UpdateEngine.Models;

/// <summary>
/// Service for detecting anomalies in Windows Update metadata using ML.NET
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
    /// Trains the anomaly detection model from historical data
    /// </summary>
    /// <param name="trainingData">Historical update metadata for training</param>
    Task TrainModelAsync(IEnumerable<UpdateMetadata> trainingData);
    
    /// <summary>
    /// Checks if the model is loaded and ready
    /// </summary>
    bool IsModelReady { get; }
}