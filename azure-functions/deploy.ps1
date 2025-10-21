# PowerShell deployment script for Microsoft Update Azure Functions

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup,
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "East US",
    
    [Parameter(Mandatory=$true)]
    [string]$FunctionAppName,
    
    [Parameter(Mandatory=$true)]
    [string]$StorageAccountName,
    
    [Parameter(Mandatory=$true)]
    [string]$MetadataStorageName,
    
    [Parameter(Mandatory=$true)]
    [string]$ContentStorageName
)

# Check if Azure CLI is installed
try {
    az --version | Out-Null
} catch {
    Write-Error "Azure CLI is not installed or not in PATH"
    exit 1
}

# Check if logged in to Azure
try {
    az account show | Out-Null
} catch {
    Write-Error "Not logged in to Azure. Run 'az login' first."
    exit 1
}

Write-Host "Deploying Microsoft Update Azure Functions..." -ForegroundColor Green
Write-Host "Resource Group: $ResourceGroup"
Write-Host "Location: $Location"
Write-Host "Function App: $FunctionAppName"

# Create resource group if it doesn't exist
Write-Host "Creating resource group..." -ForegroundColor Yellow
az group create --name $ResourceGroup --location $Location

# Deploy ARM template
Write-Host "Deploying Azure resources..." -ForegroundColor Yellow
az deployment group create `
    --resource-group $ResourceGroup `
    --template-file deploy/azuredeploy.json `
    --parameters `
        functionAppName=$FunctionAppName `
        storageAccountName=$StorageAccountName `
        metadataStorageName=$MetadataStorageName `
        contentStorageName=$ContentStorageName `
        location=$Location

# Build and publish the function
Write-Host "Building and publishing function..." -ForegroundColor Yellow
dotnet build --configuration Release

# Check if func tools are installed
try {
    func --version | Out-Null
} catch {
    Write-Error "Azure Functions Core Tools not installed. Install from https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local"
    exit 1
}

# Publish to Azure
func azure functionapp publish $FunctionAppName

Write-Host "Deployment completed successfully!" -ForegroundColor Green
Write-Host "Function App URL: https://$FunctionAppName.azurewebsites.net" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Upload metadata to the metadata storage account"
Write-Host "2. Upload content to the content storage account"
Write-Host "3. Configure Windows Update clients to use: https://$FunctionAppName.azurewebsites.net/api/ClientWebService"
Write-Host "4. Configure WSUS servers to sync from: https://$FunctionAppName.azurewebsites.net/api/ServerSyncWebService"