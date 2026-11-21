# Mostawsef

A clinic management system installed on the clinic's own PCs: an Avalonia desktop app (Arabic and English, Light and
Dark) talking over HTTP to a local ASP.NET Core API, which stores everything in PostgreSQL.

```
Desktop app (Avalonia)  --HTTP-->  API (ASP.NET Core)  --EF Core-->  PostgreSQL
```

## Prerequisites

- .NET 10 SDK (x64)
- Docker Desktop (development database and API tests)

## Quick start

```powershell
docker compose up -d
dotnet run --project backend/Mostawsef.Api --launch-profile http
dotnet run --project frontend/Mostawsef.Desktop
```

On the login screen use **Enter clinic** for the Admin portal, or sign in as `super@example.com` / `password` for the Super portal.
The API creates and updates the database on startup. Health check: http://localhost:5270/api/health

## Tests

```powershell
./scripts/test.ps1            # everything (API tests need Docker)
./scripts/test.ps1 desktop    # desktop only
./scripts/test.ps1 api Users  # API tests whose name contains "Users"
```

GitHub Actions runs the API and desktop tests on every push (`.github/workflows/tests.yml`).

More commands (migrations, publishing, PATH fix): [Docs/commands.md](Docs/commands.md).

## Where things are

| Topic | Read |
| --- | --- |
| Decisions, structure, what is built | [Docs/foundation.md](Docs/foundation.md) |
| Sign-in, roles, users, clinic settings endpoints | [Docs/auth.md](Docs/auth.md) |
| Activity logs and system errors | [Docs/logging.md](Docs/logging.md) |
| Design tokens, themes, RTL, responsiveness | [Docs/ui-foundation.md](Docs/ui-foundation.md) |
| Shared UI components | [Docs/shared-components.md](Docs/shared-components.md) |
| Rules for code (layers, validation, forms, tests) | [.cursor/rules/](.cursor/rules/) |

To add a feature, copy the users module (API + desktop list + form + tests); see `.cursor/rules/working-principles.mdc`.
