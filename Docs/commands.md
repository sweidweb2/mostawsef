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

Dev: Enter clinic on the login screen, or Super `super@example.com` / `password`

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

About 30% of the orders are unpaid, 25% partly paid and 45% paid; every order is charged its "our price" (lab costs plus a markup) and some partly
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

## Scheduled database dumps (clinic)

Super → Settings → **Backup folder** (Browse). Default on first run: a `Backups` folder next to the API install
(`AppContext.BaseDirectory\Backups`). While the API Windows service is running and that folder is set, the API checks
every hour and, when the newest dump is `ScheduledBackup:IntervalDays` old (default 1) or there is none, writes
`mostawsef-yyyyMMdd-HHmmss.dump` there (custom `pg_dump` format), keeps the newest `ScheduledBackup:KeepCount`
files **and** history rows in `scheduled_dumps` (default 7; Super → **Backups** → Auto backups), and logs `backups.scheduled`.
Clear the folder in Settings to skip scheduled dumps. Needs `pg_dump` on the API PC (`ScheduledBackup:PgDumpPath` if
it is not on PATH). If the API lives under Program Files and writes fail, pick a writable folder (another drive / USB)
instead.

Restore on a spare machine or disposable database (stop the API first):

```powershell
# Drop and recreate the public schema, then:
pg_restore --clean --if-exists --no-owner --dbname=mostawsef "D:\Mostawsef\Backups\mostawsef-20260101-120000.dump"
```

Use the clinic connection settings (host, user, `PGPASSWORD`). Start the API only after the restore finishes: it keeps
the settings rows and users' sign-in status in memory, so an API left running would serve the pre-restore values (and
a restored user's old access) until it restarts. Confirm the app starts and a known patient/order is visible before
trusting the restore on a real clinic PC.

The same applies to any change made to the database outside the API (a manual SQL fix): restart the API afterwards.

## Database migrations

```powershell
dotnet ef migrations add <Name> --project backend/Mostawsef.Infrastructure --startup-project backend/Mostawsef.Api --output-dir Persistence/Migrations
```

While the API is running, add `--configuration Release` (the Debug build is locked). Check the generated file before
committing: it must contain only your change, and no raw SQL (SQLite fallback).

## Publish the desktop app

```powershell
dotnet publish frontend/Mostawsef.Desktop -c Release -o "C:\Users\User\Desktop\mostawsef-3ammo\[2] Builds\Desktop"
```

Default without `-o`: `frontend/Mostawsef.Desktop/bin/Release/net10.0/win-x64/publish/`. A publish targets the clinic PCs (`win-x64`)
unless `-r` says otherwise, and turns on ReadyToRun (precompiled code), so the app starts faster on older clinic PCs.

## Publish the API

```powershell
dotnet publish backend/Mostawsef.Api -c Release -o "C:\Users\User\Desktop\mostawsef-3ammo\[2] Builds\Api"
```

Same rules as the desktop: `win-x64` unless `-r` says otherwise, with ReadyToRun so the first requests after a start
are not slowed down by JIT compilation. At start the API also runs the everyday queries once (`DatabaseWarmUp`).
`appsettings.Development.json` is not included in the publish output (clinic builds must not ship Docker / dev seed users).
Re-publishing overwrites `appsettings.json` — keep a copy of the clinic connection string (and never delete `appsettings.Clinic.json`).

## Install the API as a Windows service

Needs Administrator. Points at the published API folder and starts **after** native PostgreSQL:

```powershell
./scripts/install-api-service.ps1
# optional: -ApiPath "D:\Mostawsef\Api" -PostgresService postgresql-x64-17
# remove:  ./scripts/install-api-service.ps1 -Uninstall
```

Service name: `MostawsefApi` (Display name: Mostawsef API). Default path: `C:\Mostawsef\Api`. Listens on `http://localhost:5270`.
Stop the service before publishing over the same folder, then start it again (or re-run the install script).

## Build the clinic Setup.exe

Needs Inno Setup (`ISCC.exe`; the script can install it via winget). Publishes Desktop + API into `artifacts/installer-staging`, then compiles:

```powershell
./scripts/build-setup.ps1              # full Setup (bundled Postgres) — first install / USB
./scripts/build-setup.ps1 -UpdateOnly  # smaller update Setup (no Postgres) — GitHub Releases / in-app update
./scripts/build-setup.ps1 -Version 1.2.0 -UpdateOnly
# reuse staging: -SkipPublish
# replace one version only: -Version 1.0.1 -Force
```

Publishes Desktop + API with `-p:Version=` matching the Setup file (login screen shows that semver; source default is `1.0.0` in `Directory.Build.props`).

In-app **Update now** (portal banner): downloads Setup, checks sha256, then a helper runs  
`Mostawsef-Setup-{version}.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS` (UAC),  
exits the old Desktop, and relaunches it when Setup finishes. Mid-update reopen shows a wait window.  
On failure see `C:\Mostawsef\install-clinic.log`.  
After Setup, the API service restarts and applies DB migrations automatically; a short connection-lost banner is normal.  
Data safety notes (backups before major updates): `Docs/7-automating install/03-data-safety.md`.

### Publish a GitHub Release (CI)

Full ship checklist (backup note, USB, rollback): [`Docs/7-automating install/01-release-playbook.md`](7-automating%20install/01-release-playbook.md).

```powershell
# Merge when you want to ship — no manual version edit.
git checkout main
git merge dev
git push origin main
```

GitHub Actions (`.github/workflows/release.yml`) on **push to `main`**: API + Desktop tests → auto **patch** bump (latest `v*.*.*` + 1, writes `Directory.Build.props` with `[skip ci]`) → builds an **update** Setup (`-UpdateOnly`) → creates tag `vX.Y.Z` → uploads `Mostawsef-Setup-X.Y.Z.exe` and `latest.json`.  
Minor/major: Actions → **Release** → *Run workflow* → set the version input.  
Feed URL: `https://github.com/sweidweb2/mostawsef/releases/latest/download/latest.json`

Output: `C:\Users\User\Desktop\mostawsef-3ammo\[2] Builds\Mostawsef-Setup-{version}.exe` (older builds are kept; also under `artifacts\installer\`).

Clinic: run the **versioned** Setup as Administrator (e.g. `Mostawsef-Setup-1.0.1.exe`). It installs to `C:\Mostawsef`, installs PostgreSQL from the **bundled offline** installer if missing, registers `MostawsefApi`, and creates Start Menu / Desktop shortcuts that use the Mostawsef app icon (`Assets/app.ico`, also the Setup.exe icon). Apps are self-contained — no separate .NET install. Prefer this over the manual playbook; keep the playbook as fallback.

PostgreSQL installer is downloaded **once** into `artifacts\cache\postgresql\` and reused on every later build (no re-download when staging is wiped). To seed offline: put `postgresql-*-windows-x64.exe` in that folder.

Normal releases: rebuild Setup (auto-bumps) and send the new file (clinic reinstalls over the old version). If you change install behavior itself (paths, service, Postgres rules), edit `packaging/innosetup/` / `scripts/install-*.ps1` first — see the developer note in `Docs/3-Follow-ups/clinic-install-playbook.md`.