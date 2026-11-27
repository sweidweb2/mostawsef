# Edge cases — handled vs deferred

Update this when update behavior changes. Decisions: [`02-decisions.md`](02-decisions.md).

| Area | Item | Status |
| --- | --- | --- |
| CI | Missing Setup / `latest.json` | **Handled** — release job throws; `fail_on_unmatched_files` |
| CI | Bad `latest.json` fields / sha mismatch | **Handled** — validation in `release.yml` |
| CI | Tiny/corrupt Setup artifact | **Handled** — size + sha check |
| CI | Duplicate tag / republish | **Handled** — next version is latest tag + patch; collision fails the job; use workflow_dispatch override for minor/major |
| CI | Inno missing on runner | **Handled** — choco install Inno in workflow |
| CI | Public Releases | **Accepted** — Phase 0 |
| Feed | Offline / timeout / 404 / HTML | **Handled** — quiet, no banner |
| Feed | Invalid JSON / missing sha256 | **Handled** — ignored |
| Feed | Already latest / older feed | **Handled** — no banner |
| Feed | Throttle | **Handled** — 6 hours |
| Feed | Proxy / firewall blocks GitHub | **Deferred** — USB fallback; clinic IT |
| Feed | Who can update | **Handled** — any signed-in user |
| Download | Progress + cancel | **Handled** |
| Download | Checksum mismatch | **Handled** — no Setup launch |
| Download | Disk full / AV quarantine | **Deferred** — failure text; retry/USB |
| Download | Resume partial | **Deferred** — restart download |
| UX | Dirty form | **Handled** — confirm discard |
| UX | UAC deny | **Handled** — relaunches old Desktop |
| UX | Mid-update reopen | **Handled** — `update.lock` wait window |
| UX | Force update | **Deferred** — notify only |
| Install | Silent Setup + service restart | **Handled** |
| Install | Config preserve | **Handled** |
| Install | Postgres not reinstalled on update | **Handled** — `-UpdateOnly` |
| After | Migrations on API start | **Handled** — `DatabaseInitializer` |
| After | Offline banner blip | **Handled** — expected ([`03-data-safety.md`](03-data-safety.md)) |
| Ops | USB + rollback | **Handled** — [`01-release-playbook.md`](01-release-playbook.md) |
| Ops | Manual smoke | **Checklist** — [`04-smoke-checklist.md`](04-smoke-checklist.md) |

### Still deferred / rare (conscious)

- Power loss mid-Setup; wrong non-default install path; pending Windows reboot from Postgres
- Update during an active `pg_dump`; multi-user same PC mid-update
- Skip-this-version / remind-tomorrow; activity-log audit of who clicked Update
- Private Releases / tokenized feed (public Releases accepted for v1)
