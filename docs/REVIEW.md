# Security, accessibility and responsive-design review

This review was done after the features were complete, to find problems that
feature-by-feature work tends to miss. Every finding below was **reproduced first, fixed, and
then re-checked** the same way it was found. Nothing here is a claim about something that
wasn't tested; the [limits of this review](#4-limits-of-this-review) are listed at the end.

Severity is a judgement for this application (an internal tool, behind sign-in):

| Severity | Meaning |
|---|---|
| High | Exposes data or lets someone act as another user |
| Medium | Makes an attack easier, or blocks some users from a task |
| Low | Defence in depth, or a task is harder than it should be |

## 1. Security

### Method

- Read every endpoint, its authorisation attribute and the query it runs.
- `dotnet list package --vulnerable --include-transitive` and `npm audit --omit=dev`.
- Searched the repository for secrets, raw SQL (`FromSql`, `ExecuteSql`) and `innerHTML`.
- Inspected real responses (headers, status codes, bodies) from the running API.

### Findings and fixes

| # | Finding | Severity | Fix | Verified by |
|---|---|---|---|---|
| S1 | No defensive response headers: browsers could sniff a JSON response as HTML/script, frame the API (clickjacking) or send ticket URLs in the `Referer` header | Medium | `Security/SecurityHeaders.cs`: `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `X-Frame-Options: DENY`, `Permissions-Policy` (no camera, microphone, location, payment), and `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'; base-uri 'none'` (the API returns JSON, so nothing may load). The Development-only `/scalar` docs page is exempt from the CSP | `SecurityHeadersTests`: success, 401 and unknown-route responses all carry the headers |
| S2 | No HSTS, so a first visit over plain HTTP could be intercepted | Medium | `UseHsts()` outside Development, 180 days. Not in Development, where it would stick to `localhost` for every local project | Integration test asserts `Strict-Transport-Security: max-age=15552000` |
| S3 | `Server: Kestrel` header advertised the server software | Low | `AddServerHeader = false` | Checked on the running API |
| S4 | Request bodies up to Kestrel's default of 30 MB were accepted, though the largest real request is a 4,000-character description | Low | `MaxRequestBodySize = 1_000_000` (1 MB) | A 2 MB request is refused |
| S5 | That refusal came back as **500** "server error", because the exception handler turned the `BadHttpRequestException` into a 500 | Low | `ExceptionHandlerOptions.StatusCodeSelector` keeps the exception's own status code | The running API returns **413** with a ProblemDetails body |
| S6 | Staff status-change dialogs ("Ask requester for info", "Resolve", …) didn't say that the note is shown to the requester, so an agent could write something meant to stay internal | Medium | Every staff status-note dialog now says "Visible to the requester." Internal notes remain a separate, explicit choice in the reply box | `ticket-actions.spec.ts`; checked in the browser |

### Checked with no finding

- **Dependencies:** 0 vulnerable NuGet packages (including transitive), 0 vulnerabilities in npm
  production dependencies.
- **Secrets:** none in the repository or its history; demo passwords live in user secrets.
- **SQL injection:** all data access goes through EF Core LINQ; no raw SQL.
- **XSS:** Angular escapes all bound text. The only `innerHTML` binding renders the app's own
  hard-coded icon SVGs, never user input.
- **Anonymous endpoints:** each `[AllowAnonymous]` is intentional (`/health`, sign-in, the
  antiforgery token, the Development-only API docs). Everything else falls back to "signed in".
- **Authorisation:** requester visibility, internal notes, ownership and role checks were
  already covered by integration tests and hand mutation testing.

## 2. Accessibility

Target: **WCAG 2.2 level AA**.

### Method

- **axe-core 4.14.0** run in Chromium on every screen, as each role, including open dialogs:
  sign-in, dashboard, ticket list (each view), ticket detail, new and edit ticket, account,
  user administration, the Add-user dialog and the status dialogs.
- Keyboard-only walkthroughs: tab order, visible focus, Esc to close, where focus lands after
  each action.
- Contrast checked by calculation where axe couldn't decide.

### Findings and fixes

| # | Finding | WCAG | Severity | Fix |
|---|---|---|---|---|
| A1 | The account link in the sidebar had an `aria-label` ("Your account: Lerato Dlamini, Administrator") that replaced its visible text and didn't start with it, so voice-control users saying what they see may not match it | 2.5.3 Label in Name | Low | Removed the `aria-label`; the visible name and role are the link text, with a screen-reader-only "(your account)" suffix |
| A2 | Form inputs pointed `aria-describedby` at error elements that only existed while an error was shown | 4.1.2 Name, Role, Value | Low | `FieldError` always renders its container with the id (empty while valid); same for the "passwords don't match" message on the account page |
| A3 | Opening the mobile menu left keyboard focus behind it, and the page underneath could still be tabbed into | 2.4.3 Focus Order | Medium | Opening the menu focuses its first visible control, and the page behind is `inert`. Closing it (button, Esc or the dimmed background) returns focus to the menu button; choosing a link moves focus to the new page's main content |
| A4 | On phones the ticket table's header row is visually hidden (rows become stacked cards), but its four sort buttons were still in the tab order: keyboard focus disappeared onto invisible buttons, and touch users had no way to sort at all | 2.4.7 Focus Visible | Medium | Below 640 px the header row is removed (`display: none`) and a "Sort" select offers every order the column headers can produce |

**Result after the fixes: 0 axe violations on every screen and dialog, for every role.**

### Items axe marks "needs review", and why they pass

| Item | Why axe can't decide | Manual result |
|---|---|---|
| Text in `<select>` filters | The chevron is a background image, so axe can't compute the background | Dark text on white: 17:1 |
| Dialog text | The dialog sits over the dimmed backdrop | Muted dialog text on the white panel: 5.4:1 (AA needs 4.5:1) |
| `aria-hidden-focus` on page content behind a dialog | Angular CDK hides the page from screen readers while a dialog is open; its focus trap keeps focus inside the dialog | Expected behaviour of a modal dialog |
| A view tab partly scrolled out of the tab strip on phones | "Partially obscured" | Same colours as the other tabs, which pass |

## 3. Responsive design

### Method

A script in the browser checked, on every screen, whether the page scrolls sideways and listed
any element wider than the viewport. It ran at **375, 768, 1024 and 1440 px**, as a requester
and as staff, followed by screenshots and manual use at each width. Dialogs were also checked
at 375 × 812 (portrait phone) and 667 × 375 (landscape phone).

### Findings and fixes

| # | Finding | Severity | Fix |
|---|---|---|---|
| R1 | No sorting on phones (A4 above) | Medium | Sort select, phones only |
| R2 | On phones the view tabs scroll sideways. Arriving on "All tickets" (last tab) left the active tab out of sight | Low | When the view changes, the active tab is scrolled into view, after the web font has loaded, since the font changes the tabs' widths |

No screen scrolls sideways at any of the four widths. Dialogs stay inside the screen with a
16 px margin, and scroll inside themselves when the screen is shorter than they are (landscape
phone), so their buttons can always be reached.

> Note for anyone repeating the test: after the browser's viewport *emulation* changes size,
> Chromium can keep reporting the previous height for `100dvh` until the page reloads, which
> makes dialogs look too tall. Reload after resizing. A real phone rotating doesn't do this.

## 4. Limits of this review

Honest gaps. None of these were tested, so no claim is made about them:

- **No screen-reader testing** (NVDA, JAWS, VoiceOver). axe and keyboard checks catch many but
  not all problems a screen-reader user would meet.
- **Chromium only.** Not tested in Firefox or Safari, or on real phones.
- **No automated accessibility checks in CI yet.** axe-core is a dev dependency, but the
  component tests don't run it, so a regression would only be caught by repeating this review.
- **No penetration test.** The security review is a code and configuration review by the
  developer, not an independent test.

## 5. Follow-up during deployment preparation

| Item | Outcome |
|---|---|
| Login rate limiting behind a reverse proxy | **Done.** Forwarded client addresses are trusted only from configured proxies (`ReverseProxyTests`) |
| A Content Security Policy for the Angular app | **Done.** `script-src 'self'` with no inline scripts; checked in the browser with no violations ([DEPLOYMENT.md § 11](DEPLOYMENT.md#11-how-this-was-verified)) |
| HSTS `includeSubDomains` / preload | **Open.** Only once a real domain is chosen |

Deployment preparation also found and fixed a bug: behind a proxy that wasn't configured, the
first sign-in step failed with a 500. The API now refuses plain HTTP with a clear 400 and a
log message (`HttpsOnlyApiTests`).
