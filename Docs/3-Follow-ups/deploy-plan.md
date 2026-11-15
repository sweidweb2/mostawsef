# Deploy plan — from here to a clinic install

Step-by-step path until a **one-PC clinic** can run Mostawsef from a single installer.

Related: open packaging work in [`deploy-install.md`](deploy-install.md) (especially Block 5). Publish commands: [`Docs/commands.md`](../commands.md).

## Product decisions (locked for v1)

| Decision | Choice |
| --- | --- |
| Target | One Windows PC (API + Desktop + database on the same machine) |
| Installer | **One** installer for the client (Desktop + API; PostgreSQL installed or driven by that same install flow) |
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
| 2 | Native PostgreSQL on a clean / spare PC | ☐ |
| 3 | Manual smoke: published API + Desktop end-to-end | ☐ |
| 4 | API as Windows service (survives reboot) | ☐ |
| 5 | Clinic install playbook (human checklist) | ☐ |
| 6 | One Setup.exe (Velopack or similar) + optional GitHub Releases | ☐ |

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

## Phase 2 — Native PostgreSQL on a clean / spare PC

**Goal:** A machine that behaves like a clinic PC (no Docker for Mostawsef).

**Steps**

1. Install **PostgreSQL 17** natively on Windows
2. Create a database and user for Mostawsef
3. Put the connection string in the **published** API config next to the API (clinic Production config — not the Docker dev string)
4. Confirm you can connect with `psql` / pgAdmin

**Notes**

- PostgreSQL stores its data in its own data directory, not inside the Mostawsef install folder
- Client PCs never use Docker for this product

**Done when**

- Native Postgres is up and the connection string for the published API is ready

---

## Phase 3 — Manual smoke test (publish folders)

**Goal:** Prove a clinic can run without an installer yet.

**Steps (on the clean / spare PC)**

1. Install PostgreSQL (Phase 2) if not already done
2. Copy the published **API** folder onto the PC
3. Start the API once (Production)
4. Confirm first-run: clinic JWT file + install Super/Admin users (see `Docs/4-Business/auth.md`)
5. Copy the published **Desktop** folder onto the PC
6. Start the Desktop → sign in → create a patient (or equivalent real workflow)
7. Confirm API stays reachable at `http://localhost:5270`

**Done when**

- Clean machine, no Docker, no Dev config: API starts → first-run → Desktop sign-in → real work

Until Phase 6 exists, you *could* install a friendly first client this same way (copy folders + Postgres). Prefer finishing Phase 4–5 first.

**Ticks** ([`deploy-install.md` Block 5](deploy-install.md))

- [x] Publish layout reviewed once ✅
- [ ] Clean-machine install smoke test passed

---

## Phase 4 — API always on after reboot

**Goal:** Staff do not open a console to start the API.

**Steps**

1. Register the published API as a **Windows service** (or equivalent always-on host)
2. Service starts **after** PostgreSQL
3. Reboot the PC
4. Confirm Desktop can sign in with no manual API start

**Done when**

- Reboot → API is up → Desktop works

---

## Phase 5 — Clinic install playbook

**Goal:** A short checklist you follow on site so the second clinic is the same as the first.

Write it from what you actually did in Phases 2–4. Suggested outline:

1. [ ] Install PostgreSQL 17 (native)
2. [ ] Create DB / user; note connection string
3. [ ] Install / place API; set connection string
4. [ ] Install / start API Windows service
5. [ ] Install / place Desktop; shortcut for staff
6. [ ] Sign in with install Super/Admin → **change passwords**
7. [ ] Super Settings: set backup folder (scheduled `pg_dump`)
8. [ ] Smoke: login + one patient / order
9. [ ] Confirm: no Docker, localhost API only

**Done when**

- You can install another one-PC clinic from the playbook without guessing

Keep the playbook next to this file when you write it (e.g. `Docs/3-Follow-ups/clinic-install-playbook.md`) or paste the final list into Phase 5 here.

---

## Phase 6 — One Setup.exe (and optional release CI)

**Start only after Phase 3 (and ideally 4–5) work by hand.**

**Goal:** One installer the client runs. Same product decisions as above.

**Steps**

1. Choose packaging (candidate already named in docs: **Velopack**)
2. Package **Desktop + API**; install/configure **PostgreSQL** in the same install flow (or a clear step the installer drives — still one client-facing Setup)
3. Build `Setup.exe` (or equivalent)
4. Test on a clean PC: install → first-run → sign-in → create data
5. Document reinstall/update: new Setup replaces app files; **never** delete or replace the PostgreSQL data directory
6. Optional later: GitHub Actions job that publishes and uploads the Setup to GitHub Releases (CI today stays tests-only until you add this)

**Updates (v1)**

- No in-app auto-update
- You bump the version, rebuild Setup, send it to the clinic, they reinstall
- Clinic data stays in PostgreSQL

**Done when**

- [ ] Installer / update method chosen ([`deploy-install.md` Block 5](deploy-install.md))
- [ ] One clean-machine install from Setup.exe succeeds
- [ ] Reinstall with a newer Setup does not wipe the database

---

## Out of scope for this plan (later)

- Multi-PC / LAN API address (`deploy-install.md` Block 2)
- HTTPS for remote desktops (Block 4 option B)
- Clinic logo (Block 6)
- In-app automatic updates

---

## Suggested order this week

1. Phase 1 — publish both projects ✅  
2. Phase 2–3 — spare PC / VM smoke test  
3. Phase 4 — Windows service  
4. Phase 5 — write the playbook from reality  
5. Phase 6 — one Setup.exe only after the manual path is boring and reliable  
