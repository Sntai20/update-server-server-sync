<#
.SYNOPSIS
    Simulates various anomaly scenarios for demo and testing purposes.

.DESCRIPTION
    This script generates realistic anomalous update events that trigger the
    ML.NET anomaly detection system. Useful for demonstrations and validating
    the detection pipeline.

.PARAMETER Scenario
    The type of anomaly scenario to simulate:
    - UnsignedUpdate: Missing cryptographic signature
    - HashMismatch: Content hash verification failure
    - MaliciousPublisher: Low domain reputation
    - ComplexApplicability: Suspicious targeting rules
    - SupersedenceAnomaly: Unusual supersedence chain
    - CombinedThreats: Multiple red flags (high severity)
    - All: Run all scenarios sequentially

.PARAMETER Count
    Number of anomalies to generate (default: 1).

.PARAMETER Delay
    Delay in milliseconds between submissions (default: 1000).

.PARAMETER FunctionsUrl
    Base URL for Azure Functions endpoints.
    When using AppHost, check Aspire Dashboard (https://localhost:15001) for the actual port.
    Example: http://localhost:5234 (port varies each run).
    Default attempts localhost:7071 (standalone func.exe).

.EXAMPLE
    .\Invoke-AnomalySimulation.ps1 -Scenario UnsignedUpdate
    Simulates a single unsigned update.

.EXAMPLE
    .\Invoke-AnomalySimulation.ps1 -Scenario All -Count 3 -FunctionsUrl http://localhost:5234
    Runs all scenarios with 3 samples each using discovered port from Aspire.

.EXAMPLE
    .\Invoke-AnomalySimulation.ps1 -Scenario All -Count 3
    Runs all scenarios with 3 samples each.

.EXAMPLE
    .\Invoke-AnomalySimulation.ps1 -Scenario CombinedThreats -Count 5 -Delay 500
    Simulates 5 high-severity anomalies with 500ms delay.

.NOTES
    Author: Update Engine Team
    Date: November 30, 2025
    
    Expected Anomaly Scores:
    - UnsignedUpdate: 0.87-0.90 (LOW)
    - HashMismatch: 0.91-0.94 (MEDIUM)
    - MaliciousPublisher: 0.88-0.92 (LOW-MEDIUM)
    - ComplexApplicability: 0.86-0.89 (LOW)
    - SupersedenceAnomaly: 0.89-0.93 (LOW-MEDIUM)
    - CombinedThreats: 0.95-0.99 (HIGH)
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('UnsignedUpdate', 'HashMismatch', 'MaliciousPublisher', 'ComplexApplicability', 'SupersedenceAnomaly', 'CombinedThreats', 'All')]
    [string]$Scenario = 'All',
    
    [Parameter(Mandatory = $false)]
    [ValidateRange(1, 100)]
    [int]$Count = 1,
    
    [Parameter(Mandatory = $false)]
    [ValidateRange(0, 10000)]
    [int]$Delay = 1000,
    
    [Parameter(Mandatory = $false)]
    [string]$FunctionsUrl = 'http://localhost:7071',
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipHealthCheck
)

$ErrorActionPreference = 'Stop'

# ANSI color codes for better visibility
$script:Colors = @{
    Header = "`e[96m"      # Cyan
    Success = "`e[92m"     # Green
    Warning = "`e[93m"     # Yellow
    Error = "`e[91m"       # Red
    Info = "`e[90m"        # Gray
    Severity = @{
        LOW = "`e[93m"     # Yellow
        MEDIUM = "`e[38;5;208m"  # Orange
        HIGH = "`e[91m"    # Red
    }
    Reset = "`e[0m"
}

function Write-ScenarioHeader {
    param([string]$Title, [string]$Description)
    
    Write-Host ""
    Write-Host "$($script:Colors.Header)═══════════════════════════════════════════════════════════$($script:Colors.Reset)"
    Write-Host "$($script:Colors.Header)  $Title$($script:Colors.Reset)"
    Write-Host "$($script:Colors.Info)  $Description$($script:Colors.Reset)"
    Write-Host "$($script:Colors.Header)═══════════════════════════════════════════════════════════$($script:Colors.Reset)"
}

function Write-AnomalyResult {
    param(
        [string]$KbId,
        [double]$Score,
        [string]$Status,
        [string]$Severity
    )
    
    $severityColor = $script:Colors.Severity[$Severity]
    $statusSymbol = if ($Status -eq "accepted") { "✓" } else { "✗" }
    
    Write-Host "  $statusSymbol KB$KbId " -NoNewline
    Write-Host "Score: $($Score.ToString('0.000')) " -NoNewline -ForegroundColor $(if ($Score -ge 0.95) { 'Red' } elseif ($Score -ge 0.90) { 'Yellow' } else { 'White' })
    Write-Host "[$severityColor$Severity$($script:Colors.Reset)]"
}

function Invoke-AnomalyIngestion {
    param(
        [hashtable]$Payload,
        [string]$Description
    )
    
    try {
        $json = $Payload | ConvertTo-Json -Depth 10
        $url = "$FunctionsUrl/api/IngestAnomaly"
        
        Write-Host "$($script:Colors.Info)  → Ingesting: $Description$($script:Colors.Reset)"
        
        $response = Invoke-RestMethod -Uri $url -Method POST -Body $json -ContentType 'application/json' -TimeoutSec 60
        
        return $response
    }
    catch {
        Write-Host "$($script:Colors.Error)  ✗ Error: $($_.Exception.Message)$($script:Colors.Reset)"
        return $null
    }
}

function Get-RandomKbId {
    return Get-Random -Minimum 9000000 -Maximum 9999999
}

function Get-SeverityFromScore {
    param([double]$Score)
    
    if ($Score -ge 0.95) { return "HIGH" }
    elseif ($Score -ge 0.90) { return "MEDIUM" }
    elseif ($Score -ge 0.85) { return "LOW" }
    else { return "NORMAL" }
}

#region Scenario Definitions

function Invoke-UnsignedUpdateScenario {
    param([int]$Count)
    
    Write-ScenarioHeader `
        -Title "Scenario 1: Unsigned Update" `
        -Description "Missing cryptographic signature - Major security red flag"
    
    Write-Host "$($script:Colors.Info)  Expected Score: 0.87-0.90 (LOW severity)$($script:Colors.Reset)"
    Write-Host ""
    
    for ($i = 1; $i -le $Count; $i++) {
        $kbId = Get-RandomKbId
        
        $payload = @{
            kb_ID = "KB$kbId"
            fileSize = Get-Random -Minimum 10000000 -Maximum 50000000  # 10-50 MB (normal range)
            isSigned = $false  # ❌ RED FLAG
            domainReputation = 0.85  # Normal reputation
            hashMatch = $true  # Hash verified
            updateFrequency = 0.6  # Normal frequency
            supersededCount = Get-Random -Minimum 0 -Maximum 5  # Normal
            supersededByCount = 0
            bundledUpdatesCount = Get-Random -Minimum 0 -Maximum 2
            isSecurityUpdate = $true
            isCriticalUpdate = $false
            isCumulativeUpdate = $false
            applicabilityRulesCount = Get-Random -Minimum 1 -Maximum 5
            hasComplexApplicability = $false
            score = 0.88  # Estimated score
            timestamp = (Get-Date).ToUniversalTime().ToString("o")
        }
        
        $response = Invoke-AnomalyIngestion -Payload $payload -Description "Unsigned security update (KB$kbId)"
        
        if ($response) {
            $severity = Get-SeverityFromScore $payload.score
            Write-AnomalyResult -KbId $kbId -Score $payload.score -Status $response.status -Severity $severity
        }
        
        if ($i -lt $Count) { Start-Sleep -Milliseconds $Delay }
    }
}

function Invoke-HashMismatchScenario {
    param([int]$Count)
    
    Write-ScenarioHeader `
        -Title "Scenario 2: Hash Mismatch" `
        -Description "Content hash verification failure - Possible tampering or corruption"
    
    Write-Host "$($script:Colors.Info)  Expected Score: 0.91-0.94 (MEDIUM severity)$($script:Colors.Reset)"
    Write-Host ""
    
    for ($i = 1; $i -le $Count; $i++) {
        $kbId = Get-RandomKbId
        
        $payload = @{
            kb_ID = "KB$kbId"
            fileSize = Get-Random -Minimum 20000000 -Maximum 100000000
            isSigned = $true  # Signed but...
            domainReputation = 0.9  # Good reputation
            hashMatch = $false  # ❌ RED FLAG: Hash mismatch!
            updateFrequency = 0.7
            supersededCount = Get-Random -Minimum 0 -Maximum 8
            supersededByCount = 0
            bundledUpdatesCount = Get-Random -Minimum 0 -Maximum 3
            isSecurityUpdate = $true
            isCriticalUpdate = $true
            isCumulativeUpdate = $false
            applicabilityRulesCount = Get-Random -Minimum 1 -Maximum 6
            hasComplexApplicability = $false
            score = 0.92  # Higher score due to hash mismatch
            timestamp = (Get-Date).ToUniversalTime().ToString("o")
        }
        
        $response = Invoke-AnomalyIngestion -Payload $payload -Description "Hash mismatch on critical update (KB$kbId)"
        
        if ($response) {
            $severity = Get-SeverityFromScore $payload.score
            Write-AnomalyResult -KbId $kbId -Score $payload.score -Status $response.status -Severity $severity
        }
        
        if ($i -lt $Count) { Start-Sleep -Milliseconds $Delay }
    }
}

function Invoke-MaliciousPublisherScenario {
    param([int]$Count)
    
    Write-ScenarioHeader `
        -Title "Scenario 3: Malicious Publisher" `
        -Description "Low domain reputation - Untrusted or compromised source"
    
    Write-Host "$($script:Colors.Info)  Expected Score: 0.88-0.92 (LOW-MEDIUM severity)$($script:Colors.Reset)"
    Write-Host ""
    
    for ($i = 1; $i -le $Count; $i++) {
        $kbId = Get-RandomKbId
        
        $payload = @{
            kb_ID = "KB$kbId"
            fileSize = Get-Random -Minimum 5000000 -Maximum 30000000
            isSigned = $true  # Signed but from suspicious publisher
            domainReputation = (Get-Random -Minimum 20 -Maximum 45) / 100  # ❌ LOW reputation (0.2-0.45)
            hashMatch = $true
            updateFrequency = 0.3  # Infrequent updates from this source
            supersededCount = 0
            supersededByCount = 0
            bundledUpdatesCount = 0
            isSecurityUpdate = $false
            isCriticalUpdate = $false
            isCumulativeUpdate = $false
            applicabilityRulesCount = Get-Random -Minimum 1 -Maximum 4
            hasComplexApplicability = $false
            score = 0.90  # Moderate score
            timestamp = (Get-Date).ToUniversalTime().ToString("o")
        }
        
        $response = Invoke-AnomalyIngestion -Payload $payload -Description "Low reputation publisher (KB$kbId, rep=$($payload.domainReputation.ToString('0.00')))"
        
        if ($response) {
            $severity = Get-SeverityFromScore $payload.score
            Write-AnomalyResult -KbId $kbId -Score $payload.score -Status $response.status -Severity $severity
        }
        
        if ($i -lt $Count) { Start-Sleep -Milliseconds $Delay }
    }
}

function Invoke-ComplexApplicabilityScenario {
    param([int]$Count)
    
    Write-ScenarioHeader `
        -Title "Scenario 4: Complex Applicability Rules" `
        -Description "Overly complex targeting - Possible exploit targeting specific systems"
    
    Write-Host "$($script:Colors.Info)  Expected Score: 0.86-0.89 (LOW severity)$($script:Colors.Reset)"
    Write-Host ""
    
    for ($i = 1; $i -le $Count; $i++) {
        $kbId = Get-RandomKbId
        
        $payload = @{
            kb_ID = "KB$kbId"
            fileSize = Get-Random -Minimum 1000000 -Maximum 10000000  # Smaller update
            isSigned = $true
            domainReputation = 0.8
            hashMatch = $true
            updateFrequency = 0.5
            supersededCount = Get-Random -Minimum 0 -Maximum 3
            supersededByCount = 0
            bundledUpdatesCount = 0
            isSecurityUpdate = $false
            isCriticalUpdate = $false
            isCumulativeUpdate = $false
            applicabilityRulesCount = Get-Random -Minimum 25 -Maximum 50  # ❌ Unusually complex (normal: 1-5)
            hasComplexApplicability = $true  # ❌ RED FLAG
            score = 0.87
            timestamp = (Get-Date).ToUniversalTime().ToString("o")
        }
        
        $response = Invoke-AnomalyIngestion -Payload $payload -Description "Complex targeting rules (KB$kbId, $($payload.applicabilityRulesCount) rules)"
        
        if ($response) {
            $severity = Get-SeverityFromScore $payload.score
            Write-AnomalyResult -KbId $kbId -Score $payload.score -Status $response.status -Severity $severity
        }
        
        if ($i -lt $Count) { Start-Sleep -Milliseconds $Delay }
    }
}

function Invoke-SupersedenceAnomalyScenario {
    param([int]$Count)
    
    Write-ScenarioHeader `
        -Title "Scenario 5: Supersedence Anomaly" `
        -Description "Unusual supersedence chain - Potential rollup forgery"
    
    Write-Host "$($script:Colors.Info)  Expected Score: 0.89-0.93 (LOW-MEDIUM severity)$($script:Colors.Reset)"
    Write-Host ""
    
    for ($i = 1; $i -le $Count; $i++) {
        $kbId = Get-RandomKbId
        
        $payload = @{
            kb_ID = "KB$kbId"
            fileSize = Get-Random -Minimum 100000000 -Maximum 500000000  # Large update (100-500 MB)
            isSigned = $true
            domainReputation = 0.85
            hashMatch = $true
            updateFrequency = 0.4
            supersededCount = Get-Random -Minimum 50 -Maximum 100  # ❌ Supersedes MANY updates (normal: 0-10)
            supersededByCount = 0
            bundledUpdatesCount = Get-Random -Minimum 10 -Maximum 20  # ❌ Many bundled updates
            isSecurityUpdate = $false
            isCriticalUpdate = $false
            isCumulativeUpdate = $true  # Claims to be cumulative
            applicabilityRulesCount = Get-Random -Minimum 5 -Maximum 10
            hasComplexApplicability = $false
            score = 0.91
            timestamp = (Get-Date).ToUniversalTime().ToString("o")
        }
        
        $response = Invoke-AnomalyIngestion -Payload $payload -Description "Supersedes $($payload.supersededCount) updates (KB$kbId)"
        
        if ($response) {
            $severity = Get-SeverityFromScore $payload.score
            Write-AnomalyResult -KbId $kbId -Score $payload.score -Status $response.status -Severity $severity
        }
        
        if ($i -lt $Count) { Start-Sleep -Milliseconds $Delay }
    }
}

function Invoke-CombinedThreatsScenario {
    param([int]$Count)
    
    Write-ScenarioHeader `
        -Title "Scenario 6: Combined Threats (HIGH SEVERITY)" `
        -Description "Multiple red flags - Unsigned + Hash Mismatch + Low Reputation"
    
    Write-Host "$($script:Colors.Info)  Expected Score: 0.95-0.99 (HIGH severity - CRITICAL ALERT)$($script:Colors.Reset)"
    Write-Host ""
    
    for ($i = 1; $i -le $Count; $i++) {
        $kbId = Get-RandomKbId
        
        $payload = @{
            kb_ID = "KB$kbId"
            fileSize = Get-Random -Minimum 1000000 -Maximum 200000000  # Variable size
            isSigned = $false  # ❌ RED FLAG 1: Unsigned
            domainReputation = (Get-Random -Minimum 10 -Maximum 30) / 100  # ❌ RED FLAG 2: Very low reputation
            hashMatch = $false  # ❌ RED FLAG 3: Hash mismatch
            updateFrequency = 0.1  # ❌ RED FLAG 4: Rare/unusual pattern
            supersededCount = Get-Random -Minimum 30 -Maximum 70  # ❌ RED FLAG 5: Unusual supersedence
            supersededByCount = 0
            bundledUpdatesCount = Get-Random -Minimum 5 -Maximum 15
            isSecurityUpdate = $true  # Claims to be security update (suspicious!)
            isCriticalUpdate = $true  # Claims critical (very suspicious!)
            isCumulativeUpdate = $false
            applicabilityRulesCount = Get-Random -Minimum 20 -Maximum 40  # ❌ RED FLAG 6: Complex rules
            hasComplexApplicability = $true  # ❌ RED FLAG 7
            score = 0.97  # Very high score - multiple threats
            timestamp = (Get-Date).ToUniversalTime().ToString("o")
        }
        
        $response = Invoke-AnomalyIngestion -Payload $payload -Description "🚨 CRITICAL: Multiple red flags (KB$kbId)"
        
        if ($response) {
            $severity = Get-SeverityFromScore $payload.score
            Write-Host "$($script:Colors.Error)  ⚠ CRITICAL THREAT DETECTED ⚠$($script:Colors.Reset)"
            Write-AnomalyResult -KbId $kbId -Score $payload.score -Status $response.status -Severity $severity
            Write-Host "$($script:Colors.Error)  → Unsigned + Hash Mismatch + Low Reputation + Complex Rules$($script:Colors.Reset)"
        }
        
        if ($i -lt $Count) { Start-Sleep -Milliseconds $Delay }
    }
}

#endregion

#region Main Execution

Write-Host ""
Write-Host "$($script:Colors.Header)╔══════════════════════════════════════════════════════════════╗$($script:Colors.Reset)"
Write-Host "$($script:Colors.Header)║                                                              ║$($script:Colors.Reset)"
Write-Host "$($script:Colors.Header)║       Anomaly Detection Simulation Tool                      ║$($script:Colors.Reset)"
Write-Host "$($script:Colors.Header)║       ML.NET-Based Windows Update Security                   ║$($script:Colors.Reset)"
Write-Host "$($script:Colors.Header)║                                                              ║$($script:Colors.Reset)"
Write-Host "$($script:Colors.Header)╚══════════════════════════════════════════════════════════════╝$($script:Colors.Reset)"
Write-Host ""

# Verify Functions are accessible (unless skipped)
if (-not $SkipHealthCheck) {
    try {
        Write-Host "$($script:Colors.Info)→ Verifying Azure Functions connectivity...$($script:Colors.Reset)"
        $health = Invoke-RestMethod -Uri "$FunctionsUrl/api/UniversalHealth?scope=basic" -Method GET -TimeoutSec 10 -ErrorAction Stop
        
        if ($health.isHealthy) {
            Write-Host "$($script:Colors.Success)✓ Functions are healthy and ready$($script:Colors.Reset)"
            Write-Host "$($script:Colors.Info)  Status: $($health.status)$($script:Colors.Reset)"
        } else {
            Write-Host "$($script:Colors.Warning)⚠ Functions responded but status is: $($health.status)$($script:Colors.Reset)"
        }
    }
    catch {
        Write-Host "$($script:Colors.Error)✗ Cannot connect to Azure Functions at $FunctionsUrl$($script:Colors.Reset)"
        Write-Host ""
        Write-Host "$($script:Colors.Warning)Troubleshooting:$($script:Colors.Reset)"
        Write-Host "$($script:Colors.Info)  1. Make sure AppHost is running: .\scripts\test\Start-Demo.ps1$($script:Colors.Reset)"
        Write-Host "$($script:Colors.Info)  2. Check Aspire Dashboard for actual port: https://localhost:15001$($script:Colors.Reset)"
        Write-Host "$($script:Colors.Info)  3. Look for 'UpdateEngine' resource and copy the endpoint URL$($script:Colors.Reset)"
        Write-Host "$($script:Colors.Info)  4. Retry with: -FunctionsUrl http://localhost:PORT -SkipHealthCheck$($script:Colors.Reset)"
        Write-Host ""
        Write-Host "$($script:Colors.Info)Note: AppHost uses dynamic ports. Port 7071 only works with standalone 'func start'$($script:Colors.Reset)"
        exit 1
    }
} else {
    Write-Host "$($script:Colors.Warning)⚠ Skipping health check (using -SkipHealthCheck)$($script:Colors.Reset)"
}

Write-Host ""
Write-Host "$($script:Colors.Info)Configuration:$($script:Colors.Reset)"
Write-Host "$($script:Colors.Info)  Scenario: $Scenario$($script:Colors.Reset)"
Write-Host "$($script:Colors.Info)  Count: $Count sample(s) per scenario$($script:Colors.Reset)"
Write-Host "$($script:Colors.Info)  Delay: $Delay ms between submissions$($script:Colors.Reset)"
Write-Host "$($script:Colors.Info)  Endpoint: $FunctionsUrl/api/IngestAnomaly$($script:Colors.Reset)"

# Execute scenarios
$startTime = Get-Date

switch ($Scenario) {
    'UnsignedUpdate' { Invoke-UnsignedUpdateScenario -Count $Count }
    'HashMismatch' { Invoke-HashMismatchScenario -Count $Count }
    'MaliciousPublisher' { Invoke-MaliciousPublisherScenario -Count $Count }
    'ComplexApplicability' { Invoke-ComplexApplicabilityScenario -Count $Count }
    'SupersedenceAnomaly' { Invoke-SupersedenceAnomalyScenario -Count $Count }
    'CombinedThreats' { Invoke-CombinedThreatsScenario -Count $Count }
    'All' {
        Invoke-UnsignedUpdateScenario -Count $Count
        Invoke-HashMismatchScenario -Count $Count
        Invoke-MaliciousPublisherScenario -Count $Count
        Invoke-ComplexApplicabilityScenario -Count $Count
        Invoke-SupersedenceAnomalyScenario -Count $Count
        Invoke-CombinedThreatsScenario -Count $Count
    }
}

$duration = (Get-Date) - $startTime

# Summary
Write-Host ""
Write-Host "$($script:Colors.Header)═══════════════════════════════════════════════════════════$($script:Colors.Reset)"
Write-Host "$($script:Colors.Success)  ✓ Simulation Complete$($script:Colors.Reset)"
Write-Host "$($script:Colors.Header)═══════════════════════════════════════════════════════════$($script:Colors.Reset)"
Write-Host ""
Write-Host "$($script:Colors.Info)  Duration: $($duration.TotalSeconds.ToString('0.00')) seconds$($script:Colors.Reset)"
Write-Host ""
Write-Host "$($script:Colors.Info)Next Steps:$($script:Colors.Reset)"
Write-Host "$($script:Colors.Info)  1. Check Functions logs for anomaly alerts$($script:Colors.Reset)"
Write-Host "$($script:Colors.Info)  2. View queue: az storage message peek --queue-name anomaly-events --connection-string UseDevelopmentStorage=true$($script:Colors.Reset)"
Write-Host "$($script:Colors.Info)  3. Check Aspire Dashboard metrics: https://localhost:15001$($script:Colors.Reset)"
Write-Host ""

#endregion
