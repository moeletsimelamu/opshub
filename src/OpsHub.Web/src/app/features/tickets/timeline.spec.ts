import { StatusHistoryEntry, TicketComment } from '../../core/api/models';
import { buildTimeline } from './timeline';

const thandi = { id: 'u-thandi', displayName: 'Thandi Mokoena' };
const sipho = { id: 'u-sipho', displayName: 'Sipho Ndlovu' };

const history = (id: number, from: StatusHistoryEntry['fromStatus'], to: StatusHistoryEntry['toStatus'], at: string): StatusHistoryEntry =>
  ({ id, fromStatus: from, toStatus: to, changedBy: from === null ? thandi : sipho, note: null, changedAt: at });

const comment = (id: number, at: string): TicketComment =>
  ({ id, body: 'Reply', isInternal: false, author: sipho, authorIsStaff: true, createdAt: at });

/** The ticket page's activity list: status changes and comments, oldest first. */
describe('buildTimeline', () => {
  it('starts with the ticket being reported, so the whole story is shown', () => {
    const items = buildTimeline([history(1, null, 'Open', '2026-10-24T08:00:00Z')], []);

    expect(items).toHaveLength(1);
    expect(items[0]).toMatchObject({ kind: 'status', entry: { fromStatus: null, toStatus: 'Open' } });
  });

  it('merges status changes and comments in time order', () => {
    const items = buildTimeline(
      [history(1, null, 'Open', '2026-10-24T08:00:00Z'), history(2, 'Open', 'InProgress', '2026-10-24T09:00:00Z')],
      [comment(10, '2026-10-24T08:30:00Z'), comment(11, '2026-10-24T09:30:00Z')],
    );

    expect(items.map((i) => i.key)).toEqual(['s1', 'c10', 's2', 'c11']);
  });

  it('puts a status change before a comment made at the same moment', () => {
    const items = buildTimeline([history(2, 'Open', 'Resolved', '2026-10-24T09:00:00Z')], [comment(10, '2026-10-24T09:00:00Z')]);

    expect(items.map((i) => i.kind)).toEqual(['status', 'comment']);
  });
});
