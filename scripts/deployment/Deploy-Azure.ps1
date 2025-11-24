# Microsoft Update Functions Deployment Script
# Deploys Azure Functions with Premium P3V3 plan and optimal scaling configuration

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "East US",
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("dev", "staging", "prod")]
    [string]$Environment = "dev",
    
    [Parameter(Mandatory=$false)]
    [string]$FunctionAppName = "msupdate-functions",
    
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipCodeDeployment,
    
    [Parameter(Mandatory=$false)]
    [switch]$Force
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Function to write colored output
function Write-Status {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Green
}

function Write-Warning-Status {
    param([string]$Message)
    Write-Host "[WARNING] $Message" -ForegroundColor Yellow
}

function Write-Error-Status {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

# Function to check prerequisites
function Test-Prerequisites {
    Write-Status "Checking prerequisites..."
    
    # Check Azure CLI
    try {
        $azVersion = az version --output json | ConvertFrom-Json
        Write-Status "Azure CLI version: $($azVersion.'azure-cli')"
    }
    catch {
        Write-Error-Status "Azure CLI is not installed or not accessible. Please install Azure CLI."
        exit 1
    }
    
    # Check Azure Functions Core Tools
    try {
        $funcVersion = func --version
        Write-Status "Azure Functions Core Tools version: $funcVersion"
    }
    catch {
        Write-Warning-Status "Azure Functions Core Tools not found. Code deployment will be skipped."
        $script:SkipCodeDeployment = $true
    }
    
    # Check .NET SDK
    try {
        $dotnetVersion = dotnet --version
        Write-Status ".NET SDK version: $dotnetVersion"
    }
    catch {
        Write-Warning-Status ".NET SDK not found. Code deployment may fail."
    }
    
    Write-Status "Prerequisites check completed."
}

# Function to setup Azure CLI
function Initialize-AzureCLI {
    Write-Status "Setting up Azure CLI..."
    
    # Check if logged in
    try {
        $currentAccount = az account show --output json | ConvertFrom-Json
        Write-Status "Currently logged in as: $($currentAccount.user.name)"
    }
    catch {
        Write-Status "Logging into Azure..."
        az login
        $currentAccount = az account show --output json | ConvertFrom-Json
    }
    
    # Set subscription if provided
    if ($SubscriptionId) {
        Write-Status "Setting subscription to: $SubscriptionId"
        az account set --subscription $SubscriptionId
    }
    
    # Show current subscription
    $currentSub = az account show --query name --output tsv
    Write-Status "Using subscription: $currentSub"
}

# Function to create resource group
function New-ResourceGroupIfNotExists {
    Write-Status "Checking resource group: $ResourceGroupName"
    
    $rgExists = az group exists --name $ResourceGroupName --output tsv
    
    if ($rgExists -eq "false") {
        Write-Status "Creating resource group: $ResourceGroupName"
        az group create --name $ResourceGroupName --location $Location --output none
        
        if ($LASTEXITCODE -ne 0) {
            Write-Error-Status "Failed to create resource group"
            exit 1
        }
    }
    else {
        Write-Status "Resource group $ResourceGroupName already exists"
    }
}

# Function to deploy infrastructure
function Deploy-Infrastructure {
    Write-Status "Deploying infrastructure using Bicep template..."
    
    $deploymentName = "msupdate-deployment-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    # Set template and parameter file paths
    $templateFile = "./Deployment/main.bicep"
    $parametersFile = "./Deployment/main.parameters.$Environment.json"    # Check if files exist
    if (-not (Test-Path $templateFile)) {
        Write-Error-Status "Template file not found: $templateFile"
        exit 1
    }
    
    if (-not (Test-Path $parametersFile)) {
        Write-Error-Status "Parameters file not found: $parametersFile"
        exit 1
    }
    
    # Deploy using Bicep
    $deploymentResult = az deployment group create `
        --resource-group $ResourceGroupName `
        --name $deploymentName `
        --template-file $templateFile `
        --parameters $parametersFile `
        --parameters functionAppName=$FunctionAppName `
        --output json
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error-Status "Infrastructure deployment failed"
        exit 1
    }
    
    Write-Status "Infrastructure deployment completed successfully"
    return $deploymentResult | ConvertFrom-Json
}

# Function to get deployment outputs
function Get-DeploymentOutputs {
    param([string]$DeploymentName)
    
    Write-Status "Retrieving deployment outputs..."
    
    try {
        $deployment = az deployment group show `
            --resource-group $ResourceGroupName `
            --name $DeploymentName `
            --output json | ConvertFrom-Json
        
        $outputs = @{
            FunctionAppUrl = $deployment.properties.outputs.functionAppUrl.value
            FunctionAppName = $deployment.properties.outputs.functionAppName.value
            StorageAccountName = $deployment.properties.outputs.storageAccountName.value
            ApplicationInsightsName = $deployment.properties.outputs.applicationInsightsName.value
        }
        
        Write-Status "Function App URL: $($outputs.FunctionAppUrl)"
        Write-Status "Function App Name: $($outputs.FunctionAppName)"
        Write-Status "Storage Account: $($outputs.StorageAccountName)"
        
        return $outputs
    }
    catch {
        Write-Warning-Status "Could not retrieve deployment outputs: $($_.Exception.Message)"
        return $null
    }
}

# Function to deploy function code
function Deploy-FunctionCode {
    param([string]$FunctionAppName)
    
    Write-Status "Deploying function code..."
    
    if (-not $FunctionAppName) {
        Write-Error-Status "Function app name not available. Cannot deploy code."
        return $false
    }
    
    try {
        # Build the project
        Write-Status "Building the function project..."
        Push-Location "UpdateEngine/src"
        
        dotnet build --configuration Release
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed"
        }
        
        # Publish to Azure
        Write-Status "Publishing to Azure Functions..."
        func azure functionapp publish $FunctionAppName
        
        if ($LASTEXITCODE -ne 0) {
            throw "Publish failed"
        }
        
        Write-Status "Function code deployment completed successfully"
        return $true
    }
    catch {
        Write-Error-Status "Function code deployment failed: $($_.Exception.Message)"
        return $false
    }
    finally {
        Pop-Location
    }
}

# Function to verify deployment
function Test-Deployment {
    param([string]$FunctionAppUrl)
    
    Write-Status "Verifying deployment..."
    
    if (-not $FunctionAppUrl) {
        Write-Warning-Status "Function App URL not available. Skipping verification."
        return
    }
    
    # Test health endpoint
    try {
        $healthUrl = "$FunctionAppUrl/api/health"
        Write-Status "Testing health endpoint: $healthUrl"
        
        $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 30
        
        if ($response.StatusCode -eq 200) {
            Write-Status "Health check passed ✓"
        }
        else {
            Write-Warning-Status "Health check returned status: $($response.StatusCode)"
        }
    }
    catch {
        Write-Warning-Status "Health check failed: $($_.Exception.Message)"
    }
    
    # Test store status endpoint
    try {
        $statusUrl = "$FunctionAppUrl/api/GetStoreStatus"
        Write-Status "Testing store status endpoint: $statusUrl"
        
        $response = Invoke-WebRequest -Uri $statusUrl -UseBasicParsing -TimeoutSec 30
        
        if ($response.StatusCode -eq 200) {
            Write-Status "Store status check passed ✓"
        }
        else {
            Write-Warning-Status "Store status check returned status: $($response.StatusCode)"
        }
    }
    catch {
        Write-Warning-Status "Store status check failed: $($_.Exception.Message)"
    }
}

# Function to display deployment summary
function Show-DeploymentSummary {
    param([hashtable]$Outputs)
    
    Write-Host ""
    Write-Status "=== Deployment Summary ==="
    Write-Host "Environment: $Environment" -ForegroundColor Cyan
    Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor Cyan
    Write-Host "Location: $Location" -ForegroundColor Cyan
    
    if ($Outputs) {
        Write-Host "Function App Name: $($Outputs.FunctionAppName)" -ForegroundColor Cyan
        Write-Host "Function App URL: $($Outputs.FunctionAppUrl)" -ForegroundColor Cyan
        Write-Host "Storage Account: $($Outputs.StorageAccountName)" -ForegroundColor Cyan
        
        Write-Host ""
        Write-Status "Key Endpoints:"
        Write-Host "  - Health Check: $($Outputs.FunctionAppUrl)/api/health" -ForegroundColor White
        Write-Host "  - Store Status: $($Outputs.FunctionAppUrl)/api/GetStoreStatus" -ForegroundColor White
        Write-Host "  - Client Web Service: $($Outputs.FunctionAppUrl)/api/ClientWebService/ClientWebService.asmx" -ForegroundColor White
        Write-Host "  - Server Sync: $($Outputs.FunctionAppUrl)/api/ServerSyncWebService/ServerSyncWebService.asmx" -ForegroundColor White
    }
    
    Write-Host ""
    Write-Status "Configuration:"
    Write-Host "  - Premium Plan: P3V3 (8 vCPUs, 32GB RAM)" -ForegroundColor White
    Write-Host "  - Max Scale Out: 25 instances" -ForegroundColor White
    Write-Host "  - Always On: Enabled" -ForegroundColor White
    Write-Host "  - Application Insights: Enabled" -ForegroundColor White
    Write-Host ""
}

# Main execution
try {
    Write-Host "Microsoft Update Functions Deployment Script" -ForegroundColor Magenta
    Write-Host "============================================" -ForegroundColor Magenta
    Write-Host ""
    
    # Execute deployment steps
    Test-Prerequisites
    Initialize-AzureCLI
    New-ResourceGroupIfNotExists
    
    $deploymentResult = Deploy-Infrastructure
    $deploymentName = $deploymentResult.name
    
    $outputs = Get-DeploymentOutputs -DeploymentName $deploymentName
    
    # Deploy code if not skipped and tools are available
    if (-not $SkipCodeDeployment -and $outputs -and $outputs.FunctionAppName) {
        if (-not $Force) {
            $choice = Read-Host "Deploy function code? (y/n)"
            if ($choice -match '^[Yy]') {
                Deploy-FunctionCode -FunctionAppName $outputs.FunctionAppName
            }
        }
        else {
            Deploy-FunctionCode -FunctionAppName $outputs.FunctionAppName
        }
    }
    elseif ($SkipCodeDeployment) {
        Write-Status "Code deployment skipped as requested"
        if ($outputs.FunctionAppName) {
            Write-Status "To deploy code later, run: func azure functionapp publish $($outputs.FunctionAppName)"
        }
    }
    
    # Verify deployment
    if ($outputs -and $outputs.FunctionAppUrl) {
        Test-Deployment -FunctionAppUrl $outputs.FunctionAppUrl
    }
    
    # Show summary
    Show-DeploymentSummary -Outputs $outputs
    
    Write-Status "Deployment completed successfully!"
}
catch {
    Write-Error-Status "Deployment failed: $($_.Exception.Message)"
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 1
}