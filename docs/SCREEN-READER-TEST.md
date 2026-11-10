# Screen-reader test (NVDA)

A manual test of OpsHub with the NVDA screen reader on Windows. It covers what the automated
axe check can't judge: whether each screen *makes sense when listened to*, in a sensible
order, and whether changes on the page are announced.

It takes about an hour. You don't need to know screen readers well. Each check says what to
press and roughly what you should hear. Write down what actually happens.

**Status: not run yet.** When it has been run, record the date, versions and results in
[REVIEW.md § 4](REVIEW.md#4-limits-of-this-review).

## 1. Set up

1. **Install NVDA** (free) from nvaccess.org. Use the version it offers you, and note it down.
2. **Run OpsHub locally** with fresh demo data, as in the README's
   [Run locally](../README.md#run-locally) section. Delete `src/OpsHub.Api/opshub.db*` before
   you start the API, so the tickets are the seeded ones.
3. **Use Chrome** at its normal size. If you have time afterwards, repeat a few checks in
   Firefox. That also starts to cover the "other browsers" gap.
4. **Start NVDA**, then open its menu (NVDA key + N) → *Tools* → *Speech viewer*. It shows
   everything NVDA says as text, so you can copy the exact words into your notes.

**The NVDA key** is Insert, or Caps Lock if you choose the laptop layout.

| Press | What it does |
|---|---|
| Ctrl | Stop speaking |
| NVDA + T | Read the page title |
| NVDA + Tab | Read the item that has focus |
| NVDA + ↓ | Read everything from here on |
| ↑ / ↓ | Read the previous / next line (browse mode) |
| Tab / Shift + Tab | Move to the next / previous control |
| H, 1, 2 | Next heading, next level-1 heading, next level-2 heading |
| D | Next landmark (navigation, main content, …) |
| K, B, F, T, L | Next link, button, form field, table, list |
| Ctrl + Alt + arrows | Move between cells in a table |
| NVDA + F7 | A list of all headings, links or landmarks on the page |
| NVDA + Space | Switch between browse mode and focus mode (typing into fields) |
| Esc | Close a dialog |

NVDA switches to focus mode by itself when you Tab into a field, and beeps when it does.

## 2. How to record results

For each check write **Pass**, **Fail** or **Unsure**, plus what NVDA said when it isn't what
the check expects. A check fails if you have to guess, if something important isn't read, or
if something is read twice or in a confusing order. "Unsure" is a useful answer: note what
happened and move on.

Copy the template at the end, fill it in, and send it back. Each failure becomes a fix with a
test, like the findings in [REVIEW.md](REVIEW.md).

## 3. Checks

### Signed out (no account needed)

| # | Do this | You should hear |
|---|---|---|
| S1 | Open http://localhost:4200. Press NVDA + T | "Sign in · OpsHub" |
| S2 | NVDA + F7, choose *Headings* | One level-1 heading: "Sign in" |
| S3 | Tab into the email field | "Email, edit", and the same for "Password" |
| S4 | Leave both empty and press Enter | Focus moves to the email field, which is read as invalid, with its error message |
| S5 | Enter `thandi@marula.example` and a wrong password, press Enter | The error box is read out by itself, without you moving to it |
| S6 | Tab past the sign-in button | A link: "Forgot your password?" |
| S7 | Follow it, enter `pieter@marula.example`, send | The confirmation ("If an account exists for …") is read by itself |
| S8 | Open the newest `.eml` file in `src/OpsHub.Api/mail-outbox/` and open its link in Chrome. Type a new password, a *different* one in the second field, press Enter | On the second field: invalid, and "The passwords don't match" |
| S9 | Type the demo password in both fields and save. (Reusing it keeps the demo accounts on one password.) | "Your password has been changed" is read by itself, followed by a *Sign in* button |
| S10 | Open the same email link again and save a password | "This link can't be used", the reason, and a link to ask for a new one |

### Layout (sign in as `thandi@marula.example`)

| # | Do this | You should hear |
|---|---|---|
| L1 | Once the page has loaded, press Tab | "Skip to content, link". Enter puts you at the start of the page's content |
| L2 | Press D repeatedly | Landmarks including "Main navigation" and "main" |
| L3 | Find the account link in the sidebar | Her name, her role and "(your account)" |
| L4 | Choose *New ticket* in the sidebar | **Write down what NVDA says by itself.** Can you tell a new page has loaded? NVDA + T should read the new title |
| L5 | Press K through the sidebar links | The link for the page you are on is read as "current page" |

### Requester: my tickets

| # | Do this | You should hear |
|---|---|---|
| R1 | On *My tickets*, press 1 | "My tickets", then the number of tickets |
| R2 | Press D or K to reach the view tabs | The tab you're on is read as "current page" |
| R3 | Choose the other view (*All*) | The new ticket count is read by itself |
| R4 | Press T, then Ctrl + Alt + → and ↓ through a few cells | Each cell is read with its column header. The status is read as a word ("Open", "Resolved") |
| R5 | Press K on a row | The ticket title is read as a link |
| R6 | If there are page buttons at the bottom, go to the next page | The change of page is announced |

### Requester: report a problem

| # | Do this | You should hear |
|---|---|---|
| N1 | Tab through the form | Title, Category, Priority and Description, each read with its label |
| N2 | Submit it empty | Focus moves to the first field with a problem, and its error is read |
| N3 | Type a title, then choose *My tickets* in the sidebar to leave | The browser asks whether to discard your changes, and NVDA reads the question |
| N4 | Fill the form in and submit it | A confirmation is read, and you land on the new ticket |

### Requester: a ticket

| # | Do this | You should hear |
|---|---|---|
| T1 | Open a ticket. Use H to move through the headings | The title (level 1), then "Description", "Activity" and the reply box |
| T2 | Press L to reach the activity list, then ↓ through it | "List with N items". Each comment: who wrote it, "Support" for staff, how long ago, then the text |
| T3 | Write a comment and send it | A confirmation is read, and the new comment is in the list |
| T4 | If the ticket offers *Your next step* buttons, Tab to them | The group name ("Your next step") is read with the first button |

### Support agent (sign in as `sipho@marula.example`)

| # | Do this | You should hear |
|---|---|---|
| A1 | On the *Queue*, Tab to the column headers | Each sortable one is read as a button, with its sort order where it has one |
| A2 | Press one to sort | The new sort order is read |
| A3 | Tab through the filters and change one | Each filter has a name, and the new ticket count is read |
| A4 | Open a ticket and find *Assign to* | "Assign to, combo box". After you choose an agent and assign, a confirmation is read |
| A5 | Find the comment box | "Who can see this comment", then "Reply to requester, radio button, checked" and "Internal note, radio button" |
| A6 | Choose *Internal note*, send one, then find it in *Activity* | "Internal note: not visible to the requester" is read with it |
| A7 | Choose a *Next step* button that opens a dialog (e.g. *Resolve*) | "Dialog" and its title. Focus is inside it, and Tab stays inside it |
| A8 | In the dialog, try ↑ / ↓ past its edges | You can't read the page behind it |
| A9 | Press Esc | The dialog closes, and focus is back on the button that opened it |
| A10 | Open it again and confirm | The change is announced, and focus lands on the next-step buttons (or the title, if none are left) |

### Dashboard (still as Sipho)

| # | Do this | You should hear |
|---|---|---|
| D1 | Open the *Dashboard*. Press H through the page | "Dashboard", "All tickets by status", "Open tickets by priority", "Agent workload", "Recently updated" |
| D2 | Tab through the key figures | Each one reads its name, its number and the line under it, e.g. "Open tickets 12 of 40 logged" |
| D3 | Press L on *All tickets by status* and ↓ through it | Each status with its count. The bars themselves aren't read |
| D4 | Press T for the workload table | Its caption ("Active tickets per support agent…"), then the cells with their headers |

### Administrator (sign in as `lerato@marula.example`)

| # | Do this | You should hear |
|---|---|---|
| U1 | On *Users*, Tab through the search and the filters | "Search users", "Role", "Status" |
| U2 | Choose *Add user* | "Dialog" and its title, with focus on the first field |
| U3 | Save it empty | Focus moves to the first problem, and its error is read |
| U4 | Fill it in and save | The dialog closes, a confirmation is read, and focus is back on the page |
| U5 | On *Categories*, add a category the same way | As U2 to U4 |
| U6 | On *Your account*, change the password with two different new passwords | The mismatch is read on the second field |

### Narrow window (optional)

| # | Do this | You should hear |
|---|---|---|
| P1 | Make the Chrome window narrower than about 640 px, so the menu button appears. Tab to it | "Menu, button, collapsed" |
| P2 | Press Enter | "Expanded", and focus moves into the menu. Esc closes it and returns you to the button |
| P3 | On the queue, find the sort list | "Sort by, combo box", with every sort order |

## 4. Results template

```text
Date:
NVDA version:
Browser and version:
Windows version:

S1  Pass
S2  Pass
S3  Fail - NVDA said "edit" without the label
...

Anything else you noticed:
```

## 5. Things to watch for anywhere

- Something you can see that NVDA never reads, or reads without its label.
- Icons or decorations read out ("graphic", odd symbols).
- Changes on the page that happen in silence (a save, an error, a count).
- Focus jumping somewhere unexpected, or getting lost after a dialog closes.
- The same thing read twice.
