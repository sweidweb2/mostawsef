# Commands

Run from the repository root.

If a terminal says `dotnet` is not recognized, put the SDK on that terminal's PATH (or restart Cursor after installing the SDK):

```powershell
$env:Path = "C:\Program Files\dotnet;" + $env:Path
```

## Tests

While iterating, prefer one project plus a name filter (and `-Watch`). Use `all` as a full gate before push.
Close the running API/desktop apps first so the script can use Debug builds (faster than the automatic Release fallback).

```powershell
./scripts/test.ps1
```
```powershell
./scripts/test.ps1 api
```
```powershell
./scripts/test.ps1 desktop
```
```powershell
./scripts/test.ps1 api Login
```
```powershell
./scripts/test.ps1 desktop Orders
```
```powershell
./scripts/test.ps1 desktop NavigationGuard
```
```powershell
./scripts/test.ps1 api Wrong_password
```
```powershell
./scripts/test.ps1 desktop -Watch
```
```powershell
./scripts/test.ps1 desktop Orders -NoBuild
```
```powershell
./scripts/test.ps1 all -Coverage
```
```powershell
./scripts/test.ps1 api -Detailed
```

Pattern: `./scripts/test.ps1 <all|api|desktop> <part of test name> [-Watch] [-Coverage] [-Detailed] [-NoBuild]`
API tests need Docker running. While the API or desktop app is running (Debug build, files locked), the script builds
the tests in Release automatically. `-NoBuild` skips restore/build when binaries are already fresh (`-Watch` always rebuilds).

Every push to GitHub runs the same script in GitHub Actions (`.github/workflows/tests.yml`): `api` on Ubuntu (Linux
containers for Testcontainers), `desktop` on Windows. Results are in the repository's **Actions** tab.

## Run the app

Everything in one window (database, API, then desktop; closing the desktop app stops the API, run it again after code
changes):

```powershell
./scripts/dev.ps1
```

Or each part on its own:

```powershell
docker compose up -d
```
```powershell
dotnet run --project backend/Mostawsef.Api --launch-profile http
```
```powershell
dotnet run --project frontend/Mostawsef.Desktop
```

Dev logins: `admin@example.com` / `password`, `super@example.com` / `password`

Sample data (while the API runs): 36 lab tests, 45 patients (Arabic and English names) and 80 orders with cash, card,
transfer and insurance first payments. Tests and patients that already exist are skipped; each run adds `-Orders` new
orders (default 80):

```powershell
./scripts/seed.ps1
```

The API applies new migrations when it starts: restart it after pulling changes.

## Test data suites (wipe and seed)

Three ready-made data sets for testing the app at different sizes. Each one first wipes the patients, orders, payments
and logs (the sign-in accounts, the lab tests and the settings always stay), then creates its data through the API and
moves the dates back so the lists and the dashboard graphs have history. The mix is repeatable.

| Suite    | Patients | Orders | History  | Use it to check                                                      |
| -------- | -------- | ------ | -------- | -------------------------------------------------------------------- |
| `low`    | 8        | 20     | 14 days  | an almost empty clinic: empty states, a single page, tiny graphs     |
| `medium` | 80       | 500    | 60 days  | a normal busy clinic: paging, search, filters, dashboard             |
| `high`   | 1,500    | 10,500 | 365 days | volume: the "10,000+" count cap, many pages, speed, heavy graphs     |

About 30% of the orders are unpaid, 25% partly paid and 45% paid; some have an extra "our price" amount and some partly
paid ones got a second payment, so the yellow rows, the status filter and the payment form all have something to show.

```powershell
./scripts/suite.ps1 low
```
```powershell
./scripts/suite.ps1 medium
```
```powershell
./scripts/suite.ps1 high
```

Seed and open the app on that data in one go (database, API, seed, then the desktop app):

```powershell
./scripts/dev.ps1 -Suite medium
```
```powershell
./scripts/suite.ps1 high -Run
```

`low` and `medium` take seconds, `high` a few minutes (the progress is printed). If the API is not running the script
starts it for the seeding and stops it afterwards. `-NoWipe` adds a suite on top of the current data instead.

Only the wipe, without seeding anything (it asks first; `-Force` skips the question):

```powershell
./scripts/wipe.ps1
```

Before deleting, the wipe saves a copy of the whole database in `backups/` (the three newest are kept; `-NoBackup`
skips it). Restore with
`Get-Content backups\<file>.sql | docker exec -i mostawsef-postgres psql -U mostawsef -d mostawsef` after dropping and
recreating the `public` schema.

`./scripts/seed.ps1` (above) is the older, gentler seeder: it adds to the current data and keeps its dates at "now".

## Checking speed

Judge speed on Release builds only: Debug builds of Avalonia and the API are much slower and give a wrong picture.

```powershell
dotnet run --project backend/Mostawsef.Api --launch-profile http --configuration Release
```
```powershell
$env:MOSTAWSEF_PERF = "1"; dotnet run --project frontend/Mostawsef.Desktop --configuration Release
```

`MOSTAWSEF_PERF=1` turns on the performance overlay and log (API calls show their server time from `Server-Timing`).
The API logs every request in Development and only slow ones (1 s or more) in production.

## Database migrations

```powershell
dotnet ef migrations add <Name> --project backend/Mostawsef.Infrastructure --startup-project backend/Mostawsef.Api --output-dir Persistence/Migrations
```

While the API is running, add `--configuration Release` (the Debug build is locked). Check the generated file before
committing: it must contain only your change, and no raw SQL (SQLite fallback).

## Publish the desktop app

```powershell
dotnet publish frontend/Mostawsef.Desktop
```

Output: `frontend/Mostawsef.Desktop/bin/Release/net10.0/win-x64/publish/`. A publish targets the clinic PCs (`win-x64`)
unless `-r` says otherwise, and turns on ReadyToRun (precompiled code), so the app starts faster on older clinic PCs.

## Publish the API

```powershell
dotnet publish backend/Mostawsef.Api
```

Same rules as the desktop: `win-x64` unless `-r` says otherwise, with ReadyToRun so the first requests after a start
are not slowed down by JIT compilation. At start the API also runs the everyday queries once (`DatabaseWarmUp`).
