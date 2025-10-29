#!/bin/bash

# Microsoft Update Functions Deployment Script
# Deploys Azure Functions with Premium P3V3 plan and optimal scaling configuration

set -e

# Configuration
RESOURCE_GROUP=""
LOCATION="East US"
ENVIRONMENT="dev"
FUNCTION_APP_NAME="msupdate-functions"
SUBSCRIPTION_ID=""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to check if required tools are installed
check_prerequisites() {
    print_status "Checking prerequisites..."
    
    if ! command -v az &> /dev/null; then
        print_error "Azure CLI is not installed. Please install it first."
        exit 1
    fi
    
    if ! command -v func &> /dev/null; then
        print_warning "Azure Functions Core Tools not found. Install for local development."
    fi
    
    print_status "Prerequisites check completed."
}

# Function to parse command line arguments
parse_arguments() {
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
            -e|--environment)
                ENVIRONMENT="$2"
                shift 2
                ;;
            -n|--name)
                FUNCTION_APP_NAME="$2"
                shift 2
                ;;
            -s|--subscription)
                SUBSCRIPTION_ID="$2"
                shift 2
                ;;
            -h|--help)
                echo "Usage: $0 [options]"
                echo "Options:"
                echo "  -g, --resource-group   Azure Resource Group name (required)"
                echo "  -l, --location         Azure region (default: East US)"
                echo "  -e, --environment      Environment (dev/staging/prod, default: dev)"
                echo "  -n, --name             Function App name (default: msupdate-functions)"
                echo "  -s, --subscription     Azure Subscription ID (optional)"
                echo "  -h, --help             Show this help message"
                exit 0
                ;;
            *)
                print_error "Unknown option: $1"
                exit 1
                ;;
        esac
    done
    
    if [[ -z "$RESOURCE_GROUP" ]]; then
        print_error "Resource group is required. Use -g or --resource-group"
        exit 1
    fi
}

# Function to login and set subscription
setup_azure_cli() {
    print_status "Setting up Azure CLI..."
    
    # Check if already logged in
    if ! az account show &> /dev/null; then
        print_status "Logging into Azure..."
        az login
    fi
    
    # Set subscription if provided
    if [[ -n "$SUBSCRIPTION_ID" ]]; then
        print_status "Setting subscription to $SUBSCRIPTION_ID"
        az account set --subscription "$SUBSCRIPTION_ID"
    fi
    
    # Show current subscription
    CURRENT_SUB=$(az account show --query name -o tsv)
    print_status "Using subscription: $CURRENT_SUB"
}

# Function to create resource group if it doesn't exist
create_resource_group() {
    print_status "Checking resource group: $RESOURCE_GROUP"
    
    if ! az group show --name "$RESOURCE_GROUP" &> /dev/null; then
        print_status "Creating resource group: $RESOURCE_GROUP"
        az group create --name "$RESOURCE_GROUP" --location "$LOCATION"
    else
        print_status "Resource group $RESOURCE_GROUP already exists"
    fi
}

# Function to deploy infrastructure
deploy_infrastructure() {
    print_status "Deploying infrastructure using Bicep template..."
    
    local deployment_name="msupdate-deployment-$(date +%Y%m%d-%H%M%S)"
    
    az deployment group create \
        --resource-group "$RESOURCE_GROUP" \
        --name "$deployment_name" \
        --template-file "./Deployment/main.bicep" \
        --parameters "./Deployment/main.parameters.${ENVIRONMENT}.json" \
        --parameters functionAppName="$FUNCTION_APP_NAME" \
        --verbose
    
    if [[ $? -eq 0 ]]; then
        print_status "Infrastructure deployment completed successfully"
    else
        print_error "Infrastructure deployment failed"
        exit 1
    fi
}

# Function to get deployment outputs
get_deployment_outputs() {
    print_status "Retrieving deployment outputs..."
    
    local latest_deployment=$(az deployment group list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?starts_with(name, 'msupdate-deployment')] | max_by(@, &properties.timestamp).name" \
        --output tsv)
    
    if [[ -n "$latest_deployment" ]]; then
        FUNCTION_APP_URL=$(az deployment group show \
            --resource-group "$RESOURCE_GROUP" \
            --name "$latest_deployment" \
            --query "properties.outputs.functionAppUrl.value" \
            --output tsv)
        
        FUNCTION_APP_FULL_NAME=$(az deployment group show \
            --resource-group "$RESOURCE_GROUP" \
            --name "$latest_deployment" \
            --query "properties.outputs.functionAppName.value" \
            --output tsv)
        
        STORAGE_ACCOUNT_NAME=$(az deployment group show \
            --resource-group "$RESOURCE_GROUP" \
            --name "$latest_deployment" \
            --query "properties.outputs.storageAccountName.value" \
            --output tsv)
        
        print_status "Function App URL: $FUNCTION_APP_URL"
        print_status "Function App Name: $FUNCTION_APP_FULL_NAME"
        print_status "Storage Account: $STORAGE_ACCOUNT_NAME"
    else
        print_warning "Could not retrieve deployment outputs"
    fi
}

# Function to deploy function code
deploy_function_code() {
    print_status "Deploying function code..."
    
    if [[ -z "$FUNCTION_APP_FULL_NAME" ]]; then
        print_error "Function app name not available. Cannot deploy code."
        return 1
    fi
    
    # Build the project
    print_status "Building the function project..."
    cd UpdateEngine/src
    dotnet build --configuration Release
    
    # Publish to Azure
    print_status "Publishing to Azure Functions..."
    func azure functionapp publish "$FUNCTION_APP_FULL_NAME"
    
    cd ../../
    
    if [[ $? -eq 0 ]]; then
        print_status "Function code deployment completed successfully"
    else
        print_error "Function code deployment failed"
        return 1
    fi
}

# Function to verify deployment
verify_deployment() {
    print_status "Verifying deployment..."
    
    if [[ -n "$FUNCTION_APP_URL" ]]; then
        # Test health endpoint
        local health_url="${FUNCTION_APP_URL}/api/health"
        print_status "Testing health endpoint: $health_url"
        
        local response=$(curl -s -o /dev/null -w "%{http_code}" "$health_url" || echo "000")
        
        if [[ "$response" == "200" ]]; then
            print_status "Health check passed ✓"
        else
            print_warning "Health check returned status: $response"
        fi
        
        # Test store status endpoint
        local status_url="${FUNCTION_APP_URL}/api/GetStoreStatus"
        print_status "Testing store status endpoint: $status_url"
        
        local status_response=$(curl -s -o /dev/null -w "%{http_code}" "$status_url" || echo "000")
        
        if [[ "$status_response" == "200" ]]; then
            print_status "Store status check passed ✓"
        else
            print_warning "Store status check returned status: $status_response"
        fi
    fi
}

# Function to display deployment summary
display_summary() {
    echo
    print_status "=== Deployment Summary ==="
    echo "Environment: $ENVIRONMENT"
    echo "Resource Group: $RESOURCE_GROUP"
    echo "Location: $LOCATION"
    echo "Function App Name: $FUNCTION_APP_FULL_NAME"
    echo "Function App URL: $FUNCTION_APP_URL"
    echo "Storage Account: $STORAGE_ACCOUNT_NAME"
    echo
    print_status "Key Endpoints:"
    echo "  - Health Check: ${FUNCTION_APP_URL}/api/health"
    echo "  - Store Status: ${FUNCTION_APP_URL}/api/GetStoreStatus"
    echo "  - Client Web Service: ${FUNCTION_APP_URL}/api/ClientWebService/ClientWebService.asmx"
    echo "  - Server Sync: ${FUNCTION_APP_URL}/api/ServerSyncWebService/ServerSyncWebService.asmx"
    echo
    print_status "Configuration:"
    echo "  - Premium Plan: P3V3 (8 vCPUs, 32GB RAM)"
    echo "  - Max Scale Out: 25 instances"
    echo "  - Always On: Enabled"
    echo "  - Application Insights: Enabled"
    echo
}

# Main deployment flow
main() {
    echo "Microsoft Update Functions Deployment Script"
    echo "============================================"
    
    parse_arguments "$@"
    check_prerequisites
    setup_azure_cli
    create_resource_group
    deploy_infrastructure
    get_deployment_outputs
    
    # Ask user if they want to deploy code
    if command -v func &> /dev/null; then
        read -p "Deploy function code? (y/n): " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            deploy_function_code
        fi
    else
        print_warning "Azure Functions Core Tools not found. Skipping code deployment."
        print_status "To deploy code later, run: func azure functionapp publish $FUNCTION_APP_FULL_NAME"
    fi
    
    verify_deployment
    display_summary
    
    print_status "Deployment completed successfully!"
}

# Run main function with all arguments
main "$@"