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
