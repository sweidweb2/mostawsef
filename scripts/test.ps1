<#
.SYNOPSIS
    Runs Mostawsef tests with one consistent command.

.DESCRIPTION
    ./scripts/test.ps1 [target] [filter] [-Watch] [-Coverage] [-Detailed] [-NoBuild]

    target   all (default) | api | desktop
    filter   part of a test class or method name (case-sensitive), e.g. Login, NavigationGuard, Wrong_password

    Prefer a target + filter while iterating (e.g. desktop Orders). Use all only as a full gate.
    Close the running API/desktop apps before a full run so Debug builds are reused (faster than Release).
    -Watch needs api or desktop, not all.

.EXAMPLE
    ./scripts/test.ps1                      # every test (full gate)
    ./scripts/test.ps1 api                  # API integration tests (needs Docker)
    ./scripts/test.ps1 desktop              # desktop tests (no Docker)
    ./scripts/test.ps1 api Login            # API tests whose name contains "Login"
    ./scripts/test.ps1 desktop Orders       # only order-related desktop tests
    ./scripts/test.ps1 desktop NavigationGuard -Watch   # re-run on every file save
    ./scripts/test.ps1 desktop Orders -NoBuild          # skip restore/build when binaries are fresh
    ./scripts/test.ps1 all -Coverage        # with code coverage (TestResults/)
    ./scripts/test.ps1 api -Detailed        # per-test timing in the console
#>
param(
    [Parameter(Position = 0)]
    [ValidateSet("all", "api", "desktop")]
    [string]$Target = "all",

    [Parameter(Position = 1)]
    [string]$Filter,

    [switch]$Watch,
    [switch]$Coverage,
    [switch]$Detailed,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

if ($Watch -and $Target -eq "all") {
    Write-Host "-Watch needs one project: ./scripts/test.ps1 api -Watch  or  ./scripts/test.ps1 desktop -Watch" -ForegroundColor Red
    exit 1
}

if ($NoBuild -and $Watch) {
    Write-Host "-NoBuild cannot be used with -Watch (watch must rebuild)." -ForegroundColor Red
    exit 1
}

$projects = @{
    all     = "Mostawsef.slnx"
    api     = "tests/Mostawsef.Api.IntegrationTests"
    desktop = "tests/Mostawsef.Desktop.Tests"
}

# API tests start PostgreSQL in Docker.
if ($Target -in "all", "api") {
    docker info *> $null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Docker is not running. Start Docker Desktop, or run: ./scripts/test.ps1 desktop" -ForegroundColor Red
        exit 1
    }
}

# A running app locks the Debug build output, so build tests in Release instead.
$configuration = "Debug"
$running = @(Get-Process Mostawsef.Desktop, Mostawsef.Api -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    $configuration = "Release"
    Write-Host "App is running - using Release build to avoid locked files. Close it for faster Debug runs." -ForegroundColor Yellow
}

$arguments = @($projects[$Target], "-c", $configuration)
if ($Filter) { $arguments += @("--filter", "FullyQualifiedName~$Filter") }
if ($NoBuild) { $arguments += @("--no-restore", "--no-build") }
if ($Coverage) { $arguments += @("--collect", "XPlat Code Coverage", "--results-directory", "TestResults") }
if ($Detailed) { $arguments += @("--logger", "console;verbosity=detailed") }

Push-Location $root
try {
    if ($Watch) {
        Write-Host "> dotnet watch test $($arguments -join ' ')" -ForegroundColor Cyan
        dotnet watch --project $projects[$Target] test -- @($arguments | Select-Object -Skip 1)
    }
    else {
        Write-Host "> dotnet test $($arguments -join ' ')" -ForegroundColor Cyan
        dotnet test @arguments
    }
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
