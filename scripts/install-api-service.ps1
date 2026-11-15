<#
.SYNOPSIS
    Install or update the Mostawsef API Windows service (Phase 4).

.DESCRIPTION
    Points the service at a published API folder, starts after PostgreSQL, and
    sets Automatic start. Run elevated (Administrator).

.PARAMETER ApiPath
    Folder that contains Mostawsef.Api.exe (default: C:\Mostawsef\Api).

.PARAMETER PostgresService
    Windows service name to depend on (default: postgresql-x64-17).

.PARAMETER Uninstall
    Remove the MostawsefApi service instead of installing it.
#>
[CmdletBinding()]
param(
    [string] $ApiPath = 'C:\Mostawsef\Api',
    [string] $PostgresService = 'postgresql-x64-17',
    [switch] $Uninstall
)

$ErrorActionPreference = 'Stop'
$serviceName = 'MostawsefApi'
$displayName = 'Mostawsef API'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host 'Re-launching elevated...' -ForegroundColor Cyan
    $argList = @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass',
        '-File', "`"$PSCommandPath`"",
        '-ApiPath', "`"$ApiPath`"",
        '-PostgresService', "`"$PostgresService`""
    )
    if ($Uninstall) { $argList += '-Uninstall' }
    $p = Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $argList -Wait -PassThru
    exit $p.ExitCode
}

function Get-ExistingService {
    Get-Service -Name $serviceName -ErrorAction SilentlyContinue
}

if ($Uninstall) {
    $existing = Get-ExistingService
    if (-not $existing) {
        Write-Host "Service '$serviceName' is not installed." -ForegroundColor Yellow
        exit 0
    }
    if ($existing.Status -ne 'Stopped') {
        Stop-Service -Name $serviceName -Force
    }
    sc.exe delete $serviceName | Out-Host
    Write-Host "Removed service '$serviceName'." -ForegroundColor Green
    exit 0
}

$exe = Join-Path $ApiPath 'Mostawsef.Api.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Mostawsef.Api.exe not found under: $ApiPath"
}

$pg = Get-Service -Name $PostgresService -ErrorAction SilentlyContinue
if (-not $pg) {
    throw "PostgreSQL service '$PostgresService' not found. Install native PostgreSQL first (Phase 2)."
}

# Free the HTTP port if a console copy of the API is still running.
Get-Process -Name 'Mostawsef.Api' -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Stopping console API PID $($_.Id)..." -ForegroundColor Cyan
    Stop-Process -Id $_.Id -Force
}

$existing = Get-ExistingService
if ($existing) {
    if ($existing.Status -ne 'Stopped') {
        Stop-Service -Name $serviceName -Force
    }
    sc.exe delete $serviceName | Out-Host
    Start-Sleep -Seconds 2
}

# Quote the binPath the way sc.exe expects (path may contain spaces / brackets).
$binPath = "`"$exe`""
sc.exe create $serviceName binPath= $binPath start= auto DisplayName= $displayName obj= 'LocalSystem' | Out-Host
if ($LASTEXITCODE -ne 0) { throw "sc create failed ($LASTEXITCODE)" }

sc.exe config $serviceName depend= $PostgresService | Out-Host
if ($LASTEXITCODE -ne 0) { throw "sc config depend failed ($LASTEXITCODE)" }

sc.exe description $serviceName 'Mostawsef clinic API (localhost:5270). Starts after PostgreSQL.' | Out-Host

Start-Service -Name $serviceName
$svc = Get-Service -Name $serviceName
Write-Host "Service '$serviceName' is $($svc.Status); StartType=$($svc.StartType); DependsOn=$PostgresService" -ForegroundColor Green
Write-Host "Exe: $exe"
