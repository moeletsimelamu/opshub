import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { BehaviorSubject, firstValueFrom, switchMap } from 'rxjs';

import { CategoriesApi } from '../../core/api/categories-api';
import { CategoryAdmin } from '../../core/api/models';
import { keepPreviousData, toLoadState } from '../../core/load-state';
import { ToastService } from '../../core/toast.service';
import { Icon } from '../../shared/ui/icon';
import { CategoryDialog, CategoryDialogData } from './category-dialog';

/** Administrator-only category management (the route is guarded and the API enforces AdminOnly). */
@Component({
  selector: 'app-category-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  templateUrl: './category-list.html',
  styleUrls: ['./user-list.css', './category-list.css'],
})
export class CategoryList {
  private readonly api = inject(CategoriesApi);
  private readonly dialog = inject(Dialog);
  private readonly toasts = inject(ToastService);

  private readonly reload$ = new BehaviorSubject<void>(undefined);
  protected readonly state = toSignal(
    this.reload$.pipe(
      switchMap(() => toLoadState(this.api.list())),
      keepPreviousData(),
    ),
    { initialValue: { kind: 'loading', data: null } as const },
  );

  protected readonly activeCount = computed(() => this.state().data?.filter((c) => c.isActive).length ?? 0);

  protected retry(): void {
    this.reload$.next();
  }

  protected async openDialog(category: CategoryAdmin | null): Promise<void> {
    const data: CategoryDialogData = { category };
    const ref = this.dialog.open<CategoryAdmin>(CategoryDialog, { data, ariaLabelledBy: 'category-dialog-title' });
    const saved = await firstValueFrom(ref.closed);
    if (saved) {
      this.toasts.success(
        !category ? `${saved.name} added`
          : category.isActive && !saved.isActive ? `${saved.name} deactivated`
          : !category.isActive && saved.isActive ? `${saved.name} reactivated`
          : `${saved.name} updated`,
      );
      this.reload$.next();
    }
  }
}
