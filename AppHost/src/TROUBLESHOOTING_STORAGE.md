# Azure Storage Container Troubleshooting Guide

## Issue: Containers Not Being Created

If you're only seeing the webhost container but not the Azure Storage containers (`metadata` and `content`), follow these steps:

### 1. Verify Azurite is Running

Check that the Azurite emulator is running and accessible:

```bash
# Check if Azurite container is running
docker ps | findstr azurite

# Check if ports are accessible
Test-NetConnection -ComputerName 127.0.0.1 -Port 10000
Test-NetConnection -ComputerName 127.0.0.1 -Port 10001
Test-NetConnection -ComputerName 127.0.0.1 -Port 10002
```

### 2. Check Configuration

Verify your `appsettings.Development.json` has the correct settings:

```json
{
  "Storage": {
 "UseAzureStorageForMetadata": true,
  "UseAzureStorageForContent": true,  // Must be true to use Azure Storage
    "MetadataContainerName": "metadata",
    "ContentContainerName": "content"
  }
}
```

### 3. Test Storage Connectivity

Use the diagnostic endpoints to verify connectivity:

**Storage Diagnostics:**
```bash
# Get comprehensive storage diagnostics
curl http://localhost:7071/api/StorageDiagnostics

# Test Azure Storage connection and list containers
curl http://localhost:7071/api/TestAzureStorage
```

**Expected Output for TestAzureStorage:**
```json
{
  "Timestamp": "2024-01-15T10:30:00Z",
  "MetadataStorage": {
    "Connected": true,
    "AccountName": "devstoreaccount1",
 "AccountKind": "StorageV2",
 "SkuName": "Standard_LRS",
    "Containers": ["metadata", "content"],
    "ContainerCount": 2
  },
  "ContentStorage": {
    "Connected": true,
    "AccountName": "devstoreaccount1",
    "Containers": ["metadata", "content"],
    "ContainerCount": 2
  }
}
```

### 4. Check Logs

Look for container creation messages in the Azure Functions output:

```
info: UpdateEngine.Services.ServiceCollectionExtensions[0]
      Created Azure Blob container: metadata
info: UpdateEngine.Services.ServiceCollectionExtensions[0]
      Created Azure Blob container: content
```

If you see errors like:
```
Failed to initialize Azure Blob Storage for metadata store
```

Check the detailed error message for the root cause.

### 5. Verify Connection String

The connection string should point to localhost (127.0.0.1), not a container name:

**Correct:**
```
DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;...;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;
```

**Incorrect:**
```
BlobEndpoint=http://azurite:10000/devstoreaccount1;  // Won't work for local Functions process
```

### 6. Manual Container Creation

You can manually verify Azurite by using Azure Storage Explorer or the Azure CLI:

**Using Azure Storage Explorer:**
1. Connect to "Local Storage Emulator"
2. Navigate to Blob Containers
3. You should see `metadata` and `content` containers

**Using Azure CLI:**
```bash
# Install Azure Storage CLI
pip install azure-cli

# Set connection string
$env:AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;"

# List containers
az storage container list --output table

# Create containers manually if needed
az storage container create --name metadata
az storage container create --name content
```

### 7. Common Issues and Solutions

#### Issue: "UseAzureStorage" is false
**Solution:** Set `"UseAzureStorageForMetadata": true,
        "UseAzureStorageForContent": true` in `appsettings.Development.json`

#### Issue: Azurite not accessible
**Symptoms:** Connection refused errors on ports 10000-10002
**Solution:** 
- Ensure Azurite container is running: `docker-compose up -d` or start via Aspire AppHost
- Check Docker Desktop is running
- Verify port mappings: `docker port <azurite-container-id>`

#### Issue: Containers created but not visible
**Cause:** Looking at the wrong storage account
**Solution:** 
- Azure Functions use LOCAL process, so connect to 127.0.0.1:10000
- Don't use the container network name

#### Issue: "The requested URI does not represent any resource on the server"
**Cause:** Container doesn't exist or wasn't created
**Solution:**
- Check the initialization logs
- Call `/api/StorageDiagnostics` to see if containers were created
- Manually create containers using Storage Explorer or CLI

### 8. Force Re-initialization

If containers aren't being created, you can force re-initialization:

1. Stop the AppHost/Functions
2. Remove Azurite data volume:
   ```bash
   docker volume rm <azurite-volume-name>
   ```
3. Restart AppHost - containers should be created fresh

### 9. Debugging Container Creation

Add breakpoints or logging in:
- `ServiceCollectionExtensions.cs` - `RegisterMetadataStore()` and `RegisterContentStore()`
- Look for the `CreateIfNotExists()` calls
- Check the return value - if null, container already existed

### 10. Health Check Endpoint

Check system health including storage:

```bash
curl http://localhost:7071/api/HealthCheck
```

Expected response should show storage is healthy.

## Quick Diagnostic Checklist

- [ ] Azurite container is running (`docker ps`)
- [ ] Ports 10000-10002 are accessible
- [ ] `UseAzureStorage` is set to `true` in config
- [ ] Connection string uses `127.0.0.1`, not container name
- [ ] Functions show "Created Azure Blob container" in logs
- [ ] `/api/TestAzureStorage` shows containers exist
- [ ] Storage Explorer shows `metadata` and `content` containers

## Still Having Issues?

Run the full diagnostic suite:

```bash
# 1. Storage diagnostics
curl http://localhost:7071/api/StorageDiagnostics > storage-diagnostics.json

# 2. Azure Storage test
curl http://localhost:7071/api/TestAzureStorage > storage-test.json

# 3. Health check
curl http://localhost:7071/api/HealthCheck > health.json

# 4. Environment (requires Function auth key)
curl http://localhost:7071/api/EnvironmentDiagnostics?code=<function-key> > environment.json
```

Review the JSON files for detailed information about your storage configuration and status.

## Expected Container Structure

When everything is working correctly, you should see:

```
Azurite Blob Storage (127.0.0.1:10000)
??? metadata (container)
?   ??? identities-index (blob)
?   ??? metadata (page blob)
?   ??? index/ (virtual directory)
?  ??? toc.json
?     ??? various index blobs
??? content (container)
    ??? (content files with SHA hashes)
```

You can verify this structure using Azure Storage Explorer or the diagnostic endpoints.
