# Clinic PC — paths & install facts

One-PC install. Default root: `C:\Mostawsef`.

---

## App install

| What | Path |
| --- | --- |
| Install root | `C:\Mostawsef\` |
| API | `C:\Mostawsef\Api\` |
| Desktop | `C:\Mostawsef\Desktop\Mostawsef.Desktop.exe` |
| Install scripts | `C:\Mostawsef\scripts\` |
| Bundled Postgres installer | `C:\Mostawsef\redist\` |
| Clinic install log | `C:\Mostawsef\install-clinic.log` |

---

## Database (PostgreSQL 17)

| What | Path / value |
| --- | --- |
| Data directory (all clinic data) | `C:\Program Files\PostgreSQL\17\data\` |
| Tools (`psql`, `pg_dump`) | `C:\Program Files\PostgreSQL\17\bin\` |
| Config (`pg_hba.conf`, `postgresql.conf`) | `C:\Program Files\PostgreSQL\17\data\` |
| Windows service | `postgresql-x64-17` |
| Database name | `mostawsef` |
| App DB user | `mostawsef` (password inside connection string) |
| Superuser | `postgres` |

**Never delete the data directory** when updating or uninstalling the app.

---

## Passwords

### Postgres superuser (`postgres`)

| Situation | What to do |
| --- | --- |
| Postgres **already** on the PC | Setup field = **existing** correct password |
| **Fresh** Postgres via Setup (field blank) | Random password → `C:\Mostawsef\postgres-superuser.password.txt` |
| Typed in Setup (existing Postgres) | Briefly saved as `C:\Mostawsef\scripts\postgres-super.password` (removed after successful install) |

### App access

| Path | How |
| --- | --- |
| Admin (clinic) | **Enter clinic** on the login screen (no password) |
| Super | `ali@etkan` / `ali@1234` (seeded once) — change after first sign-in |

---

## Config files (keep across updates)

| What | Path |
| --- | --- |
| Connection string | `C:\Mostawsef\Api\appsettings.json` → `ConnectionStrings:Default` |
| Clinic secrets (JWT, etc.) | `C:\Mostawsef\Api\appsettings.Clinic.json` |
| Desktop settings (API URL, theme, language) | `%AppData%\Mostawsef\settings.json` → usually `C:\Users\<user>\AppData\Roaming\Mostawsef\settings.json` |

Setup **preserves** `appsettings.Clinic.json` and restores the connection string on upgrade.

---

## Services & network

| What | Value |
| --- | --- |
| API Windows service | `MostawsefApi` (Automatic; depends on `postgresql-x64-17`) |
| API URL | `http://localhost:5270` |
| Health check | `http://localhost:5270/api/health` |

---

## Backups

| What | Where |
| --- | --- |
| Scheduled `pg_dump` folder | Set in app: **Super → Settings → backup folder** (clinic chooses; prefer not only inside `C:\Mostawsef`) |
| Dump tool | `C:\Program Files\PostgreSQL\17\bin\pg_dump.exe` |

Live DB files stay under `C:\Program Files\PostgreSQL\17\data\`. Dumps are copies for restore/disaster recovery.

---

## Desktop logs (this Windows user)

| What | Path |
| --- | --- |
| Errors (if API unreachable) | `%AppData%\Mostawsef\logs\errors-yyyyMMdd.log` |
| Perf (if enabled) | `%AppData%\Mostawsef\logs\perf-yyyyMMdd.log` |

---

## Updates

1. Build newer `Mostawsef-Setup-{version}.exe`.
2. Run as Administrator on the clinic PC.
3. Leave postgres password **blank** if already configured.
4. App files replace; **Postgres data + connection string + Clinic.json stay**.

---

## Quick checks (Admin PowerShell)

```powershell
Get-Service MostawsefApi, postgresql-x64-17
Invoke-WebRequest http://localhost:5270/api/health -UseBasicParsing
Get-Content C:\Mostawsef\install-clinic.log -Tail 40
```
