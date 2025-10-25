# Regenerate-WCFReferences.ps1
# Regenerates WCF service references for .NET 9 compatibility

Write-Host "=== Regenerating WCF Service References for .NET 9 ===" -ForegroundColor Cyan

# Install dotnet-svcutil if not already installed
Write-Host "`n1. Installing/updating dotnet-svcutil tool..." -ForegroundColor Yellow
dotnet tool update --global dotnet-svcutil

# Navigate to webservices project
cd D:\Repos\update-server-server-sync-fork\src\microsoft-update-webservices

# Backup existing references
Write-Host "`n2. Backing up existing Connected Services..." -ForegroundColor Yellow
$backupFolder = "Connected Services_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
if (Test-Path "Connected Services") {
    Copy-Item "Connected Services" $backupFolder -Recurse
    Write-Host "   Backed up to: $backupFolder" -ForegroundColor Green
}

# Service endpoints
$services = @{
    "ServerSync" = @{
   Url = "https://sws.update.microsoft.com/ServerSyncWebService/ServerSyncWebService.asmx?wsdl"
        Namespace = "Microsoft.UpdateServices.WebServices.ServerSync"
  }
    "DssAuthentication" = @{
        Url = "https://sws.update.microsoft.com/ServerSyncWebService/DssAuthWebService.asmx?wsdl"
        Namespace = "Microsoft.UpdateServices.WebServices.DssAuthentication"
    }
    "ClientSync" = @{
   Url = "https://sws.update.microsoft.com/ClientWebService/client.asmx?wsdl"
      Namespace = "Microsoft.UpdateServices.WebServices.ClientSync"
    }
}

# Regenerate each service reference
Write-Host "`n3. Regenerating service references..." -ForegroundColor Yellow

foreach ($serviceName in $services.Keys) {
    $service = $services[$serviceName]
    Write-Host "`nGenerating $serviceName..." -ForegroundColor Cyan
    
    $outputDir = "Connected Services\Microsoft.UpdateServices.WebServices.$serviceName"
    
    try {
        # Remove old generated files
   if (Test-Path $outputDir) {
            Remove-Item "$outputDir\Reference.cs" -ErrorAction SilentlyContinue
        }

        # Generate new service reference
        dotnet-svcutil `
       $service.Url `
            --outputDir $outputDir `
 --namespace "*,$($service.Namespace)" `
  --messageContract `
          --sync `
            --targetFramework "net9.0" `
 --verbose
        
        Write-Host "   ? $serviceName generated successfully" -ForegroundColor Green
    }
    catch {
        Write-Host "   ? Failed to generate $serviceName : $_" -ForegroundColor Red
    }
}

Write-Host "`n4. Rebuilding project..." -ForegroundColor Yellow
dotnet build

Write-Host "`n=== Regeneration Complete ===" -ForegroundColor Cyan
Write-Host "If there are issues, restore from backup: $backupFolder" -ForegroundColor Yellow
