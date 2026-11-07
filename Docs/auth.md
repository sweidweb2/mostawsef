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
- After sign-in, the desktop app opens the portal for that role. Each portal has its own shell (sidebar menu) built on a shared layout.
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
| `Services/Api/ApiClient` | HTTP calls, bearer token, maps errors (`Unreachable`, `Unauthorized`, `TooManyRequests`, ...). API address: `ApiBaseUrl` in `%AppData%\Mostawsef\settings.json` (default `http://localhost:5270`) |
| `Services/Api/AuthApi` | `LoginAsync`, `MeAsync` |
| `Services/Auth/SessionService` | Current user + token in memory, auto sign-out at token expiry or on any `401` |
| `Services/Navigation/NavigationService` | Opens the login screen or a portal; enforces `[RequiresRole]` / `[RequiresAuthentication]` |
| `ViewModels/Auth/LoginViewModel` + `Views/Auth/LoginView` | Sign-in form |
| `ViewModels/Shell/PortalShellViewModel` + `Views/Shell/PortalShellView` | Shared portal layout: sidebar, top bar (user, theme, language), sign out |
| `ViewModels/Admin/AdminShellViewModel` | `[RequiresRole(Roles.Admin)]`, Admin menu |
| `ViewModels/Super/SuperShellViewModel` | `[RequiresRole(Roles.Super)]`, Super menu |
| `Admin/AdminDashboard*`, `Super/SuperDashboard*` | Empty dashboard pages |

### Route protection

- Every shell and page view model declares `[RequiresRole(...)]`. `NavigationService` and the shells check it before opening a page; a mismatch signs out and returns to login.
- The API enforces the same roles on its endpoints, so the desktop check is for UX and the server check is the real security boundary.

### Adding a page to a portal

1. Create `ViewModels/<Role>/<Name>ViewModel` with `[RequiresRole(Roles.<Role>)]` and `Views/<Role>/<Name>View.axaml`.
2. Register the view model in `App.ConfigureServices()`.
3. Add `new ShellMenuItem("Icon...", "Nav.<Name>", typeof(<Name>ViewModel))` to the shell's `MenuItems` and add `Nav.<Name>` to both resx files.

### Adding a role

1. Add it to `Domain/Enums/UserRole` and `Contracts/Auth/Roles` (+ an API policy).
2. Create `<Role>ShellViewModel : PortalShellViewModel` with `[RequiresRole]`, then route it in `NavigationService.NavigateToHome()`.
3. Add `Portal.<Role>` and `Role.<Role>` strings.

## Run

```powershell
docker compose up -d
dotnet run --project backend/Mostawsef.Api --launch-profile http
dotnet run --project frontend/Mostawsef.Desktop
```
