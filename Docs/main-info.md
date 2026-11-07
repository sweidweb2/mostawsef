mini-market/
│
├── backend/
│   ├── MiniMarket.Api/
│   ├── MiniMarket.Application/
│   ├── MiniMarket.Domain/
│   ├── MiniMarket.Infrastructure/
│   │
│   └── MiniMarket.sln
│
├── frontend/
│   └── MiniMarket.Desktop/
│
├── docs/
│
├── scripts/
│
└── README.md


Responsibilities:
Project	Responsibility
Api	HTTP endpoints, controllers, middleware
Application	Use cases, services, DTOs
Domain	Entities, business rules, interfaces
Infrastructure	PostgreSQL, EF Core, repositories
Desktop	UI, screens, navigation, API integration

The desktop application communicates with the backend through REST APIs.