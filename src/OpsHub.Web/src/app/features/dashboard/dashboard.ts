import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { BehaviorSubject, switchMap } from 'rxjs';

import { Dashboard, STATUS_LABELS, TicketPriority, TicketStatus } from '../../core/api/models';
import { TicketsApi } from '../../core/api/tickets-api';
import { keepPreviousData, toLoadState } from '../../core/load-state';
import { EmptyState } from '../../shared/ui/empty-state';
import { Icon } from '../../shared/ui/icon';
import { PriorityMeter } from '../../shared/ui/priority-meter';
import { RelativeTime } from '../../shared/ui/relative-time';
import { StatusTag } from '../../shared/ui/status-tag';

/** One row of a horizontal bar list. */
interface BarRow<T> {
  key: T;
  count: number;
  /** Bar length as a percentage of the largest value in the same list. */
  width: number;
  /** Share of the list's total, for the hover text. */
  share: number;
}

const timeFormat = new Intl.DateTimeFormat(undefined, { timeStyle: 'short' });

/** "45 min", "6.1 h", "3.2 days": the unit people would use when saying it out loud. */
export function formatDuration(hours: number): string {
  if (hours < 1) return `${Math.max(1, Math.round(hours * 60))} min`;
  if (hours < 48) return `${hours.toFixed(1)} h`;
  return `${(hours / 24).toFixed(1)} days`;
}

/**
 * Staff overview (DESIGN.md "Dashboard"). Every figure comes from GET /api/dashboard,
 * which computes it from the database on each request; nothing here is hard-coded.
 */
@Component({
  selector: 'app-dashboard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, StatusTag, PriorityMeter, RelativeTime, EmptyState, Icon],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class DashboardPage {
  private readonly api = inject(TicketsApi);

  private readonly refresh$ = new BehaviorSubject<void>(undefined);
  protected readonly state = toSignal(
    this.refresh$.pipe(
      switchMap(() => toLoadState(this.api.dashboard())),
      keepPreviousData(),
    ),
    { initialValue: { kind: 'loading', data: null } as const },
  );

  protected readonly data = computed<Dashboard | null>(() => this.state().data);
  protected readonly asOf = computed(() => {
    const d = this.data();
    return d ? timeFormat.format(new Date(d.generatedAt)) : '';
  });

  protected readonly statusRows = computed(() =>
    toBars(this.data()?.byStatus.map((s) => ({ key: s.status, count: s.count })) ?? []),
  );
  protected readonly priorityRows = computed(() =>
    // Most urgent first: that's the order people scan for.
    toBars(this.data()?.openByPriority.map((p) => ({ key: p.priority, count: p.count })).reverse() ?? []),
  );
  protected readonly maxWorkload = computed(() => Math.max(1, ...(this.data()?.agentWorkload.map((a) => a.activeTickets) ?? [])));

  protected readonly resolution = computed(() => {
    const r = this.data()?.resolutionTime;
    if (!r) return null;
    return {
      ...r,
      average: r.averageHours === null ? null : formatDuration(r.averageHours),
      median: r.medianHours === null ? null : formatDuration(r.medianHours),
    };
  });

  protected readonly statusLabels = STATUS_LABELS;

  protected refresh(): void {
    this.refresh$.next();
  }

  protected statusHover(row: BarRow<TicketStatus>): string {
    return `${STATUS_LABELS[row.key]}: ${row.count} ${row.count === 1 ? 'ticket' : 'tickets'} (${row.share}% of all tickets)`;
  }

  protected priorityHover(row: BarRow<TicketPriority>): string {
    return `${row.key}: ${row.count} open ${row.count === 1 ? 'ticket' : 'tickets'} (${row.share}% of open tickets)`;
  }
}

function toBars<T>(items: { key: T; count: number }[]): BarRow<T>[] {
  const max = Math.max(1, ...items.map((i) => i.count));
  const total = items.reduce((sum, i) => sum + i.count, 0);
  return items.map((i) => ({
    ...i,
    width: (i.count / max) * 100,
    share: total === 0 ? 0 : Math.round((i.count / total) * 100),
  }));
}
