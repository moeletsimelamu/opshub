# OpsHub Design System

This document is written **before** the interface is built. Every visual and interaction decision
in the Angular app should trace back to something here. If the app and this document disagree,
one of them is wrong and must be fixed.

## 1. Product and audience

OpsHub is the internal service desk of Marula Logistics, a fictional freight and warehousing
company. It is a work tool used all day by some people and occasionally by everyone else.

| User | How often | What they need from the interface |
|---|---|---|
| **Requester** (warehouse, finance, dispatch staff) | A few times a month | Report a problem in under a minute; see at a glance what is happening; know when they must reply. |
| **Support Agent** | All day | Scan a queue quickly; triage (assign, set priority); move tickets through the workflow; talk to the requester or leave internal notes; never lose track of what is theirs. |
| **Administrator** | Weekly | Everything an agent does, plus add, deactivate and re-role users. |

### Primary tasks (in order of frequency)

1. **Agent:** find what needs attention now (unassigned tickets, my active tickets, critical ones).
2. **Agent:** open a ticket, understand it, act (assign, reply, change status) without leaving the page.
3. **Requester:** see my tickets and whether anything is waiting for me.
4. **Requester:** report a new problem.
5. **Admin:** manage user accounts.

## 2. Design principles

1. **Queue first.** The most important screen is a list people scan dozens of times a day. Tables, not
   cards; dense rows; the ticket reference, title, status and priority readable without hovering.
2. **Status is information, not decoration.** Status and priority always appear as **text plus a shape**.
   Colour reinforces meaning but is never the only signal.
3. **One primary action per section.** Each section of a screen has at most one filled (primary) button:
   the most likely next step. Everything else is secondary or a quiet text action. (Revised after user testing:
   the ticket page has two sections, the workflow bar and the reply box, each with its own primary.)
4. **Show the rules.** Controls appear only when the API says they will work (`permissions` on each ticket).
   When something can't be done, the interface says why instead of hiding the reason.
5. **Honest states.** Loading placeholders match the shape of the content; empty states explain what
   would appear and what to do; errors say what happened and offer a way forward. No fake numbers.
6. **Calm by default, loud only for exceptions.** Neutral surfaces; strong colour is reserved for
   Critical priority, things waiting on the user, and errors.

## 3. Visual identity

The reference is a **logistics operations board**: dispatch sheets, waybills and depot whiteboards.
That suggests:

- warm off-white "paper" surfaces with thin rules instead of floating cards and shadows;
- a monospaced face for codes (`OPS-000042`), like reference numbers printed on a waybill;
- a single deep **petrol** accent (dark blue-green), sober and operational;
- small rectangular status tags (like labels on a parcel), not pill-shaped "badges".

What we deliberately avoid: gradient headers, glassmorphism, big rounded metric cards, decorative
illustrations, icons without a job, and a generic blue "admin template" look.

## 4. Tokens

All values live as CSS custom properties in `src/OpsHub.Web/src/styles/tokens.css`. Components use
tokens only, never raw values.

### Colour

| Token | Value | Use |
|---|---|---|
| `--color-bg` | `#F6F5F1` | App background ("paper") |
| `--color-surface` | `#FFFFFF` | Tables, panels, inputs |
| `--color-surface-sunken` | `#EFEDE7` | Table header, sidebar, code blocks |
| `--color-border` | `#DEDBD2` | Rules and dividers |
| `--color-border-strong` | `#8A857A` | Input borders (3.7:1, meets the 3:1 rule for control boundaries) |
| `--color-text` | `#1D1C1A` | Primary text (17.0:1 on surface) |
| `--color-text-muted` | `#5D5A53` | Secondary text (6.9:1) |
| `--color-text-subtle` | `#6E6A61` | Timestamps, hints (5.4:1 on surface, 4.9:1 on paper) |
| `--color-primary` | `#0B5563` | Primary buttons, links, focus (8.4:1) |
| `--color-primary-hover` | `#08434E` | Hover/pressed |
| `--color-primary-soft` | `#E3EFF1` | Selected row, active nav |
| `--color-danger` | `#B42318` | Errors, destructive actions, Critical |
| `--color-danger-soft` | `#FDECEA` | Error banners |
| `--color-warning` | `#8A5300` | "Needs your reply", internal notes label |
| `--color-warning-soft` | `#FDF3E1` | Internal note background, waiting banners |
| `--color-success` | `#2E6B30` | Success toasts, Resolved |
| `--color-success-soft` | `#E7F2E4` | |

All ratios above were measured with the WCAG relative-luminance formula, not estimated. The first
draft used `#77736A` for hints (4.3:1 on paper, failing) and `#B9B5AA` for input borders (2.1:1,
failing); both were darkened.

**Status tags** (text colour on tinted background; measured 5.6:1 to 7.7:1):

| Status | Text / background | Shape cue |
|---|---|---|
| Open | `#3D4A5C` / `#EBEEF3` | hollow circle ○ |
| In Progress | `#0B5563` / `#E3EFF1` | half circle ◐ |
| Waiting for Information | `#8A5300` / `#FDF3E1` | pause ‖ |
| Resolved | `#2E6B30` / `#E7F2E4` | check ✓ |
| Closed | `#5D5A53` / `#EFEDE7` | filled circle ● |

**Priority** is shown as a four-step **signal meter** (1–4 filled bars) plus the word. Only High
(`#9A4A00`) and Critical (`#B42318`) are coloured; Low and Medium use muted text. The number of bars
carries the meaning for colour-blind users.

### Typography

- **IBM Plex Sans** for the interface; **IBM Plex Mono** for ticket references and other codes.
  Self-hosted through `@fontsource` packages: no third-party font requests.
- Tabular numerals (`font-variant-numeric: tabular-nums`) in tables and counts.

| Token | Size / line height | Use |
|---|---|---|
| `--text-xs` | 12px / 16px | Tags, table meta |
| `--text-sm` | 13px / 18px | Table cells, labels, buttons |
| `--text-md` | 15px / 24px | Body text: descriptions, comments, form inputs |
| `--text-lg` | 18px / 26px | Section headings |
| `--text-xl` | 22px / 30px | Page titles |

Weights: 400 regular, 500 medium (labels, table titles), 600 semibold (headings). Long-form text
(descriptions, comments) is capped at **72 characters** per line.

### Spacing, shape, elevation

- Spacing scale (4px base): `--space-1` 4, `--space-2` 8, `--space-3` 12, `--space-4` 16, `--space-5` 24,
  `--space-6` 32, `--space-7` 48.
- Radius: `--radius-sm` 4px (tags, inputs), `--radius-md` 6px (buttons, panels), `--radius-lg` 10px (dialogs).
- Borders do the separating; **shadows only for things that float** (dialogs, menus, toasts):
  `--shadow-overlay`.
- Motion: 120–180 ms ease-out for hover and overlays; disabled under `prefers-reduced-motion`.

## 5. Components

| Component | Purpose and rules |
|---|---|
| **Button** | `primary` (one per view), `secondary` (bordered), `ghost` (text), `danger`. Height 36px (40px on touch). Shows a spinner and stays the same width while busy. |
| **Status tag** | Text + shape glyph; tinted background; never clickable. |
| **Priority meter** | 4 bars + label; compact variant (bars only, with `aria-label`) in dense tables on small screens. |
| **Field** | Label above input, optional hint, error below with icon; wires `aria-describedby` and `aria-invalid`. Errors appear after the first submit or when a field is left. |
| **Data table** | Sticky header; whole row is a link (keyboard focusable); sortable headers are buttons with `aria-sort`. On narrow screens rows become stacked list items. No horizontal scrolling. |
| **Pagination** | "Showing 21–40 of 87", previous/next, page size. |
| **Empty state** | One sentence of what would be here, one of what to do, optional action. No illustrations. |
| **Banner** | Inline messages inside a page: error, warning (conflict), info. |
| **Toast** | Brief confirmation after an action ("Ticket assigned to Sipho"). Polite `aria-live`; auto-dismiss 4s; not used for errors that need action. |
| **Dialog** | Angular CDK dialog: focus trapped, Esc closes, returns focus to the trigger. Used for status notes and user forms. |
| **Skeleton** | Grey blocks in the shape of the content while loading; announced as "Loading…" to screen readers. |
| **Icon** | Only where it speeds recognition: search, add, close, chevrons, lock (internal note), alert. 16px, `currentColor`, `aria-hidden` unless standalone. |

## 6. Screens and information hierarchy

### Navigation model

| Role | Sidebar items | Landing page |
|---|---|---|
| Requester | My tickets · **New ticket** | My tickets |
| Support Agent | Dashboard · Queue · **New ticket** | Dashboard |
| Administrator | Dashboard · Queue · Users · **New ticket** | Dashboard |

Staff land on the **Dashboard**, which summarises the desk and links into the Queue. Filters, search, sort and page live in the **URL**, so views can be
bookmarked and the back button works.

### Dashboard (staff)
Answers "what needs attention, and is the desk keeping up?" from live data only.

1. Header: title, "Figures as of 14:05", *Refresh* (secondary).
2. **Stat strip**: one panel split by thin rules, *not* separate big cards. Open tickets · Needs triage ·
   Waiting for requester · Critical open · Average resolution (30 days). A tile links to the queue only
   where the queue can show exactly that set (Open → All active, Needs triage → Needs triage, Critical
   → All active + Critical).
3. **Tickets by status** and **Open tickets by priority**, side by side: horizontal *bar lists*. A label
   (status tag / priority meter), a thin bar, and the count; one series each, so one hue and no legend.
   Bars are scaled to the largest value in that list; zero rows stay visible.
4. **Agent workload**: a table (agent, active tickets with an inline bar, of which High/Critical,
   resolved in 30 days), busiest first.
5. **Recently updated**: the six latest tickets, linking to them.

**Not enough data:** the average resolution time is withheld until at least 3 tickets were resolved in
30 days. The tile says so ("Needs 3 resolved tickets; 1 so far") instead of showing a number that
looks like a trend. The median is shown beside the average because a single slow ticket skews a mean.

**Chart colour:** `--chart-bar` `#00879f`. Checked with the data-viz palette validator: inside the
lightness band, above the chroma floor, ≥ 3:1 on both surfaces. Every bar has its value printed beside
it and a text label, so colour never carries meaning alone. Hover shows the share of the total.

### Sign in
Product name, one-line purpose, email + password, a single primary button. Errors appear as one banner
above the form (the API deliberately doesn't say which field was wrong).

### Ticket list (Queue / My tickets)
1. Page title + count ("Queue · 24 tickets").
2. **Views** as tabs (agents): *Needs triage* (Open, unassigned) · *Assigned to me* (active) · *All active* · *All*.
   Requesters: *Active* · *All*.
3. Toolbar: search (title, description or `OPS-…`), status, priority and category filters, "Clear filters".
4. Table: Reference (mono) · Title (+ category under it) · Status · Priority · Assignee (agents) / Updated
   (requesters) · Updated. Default sort: recently updated.
5. Pagination.

Requesters see a **"Needs your reply"** marker on tickets that are *Waiting for Information*: the one
thing they must act on.

### Ticket detail
Two columns on wide screens:
- **Main (left):** reference + title, then the **"Next step" bar**: a labelled, tinted section holding the
  allowed status changes as verbs (*Start work*, *Ask requester*, *Resolve*, *Close*, *Reopen*). Then the
  description, a **timeline** that merges comments and status changes in time order, starting with the ticket
  being reported, and the reply box.
  Staff also get **Send and resolve** in the reply box.

  *Change from user testing:* the first version was an unlabelled row of buttons. A tester scrolled
  to the reply box and couldn't find *Resolve*, so the bar was labelled and given its own background, and
  *Send and resolve* was added where agents finish their work.
- **Side panel (right, 300px):** properties as a definition list: status, priority, category, requester,
  assignee (with *Assign to me* and an agent picker for staff), created, updated, resolved.

Internal notes have a warm tinted background and a lock label "Internal note: not visible to the
requester". The composer for staff has a two-option switch: *Reply to requester* / *Internal note*.

On a 409 conflict the page shows a warning banner: "This ticket was changed by someone else", with a
*Reload* button. Nothing is silently overwritten.

### New / edit ticket
Single column, max 640px: title, category, priority (requester: Low–Critical with a hint; agents edit later),
description (textarea, with a character count near the limit). Primary: *Submit ticket* / *Save changes*.
Cancel returns to where the user came from.

### Your account
Profile (name, email, role; read-only, changed by an administrator) and *Change password*: current
password, new password, repeat. A mismatch is reported on the repeat field. The page explains that
other devices are signed out.

### Users (admin)
Search + role + status filters; table: name/email · role · status · created. *Add user* opens a dialog.
Row action *Edit* opens the same dialog (display name, role, active). The dialog explains the effects of
deactivation ("signs them out and returns their active tickets to the queue").

## 7. Responsive behaviour

| Width | Layout |
|---|---|
| ≥ 1280px | Sidebar 232px; content up to 1200px; detail = main + 300px side panel. |
| 960–1279px | Sidebar 232px; detail side panel 280px. |
| 640–959px | Sidebar collapses to a top bar with a menu button (drawer); detail becomes one column with properties above the timeline. |
| < 640px | Table rows become stacked list items (reference + status on line 1, title on line 2, priority + updated on line 3); filters collapse behind a "Filters" button; touch targets ≥ 44px; 16px side gutters. |

## 8. Accessibility checklist

- WCAG 2.2 AA contrast for text and essential UI (values above are checked).
- Visible focus ring on every interactive element: 2px `--color-primary` outline, 2px offset.
- "Skip to content" link; landmarks (`header`, `nav`, `main`); one `h1` per page.
- Every form control has a label; errors are linked with `aria-describedby`.
- Status changes and saves are announced through a polite live region (toasts).
- Tables use real `<table>` markup with `scope` on headers; sortable headers expose `aria-sort`.
- Full keyboard use: rows are links, dialogs trap focus and close with Esc.
- `prefers-reduced-motion` disables transitions.
