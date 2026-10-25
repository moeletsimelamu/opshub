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
3. **One primary action per view.** Each screen has a single filled (primary) button. Everything else is
   secondary or a quiet text action.
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
| Support Agent | Queue · **New ticket** | Queue → "Needs triage" view |
| Administrator | Queue · Users · **New ticket** | Queue |

The dashboard is added in Phase 7 with real metrics. Until then it isn't in the navigation, so no
link leads to an empty page. Filters, search, sort and page live in the **URL**, so views can be
bookmarked and the back button works.

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
- **Main (left):** reference + title, then the action bar (allowed status changes as verbs: *Start work*,
  *Ask requester*, *Resolve*, *Close*, *Reopen*), then the description, then a **timeline** that merges
  comments and status changes in time order, then the comment composer.
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
