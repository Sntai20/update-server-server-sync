# Regenerate-WCF-Net9-OfflineFirst.ps1
# Downloads WSDL files first, then regenerates WCF service references for .NET 9

Write-Host "=== Regenerating WCF Service References for .NET 9 (Offline-First Method) ===" -ForegroundColor Cyan

# Check if in correct directory
if (!(Test-Path "src\microsoft-update-webservices")) {
    Write-Host "? Error: Must run from repository root!" -ForegroundColor Red
    exit 1
}

# Create temp directory for WSDL files
$tempDir = "temp_wsdl_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
Write-Host "`n1. Created temporary directory: $tempDir" -ForegroundColor Yellow

# Service definitions with correct WSDL URLs
$services = @(
    @{
        Name = "ServerSync"
        WsdlUrl = "https://fe2cr.update.microsoft.com/ServerSyncWebService/ServerSyncWebService.asmx?WSDL"
      Namespace = "Microsoft.UpdateServices.WebServices.ServerSync"
        OutputDir = "src\microsoft-update-webservices\Connected Services\Microsoft.UpdateServices.WebServices.ServerSync"
    },
    @{
     Name = "DssAuthentication"
    WsdlUrl = "https://fe2cr.update.microsoft.com/DssAuthWebService/DssAuthWebService.asmx?WSDL"
      Namespace = "Microsoft.UpdateServices.WebServices.DssAuthentication"
        OutputDir = "src\microsoft-update-webservices\Connected Services\Microsoft.UpdateServices.WebServices.DssAuthentication"
    },
    @{
        Name = "ClientSync"
        WsdlUrl = "https://fe2cr.update.microsoft.com/ClientWebService/client.asmx?WSDL"
        Namespace = "Microsoft.UpdateServices.WebServices.ClientSync"
        OutputDir = "src\microsoft-update-webservices\Connected Services\Microsoft.UpdateServices.WebServices.ClientSync"
    }
)

# Download WSDL files
Write-Host "`n2. Downloading WSDL files..." -ForegroundColor Yellow
$downloadedWsdls = @()

foreach ($svc in $services) {
  Write-Host "   Downloading $($svc.Name) WSDL..." -ForegroundColor Cyan
    $wsdlFile = Join-Path $tempDir "$($svc.Name).wsdl"
    
    try {
        # Use Invoke-WebRequest with proper headers
        $response = Invoke-WebRequest -Uri $svc.WsdlUrl -OutFile $wsdlFile -UserAgent "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36" -UseBasicParsing
  
        if (Test-Path $wsdlFile) {
       $fileInfo = Get-Item $wsdlFile
Write-Host "   ? Downloaded: $([math]::Round($fileInfo.Length / 1KB, 2)) KB" -ForegroundColor Green
       $downloadedWsdls += @{ Service = $svc; WsdlFile = $wsdlFile }
      }
    } catch {
      Write-Host "   ? Failed to download: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "   Trying alternate URL..." -ForegroundColor Yellow
        
        # Try alternate URL (sws.update.microsoft.com)
        $alternateUrl = $svc.WsdlUrl -replace "fe2cr.update.microsoft.com", "sws.update.microsoft.com"
   try {
  $response = Invoke-WebRequest -Uri $alternateUrl -OutFile $wsdlFile -UserAgent "Mozilla/5.0" -UseBasicParsing
            if (Test-Path $wsdlFile) {
                Write-Host "   ? Downloaded from alternate URL" -ForegroundColor Green
 $downloadedWsdls += @{ Service = $svc; WsdlFile = $wsdlFile }
            }
        } catch {
         Write-Host "   ? Failed with alternate URL too: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

if ($downloadedWsdls.Count -eq 0) {
    Write-Host "`n? Failed to download any WSDL files!" -ForegroundColor Red
    Write-Host "This might be due to:" -ForegroundColor Yellow
    Write-Host "  - Network/firewall restrictions" -ForegroundColor White
    Write-Host "  - Corporate proxy settings" -ForegroundColor White
  Write-Host "  - Microsoft Update servers being unavailable" -ForegroundColor White
    Write-Host "`nAlternative: Use the existing Reference.cs files (they should still work)" -ForegroundColor Cyan
    
 # Restore from backup
    Write-Host "`nRestoring from backup..." -ForegroundColor Yellow
    $latestBackup = Get-ChildItem "src\microsoft-update-webservices" -Directory -Filter "Connected Services_backup_*" | 
        Sort-Object Name -Descending | 
        Select-Object -First 1
    
    if ($latestBackup) {
    Copy-Item "$($latestBackup.FullName)\*" "src\microsoft-update-webservices\Connected Services" -Recurse -Force
     Write-Host "? Restored from: $($latestBackup.Name)" -ForegroundColor Green
    }
    
    Remove-Item $tempDir -Recurse -Force
    exit 1
}

Write-Host "`n   Successfully downloaded $($downloadedWsdls.Count) of $($services.Count) WSDL files" -ForegroundColor Green

# Check dotnet-svcutil
Write-Host "`n3. Checking dotnet-svcutil..." -ForegroundColor Yellow
dotnet tool update --global dotnet-svcutil | Out-Null

# Backup existing references
Write-Host "`n4. Backing up existing references..." -ForegroundColor Yellow
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupDir = "src\microsoft-update-webservices\Connected Services_backup_$timestamp"
if (Test-Path "src\microsoft-update-webservices\Connected Services") {
    Copy-Item "src\microsoft-update-webservices\Connected Services" $backupDir -Recurse
    Write-Host "   ? Backup created: $backupDir" -ForegroundColor Green
}

# Generate from local WSDL files
Write-Host "`n5. Generating service references from local WSDL files..." -ForegroundColor Yellow
$successCount = 0

foreach ($item in $downloadedWsdls) {
    $svc = $item.Service
    $wsdlFile = $item.WsdlFile
    
    Write-Host "`n   Generating $($svc.Name)..." -ForegroundColor Cyan
    
    # Ensure output directory exists
    if (!(Test-Path $svc.OutputDir)) {
        New-Item -ItemType Directory -Path $svc.OutputDir -Force | Out-Null
    }
    
    # Remove old Reference.cs
    Remove-Item (Join-Path $svc.OutputDir "Reference.cs") -ErrorAction SilentlyContinue
    
 # Generate from local WSDL file
    $args = @(
        $wsdlFile,
      "--outputDir", $svc.OutputDir,
        "--namespace", "*,$($svc.Namespace)",
        "--targetFramework", "net9.0",
        "--outputFile", "Reference.cs",
  "--serializer", "XmlSerializer"
    )
    
    try {
        $output = & dotnet-svcutil @args 2>&1
 
      if ($LASTEXITCODE -eq 0) {
            Write-Host "   ? $($svc.Name) generated successfully" -ForegroundColor Green
  $successCount++
       
     $refFile = Join-Path $svc.OutputDir "Reference.cs"
     if (Test-Path $refFile) {
        $fileInfo = Get-Item $refFile
                Write-Host "     Generated file: $([math]::Round($fileInfo.Length / 1KB, 2)) KB" -ForegroundColor DarkGray
          }
        } else {
            Write-Host " ? Failed to generate $($svc.Name)" -ForegroundColor Red
            $output | Where-Object { $_ -match "Error" } | ForEach-Object { 
          Write-Host "     $_" -ForegroundColor Red 
      }
      }
    } catch {
        Write-Host "   ? Exception: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Cleanup
Write-Host "`n6. Cleaning up..." -ForegroundColor Yellow
Remove-Item $tempDir -Recurse -Force
Write-Host "   ? Removed temporary directory" -ForegroundColor Green

# Summary
Write-Host "`n=== SUMMARY ===" -ForegroundColor Cyan
Write-Host "Services downloaded: $($downloadedWsdls.Count) of $($services.Count)" -ForegroundColor White
Write-Host "Services generated: $successCount of $($services.Count)" -ForegroundColor White

if ($successCount -eq $services.Count) {
    Write-Host "`n? All services regenerated successfully!" -ForegroundColor Green
    
    Write-Host "`n7. Rebuilding solution..." -ForegroundColor Yellow
    dotnet build
    
    if ($LASTEXITCODE -eq 0) {
     Write-Host "`n=== SUCCESS ===" -ForegroundColor Green
        Write-Host "Next steps:" -ForegroundColor Cyan
        Write-Host "  1. Restart Azure Functions" -ForegroundColor White
        Write-Host "  2. Test sync operation" -ForegroundColor White
    } else {
        Write-Host "`n? Build had errors - check output above" -ForegroundColor Yellow
    }
} elseif ($successCount -gt 0) {
    Write-Host "`n? Partially successful - some services generated" -ForegroundColor Yellow
    Write-Host "Backup available at: $backupDir" -ForegroundColor Cyan
} else {
Write-Host "`n? No services were generated successfully" -ForegroundColor Red
    Write-Host "Restoring from backup..." -ForegroundColor Yellow
    
    if (Test-Path $backupDir) {
        Remove-Item "src\microsoft-update-webservices\Connected Services" -Recurse -Force
        Copy-Item $backupDir "src\microsoft-update-webservices\Connected Services" -Recurse
        Write-Host "? Restored original files" -ForegroundColor Green
    }
    
    Write-Host "`nThe existing WCF references may still work with .NET 9" -ForegroundColor Cyan
    Write-Host "Try building and testing anyway." -ForegroundColor Cyan
}
