# Quick Development Testing Guide

## Rapid Testing with Aspire & Azurite Storage Emulator

This guide shows how to quickly test metadata sync, content sync, and anomaly detection functionality during local development.

## Development Schedule Configuration

For rapid testing, the Development environment uses accelerated schedules:

### 🚀 **Ultra-Fast Development Schedules**

| Function | Schedule | Purpose |
|----------|----------|---------|
| **Critical Metadata Sync** | `00:02:00` (2 minutes) | Test security update detection |
| **Content Sync** | `00:03:00` (3 minutes) | Test content download functionality |
| **Comprehensive Metadata** | `00:05:00` (5 minutes) | Test full metadata sync |
| **Anomaly Detection** | `00:02:00` (2 minutes) | Test ML.NET pattern analysis |
| **Health Monitoring** | `00:01:00` (1 minute) | Test system health checks |

### 🎛️ **Testing Configuration**
- **MaxUpdateCount**: 5 (faster processing)
- **Categories**: Security & Critical Updates only
- **Azurite Storage**: Automatic container management
- **Logging**: Enhanced debug output for anomaly detection

## Quick Test Workflow

### 1. **Start Aspire AppHost**
```powershell
cd AppHost
dotnet run --project src/AppHost.csproj
```

This automatically starts:
- ✅ Azure Functions (UpdateEngine)
- ✅ Azurite storage emulator
- ✅ Health dashboards
- ✅ Service discovery

### 2. **Monitor Function Execution**

**Watch for scheduled functions in logs:**
```
[2025-01-24 12:00:00] SyncCritical triggered (every 2 minutes)
[2025-01-24 12:01:00] ScheduledHealthCheck triggered (every 1 minute)  
[2025-01-24 12:02:00] ScheduledAnomalyDetection triggered (every 2 minutes)
[2025-01-24 12:03:00] SyncContent triggered (every 3 minutes)
[2025-01-24 12:05:00] SyncComprehensive triggered (every 5 minutes)
```

### 3. **Verify Azurite Storage**

**Check containers created:**
```powershell
# View Azurite web interface
Start-Process "http://localhost:10000"
```

**Expected containers:**
- `data` - Metadata and content storage
- Automatic blob structure creation

### 4. **Test Manual Triggers**

**Trigger immediate sync:**
```powershell
# Critical metadata sync
curl -X POST "http://localhost:7071/api/UniversalSync" -H "Content-Type: application/json" -d '{"syncType":"critical"}'

# Content sync  
curl -X POST "http://localhost:7071/api/UniversalSync" -H "Content-Type: application/json" -d '{"syncType":"content","syncContent":true}'

# Anomaly detection test
curl -X POST "http://localhost:7071/api/IngestAnomaly" -H "Content-Type: application/json" -d '{"updateId":"test-001","anomalyScore":0.95}'
```

### 5. **Monitor Storage Activity**

**Check storage diagnostics:**
```powershell
curl "http://localhost:7071/api/StorageDiagnostics"
```

**Expected output:**
```json
{
  "metadataStore": {
    "isConnected": true,
    "storageType": "Azure Blob",
    "containerName": "data",
    "updateCount": 5,
    "lastSync": "2025-01-24T12:00:00Z"
  },
  "contentStore": {
    "isConnected": true,
    "storageType": "Azure Blob", 
    "containerName": "data",
    "contentItems": 3
  }
}
```

## Validation Checklist

### ✅ **Metadata Sync Verification**
- [ ] Critical updates synced within 2 minutes
- [ ] Metadata written to Azurite `data` container
- [ ] Update count limited to 5 (development setting)
- [ ] Categories filtered to Security/Critical only

### ✅ **Content Sync Verification** 
- [ ] Content sync triggered every 3 minutes
- [ ] Actual update files downloaded to Azurite
- [ ] Content store diagnostics show items
- [ ] Download progress logged

### ✅ **Anomaly Detection Verification**
- [ ] Scheduled analysis every 2 minutes
- [ ] ML.NET model processing logged
- [ ] Anomaly events ingested via HTTP endpoint
- [ ] Debug logging shows feature analysis

### ✅ **Storage Integration Verification**
- [ ] Azurite containers auto-created
- [ ] No local file system usage
- [ ] Storage diagnostics return Azure Blob type
- [ ] Aspire orchestration working

## Troubleshooting Quick Fixes

### **Functions Not Triggering**
```powershell
# Check if AzureWebJobsStorage is configured for timer triggers
# For local dev, ensure it's empty string for HTTP-only functions
```

### **Azurite Connection Issues**
```powershell
# Restart AppHost to recreate containers
dotnet run --project src/AppHost.csproj
```

### **Schedule Not Working**
```powershell
# Verify schedule format (TimeSpan: HH:mm:ss)
# Check function is not disabled in appsettings.Development.json
```

### **Storage Not Found**
```powershell
# Confirm UseAzureStorageForMetadata/Content = true
# Check Aspire logs for Azurite startup
```

## Development vs Production

| Aspect | Development | Production |
|--------|-------------|------------|
| Critical Sync | 2 minutes | 2 hours |
| Content Sync | 3 minutes | 3 days |
| Health Check | 1 minute | 15 minutes |
| Anomaly Detection | 2 minutes | 30 minutes |
| Max Updates | 5 | 1000 |
| Categories | Security+Critical | All |
| Storage | Azurite Emulator | Azure Storage |

---

**Quick Start**: `cd AppHost && dotnet run` → Functions start automatically with rapid testing schedules!