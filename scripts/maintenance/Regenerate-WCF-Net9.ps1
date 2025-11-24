# Regenerate-WCF-Net9.ps1
# Regenerates WCF service references for .NET 9 compatibility

Write-Host "=== Regenerating WCF Service References for .NET 9 ===" -ForegroundColor Cyan

# Check if in correct directory
if (!(Test-Path "src\microsoft-update-webservices")) {
    Write-Host "? Error: Must run from repository root!" -ForegroundColor Red
    Write-Host "   Current directory: $(Get-Location)" -ForegroundColor Yellow
    exit 1
}

# Check if dotnet-svcutil is installed
Write-Host "`n1. Checking dotnet-svcutil installation..." -ForegroundColor Yellow
$svcutilInstalled = $null
try {
    $svcutilInstalled = dotnet tool list --global | Select-String "dotnet-svcutil"
} catch {
    Write-Host "   dotnet-svcutil not found, installing..." -ForegroundColor Yellow
}

if (!$svcutilInstalled) {
    Write-Host "   Installing dotnet-svcutil..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-svcutil
} else {
    Write-Host "   Updating dotnet-svcutil..." -ForegroundColor Yellow
    dotnet tool update --global dotnet-svcutil
}

# Backup existing references
Write-Host "`n2. Backing up existing service references..." -ForegroundColor Yellow
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupDir = "src\microsoft-update-webservices\Connected Services_backup_$timestamp"

if (Test-Path "src\microsoft-update-webservices\Connected Services") {
    Copy-Item "src\microsoft-update-webservices\Connected Services" $backupDir -Recurse -ErrorAction SilentlyContinue
    Write-Host "   ? Backup created: $backupDir" -ForegroundColor Green
} else {
    Write-Host "   ? No existing Connected Services folder found" -ForegroundColor Yellow
}

# Service definitions
$services = @(
    @{
    Name = "ServerSync"
    Url = "https://sws.update.microsoft.com/ServerSyncWebService/ServerSyncWebService.asmx?wsdl"
        Namespace = "Microsoft.UpdateServices.WebServices.ServerSync"
        OutputDir = "src\microsoft-update-webservices\Connected Services\Microsoft.UpdateServices.WebServices.ServerSync"
    },
    @{
        Name = "DssAuthentication"
        Url = "https://sws.update.microsoft.com/DssAuthWebService/DssAuthWebService.asmx?wsdl"
        Namespace = "Microsoft.UpdateServices.WebServices.DssAuthentication"
      OutputDir = "src\microsoft-update-webservices\Connected Services\Microsoft.UpdateServices.WebServices.DssAuthentication"
    },
    @{
        Name = "ClientSync"
        Url = "https://sws.update.microsoft.com/ClientWebService/client.asmx?wsdl"
        Namespace = "Microsoft.UpdateServices.WebServices.ClientSync"
    OutputDir = "src\microsoft-update-webservices\Connected Services\Microsoft.UpdateServices.WebServices.ClientSync"
    }
)

# Regenerate each service
Write-Host "`n3. Regenerating service references..." -ForegroundColor Yellow
$successCount = 0

foreach ($svc in $services) {
    Write-Host "`n   Generating $($svc.Name)..." -ForegroundColor Cyan
    
    # Ensure output directory exists
    if (!(Test-Path $svc.OutputDir)) {
        New-Item -ItemType Directory -Path $svc.OutputDir -Force | Out-Null
        Write-Host "   Created directory: $($svc.OutputDir)" -ForegroundColor DarkGray
    }
    
    # Remove old Reference.cs
    $oldReference = Join-Path $svc.OutputDir "Reference.cs"
    if (Test-Path $oldReference) {
        Remove-Item $oldReference -Force
        Write-Host "   Removed old Reference.cs" -ForegroundColor DarkGray
    }
    
    # Build the command
  # Note: PowerShell parameters must be on the same line or use backticks for continuation
    $args = @(
        $svc.Url,
        "--outputDir", $svc.OutputDir,
        "--namespace", "*,$($svc.Namespace)",
   "--targetFramework", "net9.0",
  "--outputFile", "Reference.cs",
        "--serializer", "XmlSerializer",
        "--sync"
    )
    
    Write-Host "   Command: dotnet-svcutil $($svc.Url) --outputDir `"$($svc.OutputDir)`" ..." -ForegroundColor DarkGray
    
  try {
        # Execute dotnet-svcutil with all arguments
        $output = & dotnet-svcutil @args 2>&1
        
        if ($LASTEXITCODE -eq 0) {
     Write-Host "   ? $($svc.Name) generated successfully" -ForegroundColor Green
       $successCount++
      
   # Check if file was actually created
  if (Test-Path (Join-Path $svc.OutputDir "Reference.cs")) {
             $fileInfo = Get-Item (Join-Path $svc.OutputDir "Reference.cs")
                Write-Host "     File size: $([math]::Round($fileInfo.Length / 1KB, 2)) KB" -ForegroundColor DarkGray
            }
        } else {
  Write-Host "   ? Failed to generate $($svc.Name)" -ForegroundColor Red
            Write-Host "     Error output:" -ForegroundColor Red
      $output | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        }
    } catch {
        Write-Host "   ? Exception generating $($svc.Name): $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Summary
Write-Host "`n4. Summary" -ForegroundColor Yellow
Write-Host "   Services regenerated: $successCount of $($services.Count)" -ForegroundColor Cyan

if ($successCount -eq $services.Count) {
    Write-Host "   ? All services regenerated successfully!" -ForegroundColor Green
    
    # Rebuild the project
    Write-Host "`n5. Rebuilding webservices project..." -ForegroundColor Yellow
    dotnet build src\microsoft-update-webservices\microsoft-update-webservices.csproj
    
    if ($LASTEXITCODE -eq 0) {
 Write-Host "   ? Project rebuilt successfully" -ForegroundColor Green
  } else {
        Write-Host "   ? Build failed - check errors above" -ForegroundColor Red
        Write-Host "   You may need to manually fix compilation errors" -ForegroundColor Yellow
    }
    
    Write-Host "`n=== SUCCESS ===" -ForegroundColor Green
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Rebuild the entire solution: dotnet build" -ForegroundColor White
    Write-Host "  2. Restart Azure Functions" -ForegroundColor White
    Write-Host "  3. Test the sync operation" -ForegroundColor White
} else {
    Write-Host "   ? Some services failed to regenerate" -ForegroundColor Yellow
    Write-Host "   Check errors above and try again" -ForegroundColor Yellow
    Write-Host "   Backup available at: $backupDir" -ForegroundColor Cyan
    
    Write-Host "`n=== PARTIAL FAILURE ===" -ForegroundColor Yellow
    Write-Host "You can restore from backup if needed:" -ForegroundColor White
    Write-Host "  Remove-Item 'src\microsoft-update-webservices\Connected Services' -Recurse" -ForegroundColor DarkGray
    Write-Host "  Copy-Item '$backupDir' 'src\microsoft-update-webservices\Connected Services' -Recurse" -ForegroundColor DarkGray
}

Write-Host ""
