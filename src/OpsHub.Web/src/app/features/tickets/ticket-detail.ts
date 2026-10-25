import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, ElementRef, Injector, afterNextRender, computed, effect, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { BehaviorSubject, Observable, catchError, combineLatest, firstValueFrom, forkJoin, map, of, switchMap, tap } from 'rxjs';

import { ApiError } from '../../core/api/api-error';
import { AgentSummary, STATUS_LABELS, StatusHistoryEntry, TicketComment, TicketDetails, TicketStatus } from '../../core/api/models';
import { TicketsApi } from '../../core/api/tickets-api';
import { SessionService } from '../../core/auth/session.service';
import { keepPreviousData, toLoadState } from '../../core/load-state';
import { ToastService } from '../../core/toast.service';
import { FieldError, isInvalid } from '../../shared/ui/field-error';
import { Icon } from '../../shared/ui/icon';
import { PriorityMeter } from '../../shared/ui/priority-meter';
import { RelativeTime } from '../../shared/ui/relative-time';
import { StatusTag } from '../../shared/ui/status-tag';
import { StatusDialog } from './status-dialog';
import { StatusAction, statusActions } from './ticket-actions';

interface TicketView {
  ticket: TicketDetails;
  comments: TicketComment[];
  history: StatusHistoryEntry[];
}

/** One entry in the merged timeline of comments and status changes. */
type TimelineItem =
  | { kind: 'comment'; key: string; at: string; comment: TicketComment }
  | { kind: 'status'; key: string; at: string; entry: StatusHistoryEntry };

@Component({
  selector: 'app-ticket-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, ReactiveFormsModule, StatusTag, PriorityMeter, RelativeTime, Icon, FieldError],
  templateUrl: './ticket-detail.html',
  styleUrl: './ticket-detail.css',
})
export class TicketDetail {
  private readonly api = inject(TicketsApi);
  private readonly dialog = inject(Dialog);
  private readonly toasts = inject(ToastService);
  private readonly title = inject(Title);
  protected readonly session = inject(SessionService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  /**
   * After an action the buttons re-render (the one that was clicked may no longer exist),
   * so focus is moved to the status actions once the reloaded ticket is on screen.
   * Without this, keyboard focus would fall back to the top of the page.
   */
  private readonly focusAfterReload = signal(false);

  /** The :id route parameter (withComponentInputBinding). */
  readonly id = input.required<string>();
  private readonly ticketId = computed(() => Number(this.id()));

  private readonly reload$ = new BehaviorSubject<void>(undefined);
  protected readonly state = toSignal(
    combineLatest([toObservable(this.ticketId), this.reload$]).pipe(
      switchMap(([id]) => toLoadState(this.load(id))),
      keepPreviousData(),
    ),
    { initialValue: { kind: 'loading', data: null } as const },
  );

  protected readonly ticket = computed(() => this.state().data?.ticket ?? null);

  protected readonly timeline = computed<TimelineItem[]>(() => {
    const view = this.state().data;
    if (!view) return [];
    const items: TimelineItem[] = [
      ...view.comments.map((comment) => ({ kind: 'comment' as const, key: `c${comment.id}`, at: comment.createdAt, comment })),
      // The first history row is the ticket being reported; the header already shows that.
      ...view.history.filter((h) => h.fromStatus !== null).map((entry) => ({ kind: 'status' as const, key: `s${entry.id}`, at: entry.changedAt, entry })),
    ];
    return items.sort((a, b) => a.at.localeCompare(b.at));
  });

  protected readonly actions = computed<StatusAction[]>(() => {
    const t = this.ticket();
    return t ? statusActions(t.status, t.permissions.allowedStatuses, this.session.isStaff()) : [];
  });

  protected readonly needsReply = computed(() => {
    const t = this.ticket();
    return !!t && t.status === 'WaitingForInformation' && t.requester.id === this.session.user()?.id;
  });

  protected readonly statusLabels = STATUS_LABELS;
  protected readonly busy = signal<string | null>(null);
  protected readonly actionError = signal<ApiError | null>(null);

  // ---------- Assignment ----------
  protected readonly agents = signal<AgentSummary[] | null>(null);
  protected readonly selectedAgent = signal<string>('');
  protected readonly canAssignToMe = computed(() => {
    const t = this.ticket();
    const me = this.session.user();
    return !!t && t.permissions.canAssign && me?.role === 'SupportAgent' && t.assignedAgent?.id !== me.id;
  });

  // ---------- Comments ----------
  protected readonly commentForm = inject(NonNullableFormBuilder).group({
    body: ['', [Validators.required, Validators.maxLength(4000)]],
    visibility: ['public' as 'public' | 'internal'],
  });

  /**
   * The app is zoneless: templates re-render when signals change, and form values aren't
   * signals. Exposing the choice as a signal keeps the button label, placeholder and tint
   * in step with the radio the user picked.
   */
  protected readonly isInternalComment = toSignal(
    this.commentForm.controls.visibility.valueChanges.pipe(map((v) => v === 'internal')),
    { initialValue: false },
  );
  protected readonly isInvalid = isInvalid;

  constructor() {
    // Staff who can assign need the agent list; fetch it once the ticket says so.
    effect(() => {
      const t = this.ticket();
      if (t?.permissions.canAssign && this.agents() === null) {
        this.loadAgents();
      }
      this.selectedAgent.set(t?.assignedAgent?.id ?? '');
    });

    effect(() => {
      if (this.focusAfterReload() && this.state().kind === 'ready') {
        this.focusAfterReload.set(false);
        afterNextRender(
          () => {
            // Prefer the action bar; a closed ticket has none, so fall back to the heading.
            const root = this.host.nativeElement;
            (root.querySelector<HTMLElement>('.actions') ?? root.querySelector<HTMLElement>('h1'))?.focus();
          },
          { injector: this.injector },
        );
      }
    });
  }

  protected retry(): void {
    this.actionError.set(null);
    this.reload$.next();
  }

  // ---------- Status ----------

  protected async runAction(action: StatusAction): Promise<void> {
    const ticket = this.ticket();
    if (!ticket) return;

    let note: string | null = null;
    if (action.dialog) {
      const ref = this.dialog.open<string>(StatusDialog, { data: action.dialog, ariaLabelledBy: 'status-dialog-title' });
      const result = await firstValueFrom(ref.closed);
      if (result === undefined) return; // cancelled
      note = result || null;
    }

    await this.mutate(
      `status-${action.to}`,
      this.api.changeStatus(ticket.id, action.to, note, ticket.version),
      `${ticket.reference} moved to ${STATUS_LABELS[action.to]}`,
    );
  }

  // ---------- Assignment ----------

  private agentsRequested = false;

  private loadAgents(): void {
    if (this.agentsRequested) return;
    this.agentsRequested = true;
    this.api
      .agents()
      .pipe(catchError(() => of([])))
      .subscribe((agents) => {
        this.agentsRequested = false;
        this.agents.set(agents);
      });
  }

  protected async assign(agentId: string | null): Promise<void> {
    const ticket = this.ticket();
    if (!ticket) return;

    const agentName = agentId
      ? (this.agents()?.find((a) => a.id === agentId)?.displayName ?? this.session.user()?.displayName ?? 'agent')
      : null;
    await this.mutate(
      'assign',
      this.api.assign(ticket.id, agentId, ticket.version),
      agentName ? `Assigned to ${agentName}` : 'Ticket unassigned and returned to the queue',
    );
    this.agents.set(null); // workload counts changed; refetch next time
  }

  // ---------- Comments ----------

  protected async addComment(): Promise<void> {
    const ticket = this.ticket();
    this.commentForm.markAllAsTouched();
    if (!ticket || this.commentForm.invalid) return;

    const { body, visibility } = this.commentForm.getRawValue();
    const internal = visibility === 'internal';
    const ok = await this.mutate(
      'comment',
      this.api.addComment(ticket.id, body.trim(), internal),
      internal ? 'Internal note added' : 'Reply added',
    );
    if (ok) {
      this.commentForm.reset({ body: '', visibility });
    }
  }

  /**
   * Runs a change, then reloads the whole view so status, history and permissions stay
   * consistent with what the server decided (e.g. a reply that resumed the ticket).
   */
  private async mutate(key: string, request: Observable<unknown>, successMessage: string): Promise<boolean> {
    this.busy.set(key);
    this.actionError.set(null);
    try {
      await firstValueFrom(request);
      this.toasts.success(successMessage);
      this.focusAfterReload.set(true);
      this.reload$.next();
      return true;
    } catch (error) {
      this.actionError.set(ApiError.from(error));
      return false;
    } finally {
      this.busy.set(null);
    }
  }

  private load(id: number): Observable<TicketView> {
    return forkJoin({
      ticket: this.api.get(id),
      comments: this.api.comments(id),
      history: this.api.history(id),
    }).pipe(tap((view) => this.title.setTitle(`${view.ticket.reference} · ${view.ticket.title} · OpsHub`)));
  }

  protected statusLabel(status: TicketStatus | null): string {
    return status ? STATUS_LABELS[status] : '';
  }

  protected initials(name: string): string {
    return name
      .split(' ')
      .map((part) => part[0])
      .slice(0, 2)
      .join('')
      .toUpperCase();
  }
}
