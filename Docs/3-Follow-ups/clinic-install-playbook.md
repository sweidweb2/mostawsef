# Clinic install playbook (one PC)

Checklist for installing Mostawsef on a **single Windows clinic PC**.  
No Docker. No `dotnet run`. Written from Phases 2–4 on the smoke machine.

Related: [`deploy-plan.md`](deploy-plan.md) · publish commands: [`Docs/commands.md`](../commands.md) · auth: [`Docs/4-Business/auth.md`](../4-Business/auth.md) · ship updates: [`01-release-playbook.md`](../7-automating%20install/01-release-playbook.md)

## Preferred: first install with Setup.exe

1. On the build PC: `./scripts/build-setup.ps1` (full Setup with Postgres — writes `Mostawsef-Setup-{version}.exe`; does not overwrite older builds)
2. Copy that versioned Setup from `mostawsef-3ammo\[2] Builds\` to the clinic PC (USB is fine)
3. Run it **as Administrator**
4. If PostgreSQL is already installed, enter the **postgres** superuser password when asked (leave blank on a new PC — Setup uses the **bundled offline** Postgres installer)
5. Sign in with install Super/Admin → **change passwords** → set backup folder → smoke test

Installs to `C:\Mostawsef\` (Api + Desktop + scripts), registers `MostawsefApi`, shortcuts for staff. Uninstall does **not** remove PostgreSQL.

## Updates after the clinic is live

| Path | When | What staff do |
| --- | --- | --- |
| **In-app** | PC online | Sign in → **Update now** → UAC → wait for reopen |
| **USB** | Offline / no GitHub | Copy newer `Mostawsef-Setup-*.exe`, run as Administrator |

Both replace app files only. Postgres data and `appsettings.Clinic.json` / connection string stay. A short “connection lost” banner while the API restarts is normal.  
Before a **major** update: confirm Super → Settings → backup folder has a recent dump ([`03-data-safety.md`](../7-automating%20install/03-data-safety.md)).

**Rollback:** install the previous `Mostawsef-Setup-*.exe` from [GitHub Releases](https://github.com/sweidweb2/mostawsef/releases) or your USB archive (Admin). Data is kept; if a migration already applied and the old app will not start, restore a dump first (`Docs/commands.md`).

### Developer note (not a clinic step)

Day-to-day: merge to `main` → CI auto-bumps version + GitHub Release → clinic **Update now** (or USB). Full steps: [`01-release-playbook.md`](../7-automating%20install/01-release-playbook.md). Local update Setup: `./scripts/build-setup.ps1 -UpdateOnly`.

Only if you change **how install works** (service name, install path, Postgres rules, Setup scripts under `packaging/innosetup/` or `scripts/install-*.ps1`): update those scripts, then rebuild Setup. Clinics still just run Setup or **Update now**.

---

## Manual fallback (if Setup.exe is not used)

## What you bring

| Item | Notes |
| --- | --- |
| Published **Api** folder | From build PC: `mostawsef-3ammo\[2] Builds\Api` (or fresh `dotnet publish`) |
| Published **Desktop** folder | From build PC: `mostawsef-3ammo\[2] Builds\Desktop` |
| `scripts\install-api-service.ps1` | From the repo (or copy next to the Api folder) |
| PostgreSQL 17 | Prefer Setup.exe (bundled offline). Manual: EDB installer / winget only as fallback |
| Admin rights on the clinic PC | Needed for Postgres + Windows service |

**Suggested layout on the clinic PC**

```
C:\Mostawsef\
  Api\          ← published API (Mostawsef.Api.exe + appsettings*.json)
  Desktop\      ← published Desktop (Mostawsef.Desktop.exe)
```

Staff shortcut → `C:\Mostawsef\Desktop\Mostawsef.Desktop.exe`

---

## On-site checklist

### 1. Install PostgreSQL 17 (native)

- [ ] Prefer **Mostawsef-Setup.exe** (bundled offline Postgres when missing). **Do not use Docker.**
- [ ] Port **5432** (default). On a build PC that already has Docker on 5432, use another port (smoke used **5433**) — a real clinic has no Docker, so use **5432**.
- [ ] If installing manually, remember the **postgres** superuser password.
- [ ] Confirm service `postgresql-x64-17` is **Running** / **Automatic**. A first Postgres install may require **one reboot** — reboot once, then open Mostawsef (do not spam-click).

### 2. Create database and app user

In `psql` (or pgAdmin) as `postgres`:

```sql
CREATE ROLE mostawsef LOGIN PASSWORD 'CHOOSE_A_STRONG_PASSWORD';
CREATE DATABASE mostawsef OWNER mostawsef;
\c mostawsef
GRANT ALL ON SCHEMA public TO mostawsef;
ALTER SCHEMA public OWNER TO mostawsef;
```

- [ ] Role + database created
- [ ] Can connect as `mostawsef`:

  ```powershell
  $env:PGPASSWORD = 'CHOOSE_A_STRONG_PASSWORD'
  & "C:\Program Files\PostgreSQL\17\bin\psql.exe" -h 127.0.0.1 -p 5432 -U mostawsef -d mostawsef -c "\conninfo"
  ```

**Connection string (clinic):**

```text
Host=127.0.0.1;Port=5432;Database=mostawsef;Username=mostawsef;Password=CHOOSE_A_STRONG_PASSWORD
```

### 3. Place API and set connection string

- [ ] Copy the published **Api** folder to `C:\Mostawsef\Api\` (or chosen path)
- [ ] Edit `appsettings.json` next to `Mostawsef.Api.exe` — add / set:

  ```json
  "ConnectionStrings": {
    "Default": "Host=127.0.0.1;Port=5432;Database=mostawsef;Username=mostawsef;Password=CHOOSE_A_STRONG_PASSWORD"
  }
  ```

- [ ] Confirm there is **no** `appsettings.Development.json` in that folder (publish excludes it)
- [ ] Leave `Jwt:Key` empty — first service start creates `appsettings.Clinic.json` (do not delete that file later)

### 4. Install / start the API Windows service

From an elevated PowerShell (or let the script re-launch elevated):

```powershell
./scripts/install-api-service.ps1 -ApiPath "C:\Mostawsef\Api" -PostgresService postgresql-x64-17
```

- [ ] Service `MostawsefApi` exists, **Automatic**, depends on `postgresql-x64-17`
- [ ] Health OK: `http://localhost:5270/api/health` → `Healthy` / `Connected`
- [ ] `appsettings.Clinic.json` appeared next to the API after first start

### 5. Place Desktop and shortcut

- [ ] Copy the published **Desktop** folder to `C:\Mostawsef\Desktop\`
- [ ] Create a desktop / Start shortcut to `Mostawsef.Desktop.exe`
- [ ] Default API address is `http://localhost:5270` (one-PC; leave as-is)

### 6. First sign-in — change passwords

Install accounts (seeded once on first Production start):

| Username | Password | Role |
| --- | --- | --- |
| `ali@etkan` | `ali@1234` | Super |

- [ ] Use **Enter clinic** on the login screen (Admin portal, no password)
- [ ] Sign in as Super right away: the app asks for a new password before anything else (the initial one is public
      and the server refuses every Super action until it is changed)
- [ ] Write the new Super password somewhere safe for the clinic owner (not in the repo)

### 7. Super Settings — backup folder

- [ ] Sign in as Super → Settings → set the **backup folder** (scheduled `pg_dump` while the API service runs)
- [ ] Prefer a path on another drive / folder the clinic backs up (not only inside `C:\Mostawsef`)
- [ ] Confirm `pg_dump` exists with PostgreSQL 17 (`C:\Program Files\PostgreSQL\17\bin\pg_dump.exe`). Set `ScheduledBackup:PgDumpPath` in API config only if it is not on PATH

### 8. Smoke test

- [ ] Reboot once (or at least restart `MostawsefApi`) — Desktop signs in **without** starting the API by hand
- [ ] Create one patient (Admin)
- [ ] Optional: create one order if lab tests are already set up
- [ ] Health still OK: `http://localhost:5270/api/health`

### 9. Final confirm

- [ ] No Docker used for Mostawsef on this PC
- [ ] API is only `http://localhost:5270` (no multi-PC / HTTPS yet)
- [ ] Postgres data lives under PostgreSQL’s own data directory — **never** delete that when updating the app
- [ ] Clinic connection string + `appsettings.Clinic.json` survive app updates (re-publish overwrites `appsettings.json` — put the connection string back)

---

## Updates (manual folder replace — rare)

Prefer in-app / USB Setup — see **Updates after the clinic is live** at the top.

If you must swap publish folders by hand:

1. On the build PC: publish Desktop + API again (`Docs/commands.md`)
2. On the clinic: **stop** service `MostawsefApi`
3. Replace files under `Api\` and `Desktop\` (keep / restore connection string + keep `appsettings.Clinic.json`)
4. Start `MostawsefApi` again (or re-run `install-api-service.ps1`)
5. Smoke: login + one patient

Do **not** wipe the PostgreSQL data directory.

---

## Uninstall / move (rare)

```powershell
./scripts/install-api-service.ps1 -Uninstall
```

Then remove `C:\Mostawsef\` if desired. Database data stays until you uninstall PostgreSQL or drop the DB on purpose.

---

## Smoke-machine notes (this build PC only)

| Choice | Why |
| --- | --- |
| Postgres port **5433** | Docker already uses **5432** for daily `dotnet run` |
| Builds under `Desktop\mostawsef-3ammo\[2] Builds\` | Local release shelf, not the clinic path |
| DB password `mostawsef_clinic` | Smoke only — clinics choose their own |

A real clinic PC: port **5432**, no Docker, folders under `C:\Mostawsef\` (or similar).
