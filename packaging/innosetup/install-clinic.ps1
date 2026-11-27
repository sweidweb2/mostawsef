<#
.SYNOPSIS
    Post-copy clinic setup for Mostawsef Setup.exe (Postgres, DB, config, Windows service).

.PARAMETER InstallRoot
    Install folder (default C:\Mostawsef). Contains Api\ and Desktop\.

.PARAMETER ConfigBackup
    Optional folder with previous appsettings.json / appsettings.Clinic.json from before overwrite.

.PARAMETER PostgresService
    Windows service name for native PostgreSQL 17.

.PARAMETER PostgresSuperPassword
    postgres superuser password. Required only when PostgreSQL is already installed and
    Api\appsettings.json has no ConnectionStrings:Default yet (creates the mostawsef role/DB).
#>
[CmdletBinding()]
param(
    [string] $InstallRoot = 'C:\Mostawsef',
    [string] $ConfigBackup = '',
    [string] $PostgresService = 'postgresql-x64-17',
    [string] $PostgresSuperPassword = ''
)

$ErrorActionPreference = 'Stop'

$apiDir = Join-Path $InstallRoot 'Api'
$desktopDir = Join-Path $InstallRoot 'Desktop'
$apiExe = Join-Path $apiDir 'Mostawsef.Api.exe'
$appsettingsPath = Join-Path $apiDir 'appsettings.json'
$clinicPath = Join-Path $apiDir 'appsettings.Clinic.json'
$serviceScript = Join-Path $InstallRoot 'scripts\install-api-service.ps1'
$logPath = Join-Path $InstallRoot 'install-clinic.log'

function Write-InstallLog([string] $message) {
    $line = '{0:u}  {1}' -f (Get-Date).ToUniversalTime(), $message
    Add-Content -LiteralPath $logPath -Value $line -Encoding UTF8
    Write-Host $message
}

try {
    New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
    Set-Content -LiteralPath $logPath -Value ("{0:u}  install-clinic starting (InstallRoot={1})" -f (Get-Date).ToUniversalTime(), $InstallRoot) -Encoding UTF8
} catch {
    # Logging must not block install; continue without a log file if the disk refuses it.
}

function Assert-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'install-clinic.ps1 must run elevated (Setup.exe should request Administrator).'
    }
}

function Restore-PreservedConfig {
    if (-not $ConfigBackup -or -not (Test-Path -LiteralPath $ConfigBackup)) { return }

    $bakClinic = Join-Path $ConfigBackup 'appsettings.Clinic.json'
    if ((Test-Path -LiteralPath $bakClinic) -and -not (Test-Path -LiteralPath $clinicPath)) {
        Copy-Item -LiteralPath $bakClinic -Destination $clinicPath -Force
        Write-Host "Restored $clinicPath"
    }

    $bakApp = Join-Path $ConfigBackup 'appsettings.json'
    if (-not (Test-Path -LiteralPath $bakApp)) { return }
    if (-not (Test-Path -LiteralPath $appsettingsPath)) { return }

    $prev = Get-Content -LiteralPath $bakApp -Raw | ConvertFrom-Json
    $prevCs = $prev.ConnectionStrings.Default
    if ([string]::IsNullOrWhiteSpace($prevCs)) { return }

    $curr = Get-Content -LiteralPath $appsettingsPath -Raw | ConvertFrom-Json
    $currCs = $null
    if ($curr.PSObject.Properties['ConnectionStrings'] -and $curr.ConnectionStrings.PSObject.Properties['Default']) {
        $currCs = [string]$curr.ConnectionStrings.Default
    }
    if (-not [string]::IsNullOrWhiteSpace($currCs)) { return }

    if (-not $curr.PSObject.Properties['ConnectionStrings']) {
        $curr | Add-Member -NotePropertyName ConnectionStrings -NotePropertyValue ([pscustomobject]@{}) 
    }
    $curr.ConnectionStrings | Add-Member -NotePropertyName Default -NotePropertyValue $prevCs -Force
    $curr | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $appsettingsPath -Encoding UTF8
    Write-Host 'Restored ConnectionStrings:Default from previous install.'
}

function Get-AppsettingsConnectionString {
    if (-not (Test-Path -LiteralPath $appsettingsPath)) { return $null }
    $json = Get-Content -LiteralPath $appsettingsPath -Raw | ConvertFrom-Json
    if ($json.PSObject.Properties['ConnectionStrings'] -and $json.ConnectionStrings.PSObject.Properties['Default']) {
        return [string]$json.ConnectionStrings.Default
    }
    return $null
}

function Set-AppsettingsConnectionString([string] $connectionString) {
    $json = Get-Content -LiteralPath $appsettingsPath -Raw | ConvertFrom-Json
    if (-not $json.PSObject.Properties['ConnectionStrings']) {
        $json | Add-Member -NotePropertyName ConnectionStrings -NotePropertyValue ([pscustomobject]@{})
    }
    $json.ConnectionStrings | Add-Member -NotePropertyName Default -NotePropertyValue $connectionString -Force
    $json | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $appsettingsPath -Encoding UTF8
}

function Get-PsqlPath {
    $candidates = @(
        'C:\Program Files\PostgreSQL\17\bin\psql.exe',
        'C:\Program Files\PostgreSQL\16\bin\psql.exe'
    )
    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath $c) { return $c }
    }
    $cmd = Get-Command psql -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function Get-PostgresPort {
    $conf = 'C:\Program Files\PostgreSQL\17\data\postgresql.conf'
    if (Test-Path -LiteralPath $conf) {
        $line = Select-String -LiteralPath $conf -Pattern '^\s*port\s*=\s*(\d+)' | Select-Object -First 1
        if ($line) { return [int]$line.Matches[0].Groups[1].Value }
    }
    return 5432
}

function Get-BundledPostgresInstaller {
    $redist = Join-Path $InstallRoot 'redist'
    if (-not (Test-Path -LiteralPath $redist)) { return $null }
    $exe = Get-ChildItem -LiteralPath $redist -Filter 'postgresql-*-windows-x64.exe' -File -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        Select-Object -First 1
    if ($exe) { return $exe.FullName }
    return Get-ChildItem -LiteralPath $redist -Filter '*.exe' -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match 'postgres' } |
        Sort-Object Name -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

# Database passwords from a cryptographic RNG (Get-Random is not one): [0-9A-Za-z], without modulo bias.
function New-RandomSecret([int]$Length) {
    $alphabet = [char[]]((48..57) + (65..90) + (97..122))
    $chars = New-Object System.Collections.Generic.List[char]
    $buffer = New-Object byte[] 64
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        while ($chars.Count -lt $Length) {
            $rng.GetBytes($buffer)
            foreach ($b in $buffer) {
                # 248 = 4 * 62: bytes above it would favour the first characters.
                if ($b -lt 248 -and $chars.Count -lt $Length) { $chars.Add($alphabet[$b % 62]) }
            }
        }
    } finally {
        $rng.Dispose()
    }
    return -join $chars
}

function Ensure-Postgres {
    $svc = Get-Service -Name $PostgresService -ErrorAction SilentlyContinue
    if ($svc) {
        if ($svc.Status -ne 'Running') {
            Start-Service -Name $PostgresService
        }
        Write-Host "PostgreSQL service '$PostgresService' is running."
        return
    }

    # Fresh clinic install: port 5432. Superuser password is local to this machine only.
    $superPass = New-RandomSecret 24
    $superPassFile = Join-Path $InstallRoot 'postgres-superuser.password.txt'
    Set-Content -LiteralPath $superPassFile -Value $superPass -Encoding ASCII
    try {
        $acl = Get-Acl -LiteralPath $superPassFile
        $acl.SetAccessRuleProtection($true, $false)
        $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
            ([System.Security.Principal.WindowsIdentity]::GetCurrent().Name),
            'FullControl', 'Allow')
        $acl.AddAccessRule($rule)
        Set-Acl -LiteralPath $superPassFile -AclObject $acl
    } catch {
        Write-Host "Could not tighten ACL on $superPassFile : $($_.Exception.Message)" -ForegroundColor Yellow
    }

    $silentArgs = "--mode unattended --unattendedmodeui none --superpassword `"$superPass`" --serverport 5432 --servicename $PostgresService --disable-components stackbuilder"
    $bundled = Get-BundledPostgresInstaller
    if ($bundled) {
        Write-Host "Installing PostgreSQL 17 from bundled installer (offline): $bundled"
        $proc = Start-Process -FilePath $bundled -ArgumentList $silentArgs -Wait -PassThru -NoNewWindow
        if ($proc.ExitCode -ne 0) {
            throw "Bundled PostgreSQL installer failed (exit $($proc.ExitCode))."
        }
    } else {
        $winget = Get-Command winget -ErrorAction SilentlyContinue
        if (-not $winget) {
            throw 'PostgreSQL is not installed, no bundled installer under redist\, and winget was not found. Rebuild Setup with build-setup.ps1 (downloads Postgres into the package).'
        }

        Write-Host 'WARNING: No bundled Postgres installer — falling back to winget (needs internet)...' -ForegroundColor Yellow
        & winget install --id PostgreSQL.PostgreSQL.17 -e --accept-package-agreements --accept-source-agreements --disable-interactivity --override $silentArgs
        if ($LASTEXITCODE -ne 0) {
            throw "winget failed to install PostgreSQL 17 (exit $LASTEXITCODE)."
        }
    }

    $deadline = (Get-Date).AddMinutes(5)
    do {
        $svc = Get-Service -Name $PostgresService -ErrorAction SilentlyContinue
        if ($svc -and $svc.Status -eq 'Running') { break }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    if (-not $svc -or $svc.Status -ne 'Running') {
        throw "PostgreSQL service '$PostgresService' did not start after install. A reboot may be required after the first Postgres install; then run Setup again (or start the service and Desktop)."
    }

    $script:NewPostgresSuperPassword = $superPass
    Write-Host "PostgreSQL installed. Superuser password saved at $superPassFile (keep private; delete after you have recorded it)."
}

function Escape-SqlLiteral([string] $value) {
    return $value.Replace("'", "''")
}

function Ensure-Database {
    param(
        [string] $Port,
        [string] $AppPassword,
        [string] $SuperPassword
    )

    $psql = Get-PsqlPath
    if (-not $psql) { throw 'psql.exe not found after PostgreSQL install.' }

    $safeAppPass = Escape-SqlLiteral $AppPassword
    $env:PGPASSWORD = $SuperPassword

    function Invoke-Psql([string[]] $SqlArgs, [string] $failMessage) {
        $output = & $psql @SqlArgs 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "$failMessage (psql exit $LASTEXITCODE): $output"
        }
        return (($output | Out-String).Trim())
    }

    $roleExists = Invoke-Psql @(
        '-h', '127.0.0.1', '-p', "$Port", '-U', 'postgres', '-d', 'postgres', '-tAc',
        "SELECT 1 FROM pg_roles WHERE rolname = 'mostawsef'"
    ) 'Could not connect as postgres (check the password you entered in Setup)'

    if ($roleExists -ne '1') {
        Invoke-Psql @(
            '-h', '127.0.0.1', '-p', "$Port", '-U', 'postgres', '-d', 'postgres', '-v', 'ON_ERROR_STOP=1', '-c',
            "CREATE ROLE mostawsef LOGIN PASSWORD '$safeAppPass'"
        ) 'Failed to create role mostawsef' | Out-Null
    } else {
        Invoke-Psql @(
            '-h', '127.0.0.1', '-p', "$Port", '-U', 'postgres', '-d', 'postgres', '-v', 'ON_ERROR_STOP=1', '-c',
            "ALTER ROLE mostawsef WITH LOGIN PASSWORD '$safeAppPass'"
        ) 'Failed to update role mostawsef' | Out-Null
    }

    $dbExists = Invoke-Psql @(
        '-h', '127.0.0.1', '-p', "$Port", '-U', 'postgres', '-d', 'postgres', '-tAc',
        "SELECT 1 FROM pg_database WHERE datname = 'mostawsef'"
    ) 'Failed to check database mostawsef'

    if ($dbExists -ne '1') {
        Invoke-Psql @(
            '-h', '127.0.0.1', '-p', "$Port", '-U', 'postgres', '-d', 'postgres', '-v', 'ON_ERROR_STOP=1', '-c',
            'CREATE DATABASE mostawsef OWNER mostawsef;'
        ) 'Failed to create database mostawsef' | Out-Null
    } else {
        Invoke-Psql @(
            '-h', '127.0.0.1', '-p', "$Port", '-U', 'postgres', '-d', 'postgres', '-c',
            'ALTER DATABASE mostawsef OWNER TO mostawsef;'
        ) 'Failed to set database owner' | Out-Null
    }

    Invoke-Psql @(
        '-h', '127.0.0.1', '-p', "$Port", '-U', 'postgres', '-d', 'mostawsef', '-v', 'ON_ERROR_STOP=1', '-c',
        'GRANT ALL ON SCHEMA public TO mostawsef; ALTER SCHEMA public OWNER TO mostawsef;'
    ) 'Failed to grant schema rights' | Out-Null

    $env:PGPASSWORD = $AppPassword
    Invoke-Psql @(
        '-h', '127.0.0.1', '-p', "$Port", '-U', 'mostawsef', '-d', 'mostawsef', '-c', '\conninfo'
    ) 'mostawsef user could not connect after create' | Out-Host
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}

function Parse-ConnectionString([string] $cs) {
    $map = @{}
    foreach ($part in $cs.Split(';', [System.StringSplitOptions]::RemoveEmptyEntries)) {
        $kv = $part.Split('=', 2)
        if ($kv.Count -eq 2) { $map[$kv[0].Trim()] = $kv[1].Trim() }
    }
    return $map
}

function Read-PostgresSuperPasswordFile {
    $candidates = @(
        (Join-Path $InstallRoot 'scripts\postgres-super.password')
    )
    if (-not [string]::IsNullOrWhiteSpace($ConfigBackup)) {
        $candidates += (Join-Path $ConfigBackup 'postgres-super.password')
    }

    foreach ($passFile in $candidates) {
        if (Test-Path -LiteralPath $passFile) {
            $value = (Get-Content -LiteralPath $passFile -Raw -ErrorAction SilentlyContinue)
            if (-not [string]::IsNullOrWhiteSpace($value)) {
                Write-InstallLog "Read postgres superuser password from $passFile"
                return $value.Trim()
            }
            Write-InstallLog "Password file exists but is empty: $passFile"
        }
    }

    Write-InstallLog 'No postgres superuser password file found under scripts\ or ConfigBackup.'
    return ''
}

# --- main ---
if ([string]::IsNullOrWhiteSpace($PostgresSuperPassword)) {
    $PostgresSuperPassword = Read-PostgresSuperPasswordFile
}

try {
Write-InstallLog 'Asserting admin + layout...'
Assert-Admin

if (-not (Test-Path -LiteralPath $apiExe)) {
    throw "Mostawsef.Api.exe not found at $apiExe"
}
if (-not (Test-Path -LiteralPath (Join-Path $desktopDir 'Mostawsef.Desktop.exe'))) {
    throw "Mostawsef.Desktop.exe not found under $desktopDir"
}
if (-not (Test-Path -LiteralPath $serviceScript)) {
    throw "install-api-service.ps1 not found at $serviceScript"
}

Restore-PreservedConfig

Ensure-Postgres
$port = Get-PostgresPort
Write-InstallLog "PostgreSQL port=$port"

$existingCs = Get-AppsettingsConnectionString
if ([string]::IsNullOrWhiteSpace($existingCs)) {
    $superPass = $script:NewPostgresSuperPassword
    if ([string]::IsNullOrWhiteSpace($superPass)) {
        $superPass = $PostgresSuperPassword
    }
    if ([string]::IsNullOrWhiteSpace($superPass)) {
        throw @"
No ConnectionStrings:Default in Api\appsettings.json and PostgreSQL was already installed.
Setup must save the postgres password (wizard page), or pass -PostgresSuperPassword.
See $logPath and Docs/3-Follow-ups/clinic-install-playbook.md.
"@
    }

    $appPass = New-RandomSecret 24
    Write-InstallLog 'Creating/updating mostawsef role and database...'
    Ensure-Database -Port $port -AppPassword $appPass -SuperPassword $superPass
    $cs = "Host=127.0.0.1;Port=$port;Database=mostawsef;Username=mostawsef;Password=$appPass"
    Set-AppsettingsConnectionString $cs
    Write-InstallLog "Wrote ConnectionStrings:Default (port $port)."
} else {
    Write-InstallLog 'ConnectionStrings:Default already present; leaving it unchanged.'
    # Still verify the app user can connect when we know the password from the string.
    $map = Parse-ConnectionString $existingCs
    if ($map['Password'] -and $map['Username'] -and $map['Database']) {
        $psql = Get-PsqlPath
        if ($psql) {
            $env:PGPASSWORD = $map['Password']
            $p = if ($map['Port']) { $map['Port'] } else { "$port" }
            $h = if ($map['Host']) { $map['Host'] } else { '127.0.0.1' }
            & $psql -h $h -p $p -U $map['Username'] -d $map['Database'] -c 'SELECT 1;' | Out-Null
            if ($LASTEXITCODE -ne 0) {
                Write-InstallLog 'Warning: could not verify DB login with the saved connection string.'
            }
            Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
        }
    }
}

Write-InstallLog 'Installing / updating MostawsefApi Windows service...'
& $serviceScript -ApiPath $apiDir -PostgresService $PostgresService
if ($LASTEXITCODE -ne 0) { throw "install-api-service.ps1 failed ($LASTEXITCODE)" }

$ok = $false
for ($i = 0; $i -lt 45; $i++) {
    Start-Sleep -Seconds 1
    try {
        $h = Invoke-RestMethod -Uri 'http://localhost:5270/api/health' -TimeoutSec 2
        Write-InstallLog "Health: $($h | ConvertTo-Json -Compress)"
        $ok = $true
        break
    } catch {
        if ($i % 10 -eq 0) { Write-InstallLog "Waiting for API... ($i)" }
    }
}
if (-not $ok) {
    throw "API service did not become healthy at http://localhost:5270/api/health (see $logPath and Event Viewer)."
}

# Do not leave the superuser password on disk after a successful configure.
$localPassFile = Join-Path $InstallRoot 'scripts\postgres-super.password'
if (Test-Path -LiteralPath $localPassFile) {
    Remove-Item -LiteralPath $localPassFile -Force -ErrorAction SilentlyContinue
}

Write-InstallLog 'Clinic install steps finished.'
Write-Host 'Clinic install steps finished.' -ForegroundColor Green
}
catch {
    Write-InstallLog ("FAILED: " + $_.Exception.Message)
    Write-Host ("install-clinic FAILED: " + $_.Exception.Message) -ForegroundColor Red
    Write-Host "Log: $logPath" -ForegroundColor Yellow
    # Non-zero exit so Inno [Run] waituntilterminated aborts Setup; Desktop can point at this log.
    exit 1
}
finally {
    # Best-effort: never leave the wizard password file behind after the run.
    $localPassFile = Join-Path $InstallRoot 'scripts\postgres-super.password'
    if (Test-Path -LiteralPath $localPassFile) {
        Remove-Item -LiteralPath $localPassFile -Force -ErrorAction SilentlyContinue
    }
}
