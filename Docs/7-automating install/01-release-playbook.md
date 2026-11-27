# Release playbook (ship a new Mostawsef version)

**Start here** for day-to-day releases. Deeper detail: [`02-decisions.md`](02-decisions.md) · [`03-data-safety.md`](03-data-safety.md) · [`04-smoke-checklist.md`](04-smoke-checklist.md) · [`Docs/commands.md`](../commands.md)

---

## Normal release (in-app update)

1. Finish work on `dev` → PR / merge to `main` (tests green).
2. Confirm Super backup folder is set on clinics that will update soon (major releases: prefer a fresh dump — [`03-data-safety.md`](03-data-safety.md)).
3. Tag and push (semver with `v`):

   ```powershell
   git checkout main
   git pull
   git tag v1.2.0
   git push origin v1.2.0
   ```

4. Wait for GitHub Actions **Release** (API + Desktop tests → `-UpdateOnly` Setup → Release assets).
5. Confirm the Release has `Mostawsef-Setup-1.2.0.exe` and `latest.json`.
6. Clinic: open Mostawsef → sign in → **Update now** (UAC) → app reopens on the new version.

Feed URL (app already uses this):  
`https://github.com/sweidweb2/mostawsef/releases/latest/download/latest.json`

---

## First install (new clinic PC)

1. Build a **full** Setup (Postgres bundled): `./scripts/build-setup.ps1` (no `-UpdateOnly`), or download a full Setup if you keep one for USB.
2. Run Setup **as Administrator** on the clinic PC.
3. Follow [`clinic-install-playbook.md`](../3-Follow-ups/clinic-install-playbook.md): change passwords, set backup folder, smoke test.

After that, day-to-day updates use the in-app path above.

---

## USB fallback (offline or GitHub blocked)

1. On the build PC: `./scripts/build-setup.ps1 -UpdateOnly -Version X.Y.Z` (or full Setup for a brand-new PC).
2. Copy `Mostawsef-Setup-X.Y.Z.exe` to USB.
3. On the clinic: run Setup as Administrator.  
   Postgres data and clinic config are kept on upgrade.

---

## Rollback

1. Download the previous `Mostawsef-Setup-*.exe` from [GitHub Releases](https://github.com/sweidweb2/mostawsef/releases) (or from your Builds / USB archive).
2. Run it as Administrator over the current install.
3. Postgres data is **not** deleted. If a new migration already ran and the old app cannot start, restore a dump from the backup folder (`Docs/commands.md`) then install the older Setup.

---

## If install behavior itself changes

Paths, service name, Postgres rules, or scripts under `packaging/innosetup/` / `scripts/install-*.ps1`: update those first, rebuild Setup, then tag. Clinics still only run Setup or **Update now**.
