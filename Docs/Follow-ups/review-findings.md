# Review Findings

Code review from November 2026. All 274 tests passed at the time; no memory leaks or performance problems were found.

✅ = done.

## Must do before the first clinic installation

1. **Production setup.** A production install cannot start or sign anyone in yet. `Jwt:Key` is empty in
   `appsettings.json`, so the API refuses to start, and the only users come from the Development seed, so there is no
   first Super user. Covered by roadmap item 12.
2. **Backups.** There are no backups yet. For medical records this matters most. Covered by roadmap item 13.
3. **Server address.** Other PCs cannot reach the server: the API only listens on `localhost`, and the desktop app
   always connects to `http://localhost:5270` with no screen to change it. Part of roadmap item 12.
4. **Encrypted traffic.** The API runs on plain HTTP on purpose (see the comment in `Program.cs`), so passwords,
   login tokens and later patient data cross the clinic network readable by anyone on it. Decide consciously: HTTPS
   with a certificate per clinic, or accept the risk on a trusted network. Not on the roadmap yet.

## Security issues in the code

5. **Password changes keep old sessions alive.** Changing or resetting a password does not sign out existing
   sessions: whoever holds an old token keeps access until midnight. `RejectStaleUserAsync` only checks "active"
   and "role". Fix: a token-version number on `User`, put in the token and compared there (that check already loads
   the user, so it costs nothing extra). Until then, deactivating the user is the only immediate cut-off.
6. **Activity logs can be erased.** Super can delete single activity log entries. The deletion is logged, but that
   entry can be deleted too, so the audit trail can be quietly erased. Remove single-entry deletes for activity logs
   and keep only the automatic retention cleanup.

## Smaller correctness issues

7. **Wrong PC clock breaks sign-in.** `SessionService` compares the server's expiry time with the PC's own clock. A
   clinic PC whose clock is ahead of the server's next midnight (dead motherboard battery, wrong time zone) shows
   "session expired" right after every sign-in. Base the expiry on the token's lifetime instead of an absolute time.
8. **A wrong server address stops the connection monitor.** If the address points at another device that answers
   with a web page, `ApiClient` throws while reading the reply, although it is designed never to throw. During a
   health check this stops the check for good; during sign-in it shows the generic "unexpected error".
9. **Duplicate username race returns 500.** Two people creating the same username at the same moment get a 500
   instead of 409: the database's unique-index error is not turned into "username taken". Rare for users, but
   patients will copy this pattern (e.g. unique file numbers), so handle it once in `AppDbContext`.

## Consistency

10. **Log timestamps ignore the clock rule.** `ActivityLog` and `SystemError` stamp `OccurredAt` with
    `DateTime.UtcNow` instead of `TimeProvider`. The "today" statistics use `TimeProvider`, so tests with a fixed
    clock can disagree with the stored times.
11. **Roadmap out of date.** Item 8 (clinic settings) is built but not ticked; only the logo is missing.
