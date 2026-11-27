Edge cases to plan for across the whole pipeline:

### CI / release
- Tag build fails midway (partial Release / missing Setup)
- Wrong/missing `latest.json` (bad version, bad URL, no checksum)
- Duplicate tag / republish same version
- Windows runner can’t find Inno / Postgres cache
- Accidental publish of a Debug or Dev-seeded build
- Release is public when builds should stay private

### Version check (Desktop)
- No internet / DNS fail / timeout
- Feed 404 / 5xx / HTML instead of JSON
- Invalid or untrusted JSON
- Clock skew / weird version strings
- App already on latest (or newer than feed — test build)
- Check runs too often / while offline forever
- Proxy / clinic firewall blocks GitHub
- Check while signed out vs signed in (who is allowed to see/update)

### Download
- Slow link / multi‑GB Setup on weak clinic internet
- Download interrupted / resume vs restart
- Disk full
- Checksum / signature mismatch (corrupt or tampered)
- Temp folder cleaned by AV mid-download
- Antivirus quarantines the Setup.exe
- User cancels download
- Second click while already downloading

### Before install / UX
- User mid-order / dirty form / open dialog
- Non-Admin clicks Update (UAC denied or no rights)
- Only Admin/Super should update — Receptionist clicked
- App minimized / locked screen during download
- Multiple Desktop windows / second instance
- API unhealthy while offering update
- Forced update vs optional (old incompatible build)

### Install (Setup / service)
- UAC cancelled → stay on old version, clear message
- Setup already running / previous Setup stuck
- `MostawsefApi` won’t stop (file lock)
- Desktop still running → files locked
- Reopen Desktop while Setup is running
- Silent Setup fails (log path, exit code)
- Power loss / reboot mid-Setup
- Upgrade preserves `appsettings.Clinic.json` + connection string — verify restore failed case
- Postgres missing/broken on update (should not reinstall blindly)
- Wrong install path / non-default `C:\Mostawsef`
- Insufficient disk for extract
- Pending Windows reboot from earlier Postgres install

### After install / re-enter
- Setup finished but Desktop not relaunched
- User opens old shortcut / wrong folder
- New Desktop vs old API still running (version skew)
- API starts but migrations fail
- API starts, health fails, Desktop shows Offline
- JWT / clinic config lost → can’t sign in
- Session from old version still in memory (should be fresh launch)
- “Update available” still shows after successful update (stale cache)
- Partial update: Desktop new, API old (or reverse)
- Crash loop on new version → need rollback / keep previous Setup

### Data / clinic ops
- Update during backup job / `pg_dump`
- Migration incompatible with restore from older dump
- Maintenance mode on during update
- Multi-user same PC (one updates, other still in old app)
- Offline for days then sudden big jump across many versions

### Product / policy
- Skip this version / remind tomorrow
- Mandatory update blocks work too hard
- Release notes empty / Arabic+English
- Audit: who ran the update (activity log)
- USB fallback still works if cloud path dies

If you want one rule of thumb while building: **never leave the clinic on a half-updated pair (Desktop/API), never wipe clinic config, and always have a clear “failed → still on old version” path.**


Usual flow:

Click Update — he stays in the app while it downloads (progress in a dialog/banner); work can continue until install starts.
Install starts — Desktop closes (or asks him to finish / closes itself), Setup runs elevated, replaces files, restarts the API service.
If he reopens mid-update — old Desktop may fail or fight file locks; best UX is “Updating… please wait” or block reopen until Setup finishes.
When done — he opens (or Setup relaunches) the new Desktop; API is already on the new build; DB migrations run on API start; clinic data/config stay; he signs in as usual on the new version.
So: stay in app during download → briefly out for install → back in on the new version.