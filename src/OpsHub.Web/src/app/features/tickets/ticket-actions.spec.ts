import { statusActions } from './ticket-actions';

/** The status buttons the ticket page shows, derived from the API's allowedStatuses. */
describe('statusActions', () => {
  it('labels transitions as verbs and promotes the natural next step', () => {
    const actions = statusActions('InProgress', ['WaitingForInformation', 'Resolved'], true);

    expect(actions.map((a) => a.label)).toEqual(['Ask requester for info', 'Resolve']);
    expect(actions.find((a) => a.primary)?.to).toBe('Resolved');
  });

  it('has no primary button when the natural next step is unavailable (unassigned Open ticket)', () => {
    const actions = statusActions('Open', ['Resolved', 'Closed'], true);

    expect(actions.some((a) => a.primary)).toBe(false);
  });

  it('requires a note to close an Open ticket without resolving it', () => {
    const close = statusActions('Open', ['Closed'], true)[0];

    expect(close.label).toBe('Close without resolving');
    expect(close.dialog?.noteRequired).toBe(true);
  });

  it('offers the requester "Confirm fixed and close" without a dialog', () => {
    const actions = statusActions('Resolved', ['Open', 'Closed'], false);
    const confirm = actions.find((a) => a.to === 'Closed');

    expect(confirm?.label).toBe('Confirm fixed and close');
    expect(confirm?.dialog).toBeNull();
    expect(confirm?.primary).toBe(true);
  });

  it('uses different reopen wording for staff and requesters', () => {
    expect(statusActions('Resolved', ['Open'], true)[0].label).toBe('Reopen to queue');
    expect(statusActions('Resolved', ['Open'], false)[0].label).toBe('Reopen');
  });

  it('starts work immediately, without a dialog', () => {
    const start = statusActions('Open', ['InProgress'], true)[0];

    expect(start.label).toBe('Start work');
    expect(start.dialog).toBeNull();
  });

  it('returns nothing when no transition is allowed', () => {
    expect(statusActions('Closed', [], true)).toEqual([]);
  });
});
