# OpsHub

[![CI](https://github.com/moeletsimelamu/opshub/actions/workflows/ci.yml/badge.svg)](https://github.com/moeletsimelamu/opshub/actions/workflows/ci.yml)

An internal service desk for a fictional logistics company. Employees log support tickets;
support agents triage, assign and resolve them.

> **Status: work in progress (Phase 8 of 10).** The API and the Angular interface work end to
> end: sign-in, a ticket queue with views, filters, search, sorting and paging, ticket detail
> with the status workflow, assignment, replies, internal notes and *Send and resolve*, ticket
> create/edit, user administration and change password. Session expiry, API outages, editing
> conflicts and unsaved work are handled. Staff have a live dashboard (open tickets, triage,
> status and priority breakdowns, agent workload, resolution time). Every push is built and
> tested by GitHub Actions.

- Architecture and security decisions: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- Visual design system and screen designs: [docs/DESIGN.md](docs/DESIGN.md)

## Tech stack

ASP.NET Core 10 Web API · Entity Framework Core 10 · SQLite · ASP.NET Core Identity ·
Angular 21 (standalone components, signals, zoneless) · Angular CDK · xUnit · Vitest ·
GitHub Actions

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

> `npm ci` installs from the committed lockfile and works with npm 10.9.2. Only *creating* a
> lockfile from scratch (`npm install` without one) hits the npm 10.9.2 bug
> `Cannot read properties of null (reading 'edgesOut')`; use `npx npm@11 install` in that case.

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
- Strict response headers (CSP, `nosniff`, no framing, no referrer), HSTS outside Development,
  and a 1 MB request size limit.

Details and limitations: [docs/ARCHITECTURE.md § 7](docs/ARCHITECTURE.md#7-security-model).

## Security, accessibility and responsive review

[docs/REVIEW.md](docs/REVIEW.md) records the Phase 9 review: each finding with its severity,
fix and how it was verified. The result: 0 axe-core violations on every screen and dialog
(WCAG 2.2 AA), and no sideways scrolling at 375, 768, 1024 or 1440 px. It also lists what
wasn't tested (no screen reader, Chromium only).

## Run tests

```bash
# Backend: 58 unit + 112 integration tests
dotnet test

# Frontend: 47 unit and component tests (Vitest + jsdom, no browser needed)
cd src/OpsHub.Web
npx ng test --watch=false
```

| Suite | What it covers |
|---|---|
| `tests/OpsHub.Domain.Tests` | The ticket status workflow and the resolution-time statistics, with exact values (no database, milliseconds) |
| `tests/OpsHub.Api.IntegrationTests` | The real API in memory against a private SQLite database, acting like a browser (cookies + XSRF header): sign-in and lockout, role and requester-visibility rules, validation, workflow, concurrency conflicts, dashboard figures |
| `src/OpsHub.Web/**/*.spec.ts` | Error interpretation (incl. "server unreachable"), the loading-state rules, workflow buttons, the session interceptor, route guards, and the login, status-tag and dashboard components |

Important behaviours were checked with **mutation testing by hand**: the rule was temporarily
broken to confirm a test fails (requester visibility, internal notes, deactivated accounts,
the fallback sign-in policy, dashboard filters, stale-data handling).

### Continuous integration

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs on every push to `main` and every
pull request, on a clean Ubuntu machine:

- **Backend:** restore → Release build (warnings are errors) → all .NET tests
- **Frontend:** `npm ci` → unit tests → production build (with bundle budgets) → `npm audit` of
  production dependencies

## Database migrations

```bash
# After changing an entity or configuration:
dotnet ef migrations add <Name> --project src/OpsHub.Api --output-dir Data/Migrations
```

## Known limitations (so far)

- Assignment changes aren't written to a history table (status changes are).
- No password reset by email, MFA or email verification (demo scope).
- Accessibility was checked with axe-core and the keyboard, not yet with a screen reader, and
  only in Chromium ([docs/REVIEW.md § 4](docs/REVIEW.md#4-limits-of-this-review)).
- `npm audit` reports advisories in `undici`, used only by the Angular **build tool**
  (`@angular/build`) on the developer's machine. It isn't shipped to the browser; the production
  dependencies report 0 vulnerabilities (`npm audit --omit=dev`). Waiting for an Angular patch.
