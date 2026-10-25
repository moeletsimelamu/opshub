import { ChangeDetectionStrategy, Component, ElementRef, Injector, OnInit, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, firstValueFrom, of } from 'rxjs';

import { ApiError } from '../../core/api/api-error';
import { TICKET_PRIORITIES, TicketDetails, TicketPriority } from '../../core/api/models';
import { TicketsApi } from '../../core/api/tickets-api';
import { SessionService } from '../../core/auth/session.service';
import { ToastService } from '../../core/toast.service';
import { HasUnsavedChanges, warnBeforeUnload } from '../../core/unsaved-changes';
import { FieldError, focusFirstInvalid, isInvalid } from '../../shared/ui/field-error';
import { Icon } from '../../shared/ui/icon';

const PRIORITY_HINTS: Record<TicketPriority, string> = {
  Low: 'A minor inconvenience; there is an easy workaround.',
  Medium: 'It slows down my work.',
  High: 'It stops me from doing my work.',
  Critical: 'It stops a whole team, a site or a customer delivery.',
};

/** New ticket (/tickets/new) and edit ticket (/tickets/:id/edit). */
@Component({
  selector: 'app-ticket-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(window:beforeunload)': 'onBeforeUnload($event)' },
  imports: [ReactiveFormsModule, RouterLink, FieldError, Icon],
  templateUrl: './ticket-form.html',
  styleUrl: './ticket-form.css',
})
export class TicketForm implements OnInit, HasUnsavedChanges {
  private readonly api = inject(TicketsApi);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  protected readonly session = inject(SessionService);

  /** Present when editing (route parameter :id). */
  readonly id = input<string>();
  protected readonly isEdit = computed(() => !!this.id());

  protected readonly priorities = TICKET_PRIORITIES;
  protected readonly categories = toSignal(this.api.categories().pipe(catchError(() => of(null))), { initialValue: undefined });

  protected readonly form = inject(NonNullableFormBuilder).group({
    title: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(200)]],
    categoryId: [0, [Validators.required, Validators.min(1)]],
    priority: ['Medium' as TicketPriority, Validators.required],
    description: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(4000)]],
  });

  protected readonly existing = signal<TicketDetails | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly saveError = signal<ApiError | null>(null);
  protected readonly saving = signal(false);
  protected readonly descriptionLength = signal(0);
  protected readonly selectedPriority = signal<TicketPriority>('Medium');
  protected readonly isInvalid = isInvalid;

  /** Set just before navigating away after a successful save, so the guard doesn't ask. */
  private saved = false;

  hasUnsavedChanges(): boolean {
    return this.form.dirty && !this.saved;
  }

  protected onBeforeUnload(event: BeforeUnloadEvent): void {
    warnBeforeUnload(event, this.hasUnsavedChanges());
  }

  protected readonly priorityHint = computed(() => PRIORITY_HINTS[this.selectedPriority()]);
  protected readonly cancelLink = computed(() => (this.isEdit() ? ['/tickets', this.id()] : ['/tickets']));

  constructor() {
    this.form.controls.description.valueChanges.subscribe((v) => this.descriptionLength.set(v.length));
    this.form.controls.priority.valueChanges.subscribe((v) => this.selectedPriority.set(v));
  }

  async ngOnInit(): Promise<void> {
    if (!this.isEdit()) return;

    try {
      const ticket = await firstValueFrom(this.api.get(Number(this.id())));
      this.existing.set(ticket);
      this.form.setValue({
        title: ticket.title,
        categoryId: ticket.category.id,
        priority: ticket.priority,
        description: ticket.description,
      });
      if (!ticket.permissions.canEditPriority) {
        this.form.controls.priority.disable();
      }
      if (!ticket.permissions.canEdit) {
        this.form.disable();
      }
    } catch (error) {
      this.loadError.set(ApiError.from(error));
    }
  }

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      focusFirstInvalid(this.host.nativeElement, this.injector);
      return;
    }

    this.saving.set(true);
    this.saveError.set(null);
    const value = this.form.getRawValue();
    const input = { ...value, title: value.title.trim(), description: value.description.trim(), categoryId: Number(value.categoryId) };

    try {
      const existing = this.existing();
      const saved = existing
        ? await firstValueFrom(this.api.update(existing.id, input, existing.version))
        : await firstValueFrom(this.api.create(input));

      this.toasts.success(existing ? 'Changes saved' : `Ticket ${saved.reference} submitted`);
      this.saved = true;
      await this.router.navigate(['/tickets', saved.id]);
    } catch (error) {
      const apiError = ApiError.from(error);
      // Field errors go next to their inputs; anything else becomes a banner.
      if (apiError.applyTo(this.form)) {
        focusFirstInvalid(this.host.nativeElement, this.injector);
      } else {
        this.saveError.set(apiError);
      }
    } finally {
      this.saving.set(false);
    }
  }
}
