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
- Add the code to `Application/Common/ActivityActions.cs` (`module.verb`, e.g. `patients.created`) and its label to both resx files as `Action.patients.created`.
- Never put passwords, tokens or full medical notes in `Details`.

Recorded today: `auth.signed_in`, `auth.sign_in_failed`, `auth.signed_out`, `auth.password_changed`, `users.created`,
`users.updated`, `users.deleted`, `users.password_reset`, `logs.activity_deleted`, `logs.error_deleted`, `logs.purged`.

## System error engine

| Where | How errors get recorded |
| --- | --- |
| API, unhandled exceptions | Automatic: `ExceptionHandlingMiddleware` stores the error with request, user and trace id; the client gets a generic 500 with the `traceId` only |
| API, errors you catch | `await errors.LogAsync(ex, ErrorLevel.Warning, details: new { ... });` (`ISystemErrorLogger`) |
| Desktop, unhandled exceptions | Automatic: `ErrorReporter.Install()` hooks UI-thread, task and process crashes, sends them to `POST /api/system-errors/report`, and shows a toast |
| Desktop, errors you catch | `errorReporter.Report(ex, ErrorLevels.Error, "Saving patient");` |
| Desktop, API unreachable | Written to `%AppData%\Mostawsef\logs\errors-yyyyMMdd.log` |
| Desktop, repeating errors | The same error is sent once a minute (the next report says how often it repeated), at most 10 reports a minute, one "unexpected error" toast per 10 seconds; after a 429 the reporter writes to the local file only for a minute |

## API (Super only, except report)

| Endpoint | Purpose |
| --- | --- |
| `GET /api/activity-logs?pageSize=&search=&after=\|before=\|last=true&includeTotal=` | Keyset-paged list, newest first (max page size 100); see below |
| `GET /api/activity-logs/stats` | Total, today, last 7 days, failed sign-ins today (one statement; each count is read from an index) |
| `GET /api/activity-logs/{id}` | Full record |
| `DELETE /api/activity-logs/{id}` | Delete (the deletion itself is logged) |
| `GET /api/system-errors...` | Same four endpoints for errors (stats: total, today, last 7 days, critical; critical uses the `(Level, OccurredAt)` index) |
| `POST /api/system-errors/report` | Desktop error report (anonymous, 30/min per IP) |

Lists use keyset paging (`CursorQuery` / `CursorPage<T>`), because log tables only grow: each page is read from
the `(OccurredAt, Id)` index from a cursor, so a deep page costs the same as the first one (no `OFFSET`).
Without a position the newest page comes back; `after=NextCursor` moves to older rows, `before=PreviousCursor` to newer
ones, `last=true` reads the oldest rows. A null cursor means that end is reached. The `COUNT` of matches is the
expensive part: send `includeTotal=false` when only the page changes (`TotalCount` is then null). An unknown cursor,
or more than one position, returns 400.

## Desktop pages (Super portal)

`Activity logs` and `System errors`: stat cards, search, table (long text cut with the full text in a tooltip),
pagination, an eye button (details modal) and a delete button (with confirmation) on each row.
If the list cannot load, the table shows an error state with a Retry button (a specific message when the server is unreachable).
Both pages derive from `ViewModels/Shell/PagedListViewModel`, which a new list page can reuse as well.
Page moves send one cursor request without the total; a search counts again; the stat cards load only on the
first load, Refresh and after a delete (they cover the whole table, not the shown page or search).
The shell keeps a page once opened: returning to it shows the loaded data without a new request (Refresh reloads).
Leaving a page cancels its requests still running; if that interrupted the first load, returning loads it again.
