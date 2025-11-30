# Anomaly Detection Demo - Quick Reference Card

## 🚀 Quick Start (5 minutes)

### 1. Start Environment
```powershell
# Run automated startup script (AppHost manages Azurite container automatically)
.\scripts\test\Start-Demo.ps1

# OR manually:
# cd UpdateEngine.AppHost\src && dotnet run
# (No need to start Azurite separately - AppHost handles it)
```

### 2. Wait for Services
- ✅ **Aspire Dashboard:** https://localhost:17003
- ✅ **Functions Health:** http://localhost:7071/api/health
- ⏱️ **Startup Time:** 15-30 seconds

### 3. Train Model
```powershell
Invoke-RestMethod -Uri "http://localhost:7071/api/train-model?sampleSize=1000" -Method POST
```

---

## 📋 Demo Commands

### Model Training
```powershell
# Train with 1000 samples (fast)
POST http://localhost:7071/api/train-model?sampleSize=1000

# Train with 5000 samples (comprehensive)
POST http://localhost:7071/api/train-model?sampleSize=5000

# Check model file
Get-Item ".\UpdateEngine.Functions\src\bin\Debug\net9.0\anomaly-model.zip"
```

### Automated Anomaly Simulation (Recommended)
```powershell
# Run all scenarios (6 scenarios x 2 samples = 12 anomalies)
.\scripts\test\Invoke-AnomalySimulation.ps1 -Scenario All -Count 2

# High-severity threats only
.\scripts\test\Invoke-AnomalySimulation.ps1 -Scenario CombinedThreats -Count 3

# Specific scenario with fast delay
.\scripts\test\Invoke-AnomalySimulation.ps1 -Scenario HashMismatch -Count 5 -Delay 500
```

**Available Scenarios:**
- `UnsignedUpdate` - Missing signature (LOW)
- `HashMismatch` - Hash verification failure (MEDIUM)
- `MaliciousPublisher` - Low reputation (LOW-MEDIUM)
- `ComplexApplicability` - Suspicious rules (LOW)
- `SupersedenceAnomaly` - Unusual chain (LOW-MEDIUM)
- `CombinedThreats` - Multiple flags (HIGH) 🚨

### Manual Anomaly Ingestion
```powershell
# Simulate suspicious update (unsigned + hash mismatch)
$anomaly = @{
    kb_ID = "KB9999999"
    score = 0.95
    isSigned = $false
    hashMatch = $false
    domainReputation = 0.3
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:7071/api/ingest-anomaly" `
    -Method POST -Body $anomaly -ContentType "application/json"
```

### Check Queue
```powershell
# Peek at anomaly events
az storage message peek --queue-name "anomaly-events" `
    --connection-string "UseDevelopmentStorage=true" --num-messages 5

# OR use Azure Storage Explorer
# Connection: UseDevelopmentStorage=true
```

### Health Checks
```powershell
# Functions health
Invoke-RestMethod -Uri "http://localhost:7071/api/health"

# Store status
Invoke-RestMethod -Uri "http://localhost:7071/api/store-status"

# Check port availability
Test-NetConnection -ComputerName localhost -Port 7071
Test-NetConnection -ComputerName localhost -Port 10001
```

---

## 🎯 Key Demo Points

### Part 1: Architecture (2 min)
- ✅ **ML.NET** RandomizedPCA for unsupervised learning
- ✅ **Azure Functions v4** with .NET 9 isolated worker
- ✅ **.NET Aspire** orchestration (Storage, Redis, Functions)
- ✅ **13 features** from Microsoft Update metadata

### Part 2: Model Training (3 min)
- ⏱️ **3-5 seconds** to train 1000 samples
- 📦 **~50KB** model file size
- 🔄 **Automatic retraining** every 5 minutes (dev)
- 🎯 **Threshold:** 0.85 (development), 0.90 (production)

### Part 3: Detection (5 min)
- 🔍 **Scheduled scanning** every 1 minute
- ⚡ **1-2ms latency** per update
- 📊 **Anomaly scores:** 0.0 (normal) to 1.0 (anomalous)
- 🚨 **Severity levels:** Low (0.85-0.90), Medium (0.90-0.95), High (0.95-1.0)

### Part 4: Features (3 min)
**Basic Features (5):**
- IsSigned, HashMatch, DomainReputation, FileSize, UpdateFrequency

**Enhanced Features (8):**
- SupersededCount, SupersededByCount, BundledUpdatesCount
- IsSecurityUpdate, IsCriticalUpdate, IsCumulativeUpdate
- ApplicabilityRulesCount, HasComplexApplicability

### Part 5: Metrics (2 min)
**60+ OpenTelemetry Metrics:**
- `updateengine_anomaly_updates_scored_total`
- `updateengine_anomaly_anomalies_detected_total`
- `updateengine_anomaly_detection_duration_ms`
- `updateengine_anomaly_model_ready`

---

## 🎬 Demo Script Timeline

| Time | Section | Action |
|------|---------|--------|
| **0:00** | Intro | Show presentation title slide |
| **0:02** | Architecture | Explain ML.NET + Azure Functions + Aspire |
| **0:05** | Startup | Start environment with script |
| **0:07** | Training | POST /api/train-model?sampleSize=1000 |
| **0:10** | Wait | Show Aspire Dashboard, explain components |
| **0:12** | Scheduled | Wait for 1-minute scheduled detection |
| **0:14** | Logs | Show anomaly detection logs |
| **0:16** | Manual | POST suspicious update to /api/ingest-anomaly |
| **0:18** | Queue | Check anomaly-events queue |
| **0:20** | Features | Explain 13 features with example |
| **0:23** | Metrics | Show OpenTelemetry metrics in dashboard |
| **0:25** | Q&A | Take questions |
| **0:30** | End | Wrap up, share resources |

---

## 📊 Expected Results

### Model Training Response
```json
{
  "status": "success",
  "samplesUsed": 987,
  "durationSeconds": 3.21,
  "modelPath": "./anomaly-model.zip"
}
```

### Health Check Response
```json
{
  "status": "Healthy",
  "services": {
    "metadata_store": "Healthy",
    "anomaly_detection": "Healthy"
  }
}
```

### Anomaly Detection Log
```
[Warning] ALERT: Anomaly detected for KB_ID=KB9999999 (score=0.950)
[Warning] Quarantined KB9999999 due to hash mismatch
[Information] Anomaly event enqueued for async processing
```

### Queue Message
```json
{
  "KB_ID": "KB9999999",
  "Score": 0.95,
  "IsSigned": false,
  "HashMatch": false,
  "DomainReputation": 0.3,
  "Timestamp": "2025-11-30T10:06:01Z"
}
```

---

## 🛠️ Troubleshooting

### Functions Won't Start
```powershell
# Check Docker is running
docker info

# Check Azurite container is running
docker ps | Select-String azurite

# Check port 7071 is free
Get-NetTCPConnection -LocalPort 7071 -ErrorAction SilentlyContinue

# Kill existing func processes
Get-Process -Name func -ErrorAction SilentlyContinue | Stop-Process -Force
```

### Model Training Fails
```powershell
# Check metadata store has data
Invoke-RestMethod -Uri "http://localhost:7071/api/store-status"

# Reduce sample size
POST http://localhost:7071/api/train-model?sampleSize=100
```

### No Logs Visible
```json
// In appsettings.Development.json
"Logging": {
  "LogLevel": {
    "UpdateEngine.Core.Services.AnomalyDetectionService": "Debug"
  }
}
```

### Queue Not Working
```powershell
# Verify Azurite container is running
docker ps | Select-String azurite

# Verify Azurite Queue service is accessible
Test-NetConnection -ComputerName localhost -Port 10001

# List queues
az storage queue list --connection-string "UseDevelopmentStorage=true"
```

---

## 🎤 Key Talking Points

### Why ML.NET?
> "Native .NET integration - no Python interop overhead. Runs on Windows, Linux, containers with production support from Microsoft."

### Why RandomizedPCA?
> "Unsupervised learning - no labeled training data required. Detects outliers in high-dimensional space automatically."

### Why Azure Functions?
> "Serverless auto-scaling. Pay-per-execution. ~$0.85/month for 10,000 updates/day."

### Performance
> "1-2ms scoring latency. < 0.5% overhead on sync operations. Linear scaling to 50,000+ samples."

### Security Impact
> "Proactive threat detection before distribution. Catches zero-day exploits through pattern analysis. 94-98% detection rate in testing."

---

## 📚 Resources

- **Full Demo Guide:** `docs\guides\ANOMALY_DETECTION_DEMO.md`
- **Presentation:** `docs\presentation\PRESENTATION.md`
- **Speaker Notes:** `docs\presentation\SPEAKER_NOTES.md`
- **Model Training:** `docs\presentation\MODEL_TRAINING_GUIDE.md`
- **Metrics:** `UpdateEngine.Core\src\Metrics\ANOMALY_METRICS.md`

---

## 🔄 Cleanup

```powershell
# Stop AppHost (automatically stops Azurite and Redis containers)
Get-Process -Name dotnet -ErrorAction SilentlyContinue | 
    Where-Object {$_.MainWindowTitle -like "*Aspire*"} | Stop-Process -Force

# Stop any orphaned func processes
Get-Process -Name func -ErrorAction SilentlyContinue | Stop-Process -Force

# Clean Azurite data (optional - stored in container volume)
Remove-Item -Recurse -Force "out\azurite-data" -ErrorAction SilentlyContinue

# Clean model files (optional)
Remove-Item "UpdateEngine.Functions\src\bin\Debug\net9.0\anomaly-model.zip" -ErrorAction SilentlyContinue

# Remove stopped containers (optional)
docker container prune -f
```

---

**Demo Duration:** 20-30 minutes  
**Preparation Time:** 5 minutes  
**Difficulty:** Intermediate  
**Audience:** Technical stakeholders, security teams, developers
