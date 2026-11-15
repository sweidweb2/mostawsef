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

.PARAMETER SkipPostgresDownload
    Reuse an existing postgresql-*-windows-x64.exe under artifacts\installer-staging\redist
    (or fail if missing). Use when building offline after a previous download.
#>
[CmdletBinding()]
param(
    [string] $Version = '1.0.0',
    [string] $OutputDir = 'C:\Users\User\Desktop\mostawsef-3ammo\[2] Builds',
    [switch] $SkipPublish,
    [switch] $SkipInnoInstall,
    [switch] $SkipPostgresDownload
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

function Find-StagedPostgresInstaller([string] $redistDir) {
    if (-not (Test-Path -LiteralPath $redistDir)) { return $null }
    $hit = Get-ChildItem -LiteralPath $redistDir -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -eq '.exe' -and $_.Name -match 'postgres' } |
        Sort-Object Length -Descending |
        Select-Object -First 1
    if ($hit) { return $hit.FullName }
    return $null
}

function Ensure-PostgresRedist([string] $redistDir) {
    New-Item -ItemType Directory -Force -Path $redistDir | Out-Null
    $existing = Find-StagedPostgresInstaller $redistDir
    if ($existing) {
        Write-Host "Using bundled PostgreSQL installer: $existing"
        return
    }

    if ($SkipPostgresDownload) {
        throw "No PostgreSQL installer under $redistDir and -SkipPostgresDownload was set. Run without that switch once (needs internet on the build PC)."
    }

    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if (-not $winget) {
        throw 'winget not found. Install winget, or place postgresql-*-windows-x64.exe into artifacts\installer-staging\redist and re-run with -SkipPostgresDownload.'
    }

    Write-Host 'Downloading PostgreSQL 17 Windows installer into Setup (build PC needs internet; clinic Setup stays offline)...'
    $downloadDir = Join-Path $redistDir '_winget-download'
    if (Test-Path -LiteralPath $downloadDir) {
        Remove-Item -LiteralPath $downloadDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $downloadDir | Out-Null

    & winget download --id PostgreSQL.PostgreSQL.17 -e -d $downloadDir --architecture x64 `
        --accept-package-agreements --accept-source-agreements --disable-interactivity
    if ($LASTEXITCODE -ne 0) {
        throw "winget download of PostgreSQL 17 failed ($LASTEXITCODE)."
    }

    $downloaded = Find-StagedPostgresInstaller $downloadDir
    if (-not $downloaded) {
        throw "winget download finished but no PostgreSQL .exe was found under $downloadDir"
    }

    $destName = if ($downloaded -match 'postgresql-.+\.exe$') { Split-Path $downloaded -Leaf } else { 'postgresql-17-windows-x64.exe' }
    $dest = Join-Path $redistDir $destName
    Copy-Item -LiteralPath $downloaded -Destination $dest -Force
    Remove-Item -LiteralPath $downloadDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Bundled for Setup: $dest ($([math]::Round((Get-Item $dest).Length / 1MB)) MB)"
}

if (-not $SkipPublish) {
    Write-Host 'Publishing Desktop + API (Release win-x64, self-contained)...'
    # Keep redist across a restage so Postgres is not re-downloaded every build unless missing.
    $redistKeep = Join-Path $staging 'redist'
    $redistTemp = Join-Path $repoRoot 'artifacts\installer-redist-keep'
    if (Test-Path -LiteralPath $redistKeep) {
        if (Test-Path -LiteralPath $redistTemp) { Remove-Item -LiteralPath $redistTemp -Recurse -Force }
        Move-Item -LiteralPath $redistKeep -Destination $redistTemp -Force
    }
    if (Test-Path -LiteralPath $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path (Join-Path $staging 'Api') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $staging 'Desktop') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $staging 'scripts') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $staging 'redist') | Out-Null
    if (Test-Path -LiteralPath $redistTemp) {
        Copy-Item -LiteralPath (Join-Path $redistTemp '*') -Destination (Join-Path $staging 'redist') -Recurse -Force
        Remove-Item -LiteralPath $redistTemp -Recurse -Force
    }

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
    Ensure-PostgresRedist (Join-Path $staging 'redist')
    Write-Host "Staging ready: $staging"
} else {
    if (-not (Test-Path -LiteralPath (Join-Path $staging 'Api\Mostawsef.Api.exe'))) {
        throw "SkipPublish set but staging API missing under $staging"
    }
    Ensure-PostgresRedist (Join-Path $staging 'redist')
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
Write-Host 'Clinic install: run Mostawsef-Setup.exe as Administrator (offline: apps are self-contained; Postgres is bundled).'
Write-Host 'After install: open Mostawsef from the Desktop shortcut (do not spam-click if a reboot was requested).'
Write-Host 'Updates: run a newer Setup; Postgres data directory is never deleted.'
