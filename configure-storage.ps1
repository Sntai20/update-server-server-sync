#!/usr/bin/env pwsh

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("FileSystem", "AzureEmulator")]
    [string]$StorageMode = "FileSystem",
    
    [Parameter(Mandatory=$false)]
    [switch]$StartFunctions,
    
    [Parameter(Mandatory=$false)]
    [switch]$UseAppHost
)

# Load storage configuration
$configPath = "./MicrosoftUpdateFunctions/src/storage-config.json"
if (-not (Test-Path $configPath)) {
    Write-Error "Configuration file not found: $configPath"
    exit 1
}

$config = Get-Content $configPath | ConvertFrom-Json

# Update storage mode in config
$config.StorageMode = $StorageMode

# Save updated config
$config | ConvertTo-Json -Depth 4 | Set-Content $configPath

Write-Host "🔧 Storage mode set to: $StorageMode" -ForegroundColor Green

# Generate appropriate local.settings.json
$localSettings = @{
    IsEncrypted = $false
    Values = @{
        FUNCTIONS_WORKER_RUNTIME = "dotnet-isolated"
        AZURE_FUNCTIONS_ENVIRONMENT = "Development"
        AzureWebJobsSecretStorageType = "files"
        ContentHttpRoot = $config.ServiceConfiguration.ContentUrl
        ServiceConfigurationJson = ($config.ServiceConfiguration | ConvertTo-Json -Compress)
    }
}

if ($StorageMode -eq "FileSystem") {
    Write-Host "📁 Configuring for local file system storage..." -ForegroundColor Yellow
    
    # Create directories
    $metadataPath = $config.FileSystemStorage.MetadataStorePath
    $contentPath = $config.FileSystemStorage.ContentStorePath
    
    New-Item -ItemType Directory -Force -Path "./MicrosoftUpdateFunctions/src/$metadataPath" | Out-Null
    New-Item -ItemType Directory -Force -Path "./MicrosoftUpdateFunctions/src/$contentPath" | Out-Null
    
    $localSettings.Values.AzureWebJobsStorage = ""
    $localSettings.Values.MetadataStorePath = $metadataPath
    $localSettings.Values.ContentStorePath = $contentPath
    
    Write-Host "  ✅ Metadata will be stored in: $metadataPath"
    Write-Host "  ✅ Content will be stored in: $contentPath"
    
} elseif ($StorageMode -eq "AzureEmulator") {
    Write-Host "🐳 Configuring for Azure Storage Emulator..." -ForegroundColor Cyan
    
    $connectionString = $config.AzureStorageEmulator.ConnectionString
    $localSettings.Values.AzureWebJobsStorage = $connectionString
    $localSettings.Values.StorageConnection = $connectionString
    $localSettings.Values.MetadataStorageConnection = $connectionString
    $localSettings.Values.ContentStorageConnection = $connectionString
    $localSettings.Values.MetadataStorePath = $config.AzureStorageEmulator.MetadataContainerName
    $localSettings.Values.ContentStorePath = $config.AzureStorageEmulator.ContentContainerName
    
    Write-Host "  ✅ Using Azure Storage Emulator connection"
    Write-Host "  ✅ Metadata container: $($config.AzureStorageEmulator.MetadataContainerName)"
    Write-Host "  ✅ Content container: $($config.AzureStorageEmulator.ContentContainerName)"
}

# Write local.settings.json
$localSettingsPath = "./MicrosoftUpdateFunctions/src/local.settings.json"
$localSettings | ConvertTo-Json -Depth 3 | Set-Content $localSettingsPath

Write-Host "💾 Updated $localSettingsPath" -ForegroundColor Green

# Update AppHost Program.cs if using AppHost
if ($UseAppHost) {
    Write-Host "🚀 Configuring AppHost for $StorageMode mode..." -ForegroundColor Magenta
    
    $appHostPath = "./MicrosoftUpdateFunctions.AppHost/Program.cs"
    
    if ($StorageMode -eq "AzureEmulator") {
        $appHostContent = @"
using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Storage Emulator as a containerized resource
var storage = builder.AddAzureStorage("storage").RunAsEmulator();

// Add the Microsoft Update Functions with Azure Storage Emulator
var updateFunctions = builder.AddExecutable("update-functions", "func", "../MicrosoftUpdateFunctions/src", "start", "--port", "7071")
    .WithEnvironment("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated")
    .WithEnvironment("AzureWebJobsSecretStorageType", "files")
    .WithEnvironment("AZURE_FUNCTIONS_ENVIRONMENT", "Development")
    .WithEnvironment("ContentHttpRoot", "http://localhost:7071/api/content")
    .WithEnvironment("ServiceConfigurationJson", ``````
        {
            "ServiceUrl": "http://localhost:7071",
            "ContentUrl": "http://localhost:7071/api/content",
            "MaxUpdateCount": 1000,
            "SupportedCategories": ["Security Updates", "Critical Updates", "Feature Packs"]
        }
        ``````)
    .WithReference(storage)
    .WithHttpEndpoint(port: 7071, name: "http");

var app = builder.Build();

app.Run();
"@
    } else {
        $appHostContent = @"
using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Add the Microsoft Update Functions with local file system storage
var updateFunctions = builder.AddExecutable("update-functions", "func", "../MicrosoftUpdateFunctions/src", "start", "--port", "7071")
    .WithEnvironment("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated")
    .WithEnvironment("AzureWebJobsStorage", "")
    .WithEnvironment("AzureWebJobsSecretStorageType", "files")
    .WithEnvironment("AZURE_FUNCTIONS_ENVIRONMENT", "Development")
    .WithEnvironment("MetadataStorePath", "./store")
    .WithEnvironment("ContentStorePath", "./content")
    .WithEnvironment("ContentHttpRoot", "http://localhost:7071/api/content")
    .WithEnvironment("ServiceConfigurationJson", ``````
        {
            "ServiceUrl": "http://localhost:7071",
            "ContentUrl": "http://localhost:7071/api/content",
            "MaxUpdateCount": 1000,
            "SupportedCategories": ["Security Updates", "Critical Updates", "Feature Packs"]
        }
        ``````)
    .WithHttpEndpoint(port: 7071, name: "http");

var app = builder.Build();

app.Run();
"@
    }
    
    Set-Content -Path $appHostPath -Value $appHostContent
    Write-Host "  ✅ Updated AppHost configuration"
}

# Display current configuration
Write-Host ""
Write-Host "📋 Current Configuration:" -ForegroundColor White
Write-Host "  Storage Mode: $StorageMode"
Write-Host "  Service URL: $($config.ServiceConfiguration.ServiceUrl)"
Write-Host "  Content URL: $($config.ServiceConfiguration.ContentUrl)"

if ($StorageMode -eq "AzureEmulator") {
    Write-Host ""
    Write-Host "⚠️  Note: Azure Storage Emulator must be running for advanced triggers to work." -ForegroundColor Yellow
    Write-Host "   You can start it with: azurite --silent --location ./azurite-data &"
}

# Start functions if requested
if ($StartFunctions -and $UseAppHost) {
    Write-Host ""
    Write-Host "🚀 Starting AppHost..." -ForegroundColor Green
    Set-Location "./MicrosoftUpdateFunctions.AppHost"
    dotnet run
} elseif ($StartFunctions) {
    Write-Host ""
    Write-Host "🚀 Starting Azure Functions..." -ForegroundColor Green
    Set-Location "./MicrosoftUpdateFunctions/src"
    func start --port 7071
}