# Test script to verify Azurite storage configuration
Write-Host "=== Testing Azurite Storage Configuration ===" -ForegroundColor Green

# Stop any existing processes
Write-Host "Stopping existing processes..."
Get-Process | Where-Object {$_.ProcessName -like "*func*" -or $_.ProcessName -like "*azurite*"} | Stop-Process -Force -ErrorAction SilentlyContinue

# Start Azurite manually
Write-Host "Starting Azurite storage emulator..."
Start-Process -FilePath "npx" -ArgumentList "azurite", "--silent", "--location", "c:/temp/azurite", "--debug", "c:/temp/azurite/debug.log" -WindowStyle Hidden

Start-Sleep 5

# Check if Azurite is running
Write-Host "Checking if Azurite is accessible..."
try {
    $response = Invoke-WebRequest -Uri "http://127.0.0.1:10000/" -Method GET -TimeoutSec 5
    Write-Host "✅ Azurite is running on port 10000" -ForegroundColor Green
} catch {
    Write-Host "❌ Azurite not accessible: $($_.Exception.Message)" -ForegroundColor Red
}

# Update local.settings.json for Functions to use Azurite
Write-Host "Updating UpdateEngine local.settings.json for Azurite..."
$localSettings = @{
    IsEncrypted = $false
    Values = @{
        "FUNCTIONS_WORKER_RUNTIME" = "dotnet-isolated"
        "AzureWebJobsSecretStorageType" = "files"
        "AzureWebJobsStorage" = "UseDevelopmentStorage=true"
        "MetadataStorePath" = "../../store"
        "ContentStorePath" = "../../content"
        "UseAzureStorageForMetadata" = "true"
        "UseAzureStorageForContent" = "true"
        "MetadataContainerName" = "metadata"
        "ContentContainerName" = "content"
        "ServiceConfigurationJson" = '{"ServiceUrl":"http://localhost:7071","SupportsContentDownload":true}'
    }
}

$localSettingsJson = $localSettings | ConvertTo-Json -Depth 3
Set-Content -Path "UpdateEngine/src/local.settings.json" -Value $localSettingsJson

Write-Host "✅ Updated local.settings.json for Azurite" -ForegroundColor Green

# Start Functions
Write-Host "Starting Azure Functions..."
Set-Location "UpdateEngine/src"
Start-Process -FilePath "func" -ArgumentList "start", "--port", "7071" -WindowStyle Minimized
Set-Location "../.."

Start-Sleep 20

# Test the configuration
Write-Host "Testing Functions with Azurite..."
try {
    $health = Invoke-RestMethod -Uri "http://localhost:7071/api/UniversalHealth" -Method GET -TimeoutSec 10
    Write-Host "✅ Health check successful: $($health.isHealthy)" -ForegroundColor Green
    
    $storage = Invoke-RestMethod -Uri "http://localhost:7071/api/StorageDiagnostics" -Method GET -TimeoutSec 10
    Write-Host "✅ Storage diagnostics successful" -ForegroundColor Green
    Write-Host "Storage type: $($storage.MetadataStore.Type)"
    
    if ($storage.MetadataStore.Type -eq "AzureBlobPackageStore") {
        Write-Host "🎉 SUCCESS: Using Azure Blob Storage (Azurite)!" -ForegroundColor Cyan
    } else {
        Write-Host "⚠️  WARNING: Still using local storage: $($storage.MetadataStore.Type)" -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "❌ Error testing Functions: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "=== Test Complete ===" -ForegroundColor Green