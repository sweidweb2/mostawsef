# Activity Logs and System Errors

Two app-wide engines. Both write to their own table, never throw, and never block the action being logged.

## Tables

| Table | What | Key columns (max length) |
| --- | --- | --- |
| `activity_logs` | Every main action: who did what, when | `OccurredAt`, `Action` (100), `Module` (50), `Description` (500), `UserId`, `Username` (50), `EntityType`/`EntityId` (100), `IpAddress` (45), `UserAgent` (300), `Details` JSON (8000) |
| `system_errors` | Every unexpected failure (API and desktop) | `OccurredAt`, `Level` (Warning/Error/Critical), `Source` (Api/Desktop), `Message` (2000), `ExceptionType` (300), `StackTrace` (20000), `RequestPath` (500), `RequestMethod`, `StatusCode`, `UserId`, `Username`, `TraceId`, `IpAddress`, `Details` JSON (8000) |

Longer values are cut to the column limit automatically (`Domain/Common/Text.Truncate`), so a write never fails on long input.

## Search

Each row also stores `SearchText`: the searchable fields lowercased and joined (`Text.SearchKey`, set in the entity constructor).
Activity logs: username, action, module, description. System errors: message, source, exception type, request path, username.
The list search is a single `LIKE '%term%'` on that column (`Repositories/LogSearch.Matching`; `%` and `_` in the term are literal),
served on PostgreSQL by a `pg_trgm` GIN index (an index-only PostgreSQL feature: the query stays plain `LIKE`, so SQLite still works).
The index reads groups of three characters, so a term needs at least 3 letters or digits (`CursorQuery.MinSearchLength`):
shorter terms get a 400 (`errors.Search = ["min_length|3"]`), and the desktop app shows a warning instead of sending them.
Rows written before `SearchText` existed are filled once by `SearchTextBackfill` (run by `DatabaseInitializer` right after
the `LogSearchText` migration), with EF Core code, not SQL. A new searchable field goes into the entity's `RebuildSearchText`,
plus a backfill like that one for existing rows.

## Retention

Old records are deleted automatically by `LogRetentionWorker` (API startup, then once a day), in batches of 5000 rows.
A purge that removed anything is itself recorded as `logs.purged` with the counts.

```json
"LogRetention": { "ActivityLogDays": 365, "SystemErrorDays": 90 }
```

`0` keeps that table forever; a negative value stops the API at startup.

## Activity log engine (backend)

Call `IActivityLogger` from Application services after every **main** action (create, update, delete, sign-in, settings, printing...):

```csharp
await activity.LogAsync(
    new ActivityEntry(ActivityActions.PatientCreated, ActivityModules.Patients, "Created patient")
    {
        EntityType = nameof(Patient),
        EntityId = patient.Id.ToString(),
        Details = new { patient.FileNumber },   // any object, stored as JSON
    },
    cancellationToken);
```

- User, IP address and client are filled in from the current request.
- Entries are buffered for the request and written once after the action (several logs share a single insert; callers of `LogAsync` return before the DB write).
- Add the code to `Application/Common/ActivityActions.cs` (`module.verb`, e.g. `patients.created`) and its label to both resx files as `Action.patients.created`.
- Never put passwords, tokens or full medical notes in `Details`.

Recorded today: `auth.signed_in`, `auth.sign_in_failed`, `auth.signed_out`, `auth.password_changed`, `users.created`,
`users.updated`, `users.deleted`, `users.password_reset`, `settings.clinic_updated`, `logs.activity_deleted`,
`logs.error_deleted`, `logs.errors_deleted`, `logs.activity_cleared`, `logs.errors_cleared`, `logs.purged`,
`backups.created`, `backups.exported`, `backups.scheduled`, `system.data_reset`.

## System error engine

| Where | How errors get recorded |
| --- | --- |
| API, unhandled exceptions | Automatic: `ExceptionHandlingMiddleware` stores the error with request, user and trace id; the client gets a generic 500 with the `traceId` only |
| API, errors you catch | `await errors.LogAsync(ex, ErrorLevel.Warning, details: new { ... });` (`ISystemErrorLogger`) |
| Desktop, unhandled exceptions | Automatic: `ErrorReporter.Install()` hooks UI-thread, task and process crashes, sends them to `POST /api/system-errors/report`, and shows a toast |
| Desktop, errors you catch | `errorReporter.Report(ex, ErrorLevels.Error, "Saving patient");` |
| Desktop, API unreachable | Written to `%AppData%\Mostawsef\logs\errors-yyyyMMdd.log` |
| Desktop, repeating errors | The same error is sent once a minute (the next report says how often it repeated), at most 10 reports a minute with one POST in flight at a time, one "unexpected error" toast per 10 seconds; after a 429 the reporter writes to the local file only for a minute |

## API (Super only, except report)

| Endpoint | Purpose |
| --- | --- |
| `GET /api/activity-logs?pageSize=&search=&after=\|before=\|last=true&includeTotal=` | Keyset-paged list, newest first (max page size 100); see below |
| `GET /api/activity-logs/stats` | Total, today, last 7 days, failed sign-ins today (one statement; each count is read from an index) |
| `GET /api/activity-logs/{id}` | Full record |
| `POST /api/activity-logs/bulk-delete` `{ ids }` | Deletes up to 1000 named records (`LogRules.MaxBulkDelete`), logged as `logs.activity_deleted` |
| `DELETE /api/activity-logs` | Deletes every record in batches; the clearing is logged as `logs.activity_cleared`, the first new record |
| `GET /api/system-errors...` | List, stats, get, delete, `bulk-delete` and delete-all for errors (stats: total, today, last 7 days, critical; critical uses the `(Level, OccurredAt)` index) |
| `POST /api/system-errors/report` | Desktop error report (anonymous, 30/min per IP) |

Lists use keyset paging (`CursorQuery` / `CursorPage<T>`), because log tables only grow: each page is read from
the `(OccurredAt, Id)` index from a cursor, so a deep page costs the same as the first one (no `OFFSET`).
Without a position the newest page comes back; `after=NextCursor` moves to older rows, `before=PreviousCursor` to newer
ones, `last=true` reads the oldest rows. A null cursor means that end is reached. The `COUNT` of matches is the
expensive part: send `includeTotal=false` when only the page changes (`TotalCount` is then null). An unknown cursor,
or more than one position, returns 400.

## Performance debugger (desktop + API)

Dev aid for spotting slow operations while using the app. Not a business log; do not store medical data there.

| Where | What |
| --- | --- |
| Desktop overlay | Bottom-right panel on `MainWindow`. **Off by default** (live updates during Nav were skewing GC/UI timings). **Ctrl+Shift+P** shows it; refresh is deferred to idle. Amber ≥ 200 ms, red ≥ 500 ms |
| Desktop file | `%AppData%\Mostawsef\logs\perf-yyyyMMdd.log` always written while enabled (Debug on by default). Release: set env `MOSTAWSEF_PERF=1` |
| API | `RequestTimingMiddleware` logs duration and adds `Server-Timing: app;dur=N` (skips `/api/health`) |

| Category | Meaning |
| --- | --- |
| `Api` | HTTP round-trip (optional `server Nms` from the API) |
| `Page` | `ActivateAsync` (data load for the page) |
| `View` | `created` / `reused` = build or cache hit; `hide` / `show` = `PageHost` visibility switch (views stay in the tree) |
| `Nav` | `deactivate` / `set-page` / `total` split + `cpu=` / `ws=` / `gc` |
| `Ui` | `layout` = time until Loaded; `dispatcher-lag` = time until the UI thread is idle again |
| `Print` | PDF render + open |

If navigation *feels* slow but `Api`/`Page` look fine, read `View hide`/`show` and `Nav set-page`. PageHost keeps visited views mounted and toggles visibility so DataGrids are not torn down on every switch.

Avalonia Developer Tools are **off by default** even in Debug (set `MOSTAWSEF_DEVTOOLS=1` to enable); they walk the tree on every attach and can dominate Nav.

Instrumentation is centralized: `ApiClient`, `PageHost`, portal `Show`/`ActivateAsync`, `PrintService`, API middleware. Do not sprinkle stopwatches in view models.

## Desktop pages (Super portal)

`Activity logs` and `System errors`: stat cards, search, table (long text cut with the full text in a tooltip),
pagination and an eye button (details modal). System errors also have a delete button (with confirmation). Both pages
have a select mode (`SelectableLogListViewModel`: row checkboxes kept across pages, "delete selected") and "delete all";
every bulk delete is itself recorded as an activity. Delete-all uses `ApiClient.LongRequestTimeout` and, once confirmed, runs to the end on the server even if the client leaves (the data reset too).
The activity-log list loads only the columns it shows (same idea as system-error summaries); Details and UserAgent stay on the details call.
If the list cannot load, the table shows an error state with a Retry button (a specific message when the server is unreachable).
Both pages derive from `ViewModels/Shell/PagedListViewModel`, which a new list page can reuse as well.
Page moves send one cursor request without the total; a search counts again; the stat cards load only on the
first load, Refresh and after a system-error delete (they cover the whole table, not the shown page or search). When there is no
search, the list skips its own `COUNT` and uses `stats.Total` instead.
The shell reuses each menu page's view model and view (PageHost hides/shows; returning keeps list data).
Leaving a page cancels its requests still running; if that interrupted the first load, returning loads it again.
