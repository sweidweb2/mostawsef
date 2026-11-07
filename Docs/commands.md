# Commands

Run from the repository root.

## Tests

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
./scripts/test.ps1 desktop NavigationGuard
```
```powershell
./scripts/test.ps1 api Wrong_password
```
```powershell
./scripts/test.ps1 desktop -Watch
```
```powershell
./scripts/test.ps1 all -Coverage
```
```powershell
./scripts/test.ps1 api -Detailed
```

Pattern: `./scripts/test.ps1 <all|api|desktop> <part of test name> [-Watch] [-Coverage] [-Detailed]`
API tests need Docker running.

## Run the app

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

## Publish the desktop app

```powershell
dotnet publish frontend/Mostawsef.Desktop -r win-x64
```

Output: `frontend/Mostawsef.Desktop/bin/Release/net10.0/win-x64/publish/`. Publishing for a runtime turns on
ReadyToRun (precompiled code), so the app starts faster on older clinic PCs.
