# Authentication and Role Portals

## Overview

```
Desktop LoginView
   │
   ├─ Enter clinic ──POST /api/auth/clinic-session──► API ──► built-in "clinic" Admin user
   │                         │
   │◄────── JWT (role Admin, username clinic) ───────┘
   │
   └─ Super sign-in ──POST /api/auth/login──► API ──► Super users
                             │
        │◄────── JWT (role Super) ───────────┘
        ▼
    SessionService (token in memory) ──► NavigationService ──► Admin portal | Super portal
```

- The login screen is the landing page. **Enter clinic** opens the Admin portal with no password (one-PC / localhost install).
- **Super** still signs in with username + password. The Users page manages Super accounts only.
- The built-in Admin identity is username `clinic` (seeded at API startup with an unusable password hash). Audit fields and activity logs use `"clinic"` as the actor for clinic work.
- Closing the app clears the in-memory session.

## Database: `users`

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | uuid | Primary key |
| `Username` | varchar(50) | Unique, stored trimmed and lower-case |
| `PasswordHash` | varchar(256) | **Hash only, never the password** |
| `TokenVersion` | integer | Bumped on password change/reset; embedded in the JWT as `tv` |
| `Role` | varchar(20) | `Admin` or `Super` |
| `FullName` | varchar(100) | Shown in the top bar and the users list |
| `SearchName` | varchar(100) | `FullName` normalized for Arabic-aware search (`SearchText.Normalize`) |
| `IsActive` | boolean | Inactive users cannot sign in |
| `LastLoginAt`, `CreatedAt`, `UpdatedAt` | timestamptz | `LastLoginAt` is written directly (not an edit) |
| `CreatedBy`, `UpdatedBy` | varchar(50) | Username of who created / last changed the user (null for seeding) |
| `Version` | integer | Concurrency token: an edit based on an older version returns 409 |
| `IsDeleted`, `DeletedAt`, `DeletedBy` | | Soft delete: deleted users are hidden, cannot sign in, and their username is never reused |

Migrations (the schema starts at `*_Initial`) are applied automatically when the API starts.

## Password hashing

ASP.NET Core Identity `PasswordHasher` (format v3): **PBKDF2 with HMAC-SHA512, 100,000 iterations, a random 128-bit salt per password**.
The salt and parameters are stored inside the hash string. If the hashing parameters are strengthened in a future version, hashes are upgraded automatically at the next successful sign-in.
Password login is Super-only. Unknown usernames (and Admin / clinic) take the same hashing time as wrong passwords, so response time does not reveal which usernames exist.

## API

| Endpoint | Auth | Result |
| --- | --- | --- |
| `POST /api/auth/login` `{ username, password }` | Anonymous, max 10 requests/min per IP | `200 { accessToken, expiresAt, user, mustChangePassword }` for Super only; `400` empty fields, `401` invalid (including Admin/clinic), `429` too many |
| `GET /api/auth/clinic-access` | Anonymous | `200 { available }` — `false` while maintenance mode is on (login screen disables Enter clinic) |
| `POST /api/auth/clinic-session` | Anonymous, loopback only (Testing allows null remote IP), own rate limit | `200` Admin JWT for `clinic`; `403` non-local, or the request still carries `X-Forwarded-For` / `Forwarded` (came through a proxy the API did not resolve); `409 maintenance_mode`; `404` if clinic identity missing (startup repairs a soft-deleted / wrong-role `clinic` row) |

Login protection (configuration keys under `RateLimit:`; the API answers like a wrong password, without logging, while blocked):
- `LoginPermitLimit` (default 10): Super password login requests per minute per client IP, then `429`.
- `ClinicSessionPermitLimit` (default 10): Enter-clinic requests per minute per client IP, then `429` (separate from Super login so failed passwords do not block clinic entry on localhost).
- `LoginFailuresPerUser` (default 10) within `LoginFailureWindowMinutes` (default 5): failed sign-ins per username from any IP;
  further attempts get `401` even with the right password until the window ends. A successful sign-in clears the count.
  In memory, per API process.
- `HealthPermitLimit` (default 120): `GET /api/health` requests per minute per IP.
- Behind a reverse proxy set `ForwardedHeaders:Enabled` to `true` so the limits use the client address from `X-Forwarded-For`
  (only loopback proxies are trusted unless `KnownProxies` is configured). Passwordless Enter clinic trusts "the request comes
  from this PC": a proxy on the same PC connects from loopback, so without `ForwardedHeaders` every proxied request would look
  local and anyone on the network could open the Admin portal. The API refuses clinic sessions that still carry proxy headers,
  but never expose the API through a proxy without `ForwardedHeaders:Enabled` and a correct `KnownProxies`.
- `POST /api/system-errors/report` is anonymous: body capped at 128 KB, stored fields truncated.
| `POST /api/auth/logout` | Bearer token | `204`; logged as `auth.signed_out`. Revokes every token of a Super (bumps `tv`); the shared `clinic` identity keeps its tokens (a new one needs no password anyway, and revoking would end every clinic session) |
| `POST /api/auth/change-password` `{ currentPassword, newPassword }` | Bearer token (Super) | `200 { accessToken, expiresAt, user }` (old JWT invalidated, `mustChangePassword` cleared), `400` (`wrong_password` on `CurrentPassword`, `min_length\|8`...), `404` for the clinic Admin |
| `GET /api/users?page=&pageSize=&search=` | Super | Paged list of Super users only (clinic Admin hidden); search matches the username or the normalized full name |
| `GET /api/users/{id}` | Super | `200` details with audit fields and `version`, `404` (including clinic / non-Super) |
| `POST /api/users` `{ fullName, username, password, role }` | Super | `201` (role must be Super), `400`, `409 username_taken` (also for deleted users' usernames and for `clinic`) |
| `PUT /api/users/{id}` `{ fullName, role, isActive, version }` | Super | `200`, `400`, `404`, `409 concurrency_conflict` or `own_account` |
| `POST /api/users/{id}/reset-password` `{ newPassword }` | Super | `204`, `400`, `404`, `409 own_account` |
| `DELETE /api/users/{id}` | Super | `204` (soft delete), `404`, `409 own_account` |
| `GET /api/clinic-settings` | Bearer token (any role) | `200 { name, address, phone, currencyCode, version, updatedAt, updatedBy }` |
| `PUT /api/clinic-settings` `{ name, address, phone, currencyCode, version }` | Admin | `200`, `400`, `403` for Super, `409 concurrency_conflict` |
| `GET /api/system-settings` | Super | `200 { backupPath, maintenanceMode, version, updatedAt, updatedBy }` |
| `PUT /api/system-settings` `{ backupPath, maintenanceMode, version }` | Super | `200`, `400` (`max_length\|260`), `409 concurrency_conflict` |
| `GET /api/lab-tests?page=&pageSize=&search=` | Admin | Paged list sorted by name; search (cut to 100 characters) matches the normalized name or the code |
| `GET /api/lab-tests/all` | Admin | Every test for print/export; `?active=true` for the New Order catalog |
| `GET /api/lab-tests/{id}` | Admin | `200 { id, code, name, labCost, isActive, ..., version }`, `404` |
| `POST /api/lab-tests` `{ code, name, labCost }` | Admin | `201`, `400` (`max_length\|20`/`\|150`, `range\|0\|100000000` for a lab cost outside 0 to 1,000,000.00), `409 taken` on `Code` or `Name` |
| `POST /api/lab-tests/import` multipart `file` | Admin | CSV or Excel (`.xlsx`) Name+Price (`$12.00` shape); new codes generated; matching names update lab cost; bad/duplicate-in-file rows skipped; `200 { imported, updated, skipped }`, `400` (`required` / `invalid` / `too_large`) |
| `PUT /api/lab-tests/{id}` `{ code, name, labCost, isActive, version }` | Admin | `200`, `400`, `404`, `409 taken` or `concurrency_conflict` |
| `DELETE /api/lab-tests/{id}` | Admin | `204`, `404` |
| `GET /api/patients?page=&pageSize=&search=` | Admin | Paged list sorted by SearchName; search matches SearchText (name/phones; trigram, min 3 letters/digits or 400) |
| `GET /api/patients/all` | Admin | Every patient for print/export (notes omitted) |
| `GET /api/patients/{id}` | Admin | `200 { id, fullName, phone, notes, gender, dateOfBirth, ..., version }`, `404` |
| `POST /api/patients` `{ fullName, phone, notes, gender, dateOfBirth }` | Admin | `201`, `400` (`invalid` phone, gender or date of birth outside 1900-01-01..today) |
| `PUT /api/patients/{id}` `{ ...same, version }` | Admin | `200`, `400`, `404`, `409 concurrency_conflict` |
| `DELETE /api/patients/{id}` | Admin | `204` (soft delete), `404` |
| `GET /api/health` | Anonymous | `200 { status, database, apiVersion }`, `503` when the database is down |

- Tokens: JWT signed with HMAC-SHA256, valid until the next midnight (server local time), so staff sign in once a day; if less than one hour remains until midnight, expiry rolls to the following midnight. Claims `sub`, `unique_name`, `role`, `tv` (`User.TokenVersion`), and `pwd_change` while the account must change its initial password.
- Every request re-checks the token's user (active flag, role and token version, cached until that user is updated, deleted or their password changes): a
  deactivated, deleted, re-roled or password-changed user gets `401` on the next request (the desktop then returns to the login screen),
  not only when the token expires. Changing your own password returns a fresh token so the current session stays signed in; other sessions with the old `tv` are rejected.
- Maintenance mode (Super portal, Settings page): while on, `GET /api/auth/clinic-access` returns `available: false` and
  `POST /api/auth/clinic-session` returns `409 maintenance_mode` (logged as `auth.clinic_blocked`). Super password login
  still works. Users already in a clinic session keep working until they leave.
- A Super cannot deactivate, reset or delete their own account (`409 own_account`); they change their own password
  with `change-password`. Passwords: at least 8 characters.
- Role policies: `[Authorize(Policy = AuthPolicies.AdminOnly)]`, `[Authorize(Policy = AuthPolicies.SuperOnly)]`.

## Configuration

| Key | Where | Notes |
| --- | --- | --- |
| `Jwt:Key` | `appsettings.Development.json` (dev) / `appsettings.Clinic.json` (Production first-run) | **Required, at least 32 characters, unique per installation.** Development sets a fixed key. Production with an empty key creates `appsettings.Clinic.json` next to the API on first start (gitignored). Testing must set a key explicitly. |
| `Jwt:Issuer`, `Jwt:Audience` | `appsettings.json` | |
| `Seed:Users` | `appsettings.json` (install) / `appsettings.Development.json` (dev override) | Users created at startup only if that username was never used (passwords hashed) |

### Built-in clinic identity

Always seeded by `DatabaseInitializer` (not from `Seed:Users`): username `clinic`, role Admin, random unusable password.
Used only by `POST /api/auth/clinic-session`. Startup also repairs a soft-deleted, inactive, or wrong-role row that still
owns the `clinic` username, and deactivates any leftover non-clinic Admin accounts (password login is Super-only) so their JWTs stop on the next request.

### Development Super account

| Username | Password | Role |
| --- | --- | --- |
| `super@example.com` | `password` | Super |

From `appsettings.Development.json`. Does not replace install seeds in a Production run.

### Install Super account (Production / published API)

| Username | Password | Role |
| --- | --- | --- |
| `ali@etkan` | `ali@1234` | Super |

Seeded once on first Production start from `appsettings.json` (`Seed:Users`, Super only). This password is public (it is in the
repository), so it is only an initial one: seeded users get `MustChangePassword` (default `true`; only Development and the test
factory set `"MustChangePassword": false`). Signing in with it returns `mustChangePassword: true` and a token with the
`pwd_change` claim: the `SuperOnly` policy answers `403` to it, so it can only change the password (`/api/auth/change-password`)
or sign out. The desktop opens "Choose your own password" right after such a sign-in; closing it signs out again with
`Login.PasswordChangeRequired`. Sign in and choose the password right after installing, before anyone else can.

## Desktop

| Piece | Responsibility |
| --- | --- |
| `Services/Api/ApiClient` | HTTP calls, maps errors (`Unreachable`, `Unauthorized`, `TooManyRequests`, ...). Knows nothing about tokens. API address: `ApiBaseUrl` in `%AppData%\Mostawsef\settings.json` (default `http://localhost:5270`). **Install decision (deploy-install Block 4):** one-PC clinics use plain `http` on localhost — traffic stays on the machine. Multi-PC / remote API is not the current product; if that changes, put the API behind HTTPS (certificate every desktop trusts) and set `ApiBaseUrl` to `https://…`. The login screen already warns when `ApiBaseUrl` is plain http to another machine (`Services/Local/ApiAddress`). Timeouts (`ServiceRegistration`): 3 s to connect, so a switched-off API PC shows `Unreachable` quickly, and 15 s for a whole request (applied by `ApiClient`, not `HttpClient.Timeout`). Whole-table work (backup export, delete a whole log, data reset) passes `ApiClient.LongRequestTimeout` (10 min) |
| `Services/Api/AuthApi` | `LoginAsync`, `GetClinicAccessAsync`, `OpenClinicSessionAsync`, `ChangePasswordAsync`, `LogoutAsync` |
| `Services/Auth/AuthHeaderHandler` | `HttpClient` pipeline step: adds the session's bearer token to each request; a `401` for that token expires the session (a late `401` for an earlier session is ignored) |
| `Services/Auth/SessionService` | Current user + token in memory; expiry uses `LoginResponse.ExpiresInSeconds` counted from receipt (so a wrong PC clock does not expire the session at once), and also when `AuthHeaderHandler` reports a rejected token |
| `AppRoutes` (`Services/Navigation/RouteTable`) | Every screen in one table: its view, who may open it, and each role's portal (title + menu). DI, navigation, the shell and `ViewLocator` all read it |
| `Services/Navigation/NavigationService` | Opens the login screen or the user's portal; the single access check (`CanAccess`, `CreatePage`) |
| `ViewModels/Auth/LoginViewModel` + `Views/Auth/LoginView` | Super sign-in form + Enter clinic (disabled while maintenance mode is on; re-checks clinic-access every few seconds until it is available again) |
| `ViewModels/Shell/PortalShellViewModel` + `Views/Shell/PortalShellView` | Shared portal layout. Super: change password + confirmed sign-out. Clinic Admin: Back to sign-in only (no password change; still records `SignedOut` via logout) |
| `ViewModels/Shell/DashboardViewModel` + `Views/Shell/DashboardView` | Admin home: revenue, outstanding, order counts and graphs for a chosen period (other roles see an empty card until they get their own dashboard in `AppRoutes`) |

### Route protection

- A page is reachable only if `AppRoutes` lists it: `Public` pages for anyone, portal pages only for that portal's role. Anything else is denied.
- `NavigationService` and the shell check it before creating a page; a mismatch signs out and returns to login.
- The API enforces the same roles on its endpoints, so the desktop check is for UX and the server check is the real security boundary.

### Adding a page to a portal

1. Create `ViewModels/<Role>/<Name>ViewModel` and `Views/<Role>/<Name>View.axaml`.
2. Add `.Page<<Name>ViewModel, <Name>View>("Icon...", "Nav.<Name>")` to the role's portal in `AppRoutes` (this also registers it in DI) and add `Nav.<Name>` to both resx files.

### Adding a role

1. Add it to `Domain/Enums/UserRole` and `Contracts/Auth/Roles` (+ an API policy).
2. Add `.Portal(Roles.<Role>, "Portal.<Role>", portal => portal.Page<...>(...))` to `AppRoutes`.
3. Add `Portal.<Role>` and `Role.<Role>` strings.

## Run

```powershell
docker compose up -d
dotnet run --project backend/Mostawsef.Api --launch-profile http
dotnet run --project frontend/Mostawsef.Desktop
```
