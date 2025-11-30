# Anomaly Detection Metrics

## Overview

The anomaly detection system provides comprehensive OpenTelemetry metrics for monitoring ML.NET-based anomaly detection operations, model performance, and feature extraction.

## Metrics Namespaces

| Namespace | Description |
|-----------|-------------|
| `UpdateEngine.AnomalyDetection` | Core anomaly detection metrics |
| `UpdateEngine.AnomalyDetection.Ingestion` | HTTP ingestion endpoint metrics |
| `UpdateEngine.AnomalyDetection.Scheduled` | Scheduled detection timer trigger metrics |
| `UpdateEngine.AnomalyDetection.Features` | Feature extraction and distribution metrics |

## Core Anomaly Detection Metrics

### Counters

#### `anomaly_detection.anomalies_detected`
**Type:** Counter  
**Unit:** anomalies  
**Description:** Total number of anomalies detected (score > threshold)  
**Tags:**
- `severity` - Anomaly severity: `high` (>0.95), `medium` (0.90-0.95), `low` (0.80-0.90)
- `classification` - Update classification (e.g., "Security Updates", "Critical Updates")
- `product` - Product category (e.g., "Windows 11", "Windows Server")

**Example Query (Prometheus):**
```promql
rate(anomaly_detection_anomalies_detected_total{severity="high"}[5m])
```

#### `anomaly_detection.updates_scored`
**Type:** Counter  
**Unit:** updates  
**Description:** Total number of updates successfully scored for anomalies  
**Tags:**
- `result` - Scoring result: `normal` or `anomaly` or `error`

**Alert Example:**
```yaml
- alert: HighAnomalyRate
  expr: rate(anomaly_detection_anomalies_detected_total[5m]) / rate(anomaly_detection_updates_scored_total[5m]) > 0.05
  annotations:
    summary: "Anomaly detection rate above 5%"
```

#### `anomaly_detection.scoring_errors`
**Type:** Counter  
**Unit:** errors  
**Description:** Number of errors encountered during anomaly scoring  
**Tags:**
- `error_type` - Error type: `unsupported_expression`, `storage_access`, `unknown`

#### `anomaly_detection.model_loaded`
**Type:** Counter  
**Unit:** loads  
**Description:** Number of times the ML.NET model was successfully loaded  
**Tags:**
- `source` - Load source: `startup` or `reload`

#### `anomaly_detection.model_training_started`
**Type:** Counter  
**Unit:** trainings  
**Description:** Number of model training operations started  
**Tags:**
- `training_size` - Training dataset size: `small` (<100), `medium` (100-1000), `large` (>1000)

#### `anomaly_detection.model_training_completed`
**Type:** Counter  
**Unit:** trainings  
**Description:** Number of model training operations completed successfully  
**Tags:**
- `sample_count` - Number of training samples
- `training_size` - Training dataset size classification

#### `anomaly_detection.events_enqueued`
**Type:** Counter  
**Unit:** events  
**Description:** Number of anomaly events successfully enqueued to Azure Storage Queue  
**Tags:**
- `kb_id` - KB article identifier
- `score_range` - Score range: `high` (>0.9), `medium` (0.8-0.9), `low` (<0.8)

#### `anomaly_detection.events_enqueue_failed`
**Type:** Counter  
**Unit:** failures  
**Description:** Number of failed event enqueue operations  
**Tags:**
- `error_type` - Exception type name

#### `anomaly_detection.category_resolution_success`
**Type:** Counter  
**Unit:** resolutions  
**Description:** Number of successful category resolutions using categories lookup  
**Tags:**
- `category_type` - Category type: `classification` or `product`

#### `anomaly_detection.category_resolution_failures`
**Type:** Counter  
**Unit:** failures  
**Description:** Number of failed category resolutions  
**Tags:**
- `reason` - Failure reason: `invalid_guid`, `not_found`, `lookup_error`

#### `anomaly_detection.unsupported_expressions`
**Type:** Counter  
**Unit:** updates  
**Description:** Updates with unsupported applicability expression types  
**Tags:**
- `update_type` - Update type: `SoftwareUpdate`, `ApplicabilityRules`

#### `anomaly_detection.storage_access_issues`
**Type:** Counter  
**Unit:** issues  
**Description:** Storage access issues during feature extraction  
**Tags:**
- `operation` - Operation type: `score`, `applicability`, `categories`, `files`

### Histograms

#### `anomaly_detection.score`
**Type:** Histogram  
**Unit:** score (0.0-1.0)  
**Description:** Distribution of anomaly scores for all updates  
**Buckets:** 0-0.2 (normal), 0.2-0.5 (suspicious), 0.5-0.8 (concerning), 0.8-1.0 (anomaly)

**Visualization (Grafana):**
```promql
histogram_quantile(0.95, rate(anomaly_detection_score_bucket[5m]))
```

#### `anomaly_detection.detection_duration`
**Type:** Histogram  
**Unit:** milliseconds  
**Description:** Time taken to score a single update for anomalies  
**Tags:**
- `has_model` - Model availability: `true` or `false`

#### `anomaly_detection.model_training_duration`
**Type:** Histogram  
**Unit:** seconds  
**Description:** Time taken to train the ML.NET model  
**Tags:**
- `sample_count` - Number of training samples

### Observable Gauges

#### `anomaly_detection.categories_lookup_count`
**Type:** ObservableGauge  
**Description:** Current number of categories in the lookup cache  
**Update Frequency:** Real-time

#### `anomaly_detection.model_ready`
**Type:** ObservableGauge  
**Description:** Model ready status (1=ready, 0=not ready)  
**Update Frequency:** Real-time

#### `anomaly_detection.threshold`
**Type:** ObservableGauge  
**Description:** Current anomaly score threshold  
**Update Frequency:** Real-time

## HTTP Ingestion Metrics

### Counters

#### `anomaly_ingestion.requests_received`
**Type:** Counter  
**Unit:** requests  
**Description:** Total HTTP ingestion requests received  
**Tags:**
- `enabled` - Feature enabled status: `true` or `false`

#### `anomaly_ingestion.requests_accepted`
**Type:** Counter  
**Unit:** requests  
**Description:** HTTP ingestion requests successfully accepted (202 response)  
**Tags:**
- `kb_id` - KB article identifier

#### `anomaly_ingestion.requests_rejected`
**Type:** Counter  
**Unit:** requests  
**Description:** HTTP ingestion requests rejected (4xx/5xx responses)  
**Tags:**
- `status_code` - HTTP status code: `400`, `503`, `500`
- `reason` - Rejection reason: `disabled`, `invalid_payload`, `invalid_json`, `error`

#### `anomaly_ingestion.quarantined_updates`
**Type:** Counter  
**Unit:** updates  
**Description:** Number of updates quarantined due to hash mismatch  
**Tags:**
- `kb_id` - KB article identifier

### Histograms

#### `anomaly_ingestion.request_duration`
**Type:** Histogram  
**Unit:** milliseconds  
**Description:** Time to process HTTP ingestion request  
**Tags:**
- `status_code` - HTTP response status code

**SLI Example:**
```promql
histogram_quantile(0.99, rate(anomaly_ingestion_request_duration_bucket[5m])) < 500
```

## Scheduled Detection Metrics

### Counters

#### `scheduled_detection.runs_started`
**Type:** Counter  
**Unit:** runs  
**Description:** Number of scheduled detection runs started  
**Tags:**
- `enabled` - Feature enabled status: `true` or `false`

#### `scheduled_detection.runs_completed`
**Type:** Counter  
**Unit:** runs  
**Description:** Number of scheduled detection runs completed successfully  
**Tags:**
- `updates_analyzed` - Number of updates analyzed
- `anomalies_found` - Number of anomalies detected

#### `scheduled_detection.runs_failed`
**Type:** Counter  
**Unit:** runs  
**Description:** Number of scheduled detection runs failed  
**Tags:**
- `error_type` - Exception type name

### Histograms

#### `scheduled_detection.run_duration`
**Type:** Histogram  
**Unit:** seconds  
**Description:** Duration of scheduled detection runs  
**Tags:**
- `updates_analyzed` - Number of updates analyzed

#### `scheduled_detection.batch_size`
**Type:** Histogram  
**Unit:** updates  
**Description:** Number of updates analyzed per scheduled batch

#### `scheduled_detection.anomalies_per_run`
**Type:** Histogram  
**Unit:** anomalies  
**Description:** Number of anomalies detected per scheduled run

**Alert Example:**
```yaml
- alert: HighAnomaliesPerRun
  expr: histogram_quantile(0.95, rate(scheduled_detection_anomalies_per_run_bucket[10m])) > 10
  annotations:
    summary: "Scheduled detection finding > 10 anomalies per run"
```

## Feature Metrics

### Histograms

#### `features.file_size`
**Type:** Histogram  
**Unit:** bytes  
**Description:** Distribution of update file sizes analyzed

#### `features.supersedence_count`
**Type:** Histogram  
**Unit:** count  
**Description:** Distribution of supersedence relationship counts

#### `features.bundled_updates_count`
**Type:** Histogram  
**Unit:** count  
**Description:** Distribution of bundled update counts

### Counters

#### `features.unsigned_updates`
**Type:** Counter  
**Unit:** updates  
**Description:** Number of unsigned updates detected  
**Tags:**
- `classification` - Update classification

#### `features.security_updates`
**Type:** Counter  
**Unit:** updates  
**Description:** Number of security updates processed  
**Tags:**
- `is_critical` - Critical update flag: `true` or `false`

#### `features.complex_applicability`
**Type:** Counter  
**Unit:** updates  
**Description:** Updates with complex applicability rules (> 5 rules)  
**Tags:**
- `rule_count_range` - Rule count range: `medium` (6-10), `high` (>10)

## Grafana Dashboard Examples

### Anomaly Detection Overview
```json
{
  "title": "Anomaly Detection Overview",
  "panels": [
    {
      "title": "Anomaly Detection Rate",
      "targets": [
        {
          "expr": "rate(anomaly_detection_anomalies_detected_total[5m])",
          "legendFormat": "{{severity}}"
        }
      ]
    },
    {
      "title": "Updates Scored per Second",
      "targets": [
        {
          "expr": "rate(anomaly_detection_updates_scored_total[5m])",
          "legendFormat": "{{result}}"
        }
      ]
    },
    {
      "title": "Anomaly Score Distribution",
      "targets": [
        {
          "expr": "histogram_quantile(0.50, rate(anomaly_detection_score_bucket[5m]))",
          "legendFormat": "p50"
        },
        {
          "expr": "histogram_quantile(0.95, rate(anomaly_detection_score_bucket[5m]))",
          "legendFormat": "p95"
        },
        {
          "expr": "histogram_quantile(0.99, rate(anomaly_detection_score_bucket[5m]))",
          "legendFormat": "p99"
        }
      ]
    },
    {
      "title": "Model Status",
      "targets": [
        {
          "expr": "anomaly_detection_model_ready",
          "legendFormat": "Model Ready"
        }
      ]
    }
  ]
}
```

### HTTP Ingestion Performance
```json
{
  "title": "HTTP Ingestion Performance",
  "panels": [
    {
      "title": "Request Rate",
      "targets": [
        {
          "expr": "rate(anomaly_ingestion_requests_received_total[5m])",
          "legendFormat": "Received"
        },
        {
          "expr": "rate(anomaly_ingestion_requests_accepted_total[5m])",
          "legendFormat": "Accepted"
        },
        {
          "expr": "rate(anomaly_ingestion_requests_rejected_total[5m])",
          "legendFormat": "Rejected - {{reason}}"
        }
      ]
    },
    {
      "title": "Request Duration (p99)",
      "targets": [
        {
          "expr": "histogram_quantile(0.99, rate(anomaly_ingestion_request_duration_bucket[5m]))",
          "legendFormat": "{{status_code}}"
        }
      ]
    }
  ]
}
```

## Alerts

### Critical Alerts

```yaml
groups:
  - name: anomaly_detection_critical
    rules:
      - alert: AnomalyDetectionModelNotReady
        expr: anomaly_detection_model_ready == 0
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Anomaly detection model is not ready"
          description: "The ML.NET model has not been loaded for 5 minutes"

      - alert: HighAnomalyDetectionRate
        expr: |
          rate(anomaly_detection_anomalies_detected_total{severity="high"}[5m]) /
          rate(anomaly_detection_updates_scored_total[5m]) > 0.10
        for: 10m
        labels:
          severity: critical
        annotations:
          summary: "High anomaly detection rate (>10%)"
          description: "More than 10% of updates are being flagged as high-severity anomalies"

      - alert: ScheduledDetectionFailures
        expr: increase(scheduled_detection_runs_failed_total[1h]) > 3
        labels:
          severity: warning
        annotations:
          summary: "Scheduled detection runs failing repeatedly"
          description: "{{ $value }} scheduled detection runs have failed in the last hour"
```

### Performance Alerts

```yaml
groups:
  - name: anomaly_detection_performance
    rules:
      - alert: SlowAnomalyDetection
        expr: histogram_quantile(0.95, rate(anomaly_detection_detection_duration_bucket[5m])) > 100
        for: 15m
        labels:
          severity: warning
        annotations:
          summary: "Anomaly detection is slow"
          description: "95th percentile detection time is {{ $value }}ms (threshold: 100ms)"

      - alert: HighIngestionLatency
        expr: histogram_quantile(0.99, rate(anomaly_ingestion_request_duration_bucket[5m])) > 1000
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "High ingestion endpoint latency"
          description: "99th percentile latency is {{ $value }}ms (threshold: 1000ms)"
```

## Metric Cardinality

### High Cardinality Tags (Use with Caution)
- `kb_id` - Unique per update (thousands of possible values)
- `sample_count` - Variable training sizes
- `updates_analyzed` - Variable batch sizes

### Low Cardinality Tags (Safe for Production)
- `severity` - 3 values: high, medium, low
- `result` - 3 values: normal, anomaly, error
- `error_type` - ~10 values
- `status_code` - ~5 values
- `enabled` - 2 values: true, false

**Best Practice:** Drop high-cardinality tags in production if cardinality becomes an issue:
```yaml
metric_relabel_configs:
  - source_labels: [kb_id]
    action: labeldrop
```

## Integration with Application Insights

All metrics are automatically exported to Azure Application Insights when using .NET Aspire ServiceDefaults.

### Custom Queries (KQL)

```kql
// Anomaly detection rate over time
customMetrics
| where name == "anomaly_detection.anomalies_detected"
| summarize Count=sum(value) by bin(timestamp, 5m)
| render timechart

// Top anomalies by severity
customMetrics
| where name == "anomaly_detection.anomalies_detected"
| extend severity = tostring(customDimensions.severity)
| summarize Count=sum(value) by severity
| render piechart

// Ingestion endpoint latency percentiles
customMetrics
| where name == "anomaly_ingestion.request_duration"
| summarize 
    p50 = percentile(value, 50),
    p95 = percentile(value, 95),
    p99 = percentile(value, 99)
  by bin(timestamp, 1m)
| render timechart
```

## Performance Impact

### Overhead
- Counter increment: < 0.1?s
- Histogram record: < 1?s
- Observable gauge: Read on demand (no overhead during operations)

### Recommendations
1. **Production**: Keep all metrics enabled (minimal overhead)
2. **High Volume**: Consider sampling histograms at 10%
3. **Cost Optimization**: Use metric aggregation rules to reduce cardinality

## See Also
- [OpenTelemetry Metrics Documentation](https://opentelemetry.io/docs/specs/otel/metrics/)
- [Azure Monitor Metrics](https://docs.microsoft.com/azure/azure-monitor/essentials/metrics-overview)
- [Grafana Dashboards](https://grafana.com/docs/grafana/latest/dashboards/)
- [Prometheus Alerting](https://prometheus.io/docs/alerting/latest/overview/)
