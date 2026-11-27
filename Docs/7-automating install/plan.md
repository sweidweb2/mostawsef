Full implementation steps (Pattern A), in order:

### 0. Decisions (lock before coding)
1. Public vs private GitHub Releases  
2. Who can update (Admin / Super only recommended)  
3. Notify-only vs force on incompatible builds  
4. Update Setup **without** Postgres redist (smaller) vs full Setup every time  
5. Feed URL shape (`latest.json` fields: version, url, sha256, notes, minApiVersion)

### 1. Versioning in the product
6. Ship a real app version in Desktop + API (Assembly/Informational version = Setup version)  
7. Expose current version in UI (About / portal) and in update compare logic  
8. Keep `ApiVersion.Current` for breaking API/desktop mismatch

### 2. Make Setup update-friendly
9. Confirm Inno upgrade path: preserve `appsettings.Clinic.json` + connection string  
10. Add silent flags path (`/VERYSILENT` / `/SUPPRESSMSGBOXES` as you choose) for in-app launch  
11. Optional: “update” build that skips Postgres redist  
12. Write Setup exit codes / log location (`install-clinic.log`) so Desktop can report failure  
13. Decide relaunch: Setup starts Desktop after success, or Desktop relaunches itself

### 3. CI release pipeline
14. Add GitHub Actions workflow: `on: push tags: ['v*']` on a **Windows** runner  
15. Install/cache Inno + Postgres redist (or skip redist for update builds)  
16. Run `build-setup.ps1 -Version` from the tag  
17. Compute checksum of the Setup exe  
18. Create GitHub Release with `Mostawsef-Setup-{ver}.exe` + `latest.json`  
19. Fail the job if artifact/json missing (no half releases)  
20. Keep existing `tests.yml` as a gate (require green tests before/alongside release)

### 4. Desktop — check for updates
21. Add update feed client (HTTP GET `latest.json`, timeout, no crash offline)  
22. Compare feed version vs running version  
23. Show banner/dialog when newer (and user role allowed)  
24. Handle: already latest, feed errors, invalid json, newer-than-feed  
25. Cache last check time (don’t hammer the network)  
26. Add strings EN + AR

### 5. Desktop — download + install
27. “Update” command: download Setup to a temp/cache folder with progress  
28. Verify sha256 before run  
29. Cancel / single-flight (no double download)  
30. Warn if dirty form / open work; then close Desktop cleanly  
31. Launch Setup elevated; handle UAC cancel → stay on old version + message  
32. While installing: block second instance / “Updating…” if user reopens  
33. After success: open new Desktop; clear “update available” state  
34. After failure: keep old app usable; show log hint

### 6. API / data safety
35. Confirm migrations are safe on API service restart after Setup  
36. Rely on scheduled backups before big releases; document “backup then update”  
37. Health check after update; Desktop treats unreachable API as Offline (already)

### 7. Tests
38. Unit/VM tests: version compare, feed parse errors, role gating, cancel/UAC paths (faked)  
39. Integration/CI: release workflow dry-run or artifact presence check  
40. Manual smoke on a spare PC: old → notify → download → silent Setup → sign-in → data intact

### 8. Docs / ops
41. Write release playbook: tag → wait for Release → clinic updates  
42. Document USB fallback  
43. Document rollback (reinstall previous Setup from Releases)  
44. Update `deploy-plan` / clinic playbook with auto-update section  
45. Walk the `edge cases.md` list and tick what you handled vs deferred

### Suggested ship order
- **MVP:** steps 0–1, 3, 4, 8 (CI + notify; user runs Setup manually once)  
- **Full Pattern A:** add 2, 5, 6, 7  
- **Later:** Pattern B only if size/speed hurts  

I can turn this into a file under `Docs/7-automating install/` if you want it saved next to your edge cases.