# Automating install — Phase 0 decisions (locked)

Locked for Pattern A (CI + notify + update Setup).  
Related: [`steps.md`](steps.md) · [`plan.md`](plan.md) · [`edge cases.md`](edge%20cases.md)

| Id | Topic | Decision |
| --- | --- | --- |
| **0.1** | Where builds live | **Public** GitHub Releases |
| **0.2** | Who can update | **Any signed-in user** can start Update (Windows UAC still required to install) |
| **0.3** | Notify vs force | **Notify only** — never force mid-order; no hard-force for now |
| **0.4** | Package shape | **Update Setup** — omit Postgres redist (clinic already has Postgres). First install still uses full Setup with redist |
| **0.5** | Version feed | `latest.json` on each Release; app reads the stable “latest” download URL (below) |

### Phase 1 (versioning) — locked while implementing

| Topic | Decision |
| --- | --- |
| Where version shows | **Login screen** (caption under forgot-password) — visible before sign-in for support |
| Starting semver | **`1.0.0`** in `Directory.Build.props`; Setup publish overrides with `-p:Version=` from `build-setup.ps1` |
| Protocol version | Keep existing `ApiVersion.Current` (not in the update feed for v1) |

---

## 0.5 Feed contract

**Stable URL (Desktop always GETs this):**

```text
https://github.com/sweidweb2/mostawsef/releases/latest/download/latest.json
```

(No version in the path — GitHub resolves “latest”.)

**`latest.json` shape:**

```json
{
  "version": "1.2.0",
  "url": "https://github.com/sweidweb2/mostawsef/releases/download/v1.2.0/Mostawsef-Setup-1.2.0.exe",
  "sha256": "lowercase-hex-of-the-exe",
  "notesEn": "Mostawsef 1.2.0",
  "notesAr": "مستوصف 1.2.0"
}
```

| Field | Role |
| --- | --- |
| `version` | Semver of the Setup; compare to running Desktop/API version |
| `url` | Direct download of that Release’s update Setup exe |
| `sha256` | Verify file before running Setup |
| `notesEn` / `notesAr` | Text for the notify banner (bilingual UI) |

**Not in v1 feed:** `minApiVersion` / force-update — add only if notify-only is not enough later.

**CI must upload on every `v*` tag Release:**

1. `Mostawsef-Setup-{version}.exe` (update build: no Postgres redist)
2. `latest.json` with matching `version`, `url`, `sha256`, notes

**App compare rule:** if feed `version` > installed version → show notify; else do nothing. Offline / bad feed → no crash, no spam.

---

## Phase 2 (Setup update-friendly) — locked

| Topic | Decision |
| --- | --- |
| Config on upgrade | Already preserved (Inno backup + `install-clinic` restore). Smoke on a real PC when convenient. |
| Silent flags (in-app Update) | See command below |
| Release artifact | `./scripts/build-setup.ps1 -UpdateOnly` (no Postgres redist). USB/first install: omit `-UpdateOnly` |
| Failure signal | `install-clinic.ps1` exits `1` on failure; log at `{InstallRoot}\install-clinic.log` (default `C:\Mostawsef\install-clinic.log`) |
| Relaunch | **Desktop relaunches itself** after a successful silent update. Setup must **not** auto-start the app when silent (`skipifsilent` on Launch). |

### Silent update command (Phase 5 will run this elevated)

```text
Mostawsef-Setup-{version}.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS
```

| Flag | Why |
| --- | --- |
| `/VERYSILENT` | No wizard UI |
| `/SUPPRESSMSGBOXES` | No message boxes |
| `/NORESTART` | Do not reboot mid-update |
| `/CLOSEAPPLICATIONS` | Let Inno close locked app files (`CloseApplications=force` in the .iss) |

Before launch: Desktop should exit cleanly so files unlock. After success: Desktop starts itself again (not Setup).

---

## Phase 3 (CI release) — locked

| Topic | Decision |
| --- | --- |
| Trigger | Push tag `v1.2.3` (semver with `v` prefix) |
| Workflow | `.github/workflows/release.yml` |
| Gate | API + Desktop tests must pass, then build |
| Artifact | `build-setup.ps1 -UpdateOnly` → `Mostawsef-Setup-{version}.exe` + `latest.json` |
| Publish | Public GitHub Release on the same tag |

**How you ship a build:** merge to `main` → `git tag v1.2.0` → `git push origin v1.2.0` → wait for Actions → Release appears with Setup + feed.

---

## Phase 4 (notify UI) — locked

| Topic | Decision |
| --- | --- |
| When | On portal open (after sign-in) |
| Who | Any signed-in user |
| UI | Info banner above the page + **Download update** |
| Action (MVP) | Opens Setup URL in the browser; user runs Setup manually |
| Offline | No banner, no error spam |
| Throttle | At most one feed check every 6 hours (unless forced in tests) |
