<#
.SYNOPSIS
    Resets the development data and fills it with one of three test suites: low, medium or high.

.DESCRIPTION
    ./scripts/suite.ps1 <low|medium|high> [-NoWipe] [-Run] [-BaseUrl http://localhost:5270]

    What each suite holds (the lab tests and the sign-in accounts are always kept):

      low     8 patients,    20 orders, spread over the last 14 days     - a quick look, an almost empty clinic
      medium  80 patients,  500 orders, spread over the last 60 days     - a normal busy clinic, a few pages of everything
      high    1,500 patients, 10,500 orders, spread over the last 365 days - more than the 10,000 count cap ("10,000+"),
                                                                            many pages, heavy dashboard graphs

    Steps: wipe the data (a backup goes to backups/ first, see wipe.ps1; -NoWipe adds on top instead), create the
    patients, orders and payments through the running API (so validation, order numbers, search text and activity logs
    behave as in real use), then move their dates back in time so the dashboard graphs and the lists have history.
    The mix is repeatable (fixed seed): about 30% unpaid orders, 25% partly paid, 45% paid, 10% with an extra "our price"
    amount, and some of the partly paid orders get a second payment. A few tests are ordered much more than the rest, so
    the top-tests graph has a shape.

    If the API is not running it is started for the seeding and stopped afterwards. -Run then opens the app
    (./scripts/dev.ps1) on that data. The high suite takes several minutes: the progress is printed.
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet("low", "medium", "high")]
    [string]$Suite,
    [switch]$NoWipe,
    [switch]$Run,
    [string]$BaseUrl = "http://localhost:5270"
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$container = "mostawsef-postgres"

$suites = @{
    low    = @{ Patients = 8;    Orders = 20;    Days = 14 }
    medium = @{ Patients = 80;   Orders = 500;   Days = 60 }
    high   = @{ Patients = 1500; Orders = 10500; Days = 365 }
}
$config = $suites[$Suite]
$random = [System.Random]::new(2026)
$script:headers = @{}

function Invoke-Sql([string]$Sql, [string[]]$Variables = @()) {
    $arguments = @("exec", "-i", $container, "psql", "-U", "mostawsef", "-d", "mostawsef", "-v", "ON_ERROR_STOP=1", "-At", "-1")
    foreach ($variable in $Variables) { $arguments += @("-v", $variable) }
    $result = $Sql | & docker @arguments -f -
    if ($LASTEXITCODE -ne 0) { throw "SQL failed." }
    return $result
}

function Invoke-Api([string]$Method, [string]$Path, $Body = $null) {
    $request = @{ Uri = "$BaseUrl/api/$Path"; Method = $Method; Headers = $script:headers; UseBasicParsing = $true }
    if ($null -ne $Body) {
        $request.ContentType = "application/json; charset=utf-8"
        $request.Body = [System.Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 6 -Compress))
    }
    try {
        $response = Invoke-WebRequest @request
        $bytes = $response.RawContentStream.ToArray()
        if ($bytes.Length -eq 0) { return $null }
        return [System.Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json
    }
    catch [System.Net.WebException] {
        $details = ""
        if ($_.Exception.Response) {
            $details = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream()).ReadToEnd()
        }
        throw "$Method $Path failed: $($_.Exception.Message) $details"
    }
}

function Get-All([string]$Path) {
    $items = @()
    $page = 1
    do {
        $result = Invoke-Api GET "$Path`?page=$page&pageSize=100"
        $items += $result.items
        $page++
    } while ($page -le $result.totalPages)
    return $items
}

function Pick($list) { $list[$random.Next($list.Count)] }

# A pick that favours the start of the list: index = n * u^power.
function Pick-Skewed($list, [double]$Power) { $list[[int][Math]::Floor($list.Count * [Math]::Pow($random.NextDouble(), $Power))] }

function New-Phone { "01$(Pick @('0', '1', '2', '5')) $($random.Next(1000, 10000)) $($random.Next(1000, 10000))" }

function Test-Api {
    try { Invoke-WebRequest "$BaseUrl/api/health" -UseBasicParsing -TimeoutSec 3 | Out-Null; return $true } catch { return $false }
}

function Write-Progress-Line([string]$What, [int]$Done, [int]$Total, $Watch) {
    $seconds = [Math]::Max(1, [int]$Watch.Elapsed.TotalSeconds)
    $left = if ($Done -gt 0) { [int]($seconds * ($Total - $Done) / $Done) } else { 0 }
    Write-Host ("  {0}: {1} / {2}  ({3}s, about {4}s left)" -f $What, $Done, $Total, $seconds, $left)
}

# --- Checks ---------------------------------------------------------------------------------------------------------
docker info *> $null
if ($LASTEXITCODE -ne 0) { Write-Host "Docker is not running. Start Docker Desktop and try again." -ForegroundColor Red; exit 1 }
if (-not (docker ps --filter "name=^$container$" --format "{{.Names}}")) {
    Write-Host "The database container '$container' is not running (docker compose up -d)." -ForegroundColor Red
    exit 1
}

$startedApi = $null
Push-Location $root
try {
    if (-not (Test-Api)) {
        Write-Host "> the API is not running: building and starting it for the seeding" -ForegroundColor Cyan
        # Build in the open, so a failed build (e.g. a locked file) shows its errors here.
        dotnet build backend/Mostawsef.Api --nologo -v q
        if ($LASTEXITCODE -ne 0) { Write-Host "The API build failed (see above). Close the running API/desktop app and try again." -ForegroundColor Red; exit 1 }

        $apiLog = Join-Path ([System.IO.Path]::GetTempPath()) "mostawsef-suite-api.log"
        $apiErr = "$apiLog.err"
        $startedApi = Start-Process dotnet -WindowStyle Hidden -PassThru -RedirectStandardOutput $apiLog -RedirectStandardError $apiErr -ArgumentList @(
            "run", "--project", "backend/Mostawsef.Api", "--launch-profile", "http", "--no-build")
        $deadline = (Get-Date).AddSeconds(120)
        while (-not (Test-Api)) {
            if ($startedApi.HasExited -or (Get-Date) -gt $deadline) {
                $why = if ($startedApi.HasExited) { "The API stopped while starting." } else { "The API did not answer within 2 minutes." }
                Write-Host "$why Its output:" -ForegroundColor Red
                foreach ($file in $apiLog, $apiErr) { if (Test-Path $file) { Get-Content $file -Tail 25 } }
                exit 1
            }
            Start-Sleep -Seconds 1
        }
    } else {
        Write-Host "> using the API that is already running at $BaseUrl (keep it, and the app that started it, open until this finishes)" -ForegroundColor Yellow
    }

    # --- Wipe -------------------------------------------------------------------------------------------------------
    if (-not $NoWipe) {
        Write-Host "> wiping the data (users, lab tests and settings stay)" -ForegroundColor Cyan
        & "$PSScriptRoot/wipe.ps1" -Force
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    $login = Invoke-Api POST "auth/login" @{ username = "admin@example.com"; password = "password" }
    $script:headers = @{ Authorization = "Bearer $($login.accessToken)" }

    $tests = @(Get-All "lab-tests" | Where-Object { $_.isActive })
    if ($tests.Count -eq 0) { Write-Host "There are no lab tests to order. Add lab tests first (./scripts/seed.ps1)." -ForegroundColor Red; exit 1 }

    Write-Host "> seeding the $Suite suite: $($config.Patients) patients, $($config.Orders) orders over $($config.Days) days" -ForegroundColor Cyan
    $watch = [System.Diagnostics.Stopwatch]::StartNew()

    # --- Patients ---------------------------------------------------------------------------------------------------
    $maleNames = @("محمد", "أحمد", "محمود", "مصطفى", "عمر", "يوسف", "خالد", "إبراهيم", "علي", "حسن", "كريم", "طارق", "Ahmed", "Omar", "Youssef", "Karim")
    $femaleNames = @("فاطمة", "مريم", "نور", "سارة", "هدى", "آية", "منى", "ياسمين", "رحمة", "سلمى", "ندى", "شيماء", "Sara", "Nour", "Mariam", "Laila")
    $familyNames = @("عبد الرحمن", "السيد", "حسين", "إبراهيم", "عبد الله", "مصطفى", "الشربيني", "منصور", "سليمان", "فؤاد", "Hassan", "Mostafa", "Ibrahim", "Saleh")
    $allNotes = @("Diabetic", "Hypertensive", "Allergic to penicillin", "مريض سكر", "ضغط مرتفع", "Fasting since 10 PM", "حساسية من البنسلين")

    function New-PatientRequest {
        $gender = Pick @("Male", "Female")
        $first = if ($gender -eq "Male") { Pick $maleNames } else { Pick $femaleNames }
        $age = Pick @(2, 5, 9, 14, 19, 24, 28, 33, 37, 42, 46, 51, 55, 60, 64, 68, 73, 79)
        $birth = (Get-Date).AddYears(-$age).AddDays(-$random.Next(0, 365))
        @{
            fullName      = "$first $(Pick $maleNames) $(Pick $familyNames)"
            phone         = New-Phone
            guardianPhone = if ($age -lt 16) { New-Phone } else { $null }
            notes         = if ($random.Next(4) -eq 0) { Pick $allNotes } else { $null }
            isInsured     = $random.Next(4) -eq 0
            gender        = $gender
            dateOfBirth   = if ($random.Next(10) -eq 0) { $null } else { $birth.ToString("yyyy-MM-dd") }
        }
    }

    $patients = New-Object System.Collections.ArrayList
    $step = [Math]::Max(25, [int]($config.Patients / 10))
    for ($i = 1; $i -le $config.Patients; $i++) {
        $created = Invoke-Api POST "patients" (New-PatientRequest)
        [void]$patients.Add(@{ id = $created.id; insured = [bool]$created.isInsured })
        if ($i % $step -eq 0 -and $i -lt $config.Patients) { Write-Progress-Line "patients" $i $config.Patients $watch }
    }
    Write-Host "  patients: $($patients.Count) created"

    # --- Orders and payments ----------------------------------------------------------------------------------------
    $statusCounts = @{ Pending = 0; PartiallyPaid = 0; Paid = 0 }
    $secondPayments = 0
    $step = [Math]::Max(25, [int]($config.Orders / 20))
    for ($i = 1; $i -le $config.Orders; $i++) {
        $count = Pick @(1, 1, 2, 2, 3, 3, 4, 5, 6)
        $chosen = New-Object System.Collections.ArrayList
        while ($chosen.Count -lt [Math]::Min($count, $tests.Count)) {
            $test = Pick-Skewed $tests 2.2
            if ($chosen -notcontains $test) { [void]$chosen.Add($test) }
        }

        $catalogTotal = 0L
        $items = @(foreach ($test in $chosen) { $catalogTotal += $test.price; @{ labTestId = $test.id } })
        $order = @{ items = $items }
        $total = $catalogTotal
        if ($random.Next(10) -eq 0) {
            $addon = [long](Pick @(500, 1000, 2000, 5000))
            $order.addon = $addon
            $total += $addon
        }

        $patient = Pick-Skewed $patients 1.5
        $order.patientId = $patient.id

        $roll = $random.Next(100)
        if ($total -gt 0 -and $roll -ge 30) {
            $amount = if ($roll -lt 55) { [long]([Math]::Max(100, [Math]::Floor($total * (0.2 + $random.NextDouble() * 0.6) / 100) * 100)) } else { $total }
            $method = if ($patient.insured -and $random.Next(2) -eq 0) { "Insurance" } else { Pick @("Cash", "Cash", "Cash", "Cash", "Card", "Card", "Transfer") }
            $order.firstPayment = @{ amount = [Math]::Min($amount, $total); method = $method; notes = $null }
        }

        $created = Invoke-Api POST "orders" $order
        $status = $created.status
        if ($status -eq "PartiallyPaid" -and $random.Next(100) -lt 40) {
            $remaining = [long]$created.balance
            $second = if ($random.Next(100) -lt 70 -or $remaining -lt 200) { $remaining } else { [long]([Math]::Floor($remaining / 2 / 100) * 100) }
            $paid = Invoke-Api POST "orders/$($created.id)/payments" @{ amount = $second; method = (Pick @("Cash", "Card", "Transfer")); notes = $null }
            $status = $paid.status
            $secondPayments++
        }

        $statusCounts[$status] = 1 + [int]$statusCounts[$status]
        if ($i % $step -eq 0 -and $i -lt $config.Orders) { Write-Progress-Line "orders" $i $config.Orders $watch }
    }
    $mix = ($statusCounts.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Value) $($_.Name)" }) -join ", "
    Write-Host "  orders: $($config.Orders) created ($mix; $secondPayments got a second payment)"

    # --- Dates ------------------------------------------------------------------------------------------------------
    # The API stamps everything "now": spread the orders (oldest number first) over the days, keeping their order, and
    # move payments, tests and patients to match so the graphs and lists have history.
    Write-Host "> moving the dates back over $($config.Days) days" -ForegroundColor Cyan
    Invoke-Sql @'
create temp table _o as
  select "Id", row_number() over (order by "Number") as rn, count(*) over () as n from orders;
update orders o set "CreatedAt" = least(
    date_trunc('day', now()) - (:days - 1) * interval '1 day'
      + floor((_o.rn - 1)::float8 / _o.n * :days) * interval '1 day'
      + interval '8 hours'
      + ((_o.rn - 1)::float8 / _o.n * :days - floor((_o.rn - 1)::float8 / _o.n * :days)) * interval '12 hours',
    now() - (_o.n - _o.rn) * interval '1 second')
  from _o where o."Id" = _o."Id";
update order_items i set "CreatedAt" = o."CreatedAt" from orders o where i."OrderId" = o."Id";
update payments p set "PaidAt" = least(
    o."CreatedAt" + case when q.k = 1 then interval '1 minute' else (1 + random() * 9) * interval '1 day' end,
    now()), "CreatedAt" = o."CreatedAt"
  from orders o, (select "Id", row_number() over (partition by "OrderId" order by "CreatedAt") as k from payments) q
  where p."OrderId" = o."Id" and q."Id" = p."Id";
update payments set "CreatedAt" = "PaidAt";
update patients p set "CreatedAt" = coalesce(
    (select min(o."CreatedAt") from orders o where o."PatientId" = p."Id") - interval '10 minutes',
    now() - random() * :days * interval '1 day');
'@ @("days=$($config.Days)") | Out-Null

    $summary = Invoke-Sql @'
select 'patients ' || (select count(*) from patients) || ', orders ' || (select count(*) from orders)
  || ', order lines ' || (select count(*) from order_items) || ', payments ' || (select count(*) from payments)
  || ', from ' || (select to_char(min("CreatedAt"), 'YYYY-MM-DD') from orders) || ' to ' || (select to_char(max("CreatedAt"), 'YYYY-MM-DD') from orders)
'@
    Write-Host "Done in $([int]$watch.Elapsed.TotalSeconds)s: $summary" -ForegroundColor Green
    Write-Host "Sign in as admin@example.com / password." -ForegroundColor Green
}
finally {
    if ($startedApi) {
        if (-not $startedApi.HasExited) { Stop-Process -Id $startedApi.Id -Force -ErrorAction SilentlyContinue }
        Get-Process Mostawsef.Api -ErrorAction SilentlyContinue | Stop-Process -Force
        Write-Host "(the API started for the seeding was stopped)" -ForegroundColor DarkGray
    }
    Pop-Location
}

if ($Run) {
    & "$PSScriptRoot/dev.ps1"
}
