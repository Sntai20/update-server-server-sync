# Fix-WCF-ServiceReferences.ps1
# This script regenerates WCF service references that are compatible with .NET 9

Write-Host "=== WCF Service Reference Fix for .NET 9 ===" -ForegroundColor Cyan
Write-Host "This will regenerate the service references to work with .NET 9`n" -ForegroundColor Yellow

# Check if we're in the right directory
if (!(Test-Path "src\microsoft-update-webservices")) {
    Write-Host "? Error: Must run from repository root!" -ForegroundColor Red
    Write-Host "   Current directory: $(Get-Location)" -ForegroundColor Yellow
    Write-Host "   Expected directory containing: src\microsoft-update-webservices`n" -ForegroundColor Yellow
    exit 1
}

Write-Host "? IMPORTANT:" -ForegroundColor Yellow
Write-Host "The WCF service references in this project were generated with older tooling" -ForegroundColor White
Write-Host "that doesn't work properly with .NET 9. The async methods fail with:" -ForegroundColor White
Write-Host "  'Method GetAuthConfigAsync is not supported on this proxy'`n" -ForegroundColor Red

Write-Host "To fix this, you have TWO options:`n" -ForegroundColor Cyan

Write-Host "OPTION 1: Regenerate using Visual Studio (RECOMMENDED)" -ForegroundColor Green
Write-Host "  1. Open the solution in Visual Studio 2022 (17.8 or later)" -ForegroundColor White
Write-Host "  2. In Solution Explorer, expand:" -ForegroundColor White
Write-Host "     microsoft-update-webservices ? Connected Services" -ForegroundColor White
Write-Host "  3. Right-click on each service reference and select:" -ForegroundColor White
Write-Host "     'Update Service Reference'" -ForegroundColor White
Write-Host "  4. Accept the defaults and click OK" -ForegroundColor White
Write-Host "  5. Rebuild the solution`n" -ForegroundColor White

Write-Host "Services to update:" -ForegroundColor Cyan
Write-Host "  • Microsoft.UpdateServices.WebServices.ServerSync" -ForegroundColor White
Write-Host "  • Microsoft.UpdateServices.WebServices.DssAuthentication" -ForegroundColor White
Write-Host "  • Microsoft.UpdateServices.WebServices.ClientSync`n" -ForegroundColor White

Write-Host "OPTION 2: Use dotnet-svcutil command line tool" -ForegroundColor Green
Write-Host "  This is more complex and may require manual fixes.`n" -ForegroundColor Yellow

$response = Read-Host "Do you want to see the dotnet-svcutil commands? (y/n)"

if ($response -eq 'y' -or $response -eq 'Y') {
    Write-Host "`nInstalling dotnet-svcutil..." -ForegroundColor Cyan
    dotnet tool update --global dotnet-svcutil
    
   Write-Host "`nCommands to regenerate service references:" -ForegroundColor Cyan
    Write-Host @"
    
# ServerSync
dotnet-svcutil https://sws.update.microsoft.com/ServerSyncWebService/ServerSyncWebService.asmx?wsdl \
  --outputDir "src\microsoft-update-webservices\Connected Services\Microsoft.UpdateServices.WebServices.ServerSync" \
  --namespace "*,Microsoft.UpdateServices.WebServices.ServerSync" \
  --targetFramework net9.0

# DssAuthentication
dotnet-svcutil https://sws.update.microsoft.com/DssAuthWebService/DssAuthWebService.asmx?wsdl \
  --outputDir "src\microsoft-update-webservices\Connected Services\Microsoft.UpdateServices.WebServices.DssAuthentication" \
  --namespace "*,Microsoft.UpdateServices.WebServices.DssAuthentication" \
  --targetFramework net9.0

# ClientSync
dotnet-svcutil https://sws.update.microsoft.com/ClientWebService/client.asmx?wsdl \
  --outputDir "src\microsoft-update-webservices\Connected Services\Microsoft.UpdateServices.WebServices.ClientSync" \
  --namespace "*,Microsoft.UpdateServices.WebServices.ClientSync" \
  --targetFramework net9.0

"@ -ForegroundColor White

    Write-Host "`n? WARNING: After running these commands, you may need to:" -ForegroundColor Yellow
    Write-Host "  • Manually fix namespace conflicts" -ForegroundColor White
    Write-Host "  • Update project references" -ForegroundColor White
    Write-Host "  • Fix any breaking API changes`n" -ForegroundColor White
}

Write-Host "After updating the service references:" -ForegroundColor Cyan
Write-Host "  1. Rebuild the solution" -ForegroundColor White
Write-Host "  2. Restart Azure Functions" -ForegroundColor White
Write-Host "  3. Test the sync operation again`n" -ForegroundColor White

Write-Host "For more information, see:" -ForegroundColor Cyan
Write-Host "  • WCF Client migration: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/runtime#system-servicemodel" -ForegroundColor White
Write-Host "  • dotnet-svcutil tool: https://learn.microsoft.com/en-us/dotnet/core/additional-tools/dotnet-svcutil-guide`n" -ForegroundColor White
