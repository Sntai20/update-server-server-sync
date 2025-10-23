#!/bin/bash

# Microsoft Update Azure Functions Deployment Script

set -e

# Configuration
RESOURCE_GROUP=""
LOCATION="East US"
FUNCTION_APP_NAME=""
STORAGE_ACCOUNT_NAME=""
METADATA_STORAGE_NAME=""
CONTENT_STORAGE_NAME=""

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -g|--resource-group)
            RESOURCE_GROUP="$2"
            shift 2
            ;;
        -l|--location)
            LOCATION="$2"
            shift 2
            ;;
        -n|--function-app-name)
            FUNCTION_APP_NAME="$2"
            shift 2
            ;;
        -s|--storage-account)
            STORAGE_ACCOUNT_NAME="$2"
            shift 2
            ;;
        -m|--metadata-storage)
            METADATA_STORAGE_NAME="$2"
            shift 2
            ;;
        -c|--content-storage)
            CONTENT_STORAGE_NAME="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [options]"
            echo "Options:"
            echo "  -g, --resource-group      Azure resource group name (required)"
            echo "  -l, --location           Azure region (default: East US)"
            echo "  -n, --function-app-name  Function app name (required)"
            echo "  -s, --storage-account    Storage account for functions (required)"
            echo "  -m, --metadata-storage   Storage account for metadata (required)"
            echo "  -c, --content-storage    Storage account for content (required)"
            echo "  -h, --help              Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option $1"
            exit 1
            ;;
    esac
done

# Validate required parameters
if [[ -z "$RESOURCE_GROUP" || -z "$FUNCTION_APP_NAME" || -z "$STORAGE_ACCOUNT_NAME" || -z "$METADATA_STORAGE_NAME" || -z "$CONTENT_STORAGE_NAME" ]]; then
    echo "Error: Missing required parameters"
    echo "Use --help for usage information"
    exit 1
fi

echo "Deploying Microsoft Update Azure Functions..."
echo "Resource Group: $RESOURCE_GROUP"
echo "Location: $LOCATION"
echo "Function App: $FUNCTION_APP_NAME"

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo "Error: Azure CLI is not installed"
    exit 1
fi

# Check if logged in to Azure
if ! az account show &> /dev/null; then
    echo "Error: Not logged in to Azure. Run 'az login' first."
    exit 1
fi

# Create resource group if it doesn't exist
echo "Creating resource group..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION"

# Deploy ARM template
echo "Deploying Azure resources..."
az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --template-file deploy/azuredeploy.json \
    --parameters \
        functionAppName="$FUNCTION_APP_NAME" \
        storageAccountName="$STORAGE_ACCOUNT_NAME" \
        metadataStorageName="$METADATA_STORAGE_NAME" \
        contentStorageName="$CONTENT_STORAGE_NAME" \
        location="$LOCATION"

# Build and publish the function
echo "Building and publishing function..."
dotnet build --configuration Release

# Publish to Azure
func azure functionapp publish "$FUNCTION_APP_NAME"

echo "Deployment completed successfully!"
echo "Function App URL: https://$FUNCTION_APP_NAME.azurewebsites.net"
echo ""
echo "Next steps:"
echo "1. Upload metadata to the metadata storage account"
echo "2. Upload content to the content storage account" 
echo "3. Configure Windows Update clients to use: https://$FUNCTION_APP_NAME.azurewebsites.net/api/ClientWebService"
echo "4. Configure WSUS servers to sync from: https://$FUNCTION_APP_NAME.azurewebsites.net/api/ServerSyncWebService"