import { TicketStatus } from '../../core/api/models';

/**
 * Turns "allowed target statuses" (from the API's permissions) into buttons with verbs
 * people understand ("Start work", not "Set status to InProgress").
 */
export interface StatusAction {
  to: TicketStatus;
  label: string;
  primary: boolean;
  /** null = apply immediately; otherwise ask for a note first. */
  dialog: {
    title: string;
    description: string;
    noteLabel: string;
    noteHint?: string;
    noteRequired: boolean;
    confirmLabel: string;
  } | null;
}

/** The most likely next step from each status gets the single primary button (DESIGN.md principle 3). */
const PRIMARY_NEXT: Partial<Record<TicketStatus, TicketStatus>> = {
  Open: 'InProgress',
  InProgress: 'Resolved',
  WaitingForInformation: 'InProgress',
  Resolved: 'Closed',
};

export function statusActions(from: TicketStatus, allowed: readonly TicketStatus[], isStaff: boolean): StatusAction[] {
  const actions = allowed.map((to) => describe(from, to, isStaff));
  // If the natural next step isn't available (e.g. Start work needs an assignee), no button
  // is promoted: the right next action is elsewhere on the page.
  const primaryTarget = PRIMARY_NEXT[from];
  return actions.map((a) => ({ ...a, primary: a.to === primaryTarget }));
}

function describe(from: TicketStatus, to: TicketStatus, isStaff: boolean): StatusAction {
  switch (to) {
    case 'InProgress':
      return {
        to,
        primary: false,
        label: from === 'WaitingForInformation' ? 'Resume work' : from === 'Resolved' ? 'Reopen and work on it' : 'Start work',
        dialog: null,
      };
    case 'WaitingForInformation':
      return {
        to,
        primary: false,
        label: 'Ask requester for info',
        dialog: {
          title: 'Ask the requester for information',
          description: 'The ticket waits until the requester replies; their reply moves it back to In progress automatically.',
          noteLabel: 'What do you need to know?',
          noteHint: 'Shown to the requester in the ticket timeline.',
          noteRequired: true,
          confirmLabel: 'Send and wait',
        },
      };
    case 'Resolved':
      return {
        to,
        primary: false,
        label: 'Resolve',
        dialog: {
          title: 'Resolve this ticket',
          description: 'The requester can confirm the fix and close the ticket, or reopen it if the problem continues.',
          noteLabel: 'What fixed it?',
          noteHint: 'A short summary helps the requester and anyone who sees a similar problem later.',
          noteRequired: false,
          confirmLabel: 'Resolve ticket',
        },
      };
    case 'Closed':
      if (from === 'Open') {
        return {
          to,
          primary: false,
          label: 'Close without resolving',
          dialog: {
            title: 'Close without resolving',
            description: 'Use this for duplicates, requests raised by mistake, or anything outside the service desk. Closed tickets can’t be reopened.',
            noteLabel: 'Reason for closing',
            noteRequired: true,
            confirmLabel: 'Close ticket',
          },
        };
      }
      return {
        to,
        primary: false,
        label: isStaff ? 'Close' : 'Confirm fixed and close',
        dialog: isStaff
          ? {
              title: 'Close this ticket',
              description: 'Closed tickets are final and can’t be reopened or commented on.',
              noteLabel: 'Note',
              noteRequired: false,
              confirmLabel: 'Close ticket',
            }
          : null,
      };
    case 'Open':
      return {
        to,
        primary: false,
        label: isStaff ? 'Reopen to queue' : 'Reopen',
        dialog: {
          title: 'Reopen this ticket',
          description: 'The ticket goes back to the support queue.',
          noteLabel: isStaff ? 'Reason for reopening' : 'What’s still not working?',
          noteRequired: false,
          confirmLabel: 'Reopen ticket',
        },
      };
  }
}
