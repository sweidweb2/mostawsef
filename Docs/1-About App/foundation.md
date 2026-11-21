# Mostawsef — Project Foundation

Locally installed **Clinic Management System** (desktop application + local backend + local database).

This document records the foundational decisions only. No business (clinic) modules are defined yet; only the
platform modules exist: auth, activity logs and system errors.

---

## 1. Technology stack

| Layer     | Technology                      | Notes                                        |
| --------- | ------------------------------- | -------------------------------------------- |
| Runtime   | .NET 10 (LTS)                   | Supported until November 2028                |
| Backend   | ASP.NET Core Web API (C#)       | Controllers-based REST API                   |
| Frontend  | Avalonia UI 12 (C# / XAML, MVVM)| CommunityToolkit.Mvvm, see `ui-foundation.md` |
| Database  | PostgreSQL 17                   | Docker in development, native on clinic PCs  |
| ORM       | Entity Framework Core 10 + Npgsql | Data access, mappings, migrations          |
| IDE       | Cursor                          | C# + Avalonia extensions                     |

### Why PostgreSQL (not SQLite)

- Chosen deliberately, accepting that the app cannot be "copied from USB and run": PostgreSQL must be installed on the host PC.
- Supports multiple PCs on the clinic LAN sharing one database.
- SQLite remains a fallback. To keep switching cheap, follow the portability rules in section 2.

---

## 2. Architecture

**Style:** Modular monolith. No microservices.

```
┌──────────────────────────┐
│    Desktop Application   │
│       Avalonia UI        │
└────────────┬─────────────┘
             │ HTTP / REST
┌────────────▼─────────────┐
│    ASP.NET Core API      │
│      Business Logic      │
└────────────┬─────────────┘
             │ EF Core
┌────────────▼─────────────┐
│       PostgreSQL         │
│      Local Database      │
└──────────────────────────┘
```

### Rules

- The desktop application communicates with the backend **only** through the REST API.
- The desktop application **never** connects to PostgreSQL directly.
- All business operations live in the backend.
- Database packages (EF Core, Npgsql) are installed **only** in the Infrastructure project. The Api project references `Microsoft.EntityFrameworkCore.Design` only because it is the startup project for EF tooling.

### Database portability rules

Keep the option of switching to SQLite open:

- No PostgreSQL-only features: `jsonb`, array columns, `citext`, `EF.Functions.ILike`, raw SQL (also not in migrations;
  fill new columns for existing rows with EF Core code run by `DatabaseInitializer`, e.g. `SearchTextBackfill`).
- One allowed exception: **index-only** PostgreSQL features, such as the `pg_trgm` GIN indexes on the log `SearchText`
  columns. The queries they speed up stay plain LINQ (`LIKE`), so they still run on SQLite, only without that index.
- All database code stays in Infrastructure; switching provider means changing one package and one `Use...()` call.
- Store money as integers in the smallest unit (e.g. piasters), not `decimal`.
- Keep column types simple (text, integers, booleans, dates).

---

## 3. Repository structure

```
mostawsef/
│
├── backend/
│   ├── Mostawsef.Api/
│   ├── Mostawsef.Application/
│   ├── Mostawsef.Domain/
│   └── Mostawsef.Infrastructure/
│
├── shared/
│   └── Mostawsef.Contracts/
│
├── frontend/
│   └── Mostawsef.Desktop/
│
├── tests/
│   ├── Mostawsef.Api.IntegrationTests/
│   └── Mostawsef.Desktop.Tests/
│
├── .github/workflows/   ← tests.yml: CI runs the tests on every push
├── Docs/
├── scripts/                 ← test.ps1, capture-window.ps1
├── docker-compose.yml       ← development database only
├── Directory.Build.props    ← settings shared by every project
├── Directory.Packages.props ← the one list of package versions
├── .editorconfig            ← code style and analyzer rules
├── Mostawsef.slnx
└── README.md
```

The solution file lives at the **repository root** because it contains both backend and frontend projects.

### Shared build settings

- `Directory.Build.props` sets `net10.0`, nullable, implicit usings (off in the desktop project), the .NET analyzers at
  `latest-recommended`, code style checks in the build, and **warnings as errors**. A `.csproj` only lists what is
  special to it.
- `Directory.Packages.props` holds every package version (central package management). A `.csproj` references a
  package without `Version`; `dotnet add package` writes the version there.
- `.editorconfig` holds formatting, the style rules the build enforces (file-scoped namespaces, braces on every `if`)
  and the analyzer rules switched off or lowered to suggestions, each with its reason.

---

## 4. Project responsibilities

| Project                  | Responsibility                                               |
| ------------------------ | ------------------------------------------------------------ |
| Mostawsef.Api            | HTTP endpoints, controllers, middleware, startup             |
| Mostawsef.Application    | Use cases, services, application interfaces                  |
| Mostawsef.Domain         | Entities, enums, business rules, domain interfaces           |
| Mostawsef.Infrastructure | PostgreSQL, EF Core, DbContext, repositories                 |
| Mostawsef.Contracts      | Request/response DTOs shared by the API and the desktop app  |
| Mostawsef.Desktop        | UI, screens, navigation, API client                          |

### Dependency direction

```
Domain          ← depends on nothing
Application     → Domain, Contracts
Infrastructure  → Domain, Application
Api             → Application, Infrastructure, Contracts
Contracts       ← depends on nothing
Desktop         → Contracts   (never references backend projects)
```

---

## 5. Folder layout

### Backend

```
backend/
│
├── Mostawsef.Api/
│   ├── Controllers/
│   ├── Middleware/
│   ├── Extensions/
│   ├── Program.cs
│   └── appsettings.json
│
├── Mostawsef.Application/
│   ├── Common/
│   ├── Interfaces/
│   └── Modules/            ← one folder per feature (Auth, Logs; business modules later)
│
├── Mostawsef.Domain/
│   ├── Common/
│   ├── Entities/
│   ├── Enums/
│   └── Interfaces/
│
└── Mostawsef.Infrastructure/
    ├── Persistence/
    │   ├── Context/
    │   ├── Configurations/
    │   └── Repositories/
    ├── Services/
    └── DependencyInjection.cs
```

`Persistence/Migrations/` is generated by EF Core on the first migration. Do not create it manually.

### Frontend

```
frontend/Mostawsef.Desktop/
│
├── Views/
├── ViewModels/
├── Models/
├── Services/
│   ├── Api/
│   └── Local/
├── Components/
├── Converters/
├── Assets/
├── Styles/
├── Navigation/
├── App.axaml
└── Program.cs
```

These are organizational folders only, not a commitment to implement every area immediately. Folders are created when their first file is added (git does not track empty folders).

---

## 6. Environment setup (Windows)

1. Install the **.NET 10 SDK x64** (the SDK, not only the runtime): https://dotnet.microsoft.com/en-us/download/dotnet/10.0
2. Verify:
   ```powershell
   dotnet --version
   dotnet --list-sdks
   ```
3. Install Avalonia templates:
   ```powershell
   dotnet new install Avalonia.Templates
   dotnet new list avalonia
   ```
4. Install Cursor extensions:
   ```powershell
   cursor --install-extension anysphere.csharp
   cursor --install-extension AvaloniaTeam.vscode-avalonia
   ```
5. Install **Docker Desktop** (WSL 2 backend) for the development database.

> After installing the SDK, fully quit Cursor (File → Exit) and reopen it, otherwise its terminals will not find `dotnet`.

---

## 7. Scaffolding commands

Run from the repository root.

> On .NET 10, `dotnet new sln` creates a `.slnx` file by default. All commands below use `Mostawsef.slnx`.

```powershell
# Solution
dotnet new sln -n Mostawsef

# Backend
dotnet new webapi   -n Mostawsef.Api            -o backend/Mostawsef.Api --use-controllers
dotnet new classlib -n Mostawsef.Application    -o backend/Mostawsef.Application
dotnet new classlib -n Mostawsef.Domain         -o backend/Mostawsef.Domain
dotnet new classlib -n Mostawsef.Infrastructure -o backend/Mostawsef.Infrastructure

# Shared
dotnet new classlib -n Mostawsef.Contracts      -o shared/Mostawsef.Contracts

# Frontend
dotnet new avalonia.mvvm -n Mostawsef.Desktop   -o frontend/Mostawsef.Desktop

# Add to solution
dotnet sln Mostawsef.slnx add backend/Mostawsef.Api
dotnet sln Mostawsef.slnx add backend/Mostawsef.Application
dotnet sln Mostawsef.slnx add backend/Mostawsef.Domain
dotnet sln Mostawsef.slnx add backend/Mostawsef.Infrastructure
dotnet sln Mostawsef.slnx add shared/Mostawsef.Contracts
dotnet sln Mostawsef.slnx add frontend/Mostawsef.Desktop

# Project references
dotnet add backend/Mostawsef.Application    reference backend/Mostawsef.Domain
dotnet add backend/Mostawsef.Application    reference shared/Mostawsef.Contracts

dotnet add backend/Mostawsef.Infrastructure reference backend/Mostawsef.Domain
dotnet add backend/Mostawsef.Infrastructure reference backend/Mostawsef.Application

dotnet add backend/Mostawsef.Api            reference backend/Mostawsef.Application
dotnet add backend/Mostawsef.Api            reference backend/Mostawsef.Infrastructure
dotnet add backend/Mostawsef.Api            reference shared/Mostawsef.Contracts

dotnet add frontend/Mostawsef.Desktop       reference shared/Mostawsef.Contracts

# Database packages
dotnet add backend/Mostawsef.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add backend/Mostawsef.Infrastructure package Microsoft.EntityFrameworkCore.Design
dotnet add backend/Mostawsef.Api            package Microsoft.EntityFrameworkCore.Design
```

---

## 8. Development database (Docker)

Defined in `docker-compose.yml` at the repository root. Development only.

| Setting  | Value           |
| -------- | --------------- |
| Host     | `localhost`     |
| Port     | `5432`          |
| Database | `mostawsef`     |
| User     | `mostawsef`     |
| Password | `mostawsef_dev` |

The matching connection string is in `backend/Mostawsef.Api/appsettings.Development.json` under `ConnectionStrings:Default`.

```powershell
docker compose up -d                    # start PostgreSQL
docker compose --profile tools up -d    # start PostgreSQL + pgAdmin (http://localhost:5050)
docker compose down                     # stop (data is kept)
docker compose down -v                  # stop and wipe all data
```

On clinic PCs, PostgreSQL is installed natively (not Docker) and the connection string is set in `appsettings.json` next to the API.

---

## 9. Run and verify

```powershell
docker compose up -d
dotnet build Mostawsef.slnx

# Terminal 1
dotnet run --project backend/Mostawsef.Api --launch-profile http

# Terminal 2
dotnet run --project frontend/Mostawsef.Desktop
```

Health check: `GET http://localhost:5270/api/health` returns

```json
{ "status": "Healthy", "database": "Connected", "apiVersion": 1 }
```

Without a launch profile (installed API) it listens on `http://localhost:5270`, the desktop default. Anything that names
an address replaces that default: `ASPNETCORE_URLS`, `--urls`, `Urls` or `Kestrel:Endpoints` in `appsettings.json`
(`Program.cs`). Do not put `Urls` in the shared `appsettings.json`: it would override the launch profiles.

---

## 10. Current bedrock (implemented)

| Area | Where | Details |
| --- | --- | --- |
| Database | `Infrastructure/Persistence/Context/AppDbContext.cs`, `Configurations/`, `Migrations/` | Entities `User`, `ActivityLog`, `SystemError`, `ClinicSettings`, `SystemSettings`, `NumberSequence` |
| Startup | `Api/Program.cs`, `Infrastructure/Persistence/DatabaseInitializer.cs` | Applies pending migrations, creates the clinic and system settings rows and seeds `Seed:Users` when the API starts |
| Health | `Api/Controllers/HealthController.cs` | `GET /api/health` verifies API + database and reports `apiVersion` (`Contracts/Common/ApiVersion`) |
| Connection banner | desktop `Services/Api/ConnectionMonitor`, `PortalShellView` | While signed in: health check every 60 s (3 s while offline); banner when the server is unreachable or runs another API version |
| Clinic settings | `ClinicSettingsController`, `Application/Modules/Settings`, desktop `ClinicContext` + Admin page | Name, address, phone, currency. Everyone reads, Admin edits. No logo yet (storage still to decide) |
| System settings | `SystemSettingsController`, `Application/Modules/Settings`, desktop Super "Settings" page | Backup folder for scheduled `pg_dump` dumps (Browse on the edit form) and maintenance mode (only Super can sign in). Super only. Empty folder = scheduled dumps skipped |
| Backups | `BackupsController`, `ScheduledDumpsController`, `Application/Modules/Backups`, `Infrastructure/Services/Backup`, desktop Super "Backups" page + `ScheduledBackupWorker` | Two different things on one Super page: (1) table export — CSV/JSON/SQL/PostgreSQL script of selected tables (not `users`), saved where the user picks; history in `backup_records`. (2) Scheduled restorable dumps — while the API runs, `ScheduledBackupWorker` writes `mostawsef-*.dump` via `pg_dump` into `SystemSettings.BackupPath` (default: API install `\Backups`), keeps the newest `ScheduledBackup:KeepCount` files **and** the same number of `scheduled_dumps` history rows (when/where/size/keep/interval/purged), logs `backups.scheduled`. Needs PostgreSQL client tools on the API PC. Restore: `pg_restore` (see `Docs/commands.md`). Super only |
| Readable numbers | `Application/Interfaces/INumberSequence`, `Domain/Common/DocumentNumber` | `P-000123`-style numbers per sequence name, safe across PCs (gaps possible after a failed save) |
| Clock, dates, money | `TimeProvider` (Infrastructure DI), `Contracts/Common/Money` + `Currencies`, desktop `{l:Money}` and `ValueConverters.DateOnly` | One injectable clock; money as `long` minor units shown with the clinic currency; `DateOnly` for calendar dates |
| Printing | desktop `Services/Printing` (QuestPDF) | `ClinicPage` layout (clinic header, page footer, RTL in Arabic); first document: the lab test list exported from the Admin lab tests page |
| Auth | `AuthController`, `Application/Modules/Auth`, `Infrastructure/Services/Auth` | JWT, roles `Admin`/`Super`, PBKDF2 hashing, login rate limit. See `auth.md` |
| Logging | `ActivityLogsController`, `SystemErrorsController`, `Application/Modules/Logs`, `ExceptionHandlingMiddleware` | Activity logs and system errors. See `logging.md` |
| Users | `UsersController`, `Application/Modules/Users`, desktop `UsersViewModel` + forms | User management (Super) and change password (everyone). The reference module to copy for CRUD features. See `auth.md` |
| Errors and validation | `Contracts/Common/ErrorCodes`, `Application/Common/AppException`, `ExceptionHandlingMiddleware`, desktop `FormViewModel` | One error code per failure, from DataAnnotations or `AppException`; the desktop shows it next to the field or on top of the form, in both languages. See `.cursor/rules/backend.mdc` and `frontend-ui.mdc` |
| Entity conventions | `Domain/Common/Entity`, `SoftDeletableEntity`, `SearchText`, `Infrastructure/Persistence/AuditInterceptor` | Who/when audit fields, `Version` concurrency token (409 on conflicting edits), soft delete with a global filter, Arabic-aware search normalization |
| Desktop | `frontend/Mostawsef.Desktop` | Login, one portal shell per role from `AppRoutes`, Super pages for logs/errors, shared components, Light/Dark, Arabic/English. See `ui-foundation.md` |
| Tests | `tests/` | API integration tests (Testcontainers) and headless desktop tests. See `.cursor/rules/testing.mdc` |
| Dev database | `docker-compose.yml` | Development PostgreSQL (+ optional pgAdmin) |
| Lab tests | `LabTestsController`, `Application/Modules/LabTests`, desktop `LabTestsViewModel` + form | The test catalog (Admin): code, name, lab cost, active (no patient price; the clinic's charge is set per order). A test used by an order cannot be deleted (409 `in_use`) |
| Patients | `PatientsController`, `Application/Modules/Patients`, desktop `PatientsViewModel` + form | Patient records (Admin), soft delete, Arabic-aware name/phone search |
| Orders | `OrdersController`, `Application/Modules/Orders`, desktop `OrdersViewModel`, `NewOrderViewModel`, `OrderDetailsViewModel` | Admin: list (reads denormalized `TestsCount`/`Paid`), new order on one page (patient, tests catalog, optional addon + first payment; catalog loads with the page; saved in one transaction, `ORD-000123` numbers), read-only details (shows our price when > 0). `Order.Total` is the patient charge: the snapshotted lab prices plus our price (`Addon`, stored on its own; empty means no extra). A first payment is capped by that total. `OrderItem` snapshots the test's code, name and `LabCost` (Contracts `OrderItemDto.LabCost`), so a later catalog price change does not change the order, and a test used by an order cannot be deleted. No edit, cancel, discounts, refunds or printing yet |

---

## 11. Decision log

| Decision            | Choice                         | Reason                                                  |
| ------------------- | ------------------------------ | ------------------------------------------------------- |
| Architecture        | Modular monolith               | Simple to build and deploy locally                      |
| Frontend            | Avalonia                       | Mature, portable, cross-platform option kept open       |
| Database            | PostgreSQL                     | Multi-PC ready; SQLite kept as fallback                 |
| Dev database        | Docker                         | No local install, easy reset                            |
| Shared DTOs         | `Mostawsef.Contracts` project  | One definition used by API and desktop                  |
| Solution format     | `.slnx` at repository root     | .NET 10 default; contains backend and frontend          |
| Project name prefix | `Mostawsef`                    | Repository name                                         |
| UI languages        | Arabic (default, RTL) + English | Switchable at runtime                                  |
| UI themes           | Light + Dark, token based      | Brand palette can be patched in `Colors.axaml`          |
| Shared components   | Build the core set, defer the rest | New components are created when a screen needs one |
| Expected errors     | `AppException` + `ErrorCodes`, mapped by middleware | One code per failure, translated on the desktop; controllers stay one-liners |
| Paging              | Page numbers for lists sorted by a field; cursors for logs | Logs only grow (deep pages must stay fast); business lists sort by name |
| Clock               | `TimeProvider` injected | Testable time; one source for audit fields, tokens and "today" |
| Money               | `long` minor units + clinic currency | Exact, portable to SQLite, no per-amount currency |
| Reference lists     | Enums for fixed workflow values, entities for staff-edited lists | No generic lookup table to maintain |
| Printing            | QuestPDF (Community license) on the desktop | Correct Arabic shaping and RTL with the app font; PDF opened in the default viewer |
| Arabic digits       | Latin digits in both languages (ar-EG) | Matches typed input, phone numbers and printouts |

UI details: `Docs/ui-foundation.md`. Component catalog: `Docs/shared-components.md`. AI guidance: `.cursor/rules/`.

## 12. Deferred (planned for later)

- Automatically starting the backend when the desktop app opens.
- Installing PostgreSQL natively on clinic PCs (installer or setup script).
- Packaging everything into an installer.
- Backups and recovery.
- Clinic logo (decide where images are stored first: database or files next to the API).
- Unsaved-changes guard for full-page forms (forms open in dialogs today, which already ask).
- Business modules (to be defined).
