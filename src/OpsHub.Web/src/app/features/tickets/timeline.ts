import { StatusHistoryEntry, TicketComment } from '../../core/api/models';

/** One entry in the merged timeline of comments and status changes. */
export type TimelineItem =
  | { kind: 'comment'; key: string; at: string; comment: TicketComment }
  | { kind: 'status'; key: string; at: string; entry: StatusHistoryEntry };

/**
 * Merges a ticket's status history and comments into one list, oldest first. The first
 * history row (from no status to Open) is the ticket being reported, so the timeline tells
 * the whole story from submission onwards.
 */
export function buildTimeline(history: StatusHistoryEntry[], comments: TicketComment[]): TimelineItem[] {
  // History first, so an event and a comment at the same moment keep that order after the
  // (stable) sort.
  const items: TimelineItem[] = [
    ...history.map((entry) => ({ kind: 'status' as const, key: `s${entry.id}`, at: entry.changedAt, entry })),
    ...comments.map((comment) => ({ kind: 'comment' as const, key: `c${comment.id}`, at: comment.createdAt, comment })),
  ];
  return items.sort((a, b) => a.at.localeCompare(b.at));
}
