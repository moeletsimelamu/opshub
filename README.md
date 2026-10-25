# OpsHub

An internal service desk for a fictional logistics company. Employees log support tickets;
support agents triage, assign and resolve them.

> **Status: work in progress (Phase 2 of 10).** The backend foundation exists: data model,
> migrations, demo data, health check and integration tests. Authentication, ticket
> endpoints and the Angular interface are not built yet.

Design decisions are recorded in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Tech stack

ASP.NET Core 10 Web API · Entity Framework Core 10 · SQLite · ASP.NET Core Identity ·
xUnit · *(planned)* Angular 21 · GitHub Actions

## Run locally

Prerequisites: [.NET SDK 10.0.401+](https://dotnet.microsoft.com/download).

```bash
# 1. Restore the pinned dotnet-ef tool
dotnet tool restore

# 2. Choose a password for the fictional demo accounts (stored outside the repo)
dotnet user-secrets set "Seed:DemoPassword" "<at least 12 characters>" --project src/OpsHub.Api

# 3. Run the API (creates src/OpsHub.Api/opshub.db, applies migrations, seeds demo data)
dotnet run --project src/OpsHub.Api --launch-profile http
```

Then open:

| URL | What |
|---|---|
| http://localhost:5050/health | Health check (`Healthy` when the database is reachable) |
| http://localhost:5050/scalar | Interactive API documentation |
| http://localhost:5050/openapi/v1.json | OpenAPI document |

Demo accounts use `<first name>@marula.example`, e.g. `lerato@marula.example` (Administrator),
`sipho@marula.example` (Support Agent), `thandi@marula.example` (Requester).
Sign-in arrives in Phase 3.

To reset the demo data, stop the API, delete `src/OpsHub.Api/opshub.db*` and run it again.

## Run tests

```bash
dotnet test
```

## Database migrations

```bash
# After changing an entity or configuration:
dotnet ef migrations add <Name> --project src/OpsHub.Api --output-dir Data/Migrations
```
