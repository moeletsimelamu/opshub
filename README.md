# OpsHub

[![CI](https://github.com/moeletsimelamu/opshub/actions/workflows/ci.yml/badge.svg)](https://github.com/moeletsimelamu/opshub/actions/workflows/ci.yml)

An internal IT service desk for **Marula Logistics**, a fictional logistics company.
Employees report problems; support agents triage, assign and resolve them; administrators
manage accounts. Built as a full-stack portfolio project with ASP.NET Core 10 and Angular 21,
from requirements to a deployable, tested container.

## What it does

| Role | Can |
|---|---|
| **Requester** (any employee) | Report a problem, follow its progress, reply, answer an agent's questions, confirm a fix or reopen the ticket. Sees only their own tickets |
| **Support agent** | Work the queue (views for triage, "assigned to me", all active; search, filters, sorting, paging), assign tickets, reply or add internal notes, move tickets through the workflow, see the live dashboard |
| **Administrator** | Everything an agent can, plus add users, change roles and deactivate accounts |

Behind that:
- a status workflow enforced on the server (Open → In progress → Waiting for information →
  Resolved → Closed, with reopening);
- a full history of every ticket;
- protection against two people overwriting each other's edits;
- a dashboard with workload and resolution-time figures.

## Documentation

| Document | What's in it |
|---|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Context, structure, data model, security model, API conventions, business rules, and why each decision was made |
| [docs/DESIGN.md](docs/DESIGN.md) | The design system (colour, type, spacing, components) and screen designs |
| [docs/REVIEW.md](docs/REVIEW.md) | The security, accessibility (WCAG 2.2 AA) and responsive review: findings, fixes, how each was verified, and what wasn't tested |
| [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) | Running OpsHub in production: the container, every setting, first deployment, demo, upgrades, backups, go-live checklist |

## Tech stack

ASP.NET Core 10 Web API · Entity Framework Core 10 · SQLite · ASP.NET Core Identity ·
Angular 21 (standalone components, signals, zoneless) · Angular CDK · xUnit · Vitest ·
Docker · GitHub Actions

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
| https://localhost:7003/scalar | Interactive API documentation (Development only) |
| https://localhost:7003/openapi/v1.json | OpenAPI document (Development only) |

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

## Run tests

```bash
# Backend: 58 unit + 135 integration tests
dotnet test

# Frontend: 47 unit and component tests (Vitest + jsdom, no browser needed)
cd src/OpsHub.Web
npx ng test --watch=false
```

| Suite | What it covers |
|---|---|
| `tests/OpsHub.Domain.Tests` | The ticket status workflow and the resolution-time statistics, with exact values (no database, milliseconds) |
| `tests/OpsHub.Api.IntegrationTests` | The real API in memory against a private SQLite database, acting like a browser (cookies + XSRF header): sign-in and lockout, role and requester-visibility rules, validation, workflow, concurrency conflicts, dashboard figures, security headers, and the production setup (first-run setup, serving the Angular app, reverse proxies, HTTPS-only API, key storage) |
| `src/OpsHub.Web/**/*.spec.ts` | Error interpretation (incl. "server unreachable"), the loading-state rules, workflow buttons, the session interceptor, route guards, and the login, status-tag and dashboard components |

Important behaviours were checked with **mutation testing by hand**: the rule was temporarily
broken to confirm a test fails. This was done for:
- requester visibility, internal notes and deactivated accounts;
- the fallback sign-in policy;
- dashboard filters and stale-data handling;
- trusted proxies, the public Angular page, and the one-time administrator bootstrap.

### Continuous integration

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs on every push to `main` and every
pull request, on a clean Ubuntu machine:

- **Backend:** restore → Release build (warnings are errors) → all .NET tests.
- **Frontend:** `npm ci` → unit tests → production build (with bundle budgets) → `npm audit`
  of production dependencies.
- **Container:** build the Docker image → start it like a first production deployment behind an
  HTTPS reverse proxy → [smoke test](scripts/smoke-test.sh) (health, page and security headers,
  a real sign-in) → restart → smoke test again.

## Deployment

OpsHub ships as one Docker image: the API also serves the built Angular app, so the browser
talks to a single origin. HTTPS is handled by a reverse proxy in front, and the database and
session keys live on a `/data` volume. A new installation creates its roles, starter
categories and first administrator from settings; the fictional demo data is refused in
Production.

```bash
docker build --tag opshub .
```

Settings, the first deployment, a public demo, upgrades, backups and a go-live checklist:
**[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)**.

## Security at a glance

- Passwords hashed by ASP.NET Core Identity (PBKDF2); never stored or logged in plain text.
- `HttpOnly`, `Secure`, `SameSite=Strict` session cookie, plus an XSRF token on every
  state-changing request. The API refuses plain HTTP.
- Every endpoint requires sign-in unless explicitly public; roles are checked on the server.
  Requesters can't tell whether another person's ticket exists (404, not 403).
- Account lockout after 5 failed attempts, and login rate limiting per client IP address,
  which stays correct behind a proxy because forwarded addresses are only trusted from known
  proxies.
- Sign-in errors never reveal whether an email has an account.
- Content-Security-Policy (scripts only from OpsHub itself), `nosniff`, no framing, no
  referrer, HSTS outside Development, and a 1 MB request size limit.

Details: [docs/ARCHITECTURE.md § 7](docs/ARCHITECTURE.md#7-security-model) and
[docs/REVIEW.md](docs/REVIEW.md).

## Database migrations

```bash
# After changing an entity or configuration:
dotnet ef migrations add <Name> --project src/OpsHub.Api --output-dir Data/Migrations
```

## Known limitations

- **One instance only:** SQLite and file-based session keys. Scaling out needs a server
  database and shared key storage ([docs/DEPLOYMENT.md § 10](docs/DEPLOYMENT.md#10-limits)).
- **No email:** no password reset by email, no notifications, no email verification, no MFA.
- **No category screen:** a new installation gets five general IT categories.
- **No assignment history:** assignment changes aren't written to a history table (status
  changes are).
- **Session keys are stored unencrypted** on the data volume, which must be protected.
- **Accessibility testing:** done with axe-core and the keyboard, not yet with a screen reader,
  and only in Chromium ([docs/REVIEW.md § 4](docs/REVIEW.md#4-limits-of-this-review)).
- **Not deployed to a cloud platform yet:** the container is tested in CI behind a real HTTPS
  proxy.
- **`npm audit` advisories in `undici`:** it's used only by the Angular **build tool**
  (`@angular/build`) on the developer's machine and isn't shipped to the browser. The
  production dependencies report 0 vulnerabilities (`npm audit --omit=dev`).

## How it was built

The project was planned and built in ten phases. Each phase ended with a written report
(what changed, how to verify it, what remained), a manual check in the browser, and a commit.

| Phase | Result |
|---|---|
| 1. Requirements and plan | Scenario, roles, requirements, architecture, data model, phase plan |
| 2. Backend foundation | Solution structure, EF Core and SQLite, migrations, demo data, health check |
| 3. Authentication | Identity, cookie sessions, XSRF protection, roles and policies, lockout, rate limiting |
| 4. Tickets | Ticket API, status workflow, comments and internal notes, history, concurrency |
| 5. Interface | Design system, then the Angular screens built against the real API |
| 6. Integration hardening | Session expiry, outages, edit conflicts, unsaved work, account page, Send and resolve |
| 7. Dashboard | Live staff metrics and resolution-time statistics |
| 8. Automated testing | Frontend tests and the GitHub Actions pipeline |
| 9. Review | Security, accessibility and responsive review ([REVIEW.md](docs/REVIEW.md)) |
| 10. Deployment | Container, first-run setup, reverse proxy support, deployment guide |

## My personal contribution

OpsHub was built with an AI coding assistant (Claude Code). I wrote the brief and made the
decisions; the assistant wrote most of the code and documentation under my direction. I
reviewed and tested each phase before accepting it. What that meant in practice:

- **Scope and requirements.** I chose the scenario, the three roles, the tech stack and the
  ten-phase plan. I set the quality rules every phase had to meet:
  - explain every decision;
  - never claim a test passed without running it;
  - design system first, then a browser check at desktop and mobile sizes before moving on;
  - no secrets in the repository;
  - honest documentation.
- **Testing as a user.** I ran the app after each phase and walked through it as a requester,
  an agent and an administrator. While testing as a requester I couldn't find how to resolve
  a ticket. That gap led to the "Next step" bar and the *Send and resolve* action.
- **Reviewing and deciding.** I read each phase report and either approved it or asked for
  changes. I decided on the repository's history and attribution, and on when each phase was
  pushed.
- **Running the project.** I set up the GitHub repository, pushed the early phases myself and
  approved every later push.

<!--
  TODO (Moeletsi), in your own words; this comment is not shown on GitHub:
  1. Add to the list above anything you wrote, changed, investigated or fixed yourself.
  2. "What I learned": ideas from the project: cookie sessions vs tokens and why SameSite +
     XSRF; designing an API around a state machine; optimistic concurrency; why a 404 instead
     of a 403; Angular signals; accessibility testing; what changes between "works on my
     machine" and a deployable container.
  3. "What I would do next": e.g. email notifications, a category screen, PostgreSQL for
     scaling out, screen-reader testing, deploying to a cloud platform.
-->
