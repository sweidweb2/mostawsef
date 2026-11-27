# Automating install — locked decisions

Pattern A: CI Release + in-app notify + silent update Setup.  
Day-to-day ship: [`01-release-playbook.md`](01-release-playbook.md) · edge status: [`05-edge-cases.md`](05-edge-cases.md)

| Id | Topic | Decision |
| --- | --- | --- |
| **0.1** | Where builds live | **Public** GitHub Releases |
| **0.2** | Who can update | **Any signed-in user** can start Update (Windows UAC still required) |
| **0.3** | Notify vs force | **Notify only** — never force mid-order |
| **0.4** | Package shape | **Update Setup** omits Postgres redist. First install still uses full Setup |
| **0.5** | Version feed | `latest.json` on each Release; app reads the stable “latest” URL below |
| Version UI | Where it shows | Login screen; starting semver `1.0.0` in `Directory.Build.props` |
| Protocol | API mismatch | Keep `ApiVersion.Current` (not in the update feed for v1) |
| Silent flags | In-app Update | `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS` |
| Relaunch | After silent Setup | Desktop relaunches itself (`--apply-update`); Setup Launch is `skipifsilent` |
| Failure | Install script | `install-clinic.ps1` exits `1`; log at `C:\Mostawsef\install-clinic.log` |
| CI | Trigger | Push/merge to `main` → `.github/workflows/release.yml` (tests gate, version from `Directory.Build.props`, tag `vX.Y.Z`, then `-UpdateOnly`) |
| Notify UI | When / who | After sign-in; any role; 6-hour throttle; quiet if offline |
| Install | In-app | Download → sha256 → elevated silent Setup; `update.lock` if reopened mid-update |
| Data | Schema / backups | Migrations on API start; never wipe Postgres data; prefer dump before major updates |
| Later | Pattern B | Only if Setup size/time hurts |

---

## Feed contract (`0.5`)

**Stable URL:**

```text
https://github.com/sweidweb2/mostawsef/releases/latest/download/latest.json
```

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
| `version` | Semver; compare to running Desktop version |
| `url` | Direct download of that Release’s update Setup |
| `sha256` | Verify before running Setup |
| `notesEn` / `notesAr` | Banner text |

**CI uploads every main merge Release:** `Mostawsef-Setup-{version}.exe` + matching `latest.json` (and creates tag `v{version}`).  
**Compare:** feed `version` > installed → notify; else nothing. Offline / bad feed → no crash.

---

## Rule of thumb

Never leave Desktop/API half-updated; never wipe clinic config; failed update → stay on old version.
