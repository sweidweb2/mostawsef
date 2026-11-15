# Authentication and Role Portals

## Overview

```
Desktop LoginView ──POST /api/auth/login──► API ──► users table (PostgreSQL)
        │                                    │
        │◄────── JWT access token + user ────┘
        ▼
SessionService (token in memory) ──► NavigationService ──► Admin portal | Super portal
```

- Users sign in with **username + password**. Each user has one **role**: `Admin` or `Super`.
- After sign-in, the desktop app opens the portal for that role. All portals share one shell; each role gets its own title and sidebar menu from `AppRoutes`.
- Closing the app signs out (the token is kept in memory only).

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

Migrations: `*_InitialUsers`, `*_AuditFieldsAndUserProfile`. Migrations are applied automatically when the API starts.

## Password hashing

ASP.NET Core Identity `PasswordHasher` (format v3): **PBKDF2 with HMAC-SHA512, 100,000 iterations, a random 128-bit salt per password**.
The salt and parameters are stored inside the hash string. If the hashing parameters are strengthened in a future version, hashes are upgraded automatically at the next successful sign-in.
For non-Super sign-in, the maintenance-mode settings read overlaps the PBKDF2 work; a successful login then records `LastLoginAt` and the activity log in parallel.
Unknown usernames take the same hashing time as wrong passwords, so response time does not reveal which usernames exist.

## API

| Endpoint | Auth | Result |
| --- | --- | --- |
| `POST /api/auth/login` `{ username, password }` | Anonymous, max 10 requests/min per IP | `200 { accessToken, expiresAt, user }`, `400` empty fields, `401` invalid, `409 maintenance_mode` (non-Super while maintenance mode is on), `429` too many |

Login protection (configuration keys under `RateLimit:`; the API answers like a wrong password, without logging, while blocked):
- `LoginPermitLimit` (default 10): requests per minute per client IP, then `429`.
- `LoginFailuresPerUser` (default 10) within `LoginFailureWindowMinutes` (default 5): failed sign-ins per username from any IP;
  further attempts get `401` even with the right password until the window ends. A successful sign-in clears the count.
  In memory, per API process.
- `HealthPermitLimit` (default 120): `GET /api/health` requests per minute per IP.
- Behind a reverse proxy set `ForwardedHeaders:Enabled` to `true` so the limits use the client address from `X-Forwarded-For`
  (only loopback proxies are trusted unless `KnownProxies` is configured).
- `POST /api/system-errors/report` is anonymous: body capped at 128 KB, stored fields truncated.
| `GET /api/auth/me` | Bearer token | `200 { id, username, role, fullName }`, `401` |
| `POST /api/auth/change-password` `{ currentPassword, newPassword }` | Bearer token | `200 { accessToken, expiresAt, user }` (old JWT invalidated), `400` (`wrong_password` on `CurrentPassword`, `min_length\|8`...) |
| `GET /api/users?page=&pageSize=&search=` | Super | Paged list sorted by name; search matches the username or the normalized full name |
| `GET /api/users/{id}` | Super | `200` details with audit fields and `version`, `404` |
| `POST /api/users` `{ fullName, username, password, role }` | Super | `201`, `400`, `409 username_taken` (also for deleted users' usernames) |
| `PUT /api/users/{id}` `{ fullName, role, isActive, version }` | Super | `200`, `400`, `404`, `409 concurrency_conflict` or `own_account` |
| `POST /api/users/{id}/reset-password` `{ newPassword }` | Super | `204`, `400`, `404`, `409 own_account` |
| `DELETE /api/users/{id}` | Super | `204` (soft delete), `404`, `409 own_account` |
| `GET /api/clinic-settings` | Bearer token (any role) | `200 { name, address, phone, currencyCode, version, updatedAt, updatedBy }` |
| `PUT /api/clinic-settings` `{ name, address, phone, currencyCode, version }` | Admin | `200`, `400`, `403` for Super, `409 concurrency_conflict` |
| `GET /api/system-settings` | Super | `200 { backupPath, maintenanceMode, version, updatedAt, updatedBy }` |
| `PUT /api/system-settings` `{ backupPath, maintenanceMode, version }` | Super | `200`, `400` (`max_length\|260`), `409 concurrency_conflict` |
| `GET /api/lab-tests?page=&pageSize=&search=` | Admin | Paged list sorted by name; search (cut to 100 characters) matches the normalized name or the code |
| `GET /api/lab-tests/all` | Admin | Every test for print/export |
| `GET /api/lab-tests/{id}` | Admin | `200 { id, code, name, price, labCost, isActive, ..., version }`, `404` |
| `POST /api/lab-tests` `{ code, name, price, labCost }` | Admin | `201`, `400` (`max_length\|20`/`\|150`, `range\|0\|100000000` for a price outside 0 to 1,000,000.00), `409 taken` on `Code` or `Name` |
| `PUT /api/lab-tests/{id}` `{ code, name, price, labCost, isActive, version }` | Admin | `200`, `400`, `404`, `409 taken` or `concurrency_conflict` |
| `DELETE /api/lab-tests/{id}` | Admin | `204`, `404` |
| `GET /api/patients?page=&pageSize=&search=` | Admin | Paged list sorted by SearchName; search matches SearchText (name/phones; trigram, min 3 letters/digits or 400) |
| `GET /api/patients/all` | Admin | Every patient for print/export (notes omitted) |
| `GET /api/patients/{id}` | Admin | `200 { id, fullName, phone, guardianPhone, notes, isInsured, gender, dateOfBirth, ..., version }`, `404` |
| `POST /api/patients` `{ fullName, phone, guardianPhone, notes, isInsured, gender, dateOfBirth }` | Admin | `201`, `400` (`invalid` phone, gender or date of birth outside 1900-01-01..today) |
| `PUT /api/patients/{id}` `{ ...same, version }` | Admin | `200`, `400`, `404`, `409 concurrency_conflict` |
| `DELETE /api/patients/{id}` | Admin | `204` (soft delete), `404` |
| `GET /api/health` | Anonymous | `200 { status, database, apiVersion }`, `503` when the database is down |

- Tokens: JWT signed with HMAC-SHA256, valid until the next midnight (server local time), so staff sign in once a day; if less than one hour remains until midnight, expiry rolls to the following midnight. Claims `sub`, `unique_name`, `role`, `tv` (`User.TokenVersion`).
- Every request re-checks the token's user (active flag, role and token version, cached until that user is updated, deleted or their password changes): a
  deactivated, deleted, re-roled or password-changed user gets `401` on the next request (the desktop then returns to the login screen),
  not only when the token expires. Changing your own password returns a fresh token so the current session stays signed in; other sessions with the old `tv` are rejected.
- Maintenance mode (Super portal, Settings page): while on, only Super users can sign in; others get `409 maintenance_mode`
  after a correct password (logged as `auth.sign_in_blocked`). Users already signed in keep working.
- A Super cannot deactivate, demote, reset or delete their own account (`409 own_account`); they change their own password
  with `change-password`. Passwords: at least 8 characters.
- Role policies for future endpoints: `[Authorize(Policy = AuthPolicies.AdminOnly)]`, `[Authorize(Policy = AuthPolicies.SuperOnly)]`.

## Configuration

| Key | Where | Notes |
| --- | --- | --- |
| `Jwt:Key` | `appsettings.json` (empty) / `appsettings.Development.json` (dev key) | **Required, at least 32 characters, unique per installation.** The API refuses to start without it. |
| `Jwt:Issuer`, `Jwt:Audience` | `appsettings.json` | |
| `Seed:Users` | `appsettings.Development.json` | Users created at startup if missing (passwords hashed) |

### Development accounts

| Username | Password | Role |
| --- | --- | --- |
| `super@example.com` | `password` | Super |
| `admin@example.com` | `password` | Admin |

These exist only through the Development seed. Production installs must set their own `Jwt:Key` and first user (installer step, deferred).

## Desktop

| Piece | Responsibility |
| --- | --- |
| `Services/Api/ApiClient` | HTTP calls, maps errors (`Unreachable`, `Unauthorized`, `TooManyRequests`, ...). Knows nothing about tokens. API address: `ApiBaseUrl` in `%AppData%\Mostawsef\settings.json` (default `http://localhost:5270`). **Install decision (deploy-install Block 4):** one-PC clinics use plain `http` on localhost — traffic stays on the machine. Multi-PC / remote API is not the current product; if that changes, put the API behind HTTPS (certificate every desktop trusts) and set `ApiBaseUrl` to `https://…`. The login screen already warns when `ApiBaseUrl` is plain http to another machine (`Services/Local/ApiAddress`). Timeouts (`ServiceRegistration`): 3 s to connect, so a switched-off API PC shows `Unreachable` quickly, and 15 s for a whole request (applied by `ApiClient`, not `HttpClient.Timeout`). Whole-table work (backup export, delete a whole log, data reset) passes `ApiClient.LongRequestTimeout` (10 min) |
| `Services/Api/AuthApi` | `LoginAsync`, `MeAsync` |
| `Services/Auth/AuthHeaderHandler` | `HttpClient` pipeline step: adds the session's bearer token to each request; a `401` for that token expires the session (a late `401` for an earlier session is ignored) |
| `Services/Auth/SessionService` | Current user + token in memory; expiry uses `LoginResponse.ExpiresInSeconds` counted from receipt (so a wrong PC clock does not expire the session at once), and also when `AuthHeaderHandler` reports a rejected token |
| `AppRoutes` (`Services/Navigation/RouteTable`) | Every screen in one table: its view, who may open it, and each role's portal (title + menu). DI, navigation, the shell and `ViewLocator` all read it |
| `Services/Navigation/NavigationService` | Opens the login screen or the user's portal; the single access check (`CanAccess`, `CreatePage`) |
| `ViewModels/Auth/LoginViewModel` + `Views/Auth/LoginView` | Sign-in form |
| `ViewModels/Shell/PortalShellViewModel` + `Views/Shell/PortalShellView` | The one portal layout for every role: sidebar from the role's menu, top bar (user, `ui:PreferencesBar`), sign out |
| `ViewModels/Shell/DashboardViewModel` + `Views/Shell/DashboardView` | Empty dashboard, the first page of every portal (split per role in `AppRoutes` when one needs its own) |

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
