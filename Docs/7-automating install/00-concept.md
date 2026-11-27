# Cloud release + in-app update — concept (book summary)

How Mostawsef moved from **hand-carrying a USB installer** to a **cloud build + clinic self-update** loop — without turning the product into a typical single-exe auto-updater.

General / academic framing (no product names): [`00-concept-general.md`](00-concept-general.md) · day-to-day ops: [`01-release-playbook.md`](01-release-playbook.md) · locked choices: [`02-decisions.md`](02-decisions.md)

---

## The problem

Mostawsef on a clinic PC is not one program. It is:

| Piece | What it is |
| --- | --- |
| Desktop | Avalonia app staff use |
| API | Windows service (`MostawsefApi`) |
| Config | Connection string + `appsettings.Clinic.json` (must survive upgrades) |
| Database | Native PostgreSQL — data must never be wiped by an app update |

USB worked, but every change meant: build Setup → travel or send a stick → run elevated install. The goal was to keep the **same install story**, and stop the travel.

---

## The idea in one sentence

**CI builds a smaller “update” Setup, publishes it to GitHub Releases with a tiny version feed; the Desktop notices a newer version, downloads that Setup, runs it silently (Admin/UAC), and reopens — Postgres data and clinic config stay.**

That approach is **Pattern A** (reuse the real installer).  
A lighter “swap Api + Desktop folders” updater (**Pattern B**) is reserved for later, only if Setup size or time becomes painful. Classic desktop-only tools (Velopack-style) were a poor fit because they do not own a Windows service + Postgres cleanly.

---

## What types of delivery exist

| Type | When | What runs on the clinic |
| --- | --- | --- |
| **Full Setup** | First install (new PC) | Inno Setup + Postgres redist; creates service, folders, shortcuts |
| **Update Setup** | Later versions (online or USB) | Same Inno path, **no** Postgres redist; replaces app files; restores clinic config |
| **In-app Update** | PC online + signed in | Desktop checks feed → **Update now** → download → silent Update Setup → relaunch |
| **USB fallback** | Offline / GitHub blocked | Same Update (or Full) Setup, run by hand as Admin |
| **Rollback** | Bad release | Install previous Setup from Releases / archive; data kept |

All update paths share one rule: **never delete the PostgreSQL data directory; never wipe clinic JWT/config on purpose.**

---

## The pipeline (general)

```
Work on dev
    → merge to main (only when you mean to ship)
        → GitHub Actions: API + Desktop tests
            → bump version (auto patch from last v*.*.* tag)
            → build Update Setup (-UpdateOnly)
            → upload Mostawsef-Setup-{ver}.exe + latest.json
            → GitHub Release (public)
                → clinic Desktop polls latest.json
                    → banner if feed version > installed
                        → user Update now (UAC)
                            → silent Setup → API restarts → migrations → Desktop back
```

**Trigger:** push/merge to `main` (release branch). Not every commit on `dev`.  
**Artifacts:** update installer + `latest.json` (`version`, `url`, `sha256`, bilingual notes).  
**Feed URL (stable):** `…/releases/latest/download/latest.json`  
**App compare:** newer semver on the feed → notify; offline/bad feed → quiet, no spam.  
**Install UX:** notify-only (never force mid-order); any signed-in user can start Update; Windows UAC still required.

---

## What the user experiences

1. Stays in the app while the Setup **downloads** (progress; can cancel).  
2. Leaves briefly for **install** (Desktop exits; elevated silent Setup; API service restarts).  
3. If they reopen mid-install → short “Updating…” wait (`update.lock`).  
4. Comes back on the **new** version; signs in as usual.  
5. On failure / UAC deny → **old version still works**.

Schema changes apply when the new API starts (`MigrateAsync`). A short “connection lost” banner during the service restart is expected.

---

## When this works well

- One Windows PC per clinic (API + Desktop + Postgres local).  
- Clinics that can reach GitHub Releases (or accept USB when they cannot).  
- You already trust Inno + `install-clinic` to preserve config and leave Postgres alone.  
- Releases are deliberate merges to `main`, with tests as the gate.  
- Backup folder is set before major upgrades.

## When it is not enough (yet)

- No internet and no USB → no update.  
- Force-update / block-old-clients → not in v1 (notify only).  
- Private / signed feeds → public Releases for now; protect the GitHub account.  
- Very large/slow Setup downloads → consider Pattern B later.  
- Jumping across a **reset migration history** needs a one-time DB baseline (data is not wiped, but `MigrateAsync` can fail until history is aligned).

---

## Design principles (keep these)

1. **One product version** for Desktop + API Setup (semver); separate `ApiVersion` only for protocol mismatch.  
2. **Same installer for cloud and USB** — the cloud only hosts what USB already knew how to run.  
3. **Failed update → stay on old build**; never half-update Desktop vs API by design.  
4. **Clinic data ≠ app files** — Postgres and clinic config outlive Setup.  
5. **Ship from CI after tests** — no hand-built “mystery” Release as the happy path.

---

## Pointers in this repo

| Doc | Role |
| --- | --- |
| This file | Concept for book / onboarding |
| [`01-release-playbook.md`](01-release-playbook.md) | How you ship tomorrow |
| [`02-decisions.md`](02-decisions.md) | Locked product/tech choices + feed shape |
| [`03-data-safety.md`](03-data-safety.md) | Backups, migrations, offline blip |
| [`04-smoke-checklist.md`](04-smoke-checklist.md) | Spare-PC verification |
| [`05-edge-cases.md`](05-edge-cases.md) | Handled vs deferred |
| `Docs/3-Follow-ups/clinic-install-playbook.md` | First clinic install |
| `.github/workflows/release.yml` | The CI pipeline |
| `scripts/build-setup.ps1` | Local / CI Setup build (`-UpdateOnly`) |
