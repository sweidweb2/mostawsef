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
├── Docs/
├── scripts/
├── docker-compose.yml
└── Mostawsef.slnx


Responsibilities:
Project	Responsibility
Api	HTTP endpoints, controllers, middleware
Application	Use cases, services, application interfaces
Domain	Entities, enums, business rules
Infrastructure	PostgreSQL, EF Core, repositories, auth/logging services
Contracts	Request/response DTOs shared by the API and the desktop app
Desktop	UI, screens, navigation, API client

The desktop application communicates with the backend through REST APIs only.
Full details: foundation.md.
