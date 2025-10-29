@description('The name of the Function App')
param functionAppName string

@description('The name of the App Service Plan')
param appServicePlanName string = '${functionAppName}-plan'

@description('The name of the Storage Account')
param storageAccountName string = '${functionAppName}storage'

@description('The name of the Application Insights instance')
param applicationInsightsName string = '${functionAppName}-insights'

@description('The name of the Log Analytics Workspace')
param logAnalyticsWorkspaceName string = '${functionAppName}-logs'

@description('The Azure region where resources will be deployed')
param location string = resourceGroup().location

@description('Environment name (dev, staging, prod)')
@allowed(['dev', 'staging', 'prod'])
param environment string = 'dev'

@description('Maximum number of instances for auto-scaling')
param maxScaleOut int = 25

@description('Enable Application Insights')
param enableApplicationInsights bool = true

@description('Storage account type')
@allowed(['Standard_LRS', 'Standard_GRS', 'Standard_RAGRS', 'Premium_LRS'])
param storageAccountType string = 'Standard_LRS'

@description('Function App runtime stack')
param functionWorkerRuntime string = 'dotnet-isolated'

@description('Function App runtime version')
param functionsExtensionVersion string = '~4'

@description('.NET version')
param netFrameworkVersion string = 'v9.0'

// Variables
var functionAppNameUnique = '${functionAppName}-${environment}-${uniqueString(resourceGroup().id)}'
var storageAccountNameUnique = '${replace(storageAccountName, '-', '')}${uniqueString(resourceGroup().id)}'
var appInsightsNameUnique = '${applicationInsightsName}-${environment}'

// Log Analytics Workspace
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2021-12-01-preview' = if (enableApplicationInsights) {
  name: '${logAnalyticsWorkspaceName}-${environment}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    features: {
      searchVersion: 1
      legacy: 0
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

// Application Insights
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = if (enableApplicationInsights) {
  name: appInsightsNameUnique
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Request_Source: 'rest'
    WorkspaceResourceId: enableApplicationInsights ? logAnalyticsWorkspace.id : null
  }
}

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: storageAccountNameUnique
  location: location
  sku: {
    name: storageAccountType
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    encryption: {
      services: {
        file: {
          keyType: 'Account'
          enabled: true
        }
        blob: {
          keyType: 'Account'
          enabled: true
        }
      }
      keySource: 'Microsoft.Storage'
    }
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
  }
}

// App Service Plan (Premium P3V3)
resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: '${appServicePlanName}-${environment}'
  location: location
  sku: {
    name: 'P3V3'
    tier: 'Premium'
    size: 'P3V3'
    family: 'Pv3'
    capacity: 1
  }
  kind: 'elastic'
  properties: {
    perSiteScaling: false
    elasticScaleEnabled: true
    maximumElasticWorkerCount: maxScaleOut
    isSpot: false
    reserved: false
    isXenon: false
    hyperV: false
    targetWorkerCount: 0
    targetWorkerSizeId: 0
    zoneRedundant: false
  }
}

// Function App
resource functionApp 'Microsoft.Web/sites@2022-03-01' = {
  name: functionAppNameUnique
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    reserved: false
    isXenon: false
    hyperV: false
    vnetRouteAllEnabled: false
    vnetImagePullEnabled: false
    vnetContentShareEnabled: false
    siteConfig: {
      numberOfWorkers: 1
      alwaysOn: true
      http20Enabled: true
      functionAppScaleLimit: maxScaleOut
      minimumElasticInstanceCount: 1
      use32BitWorkerProcess: false
      ftpsState: 'FtpsOnly'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      netFrameworkVersion: netFrameworkVersion
      powerShellVersion: '~7'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${az.environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${az.environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppNameUnique)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: functionsExtensionVersion
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: functionWorkerRuntime
        }
        {
          name: 'WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT'
          value: string(maxScaleOut)
        }
        {
          name: 'WEBSITE_ENABLE_SYNC_UPDATE_SITE'
          value: 'true'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: enableApplicationInsights ? applicationInsights.properties.InstrumentationKey : ''
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: enableApplicationInsights ? applicationInsights.properties.ConnectionString : ''
        }
        // Microsoft Update Server specific settings
        {
          name: 'UseAzureStorage'
          value: 'true'
        }
        {
          name: 'MetadataContainerName'
          value: 'metadata'
        }
        {
          name: 'ContentContainerName'
          value: 'content'
        }
        {
          name: 'MetadataStorePath'
          value: './store'
        }
        {
          name: 'ContentStorePath'
          value: './content'
        }
        {
          name: 'DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER'
          value: '0'
        }
        // Timer schedule settings (TimeSpan format)
        {
          name: 'TimerSchedules:HealthCheck:Interval'
          value: '01:00:00'
        }
        {
          name: 'TimerSchedules:HealthCheck:UseTimeSpan'
          value: 'true'
        }
        {
          name: 'TimerSchedules:ContentSync:Interval'
          value: '7.00:00:00'
        }
        {
          name: 'TimerSchedules:ContentSync:UseTimeSpan'
          value: 'true'
        }
        {
          name: 'TimerSchedules:ComprehensiveSync:Interval'
          value: '14.00:00:00'
        }
        {
          name: 'TimerSchedules:ComprehensiveSync:UseTimeSpan'
          value: 'true'
        }
        {
          name: 'TimerSchedules:Maintenance:Interval'
          value: '30.00:00:00'
        }
        {
          name: 'TimerSchedules:Maintenance:UseTimeSpan'
          value: 'true'
        }
        // Service configuration
        {
          name: 'ServiceConfigurationJson'
          value: '{"ServiceUrl":"https://${functionAppNameUnique}.azurewebsites.net","MaxUpdateCount":1000,"EnableContentServing":true}'
        }
        // Environment-specific settings
        {
          name: 'Environment'
          value: environment
        }
        // Performance settings for P3V3
        {
          name: 'WEBSITE_HTTPSCALEV2_ENABLED'
          value: '1'
        }
        {
          name: 'FUNCTIONS_WORKER_PROCESS_COUNT'
          value: '4'
        }
        {
          name: 'WEBSITE_CONTENTOVERVNET'
          value: '0'
        }
      ]
    }
    httpsOnly: true
    clientAffinityEnabled: false
  }
}

// Storage containers for metadata and content
resource metadataContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  name: '${storageAccount.name}/default/metadata'
  properties: {
    publicAccess: 'None'
    metadata: {}
  }
}

resource contentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  name: '${storageAccount.name}/default/content'
  properties: {
    publicAccess: 'None'
    metadata: {}
  }
}

// Outputs
output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output storageAccountName string = storageAccount.name
output applicationInsightsName string = enableApplicationInsights ? applicationInsights.name : ''
output resourceGroupName string = resourceGroup().name
output principalId string = functionApp.identity.principalId