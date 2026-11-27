# Release playbook (ship a new Mostawsef version)

**Start here** for day-to-day releases. Deeper detail: [`02-decisions.md`](02-decisions.md) · [`03-data-safety.md`](03-data-safety.md) · [`04-smoke-checklist.md`](04-smoke-checklist.md) · [`Docs/commands.md`](../commands.md)

---

## Normal release (in-app update)

`main` is release-only: merge there only when you intend to ship. CI publishes the Release on that push.

1. On `dev`, bump `<Version>` in `Directory.Build.props` (e.g. `1.0.0` → `1.0.1`) — must be a new semver not already tagged.
2. Finish work on `dev` → PR / merge to `main` (Tests green on `dev` first).
3. Confirm Super backup folder is set on clinics that will update soon (major releases: prefer a fresh dump — [`03-data-safety.md`](03-data-safety.md)).
4. Wait for GitHub Actions **Release** on `main` (API + Desktop tests → `-UpdateOnly` Setup → creates tag `vX.Y.Z` → Release assets).
5. Confirm the Release has `Mostawsef-Setup-X.Y.Z.exe` and `latest.json`.
6. Clinic: open Mostawsef → sign in → **Update now** (UAC) → app reopens on the new version.

Feed URL (app already uses this):  
`https://github.com/sweidweb2/mostawsef/releases/latest/download/latest.json`

The app only accepts a feed whose `url` is `https://github.com/sweidweb2/mostawsef/releases/download/...`
(`UpdateChecker.AllowedDownloadPrefix`); any other host, `http`, a port or user info hides the update. If the repository
moves or is renamed, change that constant (and `DefaultFeedUrl`) in a release first. The SHA-256 from the feed is checked
after the download and again by the second process, which keeps the file locked until Setup exits. The feed is not signed
yet, so whoever can publish a Release on this repository can ship a Setup: keep that account protected (2FA).

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

Paths, service name, Postgres rules, or scripts under `packaging/innosetup/` / `scripts/install-*.ps1`: update those first, bump version, merge to `main`. Clinics still only run Setup or **Update now**.
