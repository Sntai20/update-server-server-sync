# Production Deployment Configuration Guide

## Environment-Aware Storage Configuration

The Microsoft Update Server-Server Sync application automatically configures storage based on the environment:

### **Development Environment**
```csharp
// Uses Azurite storage emulator with dynamic ports
var storage = builder.AddAzureStorage("Storage").RunAsEmulator();
```

### **Production Environment**  
```csharp
// Uses real Azure Storage with explicit connection strings
var storage = builder.AddAzureStorage("Storage");
```

## Production Azure Storage Setup

### **1. Create Azure Storage Accounts**

You have two options for production storage architecture:

#### **Option A: Single Storage Account** (Recommended for small/medium deployments)
```bash
# Create single storage account for both metadata and content
az storage account create \
  --name myupdateserver \
  --resource-group rg-update-server \
  --location eastus \
  --sku Standard_LRS \
  --kind StorageV2
```

#### **Option B: Separate Storage Accounts** (Recommended for large/enterprise deployments)
```bash
# Separate metadata storage (high frequency access)
az storage account create \
  --name myupdateservermetadata \
  --resource-group rg-update-server \
  --location eastus \
  --sku Standard_ZRS \
  --kind StorageV2

# Separate content storage (large files, lower frequency)
az storage account create \
  --name myupdateservercontent \
  --resource-group rg-update-server \
  --location eastus \
  --sku Standard_LRS \
  --kind StorageV2 \
  --access-tier Cool
```

### **2. Configure Connection Strings**

#### **Single Storage Account Configuration**
```json
{
  "ConnectionStrings": {
    "MetadataStorageConnection": "DefaultEndpointsProtocol=https;AccountName=myupdateserver;AccountKey=<KEY>;EndpointSuffix=core.windows.net",
    "ContentStorageConnection": "DefaultEndpointsProtocol=https;AccountName=myupdateserver;AccountKey=<KEY>;EndpointSuffix=core.windows.net"
  }
}
```

#### **Separate Storage Accounts Configuration**
```json
{
  "ConnectionStrings": {
    "MetadataStorageConnection": "DefaultEndpointsProtocol=https;AccountName=myupdateservermetadata;AccountKey=<METADATA_KEY>;EndpointSuffix=core.windows.net",
    "ContentStorageConnection": "DefaultEndpointsProtocol=https;AccountName=myupdateservercontent;AccountKey=<CONTENT_KEY>;EndpointSuffix=core.windows.net"
  }
}
```

### **3. Container Configuration**

Update `appsettings.Production.json`:
```json
{
  "Storage": {
    "MetadataContainerName": "metadata",
    "ContentContainerName": "content", 
    "UseAzureStorageForMetadata": true,
    "UseAzureStorageForContent": true
  }
}
```

## Security Configuration

### **1. Use Managed Identity (Recommended)**

For Azure hosting (App Service, Container Apps, etc.), use Managed Identity:

```json
{
  "ConnectionStrings": {
    "MetadataStorageConnection": "https://myupdateservermetadata.blob.core.windows.net/",
    "ContentStorageConnection": "https://myupdateservercontent.blob.core.windows.net/"
  }
}
```

Configure RBAC permissions:
```bash
# Grant Storage Blob Data Contributor to the managed identity
az role assignment create \
  --assignee <MANAGED_IDENTITY_PRINCIPAL_ID> \
  --role "Storage Blob Data Contributor" \
  --scope "/subscriptions/<SUB_ID>/resourceGroups/rg-update-server/providers/Microsoft.Storage/storageAccounts/myupdateserver"
```

### **2. Use Azure Key Vault**

Store connection strings in Azure Key Vault:
```json
{
  "ConnectionStrings": {
    "MetadataStorageConnection": "@Microsoft.KeyVault(SecretUri=https://myvault.vault.azure.net/secrets/MetadataStorage/)",
    "ContentStorageConnection": "@Microsoft.KeyVault(SecretUri=https://myvault.vault.azure.net/secrets/ContentStorage/)"
  }
}
```

## Environment-Specific Settings

### **Development (`appsettings.Development.json`)**
```json
{
  "Storage": {
    "UseAzureStorageForMetadata": true,
    "UseAzureStorageForContent": true,
    "MetadataContainerName": "data",
    "ContentContainerName": "data"
  },
  "Service": {
    "MaxUpdateCount": 5,
    "ServiceUrl": "http://localhost:57561"
  },
  "FunctionSchedules": {
    "SyncMetadataCriticalSchedule": "00:02:00",
    "SyncContentSchedule": "00:03:00"
  }
}
```

### **Production (`appsettings.Production.json`)**
```json
{
  "Storage": {
    "UseAzureStorageForMetadata": true,
    "UseAzureStorageForContent": true,
    "MetadataContainerName": "metadata",
    "ContentContainerName": "content"
  },
  "Service": {
    "MaxUpdateCount": 10000,
    "ServiceUrl": "https://myupdateserver.azurewebsites.net"
  },
  "FunctionSchedules": {
    "SyncMetadataCriticalSchedule": "02:00:00",
    "SyncContentSchedule": "3.00:00:00"
  }
}
```

## Deployment Methods

### **1. Azure Container Apps (Recommended)**

```yaml
# deploy.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: update-server
spec:
  template:
    spec:
      containers:
      - name: update-server
        image: myregistry.azurecr.io/update-server:latest
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__MetadataStorageConnection
          valueFrom:
            secretKeyRef:
              name: storage-secrets
              key: metadata-connection
```

### **2. Azure App Service**

```bash
# Deploy via Azure CLI
az webapp deployment source config-zip \
  --resource-group rg-update-server \
  --name myupdateserver \
  --src ./publish.zip

# Set environment variables
az webapp config appsettings set \
  --resource-group rg-update-server \
  --name myupdateserver \
  --settings ASPNETCORE_ENVIRONMENT=Production
```

### **3. Azure Functions (Serverless)**

**Important**: Azure Functions in production do NOT use `local.settings.json` - they use Application Settings configured in the Azure Portal or deployment templates.

#### **Configure Application Settings in Azure Portal**
```bash
# Set application settings via Azure CLI
az functionapp config appsettings set \
  --resource-group rg-update-server \
  --name myupdateserver \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    UseLocalStorageForMetadata=false \
    UseLocalStorageForContent=false \
    MetadataContainerName=metadata \
    ContentContainerName=content \
    'ServiceConfigurationJson={"ServiceUrl":"https://myupdateserver.azurewebsites.net","MaxUpdateCount":10000}' \
    SyncMetadataCriticalSchedule=02:00:00 \
    SyncContentSchedule=3.00:00:00

# Set connection strings separately (more secure)
az functionapp config connection-string set \
  --resource-group rg-update-server \
  --name myupdateserver \
  --connection-string-type Custom \
  --settings \
    MetadataStorageConnection="DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=<KEY>" \
    ContentStorageConnection="DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=<KEY>"
```

#### **Or use ARM/Bicep deployment template**
```json
{
  "type": "Microsoft.Web/sites",
  "apiVersion": "2021-02-01",
  "name": "[parameters('functionAppName')]",
  "properties": {
    "siteConfig": {
      "appSettings": [
        {
          "name": "ASPNETCORE_ENVIRONMENT",
          "value": "Production"
        },
        {
          "name": "UseLocalStorageForMetadata", 
          "value": "false"
        },
        {
          "name": "MetadataContainerName",
          "value": "metadata"
        },
        {
          "name": "ServiceConfigurationJson",
          "value": "{\"ServiceUrl\":\"https://myupdateserver.azurewebsites.net\",\"MaxUpdateCount\":10000}"
        }
      ],
      "connectionStrings": [
        {
          "name": "MetadataStorageConnection",
          "connectionString": "[parameters('storageConnectionString')]",
          "type": "Custom"
        }
      ]
    }
  }
}
```

## Monitoring & Diagnostics

### **1. Application Insights**
```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=<KEY>;IngestionEndpoint=https://eastus-0.in.applicationinsights.azure.com/"
  }
}
```

### **2. Storage Metrics**
Monitor key metrics:
- **Metadata Store**: Request rate, latency, error rate
- **Content Store**: Bandwidth usage, storage consumption, download patterns
- **Sync Operations**: Success rate, duration, item counts

### **3. Health Checks**
```bash
# Check system health
curl https://myupdateserver.azurewebsites.net/api/HealthCheck

# Check storage diagnostics  
curl https://myupdateserver.azurewebsites.net/api/StorageDiagnostics
```

## Performance Optimization

### **1. Storage Performance**
- **Metadata**: Use Standard_ZRS for high availability
- **Content**: Use Standard_LRS with Cool tier for cost optimization
- **Caching**: Enable CDN for content distribution

### **2. Function Scaling**
```json
{
  "FunctionSchedules": {
    "SyncMetadataCriticalSchedule": "02:00:00",
    "SyncContentSchedule": "6.00:00:00",
    "ScheduledHealthCheckSchedule": "00:15:00"
  }
}
```

### **3. Resource Limits**
```json
{
  "Service": {
    "MaxUpdateCount": 10000,
    "SupportedCategories": [
      "Security Updates",
      "Critical Updates", 
      "Updates"
    ]
  }
}
```

## Fallback Strategy

The service registration prioritizes connections in this order:

```csharp
var connectionString = configuration["AzureWebJobsStorage"]
    ?? configuration.GetConnectionString("MetadataStorageConnection")
```

**Development**: `AzureWebJobsStorage` provided by Aspire + Azurite  
**Production**: Explicit `MetadataStorageConnection`/`ContentStorageConnection` from configuration

This ensures seamless operation across environments while maintaining security and performance best practices.

---

## Quick Production Checklist

- [ ] Create Azure Storage accounts
- [ ] Configure connection strings in `appsettings.Production.json`
- [ ] Set up Managed Identity or Key Vault
- [ ] Deploy to Azure App Service/Container Apps
- [ ] Configure Application Insights monitoring
- [ ] Test health endpoints
- [ ] Verify sync operations with storage diagnostics

**Production deployment ready!** 🚀