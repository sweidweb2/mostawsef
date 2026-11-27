# Automating install — step-by-step

How to go from USB Setup installs to **cloud CI + in-app update** (Pattern A: download Setup, run it).

Related: [`decisions.md`](decisions.md) (Phase 0 locked) · [`plan.md`](plan.md) · [`edge cases.md`](edge%20cases.md) · [`to enter later.md`](to%20enter%20later.md) · clinic playbook in `Docs/3-Follow-ups/`

**Branch:** do this work on `dev`.  
**Rule of thumb:** never leave Desktop/API half-updated; never wipe clinic config; failed update → stay on old version.

---

## How to use this doc

1. Phase 0 is **locked** in [`decisions.md`](decisions.md) — follow those choices.
2. Ship in order: **MVP → Full Pattern A → later**.
3. After each phase, tick the steps and skim [`edge cases.md`](edge%20cases.md) for what you handled vs deferred.

### Ship phases

| Phase | Goal | Steps below |
| --- | --- | --- |
| **MVP** | Tag → GitHub Release; app shows “Update available”; user downloads/runs Setup (no silent install yet) | 0 → 1 → 3 → 4 → 8 |
| **Full Pattern A** | In-app Download + Update closes app, runs Setup elevated, relaunches | Add 2 → 5 → 6 → 7 |
| **Later** | Slimmer file-swap updater (Pattern B) only if Setup size/time hurts | Not in this doc |

---

## Phase 0 — Lock decisions ✅

**Locked** — full text in [`decisions.md`](decisions.md). Summary:

| Id | Decision |
| --- | --- |
| 0.1 | Public GitHub Releases |
| 0.2 | Any signed-in user can start Update (UAC still required) |
| 0.3 | Notify only |
| 0.4 | Update Setup (no Postgres redist); first install still full Setup |
| 0.5 | `latest.json` via `.../releases/latest/download/latest.json` (`version`, `url`, `sha256`, `notesEn`, `notesAr`) |

---

## Phase 1 — Versioning in the product ✅

Done on `dev`:

- `Directory.Build.props` → `<Version>1.0.0</Version>`
- `AppVersion.Current` on Desktop; login shows `Version {0}` / `الإصدار {0}`
- `build-setup.ps1` publishes with `-p:Version=$Version`
- `ApiVersion.Current` unchanged (protocol mismatch banner only)

---

## Phase 2 — Make Setup update-friendly ✅

Done on `dev` (details in [`decisions.md`](decisions.md)):

- **2.1** Config preserve already in Inno + `install-clinic` (smoke when you can)
- **2.2** Silent flags locked: `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS`
- **2.3** `./scripts/build-setup.ps1 -UpdateOnly` — no Postgres redist; Inno `skipifsourcedoesntexist` on redist
- **2.4** `install-clinic.ps1` → `exit 1` + `C:\Mostawsef\install-clinic.log`
- **2.5** Desktop relaunches itself; Setup Launch entry is `skipifsilent`

---

## Phase 3 — CI release pipeline

### Step 3.1 — Release workflow
- **Do:** Add `.github/workflows/release.yml` (or similar).
- **Trigger:** `push` of tags `v*` on a **Windows** runner.
- **Done when:** Pushing `v1.2.0` starts the job.

### Step 3.2 — Build Setup on the runner
- **Do:** Install/cache Inno Setup; reuse Postgres cache or skip redist for update builds.
- **Do:** Run `./scripts/build-setup.ps1 -Version` derived from the tag (`v1.2.0` → `1.2.0`).
- **Done when:** Job produces `Mostawsef-Setup-{version}.exe`.

### Step 3.3 — Checksum + feed
- **Do:** Compute `sha256` of the Setup.
- **Do:** Write `latest.json` with Step 0.5 fields.
- **Done when:** Both files exist in the job artifacts.

### Step 3.4 — Publish GitHub Release
- **Do:** Create/upload Release assets: Setup exe + `latest.json`.
- **Do:** Fail the job if either is missing (no half releases).
- **Done when:** Opening the Release in the browser shows both files.

### Step 3.5 — Gate with tests
- **Do:** Keep `tests.yml`; require green tests before trusting a release (same tag job needs / workflow_run / manual discipline — pick one and document it).
- **Done when:** You will not ship a Setup from a red test run by accident.

---

## Phase 4 — Desktop: check for updates (MVP UI)

### Step 4.1 — Feed client
- **Do:** HTTP GET `latest.json` with timeout; never crash if offline.
- **Done when:** Offline clinic behaves as today (no banner, no errors spam).

### Step 4.2 — Compare versions
- **Do:** Compare feed `version` to running app version.
- **Handle:** already latest; invalid JSON; feed older than app (dev build).
- **Done when:** Unit/VM tests cover compare + parse errors.

### Step 4.3 — Notify UI
- **Do:** Banner/dialog when newer **and** role is allowed (Step 0.2).
- **Do:** EN + AR strings.
- **MVP action:** button opens download URL / starts browser download (user runs Setup manually).
- **Done when:** On a PC with an older build, signing in shows the update affordance.

### Step 4.4 — Throttle checks
- **Do:** Cache last check time; don’t hit the network every few seconds.
- **Done when:** Opening pages repeatedly does not flood GitHub.

---

## Phase 5 — Desktop: download + install (Full Pattern A)

### Step 5.1 — Download Setup in-app
- **Do:** “Update” downloads to temp/cache with progress.
- **Do:** Cancel + single-flight (no double download).
- **Done when:** Progress is visible; cancel leaves app usable.

### Step 5.2 — Verify checksum
- **Do:** Compare file `sha256` to feed before running.
- **Done when:** Corrupt download never launches Setup.

### Step 5.3 — Leave the app cleanly
- **Do:** If dirty form / open work → warn, then close Desktop before Setup.
- **Done when:** Install does not fight locked Desktop files.

### Step 5.4 — Launch Setup elevated
- **Do:** Start Setup with silent flags (Step 2.2).
- **Do:** UAC cancel → stay on old version + clear message.
- **Done when:** Accepting UAC runs install; denying does not break the old app.

### Step 5.5 — Mid-update reopen
- **Do:** Block second instance or show “Updating… please wait” if user relaunches during Setup.
- **Done when:** Reopen mid-install does not corrupt files.

### Step 5.6 — Success / failure
- **Success:** New Desktop opens; clear “update available”; sign-in works; data intact.
- **Failure:** Old app still runs; show log hint (`install-clinic.log`).
- **Done when:** Both paths are manually smoke-tested once.

---

## Phase 6 — API / data safety

### Step 6.1 — Migrations on service restart
- **Do:** Confirm new API applies migrations on `MostawsefApi` start after Setup (same as today).
- **Done when:** Post-update health is OK and schema matches expectations.

### Step 6.2 — Backups before big releases
- **Do:** Document: ensure scheduled backup folder is set; prefer backup before major updates.
- **Done when:** Release playbook mentions it.

### Step 6.3 — Health after update
- **Do:** Rely on existing health + Offline banner if API is down briefly during service restart.
- **Done when:** Short Offline blip is expected/understood, not a stuck error.

---

## Phase 7 — Tests

### Step 7.1 — Desktop tests
- **Do:** Version compare, feed errors, role gating, cancel / UAC-denied paths (faked HTTP / no real Setup).
- **Done when:** `./scripts/test.ps1 desktop` covers the new VM/services.

### Step 7.2 — Release confidence
- **Do:** At least: workflow must fail if Setup/`latest.json` missing; optional dry-run.
- **Done when:** A broken publish cannot look “green”.

### Step 7.3 — Manual smoke (spare PC)
1. Install old Setup.
2. Publish newer tag to a test Release (or point feed at a test URL).
3. Open app → see update.
4. MVP: download + run Setup by hand. Full A: in-app Update.
5. Sign in → patients/orders still there → version UI shows new number.
6. Kill network → no crash; USB old Setup still installable as fallback.

- **Done when:** Checklist above passes once.

---

## Phase 8 — Docs / ops

### Step 8.1 — Release playbook
- **Do:** Short doc: change code on `dev` → PR/merge → tag `vX.Y.Z` → wait for Release → clinic updates.
- **Done when:** You can release without rereading this whole folder.

### Step 8.2 — USB fallback + rollback
- **Do:** USB still works offline.
- **Do:** Rollback = install previous `Mostawsef-Setup-*.exe` from Releases (Postgres data kept).
- **Done when:** Both are one paragraph in the playbook.

### Step 8.3 — Wire into existing install docs
- **Do:** Add an auto-update section to clinic playbook / deploy-plan.
- **Done when:** First-install vs update paths are both documented.

### Step 8.4 — Edge-case pass
- **Do:** Walk [`edge cases.md`](edge%20cases.md); mark handled vs deferred.
- **Done when:** Deferred items are conscious, not forgotten.

---

## Suggested working order (checklist)

Copy and tick as you go:

**MVP**
- [x] Phase 0 decisions locked → [`decisions.md`](decisions.md)
- [x] Phase 1 versioning (login shows semver; props + publish stamp)
- [ ] Phase 3 CI Release + `latest.json`
- [ ] Phase 4 notify UI (browser/manual Setup OK)
- [ ] Phase 8 playbook + USB note
- [ ] Manual smoke (MVP)

**Full Pattern A**
- [x] Phase 2 Setup update-friendly (silent flags, `-UpdateOnly`, app relaunches itself)
- [ ] Phase 5 in-app download/install/reopen/fail paths
- [ ] Phase 6 backup/migration notes
- [ ] Phase 7 tests + full smoke
- [ ] Edge cases reviewed

**Later**
- [ ] Pattern B only if needed

---

## What you do on a normal release (after this exists)

1. Finish work on `dev`, merge to `main` when ready.
2. Tag: `git tag v1.2.0` → push tag.
3. Wait for GitHub Actions Release (Setup + `latest.json`).
4. Clinic opens app → Update (MVP: run Setup; Full A: one click).
5. If something is wrong: install previous Setup from Releases (rollback).
