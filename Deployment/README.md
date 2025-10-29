# Microsoft Update Functions Deployment

This directory contains Azure Resource Manager (ARM) templates and deployment scripts for deploying the Microsoft Update Azure Functions with Premium P3V3 hosting plan.

## Quick Start

### Prerequisites
- Azure CLI installed and logged in
- Azure Functions Core Tools (for code deployment)
- .NET 9 SDK (for building the functions)

### Deploy with Bash Script (Linux/macOS/WSL)
```bash
# Make script executable
chmod +x Deployment/deploy.sh

# Deploy to development environment
./Deployment/deploy.sh \
  --resource-group "rg-msupdate-dev" \
  --location "East US" \
  --environment "dev" \
  --name "msupdate-functions"
```

### Deploy with PowerShell Script (Windows/Cross-platform)
```powershell
# Deploy to development environment
./Deployment/deploy.ps1 `
  -ResourceGroupName "rg-msupdate-dev" `
  -Location "East US" `
  -Environment "dev" `
  -FunctionAppName "msupdate-functions"
```

### Deploy with Azure CLI (Manual)
```bash
# Create resource group
az group create --name "rg-msupdate-dev" --location "East US"

# Deploy infrastructure
az deployment group create \
  --resource-group "rg-msupdate-dev" \
  --template-file "./Deployment/main.bicep" \
  --parameters "./Deployment/main.parameters.dev.json"

# Deploy function code (if Azure Functions Core Tools installed)
cd UpdateEngine/src
func azure functionapp publish <function-app-name>
```

## Files Overview

### Templates
- **`main.bicep`** - Main Bicep template with all resources
- **`azuredeploy.json`** - ARM template (compiled from Bicep)
- **`main.parameters.dev.json`** - Development environment parameters
- **`main.parameters.prod.json`** - Production environment parameters

### Scripts
- **`deploy.sh`** - Bash deployment script (Linux/macOS/WSL)
- **`deploy.ps1`** - PowerShell deployment script (Windows/Cross-platform)

## Infrastructure Components

### Deployed Resources
1. **Premium App Service Plan (P3V3)**
   - 8 vCPUs, 32GB RAM per instance
   - Maximum 25 instances (configurable)
   - Always-on enabled
   - Elastic scaling

2. **Azure Function App**
   - .NET 9 runtime
   - Isolated worker model
   - HTTPS-only
   - System-assigned managed identity

3. **Storage Account**
   - Blob containers for metadata and content
   - Hot tier access
   - TLS 1.2 minimum

4. **Application Insights**
   - Performance monitoring
   - Request tracking
   - Log aggregation

5. **Log Analytics Workspace**
   - Centralized logging
   - 30-day retention

### Key Configuration Settings
```json
{
  "WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT": "25",
  "FUNCTIONS_EXTENSION_VERSION": "~4",
  "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
  "WEBSITE_ENABLE_SYNC_UPDATE_SITE": "true",
  "WEBSITE_RUN_FROM_PACKAGE": "1",
  "WEBSITE_HTTPSCALEV2_ENABLED": "1",
  "FUNCTIONS_WORKER_PROCESS_COUNT": "4"
}
```

## Environment-Specific Deployments

### Development Environment
- **Storage**: Standard_LRS (locally redundant)
- **Scale limit**: 25 instances
- **Monitoring**: Full Application Insights

### Production Environment  
- **Storage**: Standard_GRS (geo-redundant)
- **Scale limit**: 25 instances (adjustable)
- **Monitoring**: Full Application Insights + alerting

## Script Parameters

### Bash Script (deploy.sh)
```bash
./deploy.sh [options]
  -g, --resource-group   Azure Resource Group name (required)
  -l, --location         Azure region (default: East US)
  -e, --environment      Environment (dev/staging/prod, default: dev)
  -n, --name             Function App name (default: msupdate-functions)
  -s, --subscription     Azure Subscription ID (optional)
  -h, --help             Show help message
```

### PowerShell Script (deploy.ps1)
```powershell
./deploy.ps1 [parameters]
  -ResourceGroupName     Azure Resource Group name (required)
  -Location              Azure region (default: East US)
  -Environment           Environment (dev/staging/prod, default: dev)
  -FunctionAppName       Function App name (default: msupdate-functions)
  -SubscriptionId        Azure Subscription ID (optional)
  -SkipCodeDeployment    Skip code deployment step
  -Force                 Skip interactive prompts
```

## Customization

### Scaling Configuration
To change the maximum scale-out limit, modify the parameters file:

```json
{
  "maxScaleOut": {
    "value": 50  // Increase from default 25
  }
}
```

### Storage Configuration
For production workloads requiring higher performance:

```json
{
  "storageAccountType": {
    "value": "Premium_LRS"  // Premium performance tier
  }
}
```

### Additional App Settings
Add custom application settings in the Bicep template:

```bicep
{
  name: 'CustomSetting'
  value: 'CustomValue'
}
```

## Monitoring and Diagnostics

### Application Insights Queries
After deployment, use these KQL queries in Application Insights:

```kusto
// Function execution times
requests
| where name contains "ClientWebService"
| summarize avg(duration), count() by bin(timestamp, 5m)

// Scaling events
traces
| where message contains "ScaleController"
| project timestamp, message, customDimensions

// Error analysis
exceptions
| where timestamp > ago(1h)
| summarize count() by problemId, outerMessage
```

### Performance Monitoring
Key metrics to monitor:
- **Response time**: Average < 5 seconds for SOAP endpoints
- **Throughput**: Concurrent request handling capacity
- **Instance count**: Auto-scaling behavior
- **Memory usage**: Per-instance memory consumption
- **Error rate**: Failed request percentage

## Troubleshooting

### Common Issues

1. **Deployment Fails with Template Errors**
   ```bash
   # Validate template first
   az deployment group validate \
     --resource-group "rg-name" \
     --template-file "./Deployment/main.bicep" \
     --parameters "./Deployment/main.parameters.dev.json"
   ```

2. **Function App Scale Limit Not Applied**
   - Verify `WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT` is set in app settings
   - Check Premium plan configuration allows elastic scaling

3. **Code Deployment Fails**
   ```bash
   # Ensure you're in the right directory
   cd UpdateEngine/src
   
   # Build first
   dotnet build --configuration Release
   
   # Then publish
   func azure functionapp publish <function-app-name>
   ```

4. **Storage Connection Issues**
   - Verify storage account key in app settings
   - Check container permissions and access
   - Ensure firewall rules allow Function App access

### Debug Deployment
Enable verbose logging during deployment:

```bash
az deployment group create \
  --resource-group "rg-name" \
  --template-file "./Deployment/main.bicep" \
  --parameters "./Deployment/main.parameters.dev.json" \
  --verbose \
  --debug
```

## Cost Optimization

### Expected Costs (P3V3 Premium Plan)
- **Base cost**: ~$800-1000/month for always-on capacity
- **Scale-out cost**: Additional charges during high load periods
- **Storage cost**: ~$20-50/month depending on data volume
- **Application Insights**: ~$10-30/month based on telemetry volume

### Cost Reduction Strategies
1. **Use smaller Premium plan** (P1V3/P2V3) for lower-load scenarios
2. **Adjust scale limits** based on actual usage patterns
3. **Configure storage lifecycle policies** for blob data retention
4. **Optimize Application Insights sampling** to reduce telemetry costs

## Security Considerations

### Network Security
- HTTPS-only access enforced
- TLS 1.2 minimum version
- Optional VNET integration available

### Identity and Access
- System-assigned managed identity for Azure resource access
- Function-level authorization for sensitive endpoints
- Storage account access via managed identity (recommended)

### Compliance
- Data encryption at rest (storage account)
- Data encryption in transit (HTTPS/TLS)
- Audit logging via Application Insights
- GDPR compliance features available

## Next Steps

After successful deployment:

1. **Configure Windows Update Clients**
   - Point Group Policy to `https://<function-app-name>.azurewebsites.net/api/ClientWebService`

2. **Configure WSUS Servers**
   - Set upstream server to `https://<function-app-name>.azurewebsites.net/api/ServerSyncWebService`

3. **Set Up Monitoring**
   - Configure Application Insights alerts
   - Set up Azure Monitor dashboards
   - Enable Log Analytics queries

4. **Initial Sync**
   - Run metadata synchronization
   - Verify content serving functionality
   - Test with pilot group of clients

5. **Performance Tuning**
   - Monitor scaling behavior
   - Adjust configuration based on usage patterns
   - Optimize timer schedules for your environment