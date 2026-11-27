# Updates — API and data safety

Related: [`01-release-playbook.md`](01-release-playbook.md) · [`02-decisions.md`](02-decisions.md) · dumps in [`Docs/commands.md`](../commands.md)

---

## Database schema after an update

When Setup finishes, it restarts the **MostawsefApi** Windows service. On every start the API runs `DatabaseInitializer` → `MigrateAsync` (new EF migrations), then seeds only what is still missing.

**You do not run migrations by hand on the clinic PC.**  
If health stays unhealthy after an update, check `C:\Mostawsef\install-clinic.log` and the Windows Event Log for the API; restore from a scheduled dump if the schema change itself failed (rare — test migrations before tagging).

Patient/order data lives in PostgreSQL’s data directory. Setup never deletes that directory.

---

## Backups before a big release

Scheduled dumps (Super → Settings → **Backup folder**) should already be on for every clinic:

1. Confirm a backup folder is set and recent `.dump` files appear while the API service runs.
2. Before a **major** update (big schema change, or first remote update on that PC), prefer waiting until a fresh dump exists — or trigger one dump and confirm the file size looks normal.
3. Keep USB / previous `Mostawsef-Setup-*.exe` as rollback; restoring data uses `pg_restore` (see `Docs/commands.md`).

Day-to-day patch updates can rely on the normal schedule; do not skip setting the backup folder.

---

## Brief “server offline” during the update

While Setup stops and starts `MostawsefApi`, Desktop may show the usual **connection lost** banner for a short time. That is expected.

- The connection monitor keeps retrying; when the API is healthy again the banner clears.
- Staff should finish the in-app **Update now** flow and wait for the app to reopen — do not force-kill Setup.
- If the banner stays after several minutes, open `http://localhost:5270/api/health` and check that service `MostawsefApi` is Running.
