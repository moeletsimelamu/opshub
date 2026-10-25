# OpsHub

An internal service desk for a fictional logistics company. Employees log support tickets;
support agents triage, assign and resolve them.

> **Status: work in progress (Phase 5 of 10).** The API and the Angular interface work end to
> end: sign-in, a ticket queue with views, filters, search, sorting and paging, ticket detail
> with the status workflow, assignment, replies and internal notes, ticket create/edit, and
> user administration. The dashboard (Phase 7) and CI (Phase 8) are not built yet.

- Architecture and security decisions: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- Visual design system and screen designs: [docs/DESIGN.md](docs/DESIGN.md)

## Tech stack

ASP.NET Core 10 Web API · Entity Framework Core 10 · SQLite · ASP.NET Core Identity ·
Angular 21 (standalone components, signals, zoneless) · Angular CDK · xUnit ·
*(planned)* GitHub Actions

## Run locally

Prerequisites: [.NET SDK 10.0.401+](https://dotnet.microsoft.com/download),
[Node.js 22.12+](https://nodejs.org/) and a trusted HTTPS development certificate.

### 1. The API

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

Then you can open:

| URL | What |
|---|---|
| https://localhost:7003/health | Health check (`Healthy` when the database is reachable) |
| https://localhost:7003/scalar | Interactive API documentation |
| https://localhost:7003/openapi/v1.json | OpenAPI document |

### 2. The web app

In a second terminal:

```bash
cd src/OpsHub.Web
npm ci
npm start
```

Open **http://localhost:4200** and sign in with a demo account below. The Angular dev server
forwards `/api` calls to the API on `https://localhost:7003` (see `proxy.conf.mjs`), so the
browser only ever talks to one origin and the session cookie works without CORS. To point it
at another API address, set `OPSHUB_API_URL` before `npm start`.

> **If `npm ci`/`npm install` fails with `Cannot read properties of null (reading 'edgesOut')`:**
> that is a bug in npm 10.9.2 (bundled with Node 22.14). Run `npx npm@11 ci` instead, or
> update npm.

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

## Known limitations (so far)

- Assignment changes aren't written to a history table (status changes are).
- No password reset by email, MFA or email verification (demo scope).
- When the mobile navigation drawer opens, focus isn't yet moved into it (Phase 9 accessibility review).
- `npm audit` reports advisories in `undici`, used only by the Angular **build tool**
  (`@angular/build`) on the developer's machine. It isn't shipped to the browser; the production
  dependencies report 0 vulnerabilities (`npm audit --omit=dev`). Waiting for an Angular patch.
