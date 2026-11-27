<#
.SYNOPSIS
    Publish Desktop + API and compile Mostawsef-Setup.exe (Inno Setup).

.PARAMETER Version
    Setup version (semver). Default: auto — next patch after the highest Mostawsef-Setup-*.exe
    already in OutputDir (starts at 1.0.0). Pass an explicit version to force one
    (fails if that file already exists, unless -Force).

.PARAMETER OutputDir
    Folder for versioned Setup exes (default: Desktop\mostawsef-3ammo\[2] Builds).
    Writes Mostawsef-Setup-{Version}.exe and never deletes older builds.

.PARAMETER Force
    Allow replacing an existing Mostawsef-Setup-{Version}.exe when -Version is set explicitly.

.PARAMETER SkipPublish
    Reuse existing staging publish folders (Api/Desktop already filled).

.PARAMETER SkipInnoInstall
    Do not winget-install Inno Setup if ISCC.exe is missing.

.PARAMETER SkipPostgresDownload
    Do not call winget. Reuse the durable cache (artifacts\cache\postgresql) or fail if missing.
    Normal builds already reuse the cache; this only blocks a first-time download when offline.

.PARAMETER UpdateOnly
    Build an update Setup (no PostgreSQL redist). For GitHub Releases / in-app update.
    First-install / USB builds omit this switch so Postgres stays bundled.
#>
[CmdletBinding()]
param(
    [string] $Version = 'auto',
    [string] $OutputDir = 'C:\Users\User\Desktop\mostawsef-3ammo\[2] Builds',
    [switch] $Force,
    [switch] $SkipPublish,
    [switch] $SkipInnoInstall,
    [switch] $SkipPostgresDownload,
    [switch] $UpdateOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$staging = Join-Path $repoRoot 'artifacts\installer-staging'
$compilerOut = Join-Path $repoRoot 'artifacts\installer'
# Durable cache: survives wiping installer-staging every publish. Download once, reuse forever.
$postgresCache = Join-Path $repoRoot 'artifacts\cache\postgresql'
$iss = Join-Path $repoRoot 'packaging\innosetup\Mostawsef.iss'
# Reject partial winget downloads (full EDB installer is ~300–400 MB).
$minPostgresInstallerBytes = 50MB

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

function Get-SetupVersions([string] $dir) {
    if (-not (Test-Path -LiteralPath $dir)) { return @() }
    Get-ChildItem -LiteralPath $dir -File -Filter 'Mostawsef-Setup-*.exe' -ErrorAction SilentlyContinue |
        ForEach-Object {
            if ($_.BaseName -match '^Mostawsef-Setup-(\d+)\.(\d+)\.(\d+)$') {
                [pscustomobject]@{
                    Major = [int]$Matches[1]
                    Minor = [int]$Matches[2]
                    Patch = [int]$Matches[3]
                    File  = $_.FullName
                }
            }
        }
}

function Get-NextSetupVersion([string] $dir) {
    $all = @(Get-SetupVersions $dir)
    if ($all.Count -eq 0) { return '1.0.0' }
    $max = $all | Sort-Object Major, Minor, Patch | Select-Object -Last 1
    return '{0}.{1}.{2}' -f $max.Major, $max.Minor, ($max.Patch + 1)
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

if ([string]::IsNullOrWhiteSpace($Version) -or $Version -eq 'auto') {
    $Version = Get-NextSetupVersion $OutputDir
    Write-Host "Auto version: $Version (next after existing Builds)" -ForegroundColor DarkGray
} elseif ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be semver like 1.2.3 (or 'auto'). Got: $Version"
}

$setupFileName = "Mostawsef-Setup-$Version.exe"
$dest = Join-Path $OutputDir $setupFileName
if ((Test-Path -LiteralPath $dest) -and -not $Force) {
    throw "Already exists: $dest`nPass a new -Version, use -Version auto, or -Force to replace this version only."
}

Write-Host "==> Mostawsef Setup build (version $Version → $setupFileName)" -ForegroundColor Cyan

# Stop a live service that may lock publish outputs under Builds\Api (not staging, but be safe).
$svc = Get-Service -Name MostawsefApi -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -eq 'Running') {
    Write-Host 'Note: MostawsefApi is running. Staging uses artifacts\installer-staging (not the live Builds folder).' -ForegroundColor DarkGray
}

function Find-PostgresInstallerExe([string] $dir) {
    if (-not (Test-Path -LiteralPath $dir)) { return $null }
    $hit = Get-ChildItem -LiteralPath $dir -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Extension -eq '.exe' -and
            $_.Name -match 'postgres' -and
            $_.FullName -notmatch '[\\/]_winget-download[\\/]' -and
            $_.Length -ge $minPostgresInstallerBytes
        } |
        Sort-Object Length -Descending |
        Select-Object -First 1
    if ($hit) { return $hit.FullName }
    return $null
}

function Copy-PostgresInstallerToRedist([string] $sourceExe, [string] $redistDir) {
    New-Item -ItemType Directory -Force -Path $redistDir | Out-Null
    $destName = if ((Split-Path $sourceExe -Leaf) -match '^postgresql-.+\.exe$') {
        Split-Path $sourceExe -Leaf
    } else {
        'postgresql-17-windows-x64.exe'
    }
    $dest = Join-Path $redistDir $destName
    if ((Test-Path -LiteralPath $dest) -and (Get-Item -LiteralPath $dest).Length -ge $minPostgresInstallerBytes) {
        return $dest
    }
    Copy-Item -LiteralPath $sourceExe -Destination $dest -Force
    return $dest
}

function Save-PostgresInstallerToCache([string] $sourceExe) {
    New-Item -ItemType Directory -Force -Path $postgresCache | Out-Null
    $destName = if ((Split-Path $sourceExe -Leaf) -match '^postgresql-.+\.exe$') {
        Split-Path $sourceExe -Leaf
    } else {
        'postgresql-17-windows-x64.exe'
    }
    $dest = Join-Path $postgresCache $destName
    if (-not ((Test-Path -LiteralPath $dest) -and (Get-Item -LiteralPath $dest).Length -eq (Get-Item -LiteralPath $sourceExe).Length)) {
        Copy-Item -LiteralPath $sourceExe -Destination $dest -Force
    }
    return $dest
}

function Ensure-PostgresRedist([string] $redistDir) {
    New-Item -ItemType Directory -Force -Path $redistDir | Out-Null
    New-Item -ItemType Directory -Force -Path $postgresCache | Out-Null

    # 1) Already staged for this build
    $existing = Find-PostgresInstallerExe $redistDir
    if ($existing) {
        Save-PostgresInstallerToCache $existing | Out-Null
        Write-Host "Using bundled PostgreSQL installer: $existing"
        return
    }

    # 2) Durable cache (survives staging wipe) — download once, reuse on every build
    $cached = Find-PostgresInstallerExe $postgresCache
    if ($cached) {
        $dest = Copy-PostgresInstallerToRedist $cached $redistDir
        Write-Host "Reusing cached PostgreSQL installer (no download): $dest ($([math]::Round((Get-Item $dest).Length / 1MB)) MB)"
        return
    }

    if ($SkipPostgresDownload) {
        throw @"
No PostgreSQL installer in cache ($postgresCache) or staging redist, and -SkipPostgresDownload was set.
Place postgresql-*-windows-x64.exe into $postgresCache once, or run without -SkipPostgresDownload (needs internet once).
"@
    }

    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if (-not $winget) {
        throw @"
winget not found. Place postgresql-*-windows-x64.exe into:
  $postgresCache
then re-run (no internet needed after that).
"@
    }

    Write-Host "Downloading PostgreSQL 17 once into cache ($postgresCache); later builds reuse it..."
    $downloadDir = Join-Path $postgresCache '_winget-download'
    if (Test-Path -LiteralPath $downloadDir) {
        Remove-Item -LiteralPath $downloadDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $downloadDir | Out-Null

    & winget download --id PostgreSQL.PostgreSQL.17 -e -d $downloadDir --architecture x64 `
        --accept-package-agreements --accept-source-agreements --disable-interactivity
    if ($LASTEXITCODE -ne 0) {
        throw "winget download of PostgreSQL 17 failed ($LASTEXITCODE)."
    }

    $downloaded = Find-PostgresInstallerExe $downloadDir
    if (-not $downloaded) {
        throw "winget download finished but no complete PostgreSQL .exe was found under $downloadDir"
    }

    $cachedPath = Save-PostgresInstallerToCache $downloaded
    Remove-Item -LiteralPath $downloadDir -Recurse -Force -ErrorAction SilentlyContinue
    $dest = Copy-PostgresInstallerToRedist $cachedPath $redistDir
    Write-Host "Cached + bundled for Setup: $dest ($([math]::Round((Get-Item $dest).Length / 1MB)) MB)"
}

if (-not $SkipPublish) {
    Write-Host 'Publishing Desktop + API (Release win-x64, self-contained)...'
    if (Test-Path -LiteralPath $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path (Join-Path $staging 'Api') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $staging 'Desktop') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $staging 'scripts') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $staging 'redist') | Out-Null

    Push-Location $repoRoot
    try {
        # Stamp the same semver into Desktop + API as the Setup file name / Inno AppVersion.
        dotnet publish frontend/Mostawsef.Desktop -c Release -o (Join-Path $staging 'Desktop') -p:Version=$Version
        if ($LASTEXITCODE -ne 0) { throw "Desktop publish failed ($LASTEXITCODE)" }
        dotnet publish backend/Mostawsef.Api -c Release -o (Join-Path $staging 'Api') -p:Version=$Version
        if ($LASTEXITCODE -ne 0) { throw "API publish failed ($LASTEXITCODE)" }
    } finally {
        Pop-Location
    }

    Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\install-api-service.ps1') -Destination (Join-Path $staging 'scripts\install-api-service.ps1') -Force
    Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging\innosetup\install-clinic.ps1') -Destination (Join-Path $staging 'scripts\install-clinic.ps1') -Force
    if ($UpdateOnly) {
        Write-Host 'UpdateOnly: skipping PostgreSQL redist (clinic already has Postgres).'
    } else {
        Ensure-PostgresRedist (Join-Path $staging 'redist')
    }
    Write-Host "Staging ready: $staging"
} else {
    if (-not (Test-Path -LiteralPath (Join-Path $staging 'Api\Mostawsef.Api.exe'))) {
        throw "SkipPublish set but staging API missing under $staging"
    }
    $redistDir = Join-Path $staging 'redist'
    if ($UpdateOnly) {
        if (Test-Path -LiteralPath $redistDir) {
            Get-ChildItem -LiteralPath $redistDir -Force -ErrorAction SilentlyContinue |
                Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        } else {
            New-Item -ItemType Directory -Force -Path $redistDir | Out-Null
        }
        Write-Host 'UpdateOnly: cleared staging redist (no PostgreSQL in this Setup).'
    } else {
        Ensure-PostgresRedist $redistDir
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

# Inno writes into artifacts\installer; we copy a versioned name into Builds (never delete older Setups).
$outputBase = "Mostawsef-Setup-$Version"
Write-Host "Compiling with $iscc ..."
& $iscc `
    "/DMyAppVersion=$Version" `
    "/DMyOutputBaseFilename=$outputBase" `
    "/DMyStagingDir=$staging" `
    "/DMyOutputDir=$compilerOut" `
    $iss
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }

$built = Join-Path $compilerOut "$outputBase.exe"
if (-not (Test-Path -LiteralPath $built)) {
    throw "Expected output missing: $built"
}

Copy-Item -LiteralPath $built -Destination $dest -Force
Write-Host "Setup ready: $dest" -ForegroundColor Green
Write-Host "Older builds in $OutputDir are kept (Mostawsef-Setup-*.exe)." -ForegroundColor DarkGray
if ($UpdateOnly) {
    Write-Host 'Update Setup (no Postgres redist). Silent in-app flags: see Docs/7-automating install/decisions.md'
    Write-Host 'Desktop relaunches itself after a silent update — Setup does not auto-start the app when silent.'
} else {
    Write-Host 'Clinic install: run the versioned Setup as Administrator (offline: self-contained apps + bundled Postgres).'
    Write-Host 'After install: open Mostawsef from the Desktop shortcut (do not spam-click if a reboot was requested).'
}
Write-Host 'Updates: send a newer Mostawsef-Setup-{version}.exe (-UpdateOnly for Releases); Postgres data directory is never deleted.'
