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
| `Role` | varchar(20) | `Admin` or `Super` |
| `IsActive` | boolean | Inactive users cannot sign in |
| `LastLoginAt`, `CreatedAt`, `UpdatedAt` | timestamptz | |

Migration: `Persistence/Migrations/*_InitialUsers`. Migrations are applied automatically when the API starts.

## Password hashing

ASP.NET Core Identity `PasswordHasher` (format v3): **PBKDF2 with HMAC-SHA512, 100,000 iterations, a random 128-bit salt per password**.
The salt and parameters are stored inside the hash string. If the hashing parameters are strengthened in a future version, hashes are upgraded automatically at the next successful sign-in.
Unknown usernames take the same hashing time as wrong passwords, so response time does not reveal which usernames exist.

## API

| Endpoint | Auth | Result |
| --- | --- | --- |
| `POST /api/auth/login` `{ username, password }` | Anonymous, max 10 requests/min per IP | `200 { accessToken, expiresAt, user }`, `400` empty fields, `401` invalid, `429` too many |
| `GET /api/auth/me` | Bearer token | `200 { id, username, role }`, `401` |

- Tokens: JWT signed with HMAC-SHA256, valid 8 hours (`Jwt:ExpiryMinutes`), claims `sub`, `unique_name`, `role`.
- Role policies for future endpoints: `[Authorize(Policy = AuthPolicies.AdminOnly)]`, `[Authorize(Policy = AuthPolicies.SuperOnly)]`.

## Configuration

| Key | Where | Notes |
| --- | --- | --- |
| `Jwt:Key` | `appsettings.json` (empty) / `appsettings.Development.json` (dev key) | **Required, at least 32 characters, unique per installation.** The API refuses to start without it. |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryMinutes` | `appsettings.json` | |
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
| `Services/Api/ApiClient` | HTTP calls, maps errors (`Unreachable`, `Unauthorized`, `TooManyRequests`, ...). Knows nothing about tokens. API address: `ApiBaseUrl` in `%AppData%\Mostawsef\settings.json` (default `http://localhost:5270`). Timeouts (`ServiceRegistration`): 3 s to connect, so a switched-off API PC shows `Unreachable` quickly, and 15 s for a whole request |
| `Services/Api/AuthApi` | `LoginAsync`, `MeAsync` |
| `Services/Auth/AuthHeaderHandler` | `HttpClient` pipeline step: adds the session's bearer token to each request; a `401` for that token expires the session (a late `401` for an earlier session is ignored) |
| `Services/Auth/SessionService` | Current user + token in memory, auto sign-out at token expiry or when `AuthHeaderHandler` reports a rejected token |
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
