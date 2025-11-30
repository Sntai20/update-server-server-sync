// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Core.Metrics;

using System.Diagnostics.Metrics;

/// <summary>
/// OpenTelemetry metrics for ML.NET-based anomaly detection system.
/// Tracks detection operations, model performance, and feature extraction.
/// </summary>
public static class AnomalyDetectionMetrics
{
    private static readonly Meter Meter = new("UpdateEngine.AnomalyDetection", "1.0.0");

    // ===== Core Detection Metrics =====
    
    /// <summary>
    /// Total number of anomalies detected (score > threshold).
    /// Tags: severity (high|medium|low), classification, product
    /// </summary>
    public static readonly Counter<long> AnomaliesDetected = Meter.CreateCounter<long>(
        "anomaly_detection.anomalies_detected",
        description: "Total number of anomalies detected above threshold",
        unit: "anomalies");

    /// <summary>
    /// Total number of updates successfully scored for anomalies.
    /// Tags: result (normal|anomaly|error)
    /// </summary>
    public static readonly Counter<long> UpdatesScored = Meter.CreateCounter<long>(
        "anomaly_detection.updates_scored",
        description: "Total number of updates scored for anomalies",
        unit: "updates");

    /// <summary>
    /// Number of errors encountered during anomaly scoring.
    /// Tags: error_type (unsupported_expression|storage_access|unknown)
    /// </summary>
    public static readonly Counter<long> ScoringErrors = Meter.CreateCounter<long>(
        "anomaly_detection.scoring_errors",
        description: "Number of errors during anomaly scoring",
        unit: "errors");

    /// <summary>
    /// Distribution of anomaly scores (0.0 - 1.0).
    /// Buckets: 0-0.2 (normal), 0.2-0.5 (suspicious), 0.5-0.8 (concerning), 0.8-1.0 (anomaly)
    /// </summary>
    public static readonly Histogram<double> AnomalyScore = Meter.CreateHistogram<double>(
        "anomaly_detection.score",
        description: "Distribution of anomaly scores for all updates",
        unit: "score");

    /// <summary>
    /// Time taken to score a single update for anomalies.
    /// Tags: has_model (true|false)
    /// </summary>
    public static readonly Histogram<double> DetectionDuration = Meter.CreateHistogram<double>(
        "anomaly_detection.detection_duration",
        description: "Time taken to detect anomalies per update",
        unit: "ms");

    // ===== Model Management Metrics =====

    /// <summary>
    /// Number of times the ML.NET model was successfully loaded from disk.
    /// Tags: source (startup|reload)
    /// </summary>
    public static readonly Counter<long> ModelLoaded = Meter.CreateCounter<long>(
        "anomaly_detection.model_loaded",
        description: "Number of times model was successfully loaded",
        unit: "loads");

    /// <summary>
    /// Number of model training operations started.
    /// Tags: training_size (small|medium|large)
    /// </summary>
    public static readonly Counter<long> ModelTrainingStarted = Meter.CreateCounter<long>(
        "anomaly_detection.model_training_started",
        description: "Number of model training operations started",
        unit: "trainings");

    /// <summary>
    /// Number of model training operations completed successfully.
    /// Tags: sample_count, training_size
    /// </summary>
    public static readonly Counter<long> ModelTrainingCompleted = Meter.CreateCounter<long>(
        "anomaly_detection.model_training_completed",
        description: "Number of model training operations completed",
        unit: "trainings");

    /// <summary>
    /// Time taken to train the ML.NET model.
    /// Tags: sample_count
    /// </summary>
    public static readonly Histogram<double> ModelTrainingDuration = Meter.CreateHistogram<double>(
        "anomaly_detection.model_training_duration",
        description: "Time taken to train the anomaly detection model",
        unit: "seconds");

    // ===== Queue Operations Metrics =====

    /// <summary>
    /// Number of anomaly events successfully enqueued to Azure Storage Queue.
    /// Tags: kb_id, score_range (high|medium|low)
    /// </summary>
    public static readonly Counter<long> EventsEnqueued = Meter.CreateCounter<long>(
        "anomaly_detection.events_enqueued",
        description: "Number of anomaly events enqueued for processing",
        unit: "events");

    /// <summary>
    /// Number of queue enqueue failures.
    /// Tags: error_type
    /// </summary>
    public static readonly Counter<long> EventsEnqueueFailed = Meter.CreateCounter<long>(
        "anomaly_detection.events_enqueue_failed",
        description: "Number of failed event enqueue operations",
        unit: "failures");

    // ===== Feature Extraction Metrics =====

    /// <summary>
    /// Number of successful category resolutions using categories lookup.
    /// Tags: category_type (classification|product)
    /// </summary>
    public static readonly Counter<long> CategoryResolutionSuccess = Meter.CreateCounter<long>(
        "anomaly_detection.category_resolution_success",
        description: "Number of successful category resolutions",
        unit: "resolutions");

    /// <summary>
    /// Number of failed category resolutions (missing or invalid categories).
    /// Tags: reason (invalid_guid|not_found|lookup_error)
    /// </summary>
    public static readonly Counter<long> CategoryResolutionFailures = Meter.CreateCounter<long>(
        "anomaly_detection.category_resolution_failures",
        description: "Number of failed category resolutions",
        unit: "failures");

    /// <summary>
    /// Number of updates with unsupported applicability expression types.
    /// Tags: update_type
    /// </summary>
    public static readonly Counter<long> UnsupportedExpressionTypes = Meter.CreateCounter<long>(
        "anomaly_detection.unsupported_expressions",
        description: "Updates with unsupported applicability expression types",
        unit: "updates");

    /// <summary>
    /// Number of storage access issues during feature extraction.
    /// Tags: operation (applicability|categories|files)
    /// </summary>
    public static readonly Counter<long> StorageAccessIssues = Meter.CreateCounter<long>(
        "anomaly_detection.storage_access_issues",
        description: "Storage access issues during feature extraction",
        unit: "issues");

    // ===== Observable Gauges (State) =====

    /// <summary>
    /// Current number of categories in the lookup cache.
    /// </summary>
    public static ObservableGauge<int> CategoriesInLookup { get; set; } = null!;

    /// <summary>
    /// Model ready status (1 = ready, 0 = not ready).
    /// </summary>
    public static ObservableGauge<int> ModelReadyStatus { get; set; } = null!;

    /// <summary>
    /// Current anomaly score threshold.
    /// </summary>
    public static ObservableGauge<double> CurrentThreshold { get; set; } = null!;

    /// <summary>
    /// Initialize observable gauges with callback functions.
    /// </summary>
    public static void InitializeGauges(
        Func<int> getCategoriesCount,
        Func<bool> getModelReady,
        Func<double> getThreshold)
    {
        CategoriesInLookup = Meter.CreateObservableGauge(
            "anomaly_detection.categories_lookup_count",
            getCategoriesCount,
            description: "Number of categories in the lookup cache");

        ModelReadyStatus = Meter.CreateObservableGauge(
            "anomaly_detection.model_ready",
            () => getModelReady() ? 1 : 0,
            description: "Model ready status (1=ready, 0=not ready)");

        CurrentThreshold = Meter.CreateObservableGauge(
            "anomaly_detection.threshold",
            getThreshold,
            description: "Current anomaly score threshold");
    }
}

/// <summary>
/// Metrics specific to HTTP ingestion endpoint.
/// </summary>
public static class AnomalyIngestionMetrics
{
    private static readonly Meter Meter = new("UpdateEngine.AnomalyDetection.Ingestion", "1.0.0");

    /// <summary>
    /// Total HTTP ingestion requests received.
    /// Tags: enabled (true|false)
    /// </summary>
    public static readonly Counter<long> IngestRequestsReceived = Meter.CreateCounter<long>(
        "anomaly_ingestion.requests_received",
        description: "Total HTTP ingestion requests received",
        unit: "requests");

    /// <summary>
    /// HTTP ingestion requests accepted (202 response).
    /// Tags: kb_id
    /// </summary>
    public static readonly Counter<long> IngestRequestsAccepted = Meter.CreateCounter<long>(
        "anomaly_ingestion.requests_accepted",
        description: "HTTP ingestion requests successfully accepted",
        unit: "requests");

    /// <summary>
    /// HTTP ingestion requests rejected (4xx/5xx responses).
    /// Tags: status_code, reason (disabled|invalid_payload|error)
    /// </summary>
    public static readonly Counter<long> IngestRequestsRejected = Meter.CreateCounter<long>(
        "anomaly_ingestion.requests_rejected",
        description: "HTTP ingestion requests rejected",
        unit: "requests");

    /// <summary>
    /// Time to process HTTP ingestion request.
    /// Tags: status_code
    /// </summary>
    public static readonly Histogram<double> IngestRequestDuration = Meter.CreateHistogram<double>(
        "anomaly_ingestion.request_duration",
        description: "Time to process ingestion request",
        unit: "ms");

    /// <summary>
    /// Number of updates quarantined due to hash mismatch.
    /// Tags: kb_id
    /// </summary>
    public static readonly Counter<long> QuarantinedUpdates = Meter.CreateCounter<long>(
        "anomaly_ingestion.quarantined_updates",
        description: "Number of updates quarantined due to hash mismatch",
        unit: "updates");
}

/// <summary>
/// Metrics specific to scheduled anomaly detection timer trigger.
/// </summary>
public static class ScheduledDetectionMetrics
{
    private static readonly Meter Meter = new("UpdateEngine.AnomalyDetection.Scheduled", "1.0.0");

    /// <summary>
    /// Number of scheduled detection runs started.
    /// Tags: enabled (true|false)
    /// </summary>
    public static readonly Counter<long> ScheduledRunsStarted = Meter.CreateCounter<long>(
        "scheduled_detection.runs_started",
        description: "Number of scheduled detection runs started",
        unit: "runs");

    /// <summary>
    /// Number of scheduled detection runs completed successfully.
    /// Tags: updates_analyzed, anomalies_found
    /// </summary>
    public static readonly Counter<long> ScheduledRunsCompleted = Meter.CreateCounter<long>(
        "scheduled_detection.runs_completed",
        description: "Number of scheduled detection runs completed",
        unit: "runs");

    /// <summary>
    /// Number of scheduled detection runs failed.
    /// Tags: error_type
    /// </summary>
    public static readonly Counter<long> ScheduledRunsFailed = Meter.CreateCounter<long>(
        "scheduled_detection.runs_failed",
        description: "Number of scheduled detection runs failed",
        unit: "runs");

    /// <summary>
    /// Duration of scheduled detection runs.
    /// Tags: updates_analyzed
    /// </summary>
    public static readonly Histogram<double> ScheduledRunDuration = Meter.CreateHistogram<double>(
        "scheduled_detection.run_duration",
        description: "Duration of scheduled detection runs",
        unit: "seconds");

    /// <summary>
    /// Number of updates analyzed per scheduled batch.
    /// </summary>
    public static readonly Histogram<long> UpdatesBatchSize = Meter.CreateHistogram<long>(
        "scheduled_detection.batch_size",
        description: "Number of updates analyzed per batch",
        unit: "updates");

    /// <summary>
    /// Number of anomalies detected per scheduled run.
    /// </summary>
    public static readonly Histogram<long> AnomaliesPerRun = Meter.CreateHistogram<long>(
        "scheduled_detection.anomalies_per_run",
        description: "Number of anomalies detected per scheduled run",
        unit: "anomalies");
}

/// <summary>
/// Metrics for individual feature values and distributions.
/// </summary>
public static class AnomalyFeatureMetrics
{
    private static readonly Meter Meter = new("UpdateEngine.AnomalyDetection.Features", "1.0.0");

    /// <summary>
    /// Distribution of update file sizes.
    /// </summary>
    public static readonly Histogram<long> FileSizeDistribution = Meter.CreateHistogram<long>(
        "features.file_size",
        description: "Distribution of update file sizes analyzed",
        unit: "bytes");

    /// <summary>
    /// Number of unsigned updates detected.
    /// Tags: classification
    /// </summary>
    public static readonly Counter<long> UnsignedUpdates = Meter.CreateCounter<long>(
        "features.unsigned_updates",
        description: "Number of unsigned updates detected",
        unit: "updates");

    /// <summary>
    /// Number of security updates processed.
    /// Tags: is_critical (true|false)
    /// </summary>
    public static readonly Counter<long> SecurityUpdates = Meter.CreateCounter<long>(
        "features.security_updates",
        description: "Number of security updates processed",
        unit: "updates");

    /// <summary>
    /// Distribution of supersedence relationship counts.
    /// </summary>
    public static readonly Histogram<int> SupersedenceCount = Meter.CreateHistogram<int>(
        "features.supersedence_count",
        description: "Distribution of supersedence relationship counts",
        unit: "count");

    /// <summary>
    /// Number of updates with complex applicability rules (> 5 rules).
    /// Tags: rule_count_range
    /// </summary>
    public static readonly Counter<long> ComplexApplicability = Meter.CreateCounter<long>(
        "features.complex_applicability",
        description: "Updates with complex applicability rules",
        unit: "updates");

    /// <summary>
    /// Distribution of bundled update counts.
    /// </summary>
    public static readonly Histogram<int> BundledUpdatesCount = Meter.CreateHistogram<int>(
        "features.bundled_updates_count",
        description: "Distribution of bundled update counts",
        unit: "count");
}
