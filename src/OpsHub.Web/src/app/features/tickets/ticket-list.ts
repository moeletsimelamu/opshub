import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { BehaviorSubject, catchError, combineLatest, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';

import { ACTIVE_STATUSES, TICKET_PRIORITIES, TicketPriority } from '../../core/api/models';
import { TicketListParams, TicketsApi } from '../../core/api/tickets-api';
import { SessionService } from '../../core/auth/session.service';
import { keepPreviousData, toLoadState } from '../../core/load-state';
import { EmptyState } from '../../shared/ui/empty-state';
import { Icon } from '../../shared/ui/icon';
import { Pagination } from '../../shared/ui/pagination';
import { PriorityMeter } from '../../shared/ui/priority-meter';
import { RelativeTime } from '../../shared/ui/relative-time';
import { StatusTag } from '../../shared/ui/status-tag';

type ViewId = 'triage' | 'mine' | 'active' | 'all';
type SortColumn = 'id' | 'status' | 'priority' | 'updatedAt';

interface View {
  id: ViewId;
  label: string;
}

const STAFF_VIEWS: View[] = [
  { id: 'triage', label: 'Needs triage' },
  { id: 'mine', label: 'Assigned to me' },
  { id: 'active', label: 'All active' },
  { id: 'all', label: 'All tickets' },
];

const REQUESTER_VIEWS: View[] = [
  { id: 'active', label: 'Active' },
  { id: 'all', label: 'All' },
];

/**
 * Phones have no visible column headers (rows are stacked), so sorting is a select there.
 * Every column/direction pair the headers can produce is listed, so the select always
 * shows the current order.
 */
const SORT_OPTIONS: { value: `${SortColumn}:${'asc' | 'desc'}`; label: string }[] = [
  { value: 'updatedAt:desc', label: 'Sort: recently updated' },
  { value: 'updatedAt:asc', label: 'Sort: least recently updated' },
  { value: 'priority:desc', label: 'Sort: highest priority' },
  { value: 'priority:asc', label: 'Sort: lowest priority' },
  { value: 'id:desc', label: 'Sort: newest' },
  { value: 'id:asc', label: 'Sort: oldest' },
  { value: 'status:asc', label: 'Sort: status, open first' },
  { value: 'status:desc', label: 'Sort: status, closed first' },
];

/** Everything the list shows is derived from the URL, so views are shareable and the back button works. */
interface ListQuery {
  view: ViewId;
  search: string;
  priority: TicketPriority | null;
  categoryId: number | null;
  sort: SortColumn;
  direction: 'asc' | 'desc';
  page: number;
}

const PAGE_SIZE = 20;

@Component({
  selector: 'app-ticket-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, StatusTag, PriorityMeter, RelativeTime, Pagination, EmptyState, Icon],
  templateUrl: './ticket-list.html',
  styleUrl: './ticket-list.css',
})
export class TicketList {
  private readonly api = inject(TicketsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly session = inject(SessionService);

  protected readonly priorities = TICKET_PRIORITIES;
  protected readonly sortOptions = SORT_OPTIONS;
  protected readonly views = computed(() => (this.session.isStaff() ? STAFF_VIEWS : REQUESTER_VIEWS));
  protected readonly categories = toSignal(this.api.categories().pipe(catchError(() => of([]))), { initialValue: [] });

  private readonly queryParams = toSignal(this.route.queryParamMap, { requireSync: true });
  protected readonly query = computed(() => this.parse(this.queryParams()));
  private readonly view = computed(() => this.query().view);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  private readonly reload$ = new BehaviorSubject<void>(undefined);
  protected readonly state = toSignal(
    combineLatest([toObservable(this.query), this.reload$]).pipe(
      switchMap(([query]) => toLoadState(this.api.list(this.toApiParams(query)))),
      keepPreviousData(),
    ),
    { initialValue: { kind: 'loading', data: null } as const },
  );

  protected readonly hasFilters = computed(() => {
    const q = this.query();
    return !!q.search || !!q.priority || !!q.categoryId;
  });

  /** The search box updates the URL after the user pauses typing. */
  protected readonly searchText = signal('');

  constructor() {
    effect(() => this.searchText.set(this.query().search));

    // On phones the view tabs scroll sideways, so the current one (e.g. "All tickets",
    // last in the row) can start out of sight. A task, so the new aria-current is rendered,
    // and after the web font has loaded, because it changes the tabs' widths.
    effect(() => {
      this.view();
      setTimeout(async () => {
        await document.fonts?.ready;
        this.host.nativeElement
          .querySelector<HTMLElement>('.tab[aria-current="page"]')
          ?.scrollIntoView?.({ block: 'nearest', inline: 'nearest' });
      });
    });

    toObservable(this.searchText)
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe((text) => {
        if (text.trim() !== this.query().search) {
          this.navigate({ q: text.trim() || null, page: null }, true);
        }
      });
  }

  protected viewLink(view: ViewId): Record<string, string | null> {
    return { view, page: null };
  }

  protected setPriority(value: string): void {
    this.navigate({ priority: value || null, page: null });
  }

  protected setCategory(value: string): void {
    this.navigate({ category: value || null, page: null });
  }

  protected clearFilters(): void {
    this.searchText.set('');
    this.navigate({ q: null, priority: null, category: null, page: null });
  }

  protected sortBy(column: SortColumn): void {
    const q = this.query();
    // Clicking the active column flips the direction; a new column starts with the most useful order.
    const direction = q.sort === column ? (q.direction === 'desc' ? 'asc' : 'desc') : column === 'id' ? 'asc' : 'desc';
    this.navigate({ sort: column, dir: direction, page: null });
  }

  /** From the phone sort select: "priority:desc". */
  protected setSort(value: string): void {
    const [column, direction] = value.split(':');
    this.navigate({ sort: column, dir: direction, page: null });
  }

  protected ariaSort(column: SortColumn): 'ascending' | 'descending' | null {
    const q = this.query();
    return q.sort === column ? (q.direction === 'asc' ? 'ascending' : 'descending') : null;
  }

  protected goToPage(page: number): void {
    this.navigate({ page: page > 1 ? page : null });
    window.scrollTo({ top: 0 });
  }

  protected retry(): void {
    this.reload$.next();
  }

  protected readonly emptyMessage = computed(() => {
    if (this.hasFilters()) {
      return { heading: 'No tickets match these filters', description: 'Try a different search term or clear the filters.' };
    }

    switch (this.query().view) {
      case 'triage':
        return { heading: 'Nothing is waiting for triage', description: 'New tickets that nobody has picked up yet will appear here.' };
      case 'mine':
        return { heading: 'You have no active tickets', description: 'Pick one up from “Needs triage”, or ask a colleague to assign one to you.' };
      default:
        return this.session.isStaff()
          ? { heading: 'No tickets here', description: 'Tickets appear here as soon as someone reports a problem.' }
          : { heading: 'You haven’t reported anything yet', description: 'When something isn’t working, report it and follow progress here.' };
    }
  });

  private navigate(changes: Record<string, string | number | null>, replaceUrl = false): void {
    void this.router.navigate([], { queryParams: changes, queryParamsHandling: 'merge', replaceUrl });
  }

  private parse(params: ParamMap): ListQuery {
    const allowedViews = this.views().map((v) => v.id);
    const requestedView = params.get('view') as ViewId | null;
    const view = requestedView && allowedViews.includes(requestedView) ? requestedView : allowedViews[0];
    const priority = params.get('priority') as TicketPriority | null;
    const sort = params.get('sort') as SortColumn | null;
    const page = Number(params.get('page'));
    const categoryId = Number(params.get('category'));

    return {
      view,
      search: params.get('q') ?? '',
      priority: priority && TICKET_PRIORITIES.includes(priority) ? priority : null,
      categoryId: Number.isInteger(categoryId) && categoryId > 0 ? categoryId : null,
      sort: sort && ['id', 'status', 'priority', 'updatedAt'].includes(sort) ? sort : 'updatedAt',
      direction: params.get('dir') === 'asc' ? 'asc' : 'desc',
      page: Number.isInteger(page) && page > 1 ? page : 1,
    };
  }

  private toApiParams(q: ListQuery): TicketListParams {
    const params: TicketListParams = {
      search: q.search || undefined,
      priority: q.priority ? [q.priority] : undefined,
      categoryId: q.categoryId ?? undefined,
      sortBy: q.sort,
      sortDirection: q.direction,
      page: q.page,
      pageSize: PAGE_SIZE,
    };

    switch (q.view) {
      case 'triage':
        return { ...params, status: ['Open'], unassigned: true };
      case 'mine':
        return { ...params, status: ACTIVE_STATUSES, assigneeId: this.session.user()?.id };
      case 'active':
        // For a requester, a Resolved ticket still needs them: confirm the fix or reopen it.
        return { ...params, status: this.session.isStaff() ? ACTIVE_STATUSES : [...ACTIVE_STATUSES, 'Resolved'] };
      default:
        return params;
    }
  }
}
