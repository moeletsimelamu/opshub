import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { BehaviorSubject, combineLatest, debounceTime, distinctUntilChanged, firstValueFrom, switchMap } from 'rxjs';

import { ROLE_LABELS, Role, UserSummary } from '../../core/api/models';
import { UserListParams, UsersApi } from '../../core/api/users-api';
import { SessionService } from '../../core/auth/session.service';
import { keepPreviousData, toLoadState } from '../../core/load-state';
import { ToastService } from '../../core/toast.service';
import { EmptyState } from '../../shared/ui/empty-state';
import { Icon } from '../../shared/ui/icon';
import { Pagination } from '../../shared/ui/pagination';
import { RelativeTime } from '../../shared/ui/relative-time';
import { UserDialog, UserDialogData } from './user-dialog';

/** Administrator-only user management (the route is guarded and the API enforces AdminOnly). */
@Component({
  selector: 'app-user-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EmptyState, Icon, Pagination, RelativeTime],
  templateUrl: './user-list.html',
  styleUrl: './user-list.css',
})
export class UserList {
  private readonly api = inject(UsersApi);
  private readonly dialog = inject(Dialog);
  private readonly toasts = inject(ToastService);
  private readonly session = inject(SessionService);

  protected readonly roleLabels = ROLE_LABELS;
  protected readonly roles = Object.entries(ROLE_LABELS) as [Role, string][];

  protected readonly searchText = signal('');
  private readonly search = signal('');
  protected readonly role = signal<Role | ''>('');
  protected readonly status = signal<'' | 'active' | 'inactive'>('');
  protected readonly page = signal(1);

  private readonly params = computed<UserListParams>(() => ({
    search: this.search() || undefined,
    role: this.role() || undefined,
    isActive: this.status() === '' ? undefined : this.status() === 'active',
    page: this.page(),
    pageSize: 20,
  }));

  private readonly reload$ = new BehaviorSubject<void>(undefined);
  protected readonly state = toSignal(
    combineLatest([toObservable(this.params), this.reload$]).pipe(
      switchMap(([params]) => toLoadState(this.api.list(params))),
      keepPreviousData(),
    ),
    { initialValue: { kind: 'loading', data: null } as const },
  );

  protected readonly hasFilters = computed(() => !!this.search() || !!this.role() || !!this.status());

  constructor() {
    toObservable(this.searchText)
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe((text) => {
        this.search.set(text.trim());
        this.page.set(1);
      });
  }

  protected setRole(value: string): void {
    this.role.set(value as Role | '');
    this.page.set(1);
  }

  protected setStatus(value: string): void {
    this.status.set(value as '' | 'active' | 'inactive');
    this.page.set(1);
  }

  protected clearFilters(): void {
    this.searchText.set('');
    this.search.set('');
    this.role.set('');
    this.status.set('');
    this.page.set(1);
  }

  protected retry(): void {
    this.reload$.next();
  }

  protected async openDialog(user: UserSummary | null): Promise<void> {
    const data: UserDialogData = { user, isSelf: user?.id === this.session.user()?.id };
    const ref = this.dialog.open<UserSummary>(UserDialog, { data, ariaLabelledBy: 'user-dialog-title' });
    const saved = await firstValueFrom(ref.closed);
    if (saved) {
      this.toasts.success(user ? `${saved.displayName} updated` : `${saved.displayName} added as ${ROLE_LABELS[saved.role].toLowerCase()}`);
      this.reload$.next();
    }
  }
}
