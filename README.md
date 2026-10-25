# OpsHub

An internal service desk for a fictional logistics company. Employees log support tickets;
support agents triage, assign and resolve them.

> **Status: work in progress (Phase 4 of 10).** The backend API is functional: sign-in with a
> secure session cookie, role-based authorisation, user administration, and ticket management
> (create, edit, assign, status workflow, comments with internal notes, history, filtering,
> search, sorting, paging). It is covered by unit and integration tests. The dashboard and the
> Angular interface are not built yet.

Design and security decisions are recorded in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Tech stack

ASP.NET Core 10 Web API · Entity Framework Core 10 · SQLite · ASP.NET Core Identity ·
xUnit · *(planned)* Angular 21 · GitHub Actions

## Run locally

Prerequisites: [.NET SDK 10.0.401+](https://dotnet.microsoft.com/download) and a trusted
HTTPS development certificate. The session cookie is only ever sent over HTTPS.

```bash
# 1. Trust the local HTTPS certificate (once per machine)
dotnet dev-certs https --trust

# 2. Restore the pinned dotnet-ef tool
dotnet tool restore

# 3. Choose a password for the fictional demo accounts (stored outside the repo)
dotnet user-secrets set "Seed:DemoPassword" "<at least 12 characters>" --project src/OpsHub.Api

# 4. Run the API (creates src/OpsHub.Api/opshub.db, applies migrations, seeds demo data)
dotnet run --project src/OpsHub.Api --launch-profile https
```

Then open:

| URL | What |
|---|---|
| https://localhost:7003/health | Health check (`Healthy` when the database is reachable) |
| https://localhost:7003/scalar | Interactive API documentation |
| https://localhost:7003/openapi/v1.json | OpenAPI document |

### Demo accounts

All use the password you set in step 3.

| Email | Role |
|---|---|
| `lerato@marula.example` | Administrator |
| `sipho@marula.example`, `ayesha@marula.example`, `johan@marula.example` | Support Agent |
| `kagiso@marula.example` | Support Agent (deactivated, so sign-in fails) |
| `thandi@marula.example`, `pieter@marula.example`, `naledi@marula.example`, `ravi@marula.example`, `emma@marula.example`, `busisiwe@marula.example` | Requester |

To reset the demo data, stop the API, delete `src/OpsHub.Api/opshub.db*` and run it again.

### Try the sign-in flow

With the API running, this script signs in the way the browser will (XSRF token → login →
admin-only call → logout) and prints each result. It prompts for the password without
showing it.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Test-SignIn.ps1 -Email lerato@marula.example
```

An administrator sees a list of users; a requester (e.g. `thandi@marula.example`) gets
**403 Forbidden** at step 3. `-ExecutionPolicy Bypass` applies only to that one command.
Forgot the demo password? `dotnet user-secrets list --project src/OpsHub.Api`.

## Security at a glance

- Passwords hashed by ASP.NET Core Identity (PBKDF2); never stored or logged in plain text.
- `HttpOnly`, `Secure`, `SameSite=Strict` session cookie, plus an XSRF token on every state-changing request.
- Every endpoint requires sign-in unless explicitly public; roles are checked on the server.
- Account lockout after 5 failed attempts, and login rate limiting per IP address.
- Sign-in errors never reveal whether an email has an account.

Details and limitations: [docs/ARCHITECTURE.md § 7](docs/ARCHITECTURE.md#7-security-model).

## Run tests

```bash
dotnet test
```

- `tests/OpsHub.Domain.Tests`: unit tests for the ticket status workflow (no database, milliseconds).
- `tests/OpsHub.Api.IntegrationTests`: start the real API in memory against a private SQLite
  database and act like a browser (they keep cookies and send the XSRF header). They cover
  permissions, requester visibility, validation, the workflow and concurrency conflicts.

## Database migrations

```bash
# After changing an entity or configuration:
dotnet ef migrations add <Name> --project src/OpsHub.Api --output-dir Data/Migrations
```
