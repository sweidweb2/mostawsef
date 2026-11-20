<#
.SYNOPSIS
    Fills the development database with sample lab tests, patients and orders through the running API.

.DESCRIPTION
    ./scripts/seed.ps1 [-Orders 80] [-BaseUrl http://localhost:5270]

    Signs in as the dev admin and uses the same endpoints as the desktop app, so validation, search names, order
    numbers and activity logs behave as in real use. Lab tests (same code) and patients (same name and phone) that
    already exist are skipped; every run adds -Orders new orders (all dated now: the API sets the creation time).
    Start the API first (./scripts/dev.ps1). The data is random but repeatable (fixed seed).
#>
param(
    [int]$Orders = 80,
    [string]$BaseUrl = "http://localhost:5270"
)
$ErrorActionPreference = "Stop"
$random = [System.Random]::new(42)
$script:headers = @{}

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

function Egp([double]$amount) { [long][Math]::Round($amount * 100) }

function New-Phone {
    "01$(Pick @('0', '1', '2', '5')) $($random.Next(1000, 10000)) $($random.Next(1000, 10000))"
}

try {
    Invoke-WebRequest "$BaseUrl/api/health" -UseBasicParsing -TimeoutSec 5 | Out-Null
}
catch {
    Write-Host "The API does not answer at $BaseUrl. Start it first (./scripts/dev.ps1)." -ForegroundColor Red
    exit 1
}

$login = Invoke-Api POST "auth/login" @{ username = "admin@example.com"; password = "password" }
$script:headers = @{ Authorization = "Bearer $($login.accessToken)" }

# --- Lab tests -----------------------------------------------------------------------------------------------------
$catalog = @(
    @("CBC", "Complete Blood Count (CBC)", 90),
    @("ESR", "Erythrocyte Sedimentation Rate (ESR)", 30),
    @("HB", "Hemoglobin", 25),
    @("FBS", "Fasting Blood Sugar", 20),
    @("RBS", "Random Blood Sugar", 20),
    @("PPBS", "2h Post-Prandial Blood Sugar", 22),
    @("HBA1C", "Glycated Hemoglobin (HbA1c)", 150),
    @("LIPID", "Lipid Profile", 200),
    @("CHOL", "Total Cholesterol", 35),
    @("TG", "Triglycerides", 35),
    @("ALT", "ALT (SGPT)", 30),
    @("AST", "AST (SGOT)", 30),
    @("LFT", "Liver Function Tests", 240),
    @("KFT", "Kidney Function Tests", 180),
    @("CREAT", "Serum Creatinine", 30),
    @("UREA", "Blood Urea", 30),
    @("UA", "Uric Acid", 30),
    @("CA", "Serum Calcium", 40),
    @("TSH", "Thyroid Stimulating Hormone (TSH)", 120),
    @("FT3", "Free T3", 120),
    @("FT4", "Free T4", 120),
    @("VITD", "Vitamin D (25-OH)", 400),
    @("B12", "Vitamin B12", 300),
    @("FERR", "Ferritin", 220),
    @("IRON", "Serum Iron", 70),
    @("CRP", "C-Reactive Protein (CRP)", 70),
    @("ASO", "Anti-Streptolysin O (ASO)", 70),
    @("RF", "Rheumatoid Factor (RF)", 70),
    @("PT", "Prothrombin Time and INR", 90),
    @("HCV", "HCV Antibodies", 150),
    @("HBSAG", "Hepatitis B Surface Antigen (HBsAg)", 120),
    @("HIV", "HIV 1 and 2 Antibodies", 180),
    @("BHCG", "Pregnancy Test (Beta hCG)", 100),
    @("PSA", "Prostate Specific Antigen (PSA)", 180),
    @("URINE", "Complete Urine Analysis", 25),
    @("STOOL", "Complete Stool Analysis", 25)
)

$existingCodes = @{}
foreach ($test in Get-All "lab-tests") { $existingCodes[$test.code] = $true }
$addedTests = 0
foreach ($row in $catalog) {
    if ($existingCodes.ContainsKey($row[0])) { continue }
    Invoke-Api POST "lab-tests" @{ code = $row[0]; name = $row[1]; labCost = (Egp $row[2]) } | Out-Null
    $addedTests++
}
Write-Host "Lab tests: $addedTests added, $($catalog.Count - $addedTests) already there"
$tests = @(Get-All "lab-tests" | Where-Object { $_.isActive })

# --- Patients ------------------------------------------------------------------------------------------------------
$maleNames = @("محمد", "أحمد", "محمود", "مصطفى", "عمر", "يوسف", "خالد", "إبراهيم", "علي", "حسن", "كريم", "طارق", "Ahmed", "Omar", "Youssef", "Karim")
$femaleNames = @("فاطمة", "مريم", "نور", "سارة", "هدى", "آية", "منى", "ياسمين", "رحمة", "سلمى", "ندى", "شيماء", "Sara", "Nour", "Mariam", "Laila")
$familyNames = @("عبد الرحمن", "السيد", "حسين", "إبراهيم", "عبد الله", "مصطفى", "الشربيني", "منصور", "سليمان", "فؤاد", "Hassan", "Mostafa", "Ibrahim", "Saleh")
$allNotes = @("Diabetic", "Hypertensive", "Allergic to penicillin", "مريض سكر", "ضغط مرتفع", "Fasting since 10 PM", "حساسية من البنسلين")

function New-PatientRequest {
    $gender = Pick @("Male", "Female")
    $first = if ($gender -eq "Male") { Pick $maleNames } else { Pick $femaleNames }
    $age = Pick @(2, 5, 9, 14, 19, 24, 28, 33, 37, 42, 46, 51, 55, 60, 64, 68, 73, 79)
    $birth = (Get-Date).AddYears(-$age).AddDays(-$random.Next(0, 365))
    $phone = New-Phone
    @{
        fullName      = "$first $(Pick $maleNames) $(Pick $familyNames)"
        phone         = $phone
        guardianPhone = if ($age -lt 16) { New-Phone } else { $null }
        notes         = if ($random.Next(4) -eq 0) { Pick $allNotes } else { $null }
        isInsured     = $random.Next(4) -eq 0
        gender        = $gender
        dateOfBirth   = if ($random.Next(10) -eq 0) { $null } else { $birth.ToString("yyyy-MM-dd") }
    }
}

$existingPatients = @{}
foreach ($patient in Get-All "patients") { $existingPatients["$($patient.fullName)|$($patient.phone)"] = $true }
$addedPatients = 0
for ($i = 0; $i -lt 45; $i++) {
    $request = New-PatientRequest
    if ($existingPatients.ContainsKey("$($request.fullName)|$($request.phone)")) { continue }
    Invoke-Api POST "patients" $request | Out-Null
    $existingPatients["$($request.fullName)|$($request.phone)"] = $true
    $addedPatients++
}
Write-Host "Patients: $addedPatients added"
$patients = @(Get-All "patients")

# --- Orders --------------------------------------------------------------------------------------------------------
$statusCounts = @{}
for ($i = 0; $i -lt $Orders; $i++) {
    $count = Pick @(1, 1, 2, 2, 3, 3, 4, 5, 6)
    $chosen = @($tests | Sort-Object { $random.Next() } | Select-Object -First $count)
    $labCostTotal = 0L
    $items = @(foreach ($test in $chosen) {
            $labCostTotal += $test.labCost
            @{ labTestId = $test.id }
        })

    $order = @{ items = $items }
    # Our price: what the clinic charges, the lab costs plus a markup (an order has no total without it).
    $total = 0L
    if ($labCostTotal -gt 0) {
        $total = [long]([Math]::Max(500, [Math]::Floor($labCostTotal * (1.3 + $random.NextDouble() * 0.7) / 500) * 500))
        $order.addon = $total
    }
    $insured = $false
    if ($random.Next(10) -eq 0) {
        $order.newPatient = New-PatientRequest
        $insured = $order.newPatient.isInsured
    }
    else {
        $patient = Pick $patients
        $order.patientId = $patient.id
        $insured = $patient.isInsured
    }

    $roll = $random.Next(100)
    if ($total -gt 0 -and $roll -ge 30) {
        $amount = if ($roll -lt 55) { [long]([Math]::Max(100, [Math]::Floor($total * (0.2 + $random.NextDouble() * 0.6) / 100) * 100)) } else { $total }
        $method = if ($insured -and $random.Next(2) -eq 0) { "Insurance" } else { Pick @("Cash", "Cash", "Cash", "Cash", "Card", "Card", "Transfer") }
        $order.firstPayment = @{ amount = [Math]::Min($amount, $total); method = $method; notes = $null }
    }

    $created = Invoke-Api POST "orders" $order
    $statusCounts[$created.status] = 1 + [int]$statusCounts[$created.status]
}
$summary = ($statusCounts.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Value) $($_.Name)" }) -join ", "
Write-Host "Orders: $Orders added ($summary)"
Write-Host "Done. Sign in as admin@example.com / password to see the data." -ForegroundColor Green
