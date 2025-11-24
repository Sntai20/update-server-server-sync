#!/usr/bin/env pwsh
# Test Language Configuration Script
# This script demonstrates how to test different language filter configurations

param(
    [string]$BaseUrl = "http://localhost:7071",
    [string]$SyncType = "critical",
    [switch]$ShowExamples,
    [switch]$TestEnglishOnly,
    [switch]$TestMultiLanguage,
    [switch]$TestNeutralOnly
)

Write-Host "Microsoft Update Language Configuration Test Script" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

if ($ShowExamples) {
    Write-Host "`nAvailable Language Filter Examples:" -ForegroundColor Yellow
    Write-Host "1. English + Neutral (Default):" -ForegroundColor Green
    Write-Host '   ["en", "en-US", "neutral", ""]' -ForegroundColor White
    
    Write-Host "`n2. Multi-Language European:" -ForegroundColor Green
    Write-Host '   ["en", "en-US", "de", "fr", "es", "neutral", ""]' -ForegroundColor White
    
    Write-Host "`n3. Asian Languages:" -ForegroundColor Green
    Write-Host '   ["en", "en-US", "ja", "ko", "zh-CN", "zh-TW", "neutral", ""]' -ForegroundColor White
    
    Write-Host "`n4. Neutral Only (Minimal):" -ForegroundColor Green
    Write-Host '   ["neutral", ""]' -ForegroundColor White
    
    Write-Host "`nUsage Examples:" -ForegroundColor Yellow
    Write-Host "  .\Test-LanguageConfig.ps1 -TestEnglishOnly" -ForegroundColor White
    Write-Host "  .\Test-LanguageConfig.ps1 -TestMultiLanguage" -ForegroundColor White
    Write-Host "  .\Test-LanguageConfig.ps1 -TestNeutralOnly" -ForegroundColor White
    return
}

function Invoke-LanguageFilterSync {
    param(
        [string]$Url,
        [array]$LanguageFilters,
        [string]$Description,
        [string]$SyncType = "critical"
    )
    
    Write-Host "`nTesting: $Description" -ForegroundColor Yellow
    Write-Host "Languages: $($LanguageFilters -join ', ')" -ForegroundColor Cyan
    
    $body = @{
        syncType = $SyncType
        syncContent = $true
        languageFilters = $LanguageFilters
        productFilters = @("Windows 11")
        classificationFilters = @("Security Updates", "Critical Updates")
        contentDaysBack = 30
    } | ConvertTo-Json -Depth 3
    
    Write-Host "Request body:" -ForegroundColor Gray
    Write-Host $body -ForegroundColor DarkGray
    
    try {
        $response = Invoke-RestMethod -Uri "$Url/api/SyncWithLanguageFilter" `
                                    -Method POST `
                                    -Body $body `
                                    -ContentType "application/json" `
                                    -TimeoutSec 300
        
        Write-Host "✅ Success!" -ForegroundColor Green
        Write-Host "Response: $($response | ConvertTo-Json -Depth 2)" -ForegroundColor Green
    }
    catch {
        Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "Error details: $responseBody" -ForegroundColor Red
        }
    }
}

# Check if service is running
try {
    $healthCheck = Invoke-RestMethod -Uri "$BaseUrl/api/GetStoreStatus" -Method GET -TimeoutSec 10
    Write-Host "✅ Service is running at $BaseUrl" -ForegroundColor Green
}
catch {
    Write-Host "❌ Service not available at $BaseUrl" -ForegroundColor Red
    Write-Host "Please start the AppHost or UpdateEngine first:" -ForegroundColor Yellow
    Write-Host "  cd UpdateEngine.AppHost/src && dotnet run" -ForegroundColor White
    Write-Host "  OR" -ForegroundColor Yellow
    Write-Host "  cd UpdateEngine.Functions/src && func start" -ForegroundColor White
    return
}

if ($TestEnglishOnly) {
    Invoke-LanguageFilterSync -Url $BaseUrl -SyncType $SyncType `
        -LanguageFilters @("en", "en-US", "neutral", "") `
        -Description "English + Neutral Languages Only"
}

if ($TestMultiLanguage) {
    Invoke-LanguageFilterSync -Url $BaseUrl -SyncType $SyncType `
        -LanguageFilters @("en", "en-US", "de", "fr", "es", "neutral", "") `
        -Description "Multi-Language European Setup"
}

if ($TestNeutralOnly) {
    Invoke-LanguageFilterSync -Url $BaseUrl -SyncType $SyncType `
        -LanguageFilters @("neutral", "") `
        -Description "Neutral/Universal Languages Only (Minimal)"
}

if (-not $TestEnglishOnly -and -not $TestMultiLanguage -and -not $TestNeutralOnly) {
    Write-Host "`nNo specific test selected. Running default English + Neutral test..." -ForegroundColor Yellow
    Invoke-LanguageFilterSync -Url $BaseUrl -SyncType $SyncType `
        -LanguageFilters @("en", "en-US", "neutral", "") `
        -Description "Default Configuration Test"
}

Write-Host "`n📋 Test Summary:" -ForegroundColor Cyan
Write-Host "- Base URL: $BaseUrl" -ForegroundColor White
Write-Host "- Sync Type: $SyncType" -ForegroundColor White
Write-Host "- Target: Windows 11 Critical/Security Updates" -ForegroundColor White

Write-Host "`n💡 Next Steps:" -ForegroundColor Yellow
Write-Host "1. Check the function logs for filter application details" -ForegroundColor White
Write-Host "2. Monitor your content storage for downloaded files" -ForegroundColor White
Write-Host "3. Review the response for sync completion status" -ForegroundColor White
Write-Host "4. Use -ShowExamples to see more configuration options" -ForegroundColor White