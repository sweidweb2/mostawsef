# Deploy plan — from here to a clinic install

Step-by-step path until a **one-PC clinic** can run Mostawsef from a single installer.

Related: open packaging work in [`deploy-install.md`](deploy-install.md) (especially Block 5). Publish commands: [`Docs/commands.md`](../commands.md).

## Product decisions (locked for v1)

| Decision | Choice |
| --- | --- |
| Target | One Windows PC (API + Desktop + database on the same machine) |
| Installer | **One** elevated `Mostawsef-Setup.exe` (Inno Setup: self-contained Desktop + API + service + **bundled offline PostgreSQL 17** installer; winget only as build-PC download / last-resort fallback) |
| Docker | **Never** on clinic PCs — native PostgreSQL 17 only (Docker is for development) |
| Network | `http://localhost:5270` only (no multi-PC / HTTPS yet) |
| Updates | **No auto-update for now.** Rebuild, make a new Setup, send it; client reinstalls. Do not wipe clinic data on update |
| CI today | Tests only (`.github/workflows/tests.yml`). Release/publish CI comes in Phase 6 |

## Architecture on the clinic PC

```
Desktop (Avalonia) ──HTTP──► API (Windows service) ──EF──► PostgreSQL (native)
```

The desktop never talks to PostgreSQL. Publishing only the Avalonia app is not enough — the installer must cover **Desktop + API**, and get PostgreSQL onto the machine.

## Progress

| Phase | Topic | Status |
| --- | --- | --- |
| 0 | Development (current daily work) | ✅ Done / ongoing |
| 1 | Publish Desktop + API locally | ✅ Done — `mostawsef-3ammo\[2] Builds\{Desktop,Api}` |
| 2 | Native PostgreSQL on a clean / spare PC | ✅ Done — native PG 17 on this PC (`:5433`) |
| 3 | Manual smoke: published API + Desktop end-to-end | ✅ Done |
| 4 | API as Windows service (survives reboot) | ✅ Done — `MostawsefApi` (after `postgresql-x64-17`) |
| 5 | Clinic install playbook (human checklist) | ✅ Done — [`clinic-install-playbook.md`](clinic-install-playbook.md) |
| 6 | One Setup.exe (Inno Setup) + optional GitHub Releases | ✅ Done — `Mostawsef-Setup.exe` (CI Releases later) |

Work phases **in order**. Do not start Phase 6 until Phase 3 works by hand.

---

## Phase 0 — Development ✅

**What you do today**

- Code against Docker PostgreSQL (`docker compose up -d`)
- `dotnet run` API + Desktop
- `./scripts/test.ps1`

**Done when**

- Features you need for the first clinic work locally

**Not this phase**

- Giving folders or an installer to a clinic

---

## Phase 1 — Publish both apps locally ✅

**Goal:** Release folders that look like what the clinic will run (no Development habits).

**Steps**

1. On your build PC, publish Desktop and API (see `Docs/commands.md`):

   ```powershell
   dotnet publish frontend/Mostawsef.Desktop -c Release -o "C:\Users\User\Desktop\mostawsef-3ammo\[2] Builds\Desktop"
   dotnet publish backend/Mostawsef.Api -c Release -o "C:\Users\User\Desktop\mostawsef-3ammo\[2] Builds\Api"
   ```

2. Note the output folders. This build uses:

   - `C:\Users\User\Desktop\mostawsef-3ammo\[2] Builds\Desktop\`
   - `C:\Users\User\Desktop\mostawsef-3ammo\[2] Builds\Api\`

   (Default without `-o`: `.../bin/Release/net10.0/win-x64/publish/`.)

3. Confirm Production packaging defaults ([`deploy-install.md` Block 5](deploy-install.md)):

   - [x] No Development seed users in the published API config (`appsettings.Development.json` not copied to publish)
   - [x] No Docker connection string in published config (clinic native string is Phase 2)
   - [x] Desktop is a Release publish
   - [x] `MOSTAWSEF_PERF` is not set for clinic use

**Done when**

- Both publish folders exist and you know where they are ✅

---

## Phase 2 — Native PostgreSQL on a clean / spare PC ✅

**Goal:** A machine that behaves like a clinic PC (no Docker for Mostawsef).

**Steps**

1. Install **PostgreSQL 17** natively on Windows ✅ (`postgresql-x64-17`, Automatic)
2. Create a database and user for Mostawsef ✅ (`mostawsef` / `mostawsef_clinic`)
3. Put the connection string in the **published** API config next to the API (clinic Production config — not the Docker dev string) ✅
4. Confirm you can connect with `psql` / pgAdmin ✅ (`psql` as `mostawsef` on `127.0.0.1:5433`)

**This build PC (smoke stand-in)**

- Native Postgres listens on **5433** so Docker can keep **5432** for daily `dotnet run` work
- A real clinic PC has no Docker and uses **5432**
- Published connection string (in `mostawsef-3ammo\[2] Builds\Api\appsettings.json` only — not in source):

  `Host=127.0.0.1;Port=5433;Database=mostawsef;Username=mostawsef;Password=mostawsef_clinic`

- Data directory: `C:\Program Files\PostgreSQL\17\data\`
- pgAdmin was installed with PostgreSQL 17 (Start Menu)

**Notes**

- PostgreSQL stores its data in its own data directory, not inside the Mostawsef install folder
- Client PCs never use Docker for this product
- Re-publishing the API overwrites `appsettings.json` — re-add the connection string after each publish (or stop wiping that file)

**Done when**

- Native Postgres is up and the connection string for the published API is ready ✅

---

## Phase 3 — Manual smoke test (publish folders) ✅

**Goal:** Prove a clinic can run without an installer yet.

**Steps (on the clean / spare PC)**

1. Install PostgreSQL (Phase 2) if not already done ✅
2. Copy the published **API** folder onto the PC ✅ (`mostawsef-3ammo\[2] Builds\Api`)
3. Start the API once (Production) ✅
4. Confirm first-run: clinic JWT file + install Super/Admin users (see `Docs/4-Business/auth.md`) ✅
5. Copy the published **Desktop** folder onto the PC ✅ (`mostawsef-3ammo\[2] Builds\Desktop`)
6. Start the Desktop → sign in → create a patient (or equivalent real workflow) ✅
7. Confirm API stays reachable at `http://localhost:5270` ✅

**Done when**

- Clean machine, no Docker, no Dev config: API starts → first-run → Desktop sign-in → real work ✅

Until Phase 6 exists, you *could* install a friendly first client this same way (copy folders + Postgres). Prefer finishing Phase 4–5 first.

**Ticks** ([`deploy-install.md` Block 5](deploy-install.md))

- [x] Publish layout reviewed once ✅
- [x] Clean-machine install smoke test passed ✅

---

## Phase 4 — API always on after reboot ✅

**Goal:** Staff do not open a console to start the API.

**Steps**

1. Register the published API as a **Windows service** ✅ (`./scripts/install-api-service.ps1` → service `MostawsefApi`)
2. Service starts **after** PostgreSQL ✅ (depends on `postgresql-x64-17`)
3. Reboot the PC — **do once when convenient** to confirm; service is Automatic + already healthy without a console
4. Confirm Desktop can sign in with no manual API start ✅ (service serves `http://localhost:5270`; Admin login OK)

**This PC**

- Binary: `mostawsef-3ammo\[2] Builds\Api\Mostawsef.Api.exe`
- `sc.exe qc MostawsefApi` → AUTO_START, DEPENDENCIES = `postgresql-x64-17`
- API hosts as a Windows service via `UseWindowsService` (content root = exe folder)

**Done when**

- Service installed, starts with the PC / after Postgres, Desktop can sign in without starting the API by hand ✅  
  (Optional: reboot once yourself to double-check)

---


## Phase 5 — Clinic install playbook ✅

**Goal:** A short checklist you follow on site so the second clinic is the same as the first.

**Playbook:** [`clinic-install-playbook.md`](clinic-install-playbook.md) (written from Phases 2–4).

1. [x] Install PostgreSQL 17 (native)
2. [x] Create DB / user; note connection string
3. [x] Install / place API; set connection string
4. [x] Install / start API Windows service
5. [x] Install / place Desktop; shortcut for staff
6. [x] Sign in with install Super/Admin → **change passwords** (called out in playbook)
7. [x] Super Settings: set backup folder (scheduled `pg_dump`) (called out in playbook)
8. [x] Smoke: login + one patient / order
9. [x] Confirm: no Docker, localhost API only

**Done when**

- You can install another one-PC clinic from the playbook without guessing ✅

---

## Phase 6 — One Setup.exe (and optional release CI) ✅

**Start only after Phase 3 (and ideally 4–5) work by hand.**

**Goal:** One installer the client runs. Same product decisions as above.

**Chosen packaging: Inno Setup** (not Velopack — Velopack cannot install a Windows service / machine-wide API).

**Steps**

1. Choose packaging ✅ — Inno Setup (`packaging/innosetup/Mostawsef.iss`)
2. Package **Desktop + API** (self-contained) + **bundled PostgreSQL 17** installer; install/configure DB in the same flow ✅ (`build-setup.ps1` downloads Postgres once; `install-clinic.ps1` runs the offline installer if Postgres is missing, else prompts for postgres password)
3. Build `Setup.exe` ✅ — `./scripts/build-setup.ps1` → `mostawsef-3ammo\[2] Builds\Mostawsef-Setup.exe`
4. Test on a clean PC: install → first-run → sign-in → create data (run Setup as Admin; see playbook)
5. Document reinstall/update ✅ — newer Setup replaces app files; **never** deletes the PostgreSQL data directory; preserves `appsettings.Clinic.json` + connection string
6. Optional later: GitHub Actions → GitHub Releases (CI today stays tests-only)

**Updates (v1)**

- No in-app auto-update
- Run `./scripts/build-setup.ps1` (auto next `Mostawsef-Setup-{version}.exe` under Builds; older files kept) or pass `-Version 1.2.0`
- Send the new Setup to the clinic; they reinstall
- Clinic data stays in PostgreSQL

**Done when**

- [x] Installer / update method chosen ([`deploy-install.md` Block 5](deploy-install.md)) — Inno Setup
- [x] Build produces `Mostawsef-Setup.exe` (clinic: run elevated; smoke PC may already have Postgres on :5433)
- [x] Reinstall / upgrade path preserves DB + clinic JWT / connection string (backup in Setup `[Code]`)

---

## Out of scope for this plan (later)

- Multi-PC / LAN API address (`deploy-install.md` Block 2)
- HTTPS for remote desktops (Block 4 option B)
- Clinic logo (Block 6)
- In-app automatic updates

---

## Suggested order this week

1. Phase 1 — publish both projects ✅  
2. Phase 2 — native PostgreSQL ✅  
3. Phase 3 — smoke test ✅  
4. Phase 4 — Windows service ✅  
5. Phase 5 — playbook ✅  
6. Phase 6 — Inno Setup.exe ✅ (optional: Releases CI later)
