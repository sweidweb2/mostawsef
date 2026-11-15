# Clinic install — open work

Checklist to finish before the first real clinic installation. Each block is independent: pick one, finish it, tick it, move on.

**Step-by-step path to a client install (phases 0→6):** [`deploy-plan.md`](deploy-plan.md).

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
| 1 | First-run setup (JWT key + first Super) | ✅ Done |
| 2 | Multi-PC server address | 🔒 Later (not for one-PC install) |
| 3 | Scheduled `pg_dump` backups + restore test | ✅ Done |
| 4 | HTTPS vs trusted-network decision | ✅ Done (A — one PC) |
| 5 | Production packaging defaults check | ✅ Done (Inno Setup `Mostawsef-Setup.exe`) |
| 6 | Clinic logo | 🔒 Later (no logo for now) |
| 7 | Large dev seed (perf) | ✅ Done |
| 8 | Session expiry vs wrong PC clock | ✅ Done |
| 9 | Log timestamps use `DateTime.UtcNow` | ✅ Done |

---

## Block 1 — First-run setup ✅

**Problem.** A Production install could not start or sign anyone in (`Jwt:Key` empty; no Production seed users).

**Done**

1. `ClinicLocalConfig` on Production first start writes a random `Jwt:Key` to `appsettings.Clinic.json` next to the API (gitignored). Development / Testing do not auto-create it.
2. Install seed users in `appsettings.json` (created only if the username was never used): Super `ali@etkan` / `ali@1234`, Admin `ana_mostawsef` / `123456789`. Development still uses `appsettings.Development.json` example accounts only.
3. Documented in `Docs/4-Business/auth.md`. Tests: `ClinicLocalConfigTests`, existing `JwtConfigurationTests` (empty/short key still fails in Testing).

- [x] Implemented
- [x] Covered by automated tests (publish smoke still recommended once packaging exists — Block 5)

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

## Block 3 — Scheduled database backups and restore ✅

**Problem.** Medical data needs a real, restorable database backup. The Super “Backups” page only exports selected tables (or all tables except `users`) on demand. That is useful for spreadsheets / scripts, not for disaster recovery.

**Done**

- Super Backups page + table exports (not a full restore).
- `SystemSettings.BackupPath` + Super Settings Browse → scheduled `pg_dump` via `ScheduledBackupWorker` / `IScheduledBackupService` (creates the folder, keeps `ScheduledBackup:KeepCount` files and history rows, shown on Super → Backups, logs `backups.scheduled`, failures → system errors).
- Restore steps in `Docs/commands.md` (Scheduled database dumps).
- End-to-end restore verified once: `pg_dump -Fc` of the live `mostawsef` DB → `pg_restore --clean --if-exists --no-owner` into disposable `mostawsef_restore_test` → user/patient/order counts and a sample username matched; live DB untouched; test DB and temp dump removed after.

**Done when**

1. ✅ Clinic sets a backup folder in Super Settings and gets automatic dumps while the API runs.
2. ✅ At least one full restore from that dump has been tested on a spare / disposable database.
3. ✅ Failure of a backup is visible (system error log).

**Evidence / files**

- `Docs/commands.md` (Scheduled database dumps)
- `ScheduledBackupService`, `PgDumpRunner`, `ScheduledBackupWorker`
- Super Settings form (Browse)

- [x] Scheduled dump implemented
- [x] Restore tested once end-to-end
- [x] Restore steps written (`Docs/commands.md`)

---

## Block 4 — HTTPS vs trusted network ✅

**Problem.** The API serves plain HTTP on purpose (`Program.cs` skips HTTPS redirection so the desktop does not drop the `Authorization` header on redirect). Passwords, JWTs and patient payloads would be readable on the LAN if another machine sniffed traffic.

**Decision: A (one-PC install).** Clinics run API + desktop + database on a single machine. `ApiBaseUrl` stays `http://localhost:5270` (or another loopback address). Traffic never leaves the PC, so LAN sniffing does not apply. Option B (HTTPS) is deferred until a multi-PC install is actually planned (same gate as Block 2).

**Already in place**

- Default `http://localhost:5270`.
- Login warning if someone points `ApiBaseUrl` at plain `http` on another host (`ApiAddress.IsInsecureRemote`).
- No “accept any certificate” bypass in the desktop client.
- Documented in `Docs/4-Business/auth.md` and this file.

- [x] Decision recorded (A — one PC / localhost HTTP)
- [x] Install docs updated
- [x] If B: N/A (not chosen)

---

## Block 5 — Production packaging defaults

**Problem.** The API is meant to run as a Windows service against native PostgreSQL. Source defaults are mostly right; the **installed** layout must not reintroduce Development habits.

**Check on every publish/install build**

1. `Seed:Users` is empty (no `admin@example.com` / `password`).
2. `Jwt:Key` is not the Development key; it comes from first-run (Block 1) or a secure clinic-specific config, never from the repo.
3. Connection string points at the clinic’s native PostgreSQL (not Docker `mostawsef_dev`).
4. API runs as a Windows service (or equivalent always-on host), starts after PostgreSQL, and survives reboot.
5. Desktop ships as a Release publish.
6. `MOSTAWSEF_PERF` is not set on clinic PCs.
7. Installer / update story chosen: **Inno Setup** → `Mostawsef-Setup.exe` (`./scripts/build-setup.ps1`). Not Velopack (cannot host the API Windows service).

**Done when**

1. A checklist (this block) is part of the release procedure.
2. One clean machine install from the installer (no Dev config, no Docker) can: start API → first-run → desktop sign-in → create a patient.

**Evidence / files**

- `backend/Mostawsef.Api/appsettings.json` vs `appsettings.Development.json`
- `Docs/foundation.md` (native PostgreSQL on clinic PCs)

- [x] Publish layout reviewed once (Phase 1: `mostawsef-3ammo\[2] Builds`; Dev config excluded from API publish)
- [x] Clean-machine install smoke test passed ✅
- [x] Installer / update method chosen ✅ — Inno Setup (`Mostawsef-Setup.exe`)

---

## Block 6 — Clinic logo 🔒

**Skip for now** — no clinic logo required. Unlock when a clinic needs branding on printouts/headers.

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

## Block 7 — Large development seed ✅

**Problem.** List pages and indexes look fine on small data. Clinics will hit slow queries only after volume grows.

**Done.** Dev seed/suite path exists (`Docs/commands.md` / `./scripts/suite.ps1`); treated as closed for now.

- [x] Seed path exists (Dev only)
- [x] One pass over main lists against the large seed

---

## Block 8 — Session expiry vs wrong PC clock ✅

**Problem.** `SessionService` compared the server's absolute token expiry with the PC's own clock. A clinic PC whose clock is ahead of the server (dead motherboard battery, wrong time zone) could show “session expired” right after every sign-in.

**Done.** Login returns `ExpiresInSeconds`; the desktop counts that lifetime from receipt (`SessionService.SignIn`). Covered by `A_session_stays_valid_when_the_pc_clock_is_ahead_of_the_server`.

- [x] Expiry no longer depends on a correct PC wall clock
- [x] Test or verified repro fixed

---

## Block 9 — Log timestamps use `DateTime.UtcNow` ✅

**Problem.** `ActivityLog` and `SystemError` stamped `OccurredAt` with `DateTime.UtcNow` instead of `TimeProvider`. “Today” statistics use `TimeProvider`, so tests with a fixed clock could disagree with the stored times.

**Done.** Writers pass `TimeProvider.GetUtcNow()` into the entity constructors. Covered by `Activity_log_and_system_error_times_come_from_the_injected_clock`.

- [x] Writers use `TimeProvider`
- [x] Fixed-clock tests agree with stored times

---

## Suggested order

Follow [`deploy-plan.md`](deploy-plan.md) for packaging → clean PC → service → playbook → one Setup.exe.

Remaining / deferred blocks here:

1. Block 5 packaging (open) — aligns with deploy-plan Phases 1–6.
2. Block 2 🔒 only if a clinic will use more than one PC.
3. Block 6 🔒 only if a clinic needs a logo on printouts/headers.

Done blocks: 1, 3, 4, 7, 8, 9.
