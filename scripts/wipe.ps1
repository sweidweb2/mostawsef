<#
.SYNOPSIS
    Deletes the development data (patients, orders, payments, logs) and keeps the sign-in accounts, the lab tests and the settings.

.DESCRIPTION
    ./scripts/wipe.ps1 [-Force] [-NoBackup]

    Works on the Docker database of docker-compose.yml (container mostawsef-postgres). Kept: users, lab tests, clinic
    and system settings. Deleted: patients, orders and their tests and payments, activity logs and system errors; the
    order numbers start again at ORD-000001.
    Before deleting it saves a copy of the whole database to backups/ (the three newest copies are kept), unless
    -NoBackup. Without -Force it asks for a "yes" first.
    Restore a copy with:  Get-Content backups\<file>.sql | docker exec -i mostawsef-postgres psql -U mostawsef -d mostawsef
    (drop and recreate the public schema first if the tables already exist).
#>
param(
    [switch]$Force,
    [switch]$NoBackup
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$container = "mostawsef-postgres"

function Invoke-Sql([string]$Sql) {
    # Through stdin: PowerShell would strip the double quotes from a -c argument.
    $result = $Sql | docker exec -i $container psql -U mostawsef -d mostawsef -v ON_ERROR_STOP=1 -At -1 -f -
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $Sql" }
    return $result
}

docker info *> $null
if ($LASTEXITCODE -ne 0) { Write-Host "Docker is not running. Start Docker Desktop and try again." -ForegroundColor Red; exit 1 }
$running = docker ps --filter "name=^$container$" --format "{{.Names}}"
if (-not $running) { Write-Host "The database container '$container' is not running (docker compose up -d)." -ForegroundColor Red; exit 1 }

function Show-Counts {
    $counts = Invoke-Sql @'
select 'users ' || (select count(*) from users) || ', lab tests ' || (select count(*) from lab_tests)
 || ', patients ' || (select count(*) from patients) || ', orders ' || (select count(*) from orders)
 || ', payments ' || (select count(*) from payments) || ', activity logs ' || (select count(*) from activity_logs)
 || ', system errors ' || (select count(*) from system_errors)
'@
    Write-Host "  $counts"
}

Write-Host "Database now:" -ForegroundColor Cyan
Show-Counts

if (-not $Force) {
    $answer = Read-Host "Delete all patients, orders, payments and logs (users, lab tests and settings stay)? Type yes"
    if ($answer -ne "yes") { Write-Host "Nothing was deleted."; exit 0 }
}

if (-not $NoBackup) {
    $folder = Join-Path $root "backups"
    New-Item -ItemType Directory -Force $folder | Out-Null
    $name = "mostawsef-before-wipe-$(Get-Date -Format 'yyyyMMdd-HHmmss').sql"
    docker exec $container pg_dump -U mostawsef -d mostawsef --no-owner -f "/tmp/$name"
    if ($LASTEXITCODE -ne 0) { throw "The backup failed; nothing was deleted." }
    docker cp "${container}:/tmp/$name" (Join-Path $folder $name) | Out-Null
    docker exec $container rm -f "/tmp/$name"
    Get-ChildItem $folder -Filter "mostawsef-before-wipe-*.sql" | Sort-Object LastWriteTime -Descending | Select-Object -Skip 3 | Remove-Item -Force
    Write-Host "Backup saved: backups\$name" -ForegroundColor Cyan
}

Invoke-Sql 'truncate table order_items, payments, orders, patients, activity_logs, system_errors; update number_sequences set "NextValue" = 1;' | Out-Null
Write-Host "Wiped. Database now:" -ForegroundColor Green
Show-Counts
