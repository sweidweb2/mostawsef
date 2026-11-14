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

# Running instances lock the build output.
Get-Process Mostawsef.Api, Mostawsef.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force

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
    # `dotnet run` starts the API as a child process; stop it as well.
    Get-Process Mostawsef.Api -ErrorAction SilentlyContinue | Stop-Process -Force
    Pop-Location
    Write-Host "Stopped." -ForegroundColor Cyan
}
