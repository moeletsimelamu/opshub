import { AssignmentHistoryEntry, StatusHistoryEntry, TicketComment } from '../../core/api/models';

/** One entry in the merged timeline of comments, status changes and assignment changes. */
export type TimelineItem =
  | { kind: 'comment'; key: string; at: string; comment: TicketComment }
  | { kind: 'status'; key: string; at: string; entry: StatusHistoryEntry }
  | { kind: 'assignment'; key: string; at: string; assignment: AssignmentHistoryEntry };

/**
 * Merges a ticket's status history, assignment changes and comments into one list, oldest
 * first. The first history row (from no status to Open) is the ticket being reported, so the
 * timeline tells the whole story from submission onwards.
 */
export function buildTimeline(
  history: StatusHistoryEntry[],
  comments: TicketComment[],
  assignments: AssignmentHistoryEntry[] = [],
): TimelineItem[] {
  // Events first, so an event and a comment at the same moment keep that order after the
  // (stable) sort. An assignment comes before a status change made at the same moment: an
  // agent is assigned, then starts work.
  const items: TimelineItem[] = [
    ...assignments.map((assignment) => ({ kind: 'assignment' as const, key: `a${assignment.id}`, at: assignment.changedAt, assignment })),
    ...history.map((entry) => ({ kind: 'status' as const, key: `s${entry.id}`, at: entry.changedAt, entry })),
    ...comments.map((comment) => ({ kind: 'comment' as const, key: `c${comment.id}`, at: comment.createdAt, comment })),
  ];
  return items.sort((a, b) => a.at.localeCompare(b.at));
}
