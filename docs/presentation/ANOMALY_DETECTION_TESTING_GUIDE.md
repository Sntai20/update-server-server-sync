# Anomaly Detection Testing Guide

Complete testing guide for the ML.NET-based anomaly detection system in UpdateEngine.

## Overview

This guide covers three types of tests for the anomaly detection system:

1. **In-Memory Integration Tests** - Fast tests without external dependencies
2. **HTTP Integration Tests** - Tests against running Functions endpoints
3. **Metrics Tests** - OpenTelemetry metrics validation

## Test Files

| Test File | Purpose | Collection | Dependencies |
|-----------|---------|------------|--------------|
| `AnomalyDetectionIntegrationTest.cs` | Core anomaly detection logic | `InMemory` | InMemoryFunctionsFixture |
| `AnomalyDetectionHttpIntegrationTest.cs` | HTTP endpoint testing | `AspireIntegration` | Running Functions |
| `AnomalyDetectionMetricsTest.cs` | OpenTelemetry metrics | `AspireIntegration` | Running Functions |

## Running Tests

### Quick Start

```powershell
# Run all anomaly detection tests
dotnet test --filter "FullyQualifiedName~AnomalyDetection"

# Run only in-memory tests (fastest)
dotnet test --filter "FullyQualifiedName~AnomalyDetectionIntegrationTest"

# Run HTTP tests (requires Functions running)
dotnet test --filter "FullyQualifiedName~AnomalyDetectionHttpIntegrationTest"

# Run metrics tests
dotnet test --filter "FullyQualifiedName~AnomalyDetectionMetricsTest"
```

### Using Test Scripts

```powershell
# Run in-memory tests only
.\scripts\test\Run-InMemoryTests.ps1

# Run all integration tests (starts Functions automatically)
.\scripts\test\Run-IntegrationTests.ps1
```

## Test Categories

### 1. In-Memory Integration Tests

**File**: `AnomalyDetectionIntegrationTest.cs`  
**Collection**: `InMemory`  
**Runtime**: ~5-10 seconds  
**Dependencies**: None (uses in-memory fixture)

#### Test Coverage

##### Anomaly Ingestion Tests
```csharp
[Fact] IngestAnomaly_WithValidPayload_ShouldAccept()
[Theory] IngestAnomaly_WithDifferentScores_ShouldClassifySeverityCorrectly(score, expectedSeverity)
  - 0.86 → LOW
  - 0.91 → MEDIUM  
  - 0.96 → HIGH
[Fact] IngestAnomaly_WithMissingFields_ShouldReturnBadRequest()
```

##### Scenario-Specific Detection Tests
```csharp
[Fact] DetectAnomaly_UnsignedUpdate_ShouldScoreLow()           // 0.85-0.90
[Fact] DetectAnomaly_HashMismatch_ShouldScoreMedium()          // 0.90-0.95
[Fact] DetectAnomaly_CombinedThreats_ShouldScoreHigh()         // 0.95-1.00
[Fact] DetectAnomaly_MaliciousPublisher_ShouldScoreLowToMedium()
[Fact] DetectAnomaly_ComplexApplicability_ShouldScoreLow()
[Fact] DetectAnomaly_SupersedenceAnomaly_ShouldScoreLowToMedium()
```

##### Batch Processing Tests
```csharp
[Fact] IngestMultipleAnomalies_ShouldProcessAllSuccessfully()  // 3 anomalies
[Fact] IngestAnomalies_WithDifferentSeverities_ShouldMaintainCorrectDistribution()
  - 3 LOW + 2 MEDIUM + 1 HIGH = correct distribution
```

##### Performance Tests
```csharp
[Fact] IngestAnomaly_Performance_ShouldCompleteQuickly()       // < 100ms
```

#### Running In-Memory Tests

```powershell
# Using dotnet test
cd UpdateEngine.Functions/test
dotnet test --filter "FullyQualifiedName~AnomalyDetectionIntegrationTest"

# Using script
.\scripts\test\Run-InMemoryTests.ps1

# Run specific test
dotnet test --filter "FullyQualifiedName~DetectAnomaly_UnsignedUpdate"
```

### 2. HTTP Integration Tests

**File**: `AnomalyDetectionHttpIntegrationTest.cs`  
**Collection**: `AspireIntegration`  
**Runtime**: ~30-60 seconds  
**Dependencies**: Functions running on localhost:7071

#### Test Coverage

##### HTTP Endpoint Tests
```csharp
[Fact] POST_IngestAnomaly_WithValidPayload_ShouldReturn202Accepted()
[Fact] POST_IngestAnomaly_WithHighSeverity_ShouldLogWarning()
[Fact] POST_IngestAnomaly_WithLowScore_ShouldReject()              // score < 0.85
[Fact] POST_IngestAnomaly_WithMissingKbId_ShouldReturn400BadRequest()
[Fact] POST_IngestAnomaly_WithMalformedJson_ShouldReturn400BadRequest()
```

##### Scenario-Based HTTP Tests
```csharp
[Fact] POST_IngestAnomaly_AllScenarios_ShouldAcceptAndClassifyCorrectly()
  - Tests all 6 scenarios via HTTP
[Fact] POST_IngestAnomalies_RapidSequence_ShouldHandleAll()       // 10 concurrent
```

##### Model Training Tests
```csharp
[Fact] POST_TrainModel_WithDefaultSampleSize_ShouldSucceed()      // 100 samples
[Fact] POST_TrainModel_WithLargeSampleSize_ShouldTakeLonger()     // 1000 samples
```

##### Health Check Tests
```csharp
[Fact] GET_Health_ShouldReturnHealthyStatus()
```

#### Running HTTP Tests

```powershell
# Method 1: Start Functions manually, then run tests
cd UpdateEngine.AppHost/src
dotnet run  # Start AppHost with Functions

# In another terminal:
cd UpdateEngine.Functions/test
dotnet test --filter "FullyQualifiedName~AnomalyDetectionHttpIntegrationTest"

# Method 2: Use AspireTestFixture (automatic)
# The fixture starts Functions automatically via func CLI
dotnet test --filter "FullyQualifiedName~AnomalyDetectionHttpIntegrationTest"
```

### 3. Metrics Tests

**File**: `AnomalyDetectionMetricsTest.cs`  
**Collection**: `AspireIntegration`  
**Runtime**: ~60-90 seconds  
**Dependencies**: Functions running with OpenTelemetry enabled

#### Test Coverage

##### Counter Metrics Tests
```csharp
[Fact] IngestAnomalies_ShouldIncrementAnomaliesDetectedCounter()   // 5 anomalies
[Fact] IngestAnomalies_WithDifferentSeverities_ShouldIncrementSeverityCounters()
  - Tests severity labels: LOW (2), MEDIUM (2), HIGH (1)
```

##### Histogram Metrics Tests
```csharp
[Fact] IngestAnomaly_ShouldRecordDetectionDuration()               // Single request
[Fact] IngestMultipleAnomalies_ShouldRecordDurationDistribution()  // 10 requests
  - Reports: avg, min, max durations
```

##### Gauge Metrics Tests
```csharp
[Fact] TrainModel_ShouldSetModelReadyGauge()                       // model_ready = 1
[Fact] DetectionWithoutModel_ShouldReflectModelReadyState()        // model_ready = 0/1
```

##### Performance Metrics Tests
```csharp
[Fact] IngestAnomalies_Performance_ShouldMeetSLO()                 // P95 < 100ms
  - Tests: P50, P95, P99 percentiles
  - 20 requests for statistical significance
```

##### Workflow Metrics Tests
```csharp
[Fact] CompleteWorkflow_ShouldEmitAllExpectedMetrics()
  - Counters: updates_scored, anomalies_detected, queue_messages_sent
  - Histograms: detection_duration, queue_operation_duration
  - Gauges: model_ready, queue_depth
```

#### Running Metrics Tests

```powershell
# Start Functions with OpenTelemetry
cd UpdateEngine.AppHost/src
dotnet run

# Run metrics tests
cd UpdateEngine.Functions/test
dotnet test --filter "FullyQualifiedName~AnomalyDetectionMetricsTest"

# Verify metrics in Azure Monitor (production)
az monitor app-insights metrics show \
  --app <insights-name> \
  --metric updateengine_anomaly_anomalies_detected_total \
  --start-time 2025-01-01T00:00:00Z
```

## Expected Metrics

### Counter Metrics

| Metric Name | Description | Labels | Expected Increments |
|-------------|-------------|--------|---------------------|
| `updateengine_anomaly_updates_scored_total` | Total updates scored | none | +1 per ingestion |
| `updateengine_anomaly_anomalies_detected_total` | Anomalies detected | `severity` | +1 per anomaly |
| `updateengine_anomaly_queue_messages_sent_total` | Queue messages sent | none | +1 per queue operation |

### Histogram Metrics

| Metric Name | Description | Buckets (ms) | Expected Range |
|-------------|-------------|--------------|----------------|
| `updateengine_anomaly_detection_duration_ms` | Detection time | 10, 50, 100, 500, 1000, 5000 | 10-100ms (typical) |
| `updateengine_anomaly_queue_operation_duration_ms` | Queue time | 10, 50, 100, 500, 1000 | 10-50ms (typical) |

### Gauge Metrics

| Metric Name | Description | Expected Values |
|-------------|-------------|-----------------|
| `updateengine_anomaly_model_ready` | Model trained and ready | 0 (not ready), 1 (ready) |
| `updateengine_anomaly_queue_depth` | Queue message count | 0-N (varies) |

## Test Data Patterns

### Anomaly Score Ranges

| Severity | Score Range | Test Count | Example KB IDs |
|----------|-------------|------------|----------------|
| **LOW** | 0.85 - 0.90 | ~40% | KB8100001, KB8400001 |
| **MEDIUM** | 0.90 - 0.95 | ~40% | KB8200001, KB8300001 |
| **HIGH** | 0.95 - 1.00 | ~20% | KB8900001 |

### Test KB ID Conventions

```
KB8000XXX - Generic test anomalies
KB8100XXX - Unsigned update scenarios
KB8200XXX - Hash mismatch scenarios  
KB8300XXX - Malicious publisher scenarios
KB8400XXX - Complex applicability scenarios
KB8500XXX - Supersedence anomaly scenarios
KB8900XXX - Combined threat scenarios
KB9000XXX - Metrics test anomalies
```

## Troubleshooting Tests

### Test Failures

#### "Functions did not become healthy after 60 seconds"

**Cause**: Functions not starting or taking too long to start  
**Solution**:
```powershell
# Verify Functions are running
curl http://localhost:7071/api/health

# Check AppHost logs
cd UpdateEngine.AppHost/src
dotnet run

# Manually start Functions
cd UpdateEngine.Functions/src
func start
```

#### "InMemoryFunctionsFixture setup failed"

**Cause**: Metadata store creation failure  
**Solution**:
```powershell
# Check temp directory permissions
$env:TEMP
dir $env:TEMP

# Run with verbose logging
dotnet test --filter "AnomalyDetection" --logger "console;verbosity=detailed"
```

#### "Metrics test failed: metric not found"

**Cause**: OpenTelemetry not configured or metrics not exported  
**Solution**:
```powershell
# Verify OpenTelemetry configuration
cat UpdateEngine.Configuration/src/shared/appsettings.Development.json

# Check for OpenTelemetry logs in Functions output
# Should see: "OpenTelemetry initialized with endpoint: http://localhost:4318"
```

### Performance Issues

#### Tests running slowly

```powershell
# Run only fast in-memory tests
dotnet test --filter "FullyQualifiedName~AnomalyDetectionIntegrationTest"

# Skip HTTP tests
dotnet test --filter "FullyQualifiedName~AnomalyDetection&FullyQualifiedName!~Http"

# Parallel test execution (use with caution)
dotnet test --parallel
```

## CI/CD Integration

### GitHub Actions

```yaml
name: Anomaly Detection Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET 9
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      
      - name: Run in-memory tests
        run: |
          cd UpdateEngine.Functions/test
          dotnet test --filter "FullyQualifiedName~AnomalyDetectionIntegrationTest" --logger trx
      
      - name: Start Functions
        run: |
          cd UpdateEngine.AppHost/src
          dotnet run &
          sleep 30
      
      - name: Run HTTP tests
        run: |
          cd UpdateEngine.Functions/test
          dotnet test --filter "FullyQualifiedName~AnomalyDetectionHttpIntegrationTest" --logger trx
      
      - name: Upload test results
        uses: actions/upload-artifact@v3
        with:
          name: test-results
          path: '**/TestResults/*.trx'
```

### Azure DevOps

```yaml
trigger:
  branches:
    include:
      - main
      - develop

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: UseDotNet@2
  inputs:
    version: '9.0.x'

- script: |
    cd UpdateEngine.Functions/test
    dotnet test --filter "FullyQualifiedName~AnomalyDetection" --logger trx --collect:"XPlat Code Coverage"
  displayName: 'Run Anomaly Detection Tests'

- task: PublishTestResults@2
  inputs:
    testResultsFormat: 'VSTest'
    testResultsFiles: '**/TestResults/*.trx'

- task: PublishCodeCoverageResults@2
  inputs:
    summaryFileLocation: '**/coverage.cobertura.xml'
```

## Test Coverage Goals

| Category | Target | Current Status |
|----------|--------|----------------|
| Line Coverage | ≥80% | ✓ Achieved |
| Branch Coverage | ≥70% | ✓ Achieved |
| Scenario Coverage | 100% (6/6) | ✓ Achieved |
| HTTP Endpoints | 100% | ✓ Achieved |
| Metrics | 80% (8/10) | ⚠️ Gauge metrics need validation |

## Next Steps

1. **Add model persistence tests**: Verify trained models are saved/loaded correctly
2. **Add queue integration tests**: Test Azure Storage Queue operations with Azurite
3. **Add E2E tests**: Complete workflow from sync → detection → alerting
4. **Add chaos tests**: Simulate failures (network, storage, model corruption)
5. **Add load tests**: Test with 1000+ concurrent anomaly ingestions

## Resources

- **Simulation Script**: `scripts/test/Invoke-AnomalySimulation.ps1`
- **Demo Guide**: `docs/guides/ANOMALY_DETECTION_DEMO.md`
- **Test Fixtures**: `UpdateEngine.Functions/test/Infrastructure/`
- **OpenTelemetry Docs**: https://opentelemetry.io/docs/instrumentation/net/

---

**Last Updated**: 2025-01-24  
**Test Framework**: xUnit 2.x with FluentAssertions  
**Target Runtime**: .NET 9.0
