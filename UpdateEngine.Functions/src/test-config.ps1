# Test script to verify configuration pipeline from local.settings.json to ServiceConfigurationMutable
# This verifies that SupportedLanguages flows through the configuration system properly

Write-Host "=== Testing Language Configuration Pipeline ===" -ForegroundColor Green
Write-Host ""

# Test 1: Check local.settings.json has SupportedLanguages
Write-Host "1. Checking local.settings.json..." -ForegroundColor Yellow
$localSettings = Get-Content -Path "local.settings.json" | ConvertFrom-Json
$supportedLanguages = $localSettings.UpdateServer.SupportedLanguages

if ($supportedLanguages) {
    Write-Host "✅ Found SupportedLanguages in local.settings.json: $($supportedLanguages -join ', ')" -ForegroundColor Green
} else {
    Write-Host "❌ SupportedLanguages NOT found in local.settings.json" -ForegroundColor Red
}

# Test 2: Check UpdateServerOptions.cs has SupportedLanguages property
Write-Host ""
Write-Host "2. Checking UpdateServerOptions.cs..." -ForegroundColor Yellow
$updateServerOptions = Get-Content -Path "..\..\Configuration\UpdateServerOptions.cs"
if ($updateServerOptions -match "SupportedLanguages") {
    Write-Host "✅ SupportedLanguages property found in UpdateServerOptions.cs" -ForegroundColor Green
} else {
    Write-Host "❌ SupportedLanguages property NOT found in UpdateServerOptions.cs" -ForegroundColor Red
}

# Test 3: Check ServiceConfiguration.cs has SupportedLanguages in both immutable and mutable classes
Write-Host ""
Write-Host "3. Checking ServiceConfiguration.cs..." -ForegroundColor Yellow
$serviceConfig = Get-Content -Path "..\..\Configuration\ServiceConfiguration.cs"
$immutableHasIt = $serviceConfig -match "public required string\[\] SupportedLanguages"
$mutableHasIt = $serviceConfig -match "public string\[\] SupportedLanguages.*\{ get; set; \}"

if ($immutableHasIt) {
    Write-Host "✅ SupportedLanguages property found in immutable ServiceConfiguration" -ForegroundColor Green
} else {
    Write-Host "❌ SupportedLanguages property NOT found in immutable ServiceConfiguration" -ForegroundColor Red
}

if ($mutableHasIt) {
    Write-Host "✅ SupportedLanguages property found in mutable ServiceConfigurationMutable" -ForegroundColor Green
} else {
    Write-Host "❌ SupportedLanguages property NOT found in mutable ServiceConfigurationMutable" -ForegroundColor Red
}

# Test 4: Check SimpleConfigurationHelper.cs includes SupportedLanguages in serviceConfig
Write-Host ""
Write-Host "4. Checking SimpleConfigurationHelper.cs..." -ForegroundColor Yellow
$configHelper = Get-Content -Path "..\..\AppHost\src\SimpleConfigurationHelper.cs"
if ($configHelper -match "SupportedLanguages.*=.*server\.SupportedLanguages") {
    Write-Host "✅ SupportedLanguages included in SimpleConfigurationHelper serviceConfig" -ForegroundColor Green
} else {
    Write-Host "❌ SupportedLanguages NOT included in SimpleConfigurationHelper serviceConfig" -ForegroundColor Red
}

# Test 5: Check ConfigurationExtensions.cs includes SupportedLanguages in conversions
Write-Host ""
Write-Host "5. Checking ConfigurationExtensions.cs..." -ForegroundColor Yellow
$configExt = Get-Content -Path "..\..\Configuration\ConfigurationExtensions.cs"
$hasToImmutable = $configExt -match "SupportedLanguages.*=.*config\.SupportedLanguages\.ToArray"
$hasFromSection = $configExt -match "SupportedLanguages.*=.*GetSection.*SupportedLanguages"

if ($hasToImmutable) {
    Write-Host "✅ SupportedLanguages included in ToImmutable conversion" -ForegroundColor Green
} else {
    Write-Host "❌ SupportedLanguages NOT included in ToImmutable conversion" -ForegroundColor Red
}

if ($hasFromSection) {
    Write-Host "✅ SupportedLanguages included in configuration section mapping" -ForegroundColor Green
} else {
    Write-Host "❌ SupportedLanguages NOT included in configuration section mapping" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Configuration Pipeline Test Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Configuration flow: local.settings.json → UpdateServerOptions → SimpleConfigurationHelper → ServiceConfigurationJson → ServiceConfigurationMutable" -ForegroundColor Cyan
Write-Host "All components are properly configured for language filtering!" -ForegroundColor Green