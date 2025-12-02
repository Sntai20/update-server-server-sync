<#
.SYNOPSIS
    Stops all running AppHost processes and cleans up PowerShell jobs.

.DESCRIPTION
    This script finds and stops:
    1. Any PowerShell background jobs running AppHost
    2. Any dotnet.exe processes running UpdateEngine.AppHost
    3. Verifies Aspire Dashboard ports (15001, 18888, 15000) are released

.EXAMPLE
    .\Stop-AllAppHosts.ps1
    Stops all AppHost processes.

.EXAMPLE
    .\Stop-AllAppHosts.ps1 -WhatIf
    Shows what would be stopped without actually stopping anything.

.NOTES
    Author: Update Engine Team
    Date: November 30, 2025
#>

[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = 'Continue'

Write-Host @"

╔══════════════════════════════════════════════════════════════╗
║          Stopping All AppHost Processes                     ║
╚══════════════════════════════════════════════════════════════╝

"@ -ForegroundColor Cyan

$stoppedCount = 0

# 1. Check for PowerShell jobs
Write-Host "Checking for PowerShell background jobs..." -ForegroundColor Yellow
$jobs = Get-Job -ErrorAction SilentlyContinue

if ($jobs) {
    Write-Host "Found $($jobs.Count) PowerShell job(s):" -ForegroundColor White
    $jobs | Format-Table Id, Name, State, HasMoreData -AutoSize
    
    foreach ($job in $jobs) {
        if ($PSCmdlet.ShouldProcess("Job $($job.Id) - $($job.Name)", "Stop and Remove")) {
            Stop-Job -Job $job -ErrorAction SilentlyContinue
            Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
            Write-Host "  ✓ Stopped job: $($job.Name) (ID: $($job.Id))" -ForegroundColor Green
            $stoppedCount++
        }
    }
} else {
    Write-Host "  ✓ No PowerShell jobs found" -ForegroundColor Gray
}

Write-Host ""

# 2. Check for dotnet processes running AppHost
Write-Host "Checking for dotnet processes running AppHost..." -ForegroundColor Yellow
$dotnetProcesses = Get-Process dotnet -ErrorAction SilentlyContinue

$appHostProcesses = @()
foreach ($proc in $dotnetProcesses) {
    try {
        $cmdLine = (Get-CimInstance Win32_Process -Filter "ProcessId = $($proc.Id)" -ErrorAction Stop).CommandLine
        if ($cmdLine -like "*UpdateEngine.AppHost*" -or $cmdLine -like "*AppHost.csproj*") {
            $appHostProcesses += [PSCustomObject]@{
                PID = $proc.Id
                StartTime = $proc.StartTime
                CPU = $proc.CPU
                CommandLine = $cmdLine
            }
        }
    } catch {
        # Process may have exited
    }
}

if ($appHostProcesses) {
    Write-Host "Found $($appHostProcesses.Count) AppHost process(es):" -ForegroundColor White
    $appHostProcesses | Format-Table PID, StartTime, CPU -AutoSize
    
    foreach ($proc in $appHostProcesses) {
        if ($PSCmdlet.ShouldProcess("Process $($proc.PID)", "Stop")) {
            Stop-Process -Id $proc.PID -Force -ErrorAction SilentlyContinue
            Write-Host "  ✓ Stopped process: PID $($proc.PID)" -ForegroundColor Green
            $stoppedCount++
        }
    }
} else {
    Write-Host "  ✓ No AppHost dotnet processes found" -ForegroundColor Gray
}

Write-Host ""

# 3. Check Aspire Dashboard ports
Write-Host "Checking Aspire Dashboard ports..." -ForegroundColor Yellow
$aspirePorts = @(15001, 18888, 15000)
$portsInUse = @()

foreach ($port in $aspirePorts) {
    $connection = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    if ($connection) {
        $processName = (Get-Process -Id $connection.OwningProcess -ErrorAction SilentlyContinue).Name
        $portsInUse += [PSCustomObject]@{
            Port = $port
            State = $connection.State
            PID = $connection.OwningProcess
            Process = $processName
        }
    }
}

if ($portsInUse) {
    Write-Host "WARNING: Ports still in use:" -ForegroundColor Red
    $portsInUse | Format-Table Port, State, PID, Process -AutoSize
    Write-Host ""
    Write-Host "To force stop these processes:" -ForegroundColor Yellow
    foreach ($portInfo in $portsInUse) {
        Write-Host "  Stop-Process -Id $($portInfo.PID) -Force" -ForegroundColor Cyan
    }
} else {
    Write-Host "  ✓ All Aspire ports (15001, 18888, 15000) are free" -ForegroundColor Green
}

Write-Host ""

# 4. Check for Docker containers (Azurite, Redis managed by Aspire)
Write-Host "Checking for Aspire-managed Docker containers..." -ForegroundColor Yellow
try {
    # Look for containers with Aspire labels (usvc-dev)
    $aspireContainers = docker ps -a --filter "label=com.microsoft.developer.usvc-dev.name" --format "{{.ID}}\t{{.Names}}\t{{.Status}}\t{{.Label `"com.microsoft.developer.usvc-dev.name`"}}" 2>$null
    
    if ($aspireContainers) {
        Write-Host "Found Aspire-managed containers:" -ForegroundColor White
        
        $containerInfo = @()
        foreach ($line in $aspireContainers) {
            if ($line) {
                $parts = $line -split '\t'
                $containerInfo += [PSCustomObject]@{
                    ID = $parts[0]
                    Name = $parts[1]
                    Status = $parts[2]
                    AspireName = $parts[3]
                }
            }
        }
        
        $containerInfo | Format-Table ID, Name, Status, AspireName -AutoSize
        
        Write-Host ""
        Write-Host "Stopping and removing old Aspire containers..." -ForegroundColor Yellow
        
        foreach ($container in $containerInfo) {
            if ($PSCmdlet.ShouldProcess("Container $($container.ID) - $($container.Name)", "Stop and Remove")) {
                docker stop $container.ID 2>$null | Out-Null
                docker rm $container.ID 2>$null | Out-Null
                Write-Host "  ✓ Removed container: $($container.Name) ($($container.AspireName))" -ForegroundColor Green
                $stoppedCount++
            }
        }
    } else {
        Write-Host "  ✓ No Aspire containers found" -ForegroundColor Gray
    }
} catch {
    Write-Host "  ℹ Docker not available or not running" -ForegroundColor Gray
}

Write-Host ""

# Summary
Write-Host @"
╔══════════════════════════════════════════════════════════════╗
║                       SUMMARY                                ║
╚══════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

if ($stoppedCount -gt 0) {
    Write-Host "✓ Stopped $stoppedCount process(es)/job(s)" -ForegroundColor Green
} else {
    Write-Host "✓ No AppHost processes were running" -ForegroundColor Green
}

Write-Host ""
Write-Host "All AppHost processes are now stopped." -ForegroundColor Green
Write-Host "You can safely run Start-Demo.ps1 again." -ForegroundColor Cyan
Write-Host ""
