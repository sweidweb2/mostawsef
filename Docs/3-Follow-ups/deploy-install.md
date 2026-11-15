# Clinic install — open work

Checklist to finish before the first real clinic installation. Each block is independent: pick one, finish it, tick it, move on.

**Skipped (already resolved in code):**

| Was | Why skip |
|---|---|
| Connection monitor dies on a wrong server address | `ApiClient` maps non-JSON success bodies to `ApiError.Server` instead of throwing; `ConnectionMonitor` marks Offline and always schedules the next check. |
| Dev seed users in Production config | `appsettings.json` already has `"Seed": { "Users": [] }`. Dev users live only in `appsettings.Development.json`. Still **verify** on a published build (Block 5). |
| Super table export / “Backups” page | Exists (CSV / JSON / SQL / PostgreSQL script). That is **not** a scheduled restorable database backup — see Block 3. |
| Password changes keep old sessions alive | `User.TokenVersion` bumped on password change/reset; JWT claim `tv` checked in `RejectStaleUserAsync`. |
| Activity logs can be erased | Single-entry activity-log deletes removed; only retention cleanup deletes. |
| Duplicate username race returns 500 | `AppDbContext.SaveChangesAsync` maps unique conflicts to 409 `taken` (users / lab tests covered by tests). |

---

## Status

| Block | Topic | Status |
|---|---|---|
| 1 | First-run setup (JWT key + first Super) | Open |
| 2 | Multi-PC server address | 🔒 Later (not for one-PC install) |
| 3 | Scheduled `pg_dump` backups + restore test | Dump ✅; restore verify still open |
| 4 | HTTPS vs trusted-network decision | Open (decision) |
| 5 | Production packaging defaults check | Open (verify + package) |
| 6 | Clinic logo | Open |
| 7 | Large dev seed (perf) | Open |
| 8 | Session expiry vs wrong PC clock | Open |
| 9 | Log timestamps use `DateTime.UtcNow` | Open |

---

## Block 1 — First-run setup

**Problem.** A Production install cannot start or sign anyone in.

- `Jwt:Key` is `""` in `backend/Mostawsef.Api/appsettings.json`. Startup validation requires at least 32 characters, so the API refuses to start.
- Seed users (`super@example.com` / `admin@example.com` / `password`) exist only under Development. A fresh Production database has no users.

**Done when**

1. First launch (or an installer step) generates a random `Jwt:Key` unique to that clinic and persists it next to the API (never commit a real key).
2. First launch creates exactly one Super user (username + password chosen by the installer / clinic admin).
3. After that, no automatic seed users are created.
4. Documented in the install steps; covered by an automated test where practical (empty key still fails; first-run path succeeds once).

**Evidence / files**

- `backend/Mostawsef.Api/appsettings.json` — empty `Jwt:Key`, empty `Seed:Users`
- `backend/Mostawsef.Api/appsettings.Development.json` — dev key + seed users
- `backend/Mostawsef.Infrastructure/Services/Auth/JwtOptions.cs`
- `backend/Mostawsef.Infrastructure/Persistence/DatabaseInitializer.cs`
- `Docs/auth.md` (Production must set its own key and first user)

- [ ] Implemented
- [ ] Verified on a publish/install build (not only Development)

---

## Block 2 — Multi-PC server address 🔒

**Skip for now** if the clinic is one PC only. Unlock when a second PC must connect to the same server.

**Problem.** Other clinic PCs cannot talk to the API PC with the current defaults, and staff have no UI to fix the address.

- API: without `urls` / Kestrel / `ASPNETCORE_URLS`, `Program.cs` binds `http://localhost:5270` only.
- Desktop: `AppSettings.ApiBaseUrl` defaults to `http://localhost:5270`.
- There is **no screen** to change the API address (only editing `%AppData%\Mostawsef\settings.json` by hand).
- `HttpClient.BaseAddress` is set once from settings at first resolve; changing the file mid-run needs an app restart.

**Done when**

1. The installed API can listen on a LAN address (or all interfaces) in a documented, safe way for the clinic server PC.
2. The desktop has a supported way to set the server URL (login/settings UI, or a first-connection screen) before or at sign-in.
3. Single-PC localhost installs keep working with no extra steps.
4. Changing the address is documented (and requires restart if the client still binds once).

**Evidence / files**

- `backend/Mostawsef.Api/Program.cs` (default `UseUrls`)
- `frontend/Mostawsef.Desktop/Services/Local/AppSettings.cs`
- `frontend/Mostawsef.Desktop/ServiceRegistration.cs`
- `frontend/Mostawsef.Desktop/ViewModels/Auth/LoginViewModel.cs` (insecure-remote warning only)

- [ ] API listen address for clinic server
- [ ] Desktop UI (or equivalent) to set `ApiBaseUrl`
- [ ] Verified: second PC signs in against the API PC

---

## Block 3 — Scheduled database backups and restore

**Problem.** Medical data needs a real, restorable database backup. The Super “Backups” page only exports selected tables (or all tables except `users`) on demand. That is useful for spreadsheets / scripts, not for disaster recovery.

**Already exists**

- Super Backups page + table exports (not a full restore).
- `SystemSettings.BackupPath` + Super Settings Browse → scheduled `pg_dump` via `ScheduledBackupWorker` / `IScheduledBackupService` (creates the folder, keeps `ScheduledBackup:KeepCount`, logs `backups.scheduled`, failures → system errors).
- Restore steps in `Docs/commands.md` (Scheduled database dumps).

**Still missing**

1. A restore from a real dump has been **run successfully** once on a spare / disposable database (then tick below).

**Done when**

1. ✅ Clinic sets a backup folder in Super Settings and gets automatic dumps while the API runs.
2. At least one full restore from that dump has been tested on a spare machine.
3. ✅ Failure of a backup is visible (system error log).

**Evidence / files**

- `Docs/commands.md` (Scheduled database dumps)
- `ScheduledBackupService`, `PgDumpRunner`, `ScheduledBackupWorker`
- Super Settings form (Browse)

- [x] Scheduled dump implemented
- [ ] Restore tested once end-to-end
- [x] Restore steps written (`Docs/commands.md`)

---

## Block 4 — HTTPS vs trusted network

**Problem.** The API serves plain HTTP on purpose (`Program.cs` skips HTTPS redirection so the desktop does not drop the `Authorization` header on redirect). Passwords, JWTs and patient payloads are readable on the LAN if another machine sniffs traffic.

**Already exists**

- Same-PC `http://localhost` is the safe default.
- Login warning when `ApiBaseUrl` is plain `http` to a non-local host (`ApiAddress.IsInsecureRemote`).
- No “accept any certificate” bypass in the desktop client.

**Decision (pick one and document it)**

| Option | When |
|---|---|
| A. Accept risk | Single trusted clinic LAN, no untrusted devices, written acceptance in install docs. |
| B. HTTPS | API behind HTTPS (reverse proxy or Kestrel cert) that every desktop PC trusts; `ApiBaseUrl` uses `https://…`. |

**Done when**

1. The chosen option is written in the install docs for every clinic topology (one PC vs server + clients).
2. If B: a repeatable cert/trust procedure exists; a desktop can sign in over `https` without disabling validation.

**Evidence / files**

- `backend/Mostawsef.Api/Program.cs` (no HTTPS redirection comment)
- `frontend/Mostawsef.Desktop/Services/Local/ApiAddress.cs`
- `Docs/auth.md` (when to use https)
- `Docs/desktop-resource-audit.md` Block 3

- [ ] Decision recorded (A or B)
- [ ] Install docs updated
- [ ] If B: cert path verified on two PCs

---

## Block 5 — Production packaging defaults

**Problem.** The API is meant to run as a Windows service against native PostgreSQL. Source defaults are mostly right; the **installed** layout must not reintroduce Development habits.

**Check on every publish/install build**

1. `Seed:Users` is empty (no `admin@example.com` / `password`).
2. `Jwt:Key` is not the Development key; it comes from first-run (Block 1) or a secure clinic-specific config, never from the repo.
3. Connection string points at the clinic’s native PostgreSQL (not Docker `mostawsef_dev`).
4. API runs as a Windows service (or equivalent always-on host), starts after PostgreSQL, and survives reboot.
5. Desktop ships as a Release publish; Debug-only login autofill (`#if DEBUG` in `LoginViewModel`) is absent.
6. `MOSTAWSEF_PERF` is not set on clinic PCs.
7. Installer / update story chosen (e.g. Velopack).

**Done when**

1. A checklist (this block) is part of the release procedure.
2. One clean machine install from the installer (no Dev config, no Docker) can: start API → first-run → desktop sign-in → create a patient.

**Evidence / files**

- `backend/Mostawsef.Api/appsettings.json` vs `appsettings.Development.json`
- `frontend/Mostawsef.Desktop/ViewModels/Auth/LoginViewModel.cs` (`#if DEBUG`)
- `Docs/foundation.md` (native PostgreSQL on clinic PCs)

- [ ] Publish layout reviewed once
- [ ] Clean-machine install smoke test passed
- [ ] Installer / update method chosen

---

## Block 6 — Clinic logo

**Problem.** Clinic settings (name, address, phone, currency) are built and used by printouts and headers. Logo storage and upload are still missing.

**Done when**

1. Decide where the logo file lives (e.g. next to clinic data / settings, not in the DB as a huge blob unless needed).
2. Admin can set/clear a logo; printouts and headers show it when present.

**Evidence / files**

- `Docs/foundation.md` (Clinic settings row — “No logo yet”)
- Clinic settings module + `ClinicContext`

- [ ] Storage decided and implemented
- [ ] Print / header shows the logo

---

## Block 7 — Large development seed

**Problem.** List pages and indexes look fine on small data. Clinics will hit slow queries only after volume grows.

**Done when**

1. A Development-only way exists to seed on the order of ~100,000 patients (and enough related rows to exercise lists).
2. Main list pages are checked once against that seed (timing / missing indexes) before the first real clinic.

- [ ] Seed path exists (Dev only)
- [ ] One pass over main lists against the large seed

---

## Block 8 — Session expiry vs wrong PC clock

**Problem.** `SessionService` compares the server's token expiry with the PC's own clock. A clinic PC whose clock is ahead of the server (dead motherboard battery, wrong time zone) can show “session expired” right after every sign-in.

**Done when**

1. Expiry is based on the token's lifetime (or another clock-safe rule), not “PC now vs absolute server midnight.”
2. Covered by a desktop test with a skewed clock if practical.

**Evidence / files**

- `frontend/Mostawsef.Desktop` `SessionService` (and login / connection flow that reads expiry)

- [ ] Expiry no longer depends on a correct PC wall clock
- [ ] Test or verified repro fixed

---

## Block 9 — Log timestamps use `DateTime.UtcNow`

**Problem.** `ActivityLog` and `SystemError` stamp `OccurredAt` with `DateTime.UtcNow` instead of `TimeProvider`. “Today” statistics use `TimeProvider`, so tests with a fixed clock can disagree with the stored times.

**Done when**

1. Both entities (or their writers) get `OccurredAt` from `TimeProvider`.
2. Existing log / stats tests still pass with a fixed clock.

**Evidence / files**

- Domain / Application writers for `ActivityLog` and `SystemError`
- Log list / “today” statistics

- [ ] Writers use `TimeProvider`
- [ ] Fixed-clock tests agree with stored times

---

## Suggested order

1. Block 1 (nothing starts without JWT + Super).
2. Block 5 packaging enough to run Blocks 1–2 on a spare PC.
3. Block 2 🔒 only if a clinic will use more than one PC.
4. Block 3 before real patient data.
5. Block 4 decision before multi-PC go-live (can be “accept risk” for a single locked LAN).
6. Block 6 anytime before printouts ship to a clinic that needs branding.
7. Block 7 before go-live (can run in parallel with packaging).
8. Blocks 8–9 anytime (code correctness; do before first clinic if possible).
