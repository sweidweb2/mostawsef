<#
.SYNOPSIS
    Publish Desktop + API and compile Mostawsef-Setup.exe (Inno Setup).

.PARAMETER Version
    Setup version (semver, default 1.0.0). Also written into the .iss define at build time.

.PARAMETER OutputDir
    Folder for Mostawsef-Setup.exe (default: Desktop\mostawsef-3ammo\[2] Builds).

.PARAMETER SkipPublish
    Reuse existing staging publish folders (Api/Desktop already filled).

.PARAMETER SkipInnoInstall
    Do not winget-install Inno Setup if ISCC.exe is missing.
#>
[CmdletBinding()]
param(
    [string] $Version = '1.0.0',
    [string] $OutputDir = 'C:\Users\User\Desktop\mostawsef-3ammo\[2] Builds',
    [switch] $SkipPublish,
    [switch] $SkipInnoInstall
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$staging = Join-Path $repoRoot 'artifacts\installer-staging'
$compilerOut = Join-Path $repoRoot 'artifacts\installer'
$iss = Join-Path $repoRoot 'packaging\innosetup\Mostawsef.iss'

function Find-Iscc {
    $candidates = @(
        "${env:LocalAppData}\Programs\Inno Setup 6\ISCC.exe",
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe',
        "${env:LocalAppData}\Programs\Inno Setup 7\ISCC.exe",
        'C:\Program Files (x86)\Inno Setup 7\ISCC.exe',
        'C:\Program Files\Inno Setup 7\ISCC.exe'
    )
    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath $c) { return $c }
    }
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

Write-Host "==> Mostawsef Setup build (version $Version)" -ForegroundColor Cyan

# Stop a live service that may lock publish outputs under Builds\Api (not staging, but be safe).
$svc = Get-Service -Name MostawsefApi -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -eq 'Running') {
    Write-Host 'Note: MostawsefApi is running. Staging uses artifacts\installer-staging (not the live Builds folder).' -ForegroundColor DarkGray
}

if (-not $SkipPublish) {
    Write-Host 'Publishing Desktop + API (Release win-x64)...'
    if (Test-Path -LiteralPath $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path (Join-Path $staging 'Api') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $staging 'Desktop') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $staging 'scripts') | Out-Null

    Push-Location $repoRoot
    try {
        dotnet publish frontend/Mostawsef.Desktop -c Release -o (Join-Path $staging 'Desktop')
        if ($LASTEXITCODE -ne 0) { throw "Desktop publish failed ($LASTEXITCODE)" }
        dotnet publish backend/Mostawsef.Api -c Release -o (Join-Path $staging 'Api')
        if ($LASTEXITCODE -ne 0) { throw "API publish failed ($LASTEXITCODE)" }
    } finally {
        Pop-Location
    }

    Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\install-api-service.ps1') -Destination (Join-Path $staging 'scripts\install-api-service.ps1') -Force
    Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging\innosetup\install-clinic.ps1') -Destination (Join-Path $staging 'scripts\install-clinic.ps1') -Force
    Write-Host "Staging ready: $staging"
} else {
    if (-not (Test-Path -LiteralPath (Join-Path $staging 'Api\Mostawsef.Api.exe'))) {
        throw "SkipPublish set but staging API missing under $staging"
    }
}

$iscc = Find-Iscc
if (-not $iscc) {
    if ($SkipInnoInstall) {
        throw 'ISCC.exe not found. Install Inno Setup 6+ or omit -SkipInnoInstall.'
    }
    Write-Host 'Installing Inno Setup via winget...'
    winget install --id JRSoftware.InnoSetup -e --accept-package-agreements --accept-source-agreements --disable-interactivity
    if ($LASTEXITCODE -ne 0) {
        throw "winget failed to install Inno Setup ($LASTEXITCODE)"
    }
    $iscc = Find-Iscc
    if (-not $iscc) {
        throw 'Inno Setup installed but ISCC.exe still not found. Open Inno Setup once, then re-run.'
    }
}

New-Item -ItemType Directory -Force -Path $compilerOut | Out-Null
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Write-Host "Compiling with $iscc ..."
& $iscc `
    "/DMyAppVersion=$Version" `
    "/DMyStagingDir=$staging" `
    "/DMyOutputDir=$compilerOut" `
    $iss
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }

$built = Join-Path $compilerOut 'Mostawsef-Setup.exe'
if (-not (Test-Path -LiteralPath $built)) {
    throw "Expected output missing: $built"
}

$dest = Join-Path $OutputDir 'Mostawsef-Setup.exe'
Copy-Item -LiteralPath $built -Destination $dest -Force
Write-Host "Setup ready: $dest" -ForegroundColor Green
Write-Host 'Clinic install: run Mostawsef-Setup.exe as Administrator.'
Write-Host 'Updates: run a newer Setup; Postgres data directory is never deleted.'
