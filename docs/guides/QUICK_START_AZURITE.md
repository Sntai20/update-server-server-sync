# Quick Start: Azure Storage with Aspire

Get your update server running with persistent Azure Blob Storage (Azurite) in 5 minutes.

## Prerequisites

- ? .NET 9 SDK installed
- ? Docker Desktop running
- ? All Phase 1-7 fixes applied

## 1. Start the AppHost

```powershell
cd UpdateEngine.AppHost/src
dotnet run
```

**What happens:**
- Aspire starts Azurite in Docker container
- Creates `./azurite-data/` directory for blob storage
- Starts Azure Functions with injected connection strings
- Starts Redis for caching

## 2. Open Aspire Dashboard

The AppHost will display a URL like:
```
Dashboard: http://localhost:15147
```

Open it in your browser.

## 3. Verify Configuration

### Check Environment Variables

In Aspire Dashboard:
1. Go to **Resources** ? **UpdateEngine**
2. Click **Environment** tab
3. Look for:

```
ConnectionStrings__MetadataStorageConnection = DefaultEndpointsProtocol=http;...
UseAzureStorageForMetadata = True
MetadataContainerName = data
```

**Good signs:**
- ? `ConnectionStrings__MetadataStorageConnection` exists
- ? `UseAzureStorageForMetadata = True`
- ? **NO** `MetadataPath` variable (when using Azure Storage)

### Check Functions Logs

In Aspire Dashboard:
1. Go to **Console** ? **UpdateEngine**
2. Look for startup logs:

```
=== UpdateEngine Configuration ===
UseAzureStorageForMetadata: True
MetadataContainerName: data

Metadata Store Configuration:
  UseAzureStorageForMetadata: True
  Connection String from Aspire: True
Opening Azure Blob Storage metadata store (container: data)
```

**Good signs:**
- ? `Connection String from Aspire: True`
- ? `Opening Azure Blob Storage metadata store`
- ? **NOT** `Opening local file system metadata store`

## 4. Verify Azurite Storage

### Check Container

```powershell
# List running containers
docker ps | Select-String "azurite"

# Should see something like:
# aspire-azurite-abc123  mcr.microsoft.com/azure-storage/azurite
```

### Check Data Directory

```powershell
# Check if directory was created
ls out/azurite-data/

# Should see:
# __blobstorage__/
# __queuestorage__/
```

## 5. Trigger a Sync

### Via HTTP API

```powershell
# Get the Functions URL from Aspire Dashboard
# Usually http://localhost:7071

# Trigger sync
Invoke-RestMethod -Uri "http://localhost:7071/api/trigger/sync/critical" -Method Post
```

### Via Aspire Dashboard

1. Go to **Resources** ? **UpdateEngine**
2. Click the **HTTP endpoint** link
3. Navigate to `/api/trigger/sync/critical`

## 6. Verify Data in Azurite

### Check Blob Files

```powershell
# Check for blob files
ls out/azurite-data/__blobstorage__/data/

# Should see GUID-named files (metadata blobs)
# Example:
# 12345678-1234-1234-1234-123456789012
# abcdefgh-abcd-abcd-abcd-abcdefghijkl
```

### Check Content Blobs

```powershell
# Check content directory
ls out/azurite-data/__blobstorage__/data/content/

# Should see files with format: {guid}_{hash}
# Example:
# 12345678-1234-1234-1234-123456789012_sha256hash
```

### Count Blobs

```powershell
# Count metadata blobs
(Get-ChildItem out/azurite-data/__blobstorage__/data/ -File).Count

# Count content blobs
(Get-ChildItem out/azurite-data/__blobstorage__/data/content/ -File).Count
```

## 7. Test Data Persistence

### Stop and Restart

```powershell
# 1. Note the number of blobs
$beforeCount = (Get-ChildItem out/azurite-data/__blobstorage__/data/ -File).Count
Write-Host "Before: $beforeCount blobs"

# 2. Stop AppHost (Ctrl+C in the terminal)

# 3. Restart AppHost
cd UpdateEngine.AppHost/src
dotnet run

# 4. Wait for startup, then check blobs again
$afterCount = (Get-ChildItem out/azurite-data/__blobstorage__/data/ -File).Count
Write-Host "After: $afterCount blobs"

# 5. Should be the same!
if ($beforeCount -eq $afterCount) {
    Write-Host "? Data persisted successfully!" -ForegroundColor Green
} else {
    Write-Host "? Data was lost!" -ForegroundColor Red
}
```

## 8. Inspect with Azure Storage Explorer (Optional)

### Install Storage Explorer

Download from: https://azure.microsoft.com/en-us/products/storage/storage-explorer/

### Connect to Azurite

1. Open Azure Storage Explorer
2. Click **Connect**
3. Select **Local Storage Emulator**
4. **Default endpoint:** `http://127.0.0.1:10000/devstoreaccount1`
5. **Account name:** `devstoreaccount1`
6. **Account key:** `Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==`

### Browse Blobs

1. Expand **Local & Attached** ? **Storage Accounts** ? **Emulator**
2. Expand **Blob Containers** ? **data**
3. See all your metadata and content blobs!

## Common Issues

### 1. Data Going to Local Filesystem

**Symptom:** Files in `./LocalContentStore` instead of `./azurite-data/`

**Fix:**
1. Check `local.settings.json` has **NO** `UpdateEngine` section
2. Verify environment variables (no `MetadataPath` when using Azure)
3. Check logs show "Opening Azure Blob Storage"

See: `docs/fixes/AZURE_STORAGE_CONFIGURATION_FIX.md`

### 2. Data Not Persisting

**Symptom:** Blobs disappear after restart

**Fix:**
1. Check `Program.cs` has `WithDataBindMount("./azurite-data")`
2. Verify `./azurite-data/` directory exists
3. Check Docker volume isn't anonymous

See: `docs/guides/AZURITE_STORAGE_OPTIONS.md`

### 3. Connection Refused

**Symptom:** `No connection could be made (127.0.0.1:10000)`

**Fix:**
1. Ensure Docker is running
2. Check Azurite container is started: `docker ps | Select-String azurite`
3. Restart AppHost

### 4. Cannot See Blobs

**Symptom:** Directory exists but no files

**Fix:**
1. Check logs for "Opening Azure Blob Storage"
2. Trigger a sync
3. Wait for sync to complete
4. Check again

## Verification Checklist

After following this guide, verify:

- [ ] AppHost running without errors
- [ ] Aspire Dashboard accessible
- [ ] Environment variable `ConnectionStrings__MetadataStorageConnection` exists
- [ ] Functions logs show "Opening Azure Blob Storage"
- [ ] `./azurite-data/__blobstorage__/data/` directory exists
- [ ] Blob files visible in data directory
- [ ] Data persists after AppHost restart
- [ ] No `./LocalMetadataStore` or `./LocalContentStore` directories

## Next Steps

### Run Comprehensive Sync

```powershell
# Sync all configured categories
Invoke-RestMethod -Uri "http://localhost:7071/api/trigger/sync/comprehensive" -Method Post
```

### Monitor Sync Progress

In Aspire Dashboard:
1. Go to **Console** ? **UpdateEngine**
2. Watch logs for:
   ```
   Synced X updates
   Downloading content files...
   Content sync complete
   ```

### Check Storage Usage

```powershell
# Get total size of Azurite data
$size = (Get-ChildItem out/azurite-data -Recurse | Measure-Object -Property Length -Sum).Sum
$sizeMB = [math]::Round($size / 1MB, 2)
Write-Host "Azurite storage: $sizeMB MB"
```

### Clean Up (Fresh Start)

```powershell
# Stop AppHost (Ctrl+C)

# Delete Azurite data
Remove-Item -Recurse -Force out/azurite-data

# Restart AppHost
cd UpdateEngine.AppHost/src
dotnet run

# Will start with empty store, trigger categories sync automatically
```

## What You've Achieved

? **Cloud-Native Development** - Using Azure Blob Storage (emulated)
? **Data Persistence** - No more re-syncing after restarts
? **Easy Debugging** - Direct file access in `./azurite-data/`
? **Production-Ready Pattern** - Same code works with real Azure Storage

## Resources

- **Storage Options:** `docs/guides/AZURITE_STORAGE_OPTIONS.md`
- **Configuration Fix Details:** `docs/fixes/AZURE_STORAGE_CONFIGURATION_FIX.md`
- **Aspire Documentation:** https://learn.microsoft.com/en-us/dotnet/aspire/
- **Azurite Documentation:** https://github.com/Azure/Azurite

## Get Help

If something doesn't work:

1. Check logs in Aspire Dashboard
2. Review environment variables
3. Verify Docker is running
4. Check documentation in `docs/` directory
5. Look for error messages in Functions console

**Common log patterns:**

? **Good:**
```
Opening Azure Blob Storage metadata store (container: data)
Metadata store initialized successfully
```

? **Bad:**
```
Opening local file system metadata store at: ./LocalMetadataStore
Connection String from Aspire: False
```

Happy syncing! ??
