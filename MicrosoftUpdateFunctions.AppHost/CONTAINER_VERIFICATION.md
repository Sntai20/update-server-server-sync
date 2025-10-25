# Container Creation Verification Steps

## What Was Added

###1. **Enhanced Logging in ServiceCollectionExtensions**
- Added explicit `CreateIfNotExists()` calls with logging
- Logs now show:
  - "Created Azure Blob container: {name}" when container is created
  - "Azure Blob container already exists: {name}" when it's found
  - Storage account name being connected to
  - Full error details if connection fails

### 2. **New Diagnostic Endpoints**

#### `/api/StorageDiagnostics`
Returns comprehensive storage configuration and status:
```json
{
  "Configuration": {
    "UseAzureStorage": true,
    "MetadataStorePath": "./store",
    "MetadataContainerName": "metadata",
  "HasMetadataConnection": true
  },
  "Storage": {
 "MetadataStore": {
      "Type": "Azure Blob Storage",
      "Initialized": true,
 "PackageCount": 0,
      "IsReindexingRequired": false
    }
  }
}
```

#### `/api/TestAzureStorage`
Tests connectivity and lists all containers:
```json
{
  "MetadataStorage": {
    "Connected": true,
    "AccountName": "devstoreaccount1",
    "Containers": ["metadata", "content"],
    "ContainerCount": 2
  }
}
```

#### `/api/EnvironmentDiagnostics` (requires auth)
Shows all environment variables (masked for security)

## How to Verify Containers Are Created

### Step 1: Start the Application
```bash
cd D:\repos\update-server-server-sync-fork
dotnet run --project MicrosoftUpdateFunctions.AppHost
```

### Step 2: Watch the Startup Logs
Look for these messages in the Functions output:
```
info: MicrosoftUpdateFunctions.Services.ServiceCollectionExtensions[0]
      Initializing metadata store - UseAzure: True, HasConnection: True
info: MicrosoftUpdateFunctions.Services.ServiceCollectionExtensions[0]
      Using Azure Blob Storage for metadata store
info: MicrosoftUpdateFunctions.Services.ServiceCollectionExtensions[0]
      Created Azure Blob container: metadata
info: MicrosoftUpdateFunctions.Services.ServiceCollectionExtensions[0]
      Successfully connected to Azure Storage account: devstoreaccount1
```

### Step 3: Call Diagnostic Endpoints

**Test Storage Connection:**
```bash
curl http://localhost:7071/api/TestAzureStorage | ConvertFrom-Json | ConvertTo-Json -Depth 10
```

**Get Storage Diagnostics:**
```bash
curl http://localhost:7071/api/StorageDiagnostics | ConvertFrom-Json | ConvertTo-Json -Depth 10
```

### Step 4: Verify with Storage Explorer

1. Open **Azure Storage Explorer** (or install from https://azure.microsoft.com/en-us/products/storage/storage-explorer/)
2. Connect to **Local & Attached** > **Storage Accounts** > **Emulator - Default Ports (Key)**
3. Expand **Blob Containers**
4. You should see:
   - `metadata` container
   - `content` container

### Step 5: Use Docker Commands

```bash
# Check Azurite is running
docker ps --filter "name=azurite"

# View Azurite logs
docker logs <azurite-container-id>

# Check if data is persisted
docker exec <azurite-container-id> ls -la /data
```

## Common Scenarios

### Scenario 1: First Run (Containers Don't Exist Yet)
**Expected Logs:**
```
Created Azure Blob container: metadata
Created Azure Blob container: content
```

**TestAzureStorage Response:**
```json
{
  "MetadataStorage": {
    "Connected": true,
    "Containers": ["metadata", "content"],
    "ContainerCount": 2
  }
}
```

### Scenario 2: Subsequent Runs (Containers Exist)
**Expected Logs:**
```
Azure Blob container already exists: metadata
Azure Blob container already exists: content
```

**TestAzureStorage Response:** Same as Scenario 1

### Scenario 3: Connection Failure
**Expected Logs:**
```
error: MicrosoftUpdateFunctions.Services.ServiceCollectionExtensions[0]
       Failed to initialize Azure Blob Storage for metadata store
 System.Net.Http.HttpRequestException: No connection could be made...
```

**TestAzureStorage Response:**
```json
{
"MetadataStorage": {
    "Connected": false,
    "Error": "No connection could be made...",
 "ConnectionStringPrefix": "DefaultEndpointsProtocol=http;AccountName=..."
  }
}
```

## Troubleshooting Flow

```
START
  ?
Are you seeing "webhost" container only?
  ? YES
Check if UseAzureStorage = true in appsettings.Development.json
  ? YES
Check if Azurite container is running (docker ps)
  ? YES
Call /api/TestAzureStorage
  ?
  ?? Connected=true, ContainerCount=2 ? Containers exist! ?
  ?  (May not show in Docker because they're IN Azurite, not separate containers)
  ?
  ?? Connected=true, ContainerCount=0 ? Check initialization logs
  ?                Call /api/StorageDiagnostics
  ?
  ?? Connected=false ? Check connection string and Azurite accessibility
   Check firewall/ports 10000-10002
```

## Important Understanding

**Azure Storage containers are NOT Docker containers!**

- **Docker containers** = Running processes (webhost, azurite, servicebus)
- **Blob containers** = Logical groupings INSIDE Azurite storage
  
When you run `docker ps`, you see Docker containers like:
- `webhost` (your app)
- `azurite` (storage emulator)
- `servicebus` (message queue)

The `metadata` and `content` **blob containers** are INSIDE the Azurite storage, not separate Docker containers.

## Verification Checklist

- [ ] `docker ps` shows Azurite container running
- [ ] Logs show "Created Azure Blob container" or "already exists"
- [ ] `/api/TestAzureStorage` returns `Connected: true`
- [ ] `/api/TestAzureStorage` shows 2 containers
- [ ] Azure Storage Explorer shows metadata and content containers
- [ ] `/api/StorageDiagnostics` shows storage initialized

## Quick Test Script

Save as `test-storage.ps1`:

```powershell
Write-Host "Testing Microsoft Update Functions Storage..." -ForegroundColor Cyan

# 1. Check Docker
Write-Host "`n1. Checking Docker containers..." -ForegroundColor Yellow
docker ps --filter "name=azurite" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

# 2. Test storage connectivity
Write-Host "`n2. Testing Azure Storage connectivity..." -ForegroundColor Yellow
try {
    $result = Invoke-RestMethod -Uri "http://localhost:7071/api/TestAzureStorage" -Method Get
    Write-Host "Metadata Storage Connected: $($result.MetadataStorage.Connected)" -ForegroundColor Green
    Write-Host "Container Count: $($result.MetadataStorage.ContainerCount)" -ForegroundColor Green
    Write-Host "Containers: $($result.MetadataStorage.Containers -join ', ')" -ForegroundColor Green
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}

# 3. Get storage diagnostics
Write-Host "`n3. Getting storage diagnostics..." -ForegroundColor Yellow
try {
    $diag = Invoke-RestMethod -Uri "http://localhost:7071/api/StorageDiagnostics" -Method Get
  Write-Host "UseAzureStorage: $($diag.Configuration.UseAzureStorage)" -ForegroundColor Cyan
    Write-Host "Metadata Store Type: $($diag.Storage.MetadataStore.Type)" -ForegroundColor Cyan
    Write-Host "Metadata Store Initialized: $($diag.Storage.MetadataStore.Initialized)" -ForegroundColor Cyan
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}

Write-Host "`nDone!" -ForegroundColor Green
```

Run with: `.\test-storage.ps1`
