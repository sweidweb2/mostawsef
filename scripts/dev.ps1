<#
.SYNOPSIS
    Runs the whole app for development with one command: database, API and desktop.

.DESCRIPTION
    ./scripts/dev.ps1 [-Suite low|medium|high]

    Starts PostgreSQL (Docker), stops any API/desktop instance that is already running, builds both, starts the API
    in this window, waits until it answers, then opens the desktop app. Closing the desktop app (or Ctrl+C) stops
    the API too. Run it again to pick up code changes.

    -Suite wipes the data and fills a test suite (see ./scripts/suite.ps1) once the API is up, before the app opens:
    ./scripts/dev.ps1 -Suite medium
#>
param(
    [ValidateSet("low", "medium", "high")]
    [string]$Suite
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$healthUrl = "http://localhost:5270/api/health"

docker info *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Docker is not running. Start Docker Desktop and try again." -ForegroundColor Red
    exit 1
}

function Stop-DevInstances {
    # Clinic Setup registers MostawsefApi as a Windows service — needs Admin to stop.
    $svc = Get-Service -Name MostawsefApi -ErrorAction SilentlyContinue
    if ($svc -and $svc.Status -ne 'Stopped') {
        $stopped = $false
        try {
            Stop-Service -Name MostawsefApi -Force -ErrorAction Stop
            $stopped = $true
        }
        catch {
            Write-Host "MostawsefApi service needs Administrator — approve the UAC prompt to stop it for dev." -ForegroundColor Yellow
            $arg = "-NoProfile -ExecutionPolicy Bypass -Command `"Stop-Service -Name MostawsefApi -Force`""
            try {
                $p = Start-Process -FilePath powershell.exe -Verb RunAs -ArgumentList $arg -Wait -PassThru
                if ($p.ExitCode -ne 0) { throw "elevated Stop-Service exited $($p.ExitCode)" }
            }
            catch {
                throw @"
Could not stop MostawsefApi (need Admin). Right-click PowerShell → Run as administrator, then:
  Stop-Service MostawsefApi
Re-run ./scripts/dev.ps1 after that.
"@
            }
            $svc = Get-Service -Name MostawsefApi -ErrorAction SilentlyContinue
            if ($svc -and $svc.Status -ne 'Stopped') {
                throw "MostawsefApi is still running after UAC stop. Run elevated: Stop-Service MostawsefApi"
            }
            $stopped = $true
        }
        if ($stopped) {
            Write-Host "Stopped Windows service MostawsefApi (clinic install on this PC)." -ForegroundColor Yellow
            Write-Host "After dev, start it again if needed: Start-Service MostawsefApi (Admin)" -ForegroundColor DarkGray
        }
    }

    Get-Process Mostawsef.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Get-Process Mostawsef.Api -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            Stop-Process -Id $_.Id -Force -ErrorAction Stop
        }
        catch {
            throw "Cannot stop Mostawsef.Api (PID $($_.Id)). Elevated: Stop-Service MostawsefApi"
        }
    }
}

# Running instances lock the build output (and bind :5270).
Stop-DevInstances

Push-Location $root
$api = $null
try {
    Write-Host "> docker compose up -d" -ForegroundColor Cyan
    docker compose up -d
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # Build one after the other: two parallel builds fight over the shared Contracts output.
    foreach ($project in "backend/Mostawsef.Api", "frontend/Mostawsef.Desktop") {
        Write-Host "> dotnet build $project" -ForegroundColor Cyan
        dotnet build $project --nologo -v q
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    Write-Host "> starting the API ($healthUrl)" -ForegroundColor Cyan
    $api = Start-Process dotnet -NoNewWindow -PassThru -ArgumentList @(
        "run", "--project", "backend/Mostawsef.Api", "--launch-profile", "http", "--no-build")

    $deadline = (Get-Date).AddSeconds(60)
    while ($true) {
        try {
            Invoke-WebRequest $healthUrl -UseBasicParsing -TimeoutSec 2 | Out-Null
            break
        }
        catch {
            if ($api.HasExited) { Write-Host "The API stopped while starting (see the output above)." -ForegroundColor Red; exit 1 }
            if ((Get-Date) -gt $deadline) { Write-Host "The API did not answer within 60 seconds." -ForegroundColor Red; exit 1 }
            Start-Sleep -Milliseconds 500
        }
    }

    if ($Suite) {
        & "$PSScriptRoot/suite.ps1" $Suite
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    Write-Host "> starting the desktop app (close it to stop everything)" -ForegroundColor Cyan
    dotnet run --project frontend/Mostawsef.Desktop --no-build
}
finally {
    if ($api -and -not $api.HasExited) { Stop-Process -Id $api.Id -Force -ErrorAction SilentlyContinue }
    # Child Mostawsef.Api from `dotnet run` (clinic Windows service was already stopped at start if present).
    Get-Process Mostawsef.Api -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Pop-Location
    Write-Host "Stopped." -ForegroundColor Cyan
}
