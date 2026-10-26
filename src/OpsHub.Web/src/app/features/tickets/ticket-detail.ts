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
import { HasUnsavedChanges, warnBeforeUnload } from '../../core/unsaved-changes';
import { FieldError, isInvalid } from '../../shared/ui/field-error';
import { Icon } from '../../shared/ui/icon';
import { PriorityMeter } from '../../shared/ui/priority-meter';
import { RelativeTime } from '../../shared/ui/relative-time';
import { StatusTag } from '../../shared/ui/status-tag';
import { StatusDialog } from './status-dialog';
import { StatusAction, statusActions } from './ticket-actions';
import { TimelineItem, buildTimeline } from './timeline';

interface TicketView {
  ticket: TicketDetails;
  comments: TicketComment[];
  history: StatusHistoryEntry[];
}

@Component({
  selector: 'app-ticket-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(window:beforeunload)': 'onBeforeUnload($event)' },
  imports: [RouterLink, ReactiveFormsModule, StatusTag, PriorityMeter, RelativeTime, Icon, FieldError],
  templateUrl: './ticket-detail.html',
  styleUrl: './ticket-detail.css',
})
export class TicketDetail implements HasUnsavedChanges {
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
    return view ? buildTimeline(view.history, view.comments) : [];
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

  /** A half-written reply counts as unsaved work. */
  hasUnsavedChanges(): boolean {
    return this.commentForm.controls.body.value.trim() !== '';
  }

  protected onBeforeUnload(event: BeforeUnloadEvent): void {
    warnBeforeUnload(event, this.hasUnsavedChanges());
  }

  /**
   * Agents usually explain the fix and resolve in one go ("Send and resolve"). Offered
   * only when the workflow allows Resolved and the comment is a public reply.
   */
  protected readonly canSendAndResolve = computed(() => {
    const t = this.ticket();
    return this.session.isStaff() && !!t && t.permissions.allowedStatuses.includes('Resolved') && !this.isInternalComment();
  });

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
   * Posts the reply, then resolves. The reply changes the ticket's version, so the fresh
   * version is fetched before the status change (otherwise it would be a 409 conflict).
   * If the reply succeeds but resolving fails, the message says exactly that.
   */
  protected async sendAndResolve(): Promise<void> {
    const ticket = this.ticket();
    this.commentForm.markAllAsTouched();
    if (!ticket || this.commentForm.invalid) return;

    this.busy.set('comment-resolve');
    this.actionError.set(null);
    try {
      await firstValueFrom(this.api.addComment(ticket.id, this.commentForm.controls.body.value.trim(), false));
    } catch (error) {
      this.actionError.set(ApiError.from(error));
      this.busy.set(null);
      return;
    }

    this.commentForm.reset({ body: '', visibility: 'public' });
    try {
      const fresh = await firstValueFrom(this.api.get(ticket.id));
      await firstValueFrom(this.api.changeStatus(ticket.id, 'Resolved', null, fresh.version));
      this.toasts.success(`Reply sent and ${ticket.reference} resolved`);
    } catch (error) {
      const cause = ApiError.from(error);
      this.actionError.set(
        new ApiError(cause.status, 'Reply sent, but the ticket wasn’t resolved', cause.message, cause.code, {}, cause.traceId),
      );
    } finally {
      this.busy.set(null);
      this.focusAfterReload.set(true);
      this.reload$.next();
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
