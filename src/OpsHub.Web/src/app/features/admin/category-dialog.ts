import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, ElementRef, Injector, inject, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { ApiError } from '../../core/api/api-error';
import { CategoriesApi } from '../../core/api/categories-api';
import { CategoryAdmin } from '../../core/api/models';
import { FieldError, focusFirstInvalid, isInvalid } from '../../shared/ui/field-error';
import { Icon } from '../../shared/ui/icon';

export interface CategoryDialogData {
  /** null = create a new category. */
  category: CategoryAdmin | null;
}

/** Add or edit a category. Closes with the saved category, or undefined if cancelled. */
@Component({
  selector: 'app-category-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, FieldError, Icon],
  styleUrl: '../../shared/ui/dialog.css',
  templateUrl: './category-dialog.html',
  styles: `
    .checkbox {
      display: flex;
      gap: var(--space-3);
      align-items: flex-start;
      font-size: var(--text-sm);
      line-height: var(--leading-sm);
    }
    .checkbox input {
      width: 18px;
      height: 18px;
      margin: 0;
      accent-color: var(--color-primary);
    }
    .checkbox strong {
      display: block;
      font-weight: var(--weight-medium);
    }
  `,
})
export class CategoryDialog {
  private readonly api = inject(CategoriesApi);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  protected readonly data = inject<CategoryDialogData>(DIALOG_DATA);
  protected readonly dialogRef = inject<DialogRef<CategoryAdmin>>(DialogRef);

  /** Same limit as the API (Category.NameMaxLength). */
  protected readonly nameMaxLength = 60;
  protected readonly isInvalid = isInvalid;
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);

  private readonly fb = inject(NonNullableFormBuilder);
  protected readonly form = this.fb.group({
    // A name of only spaces counts as empty, as it does on the server.
    name: [this.data.category?.name ?? '', [(c: AbstractControl<string>) => (c.value.trim() ? null : { required: true }), Validators.maxLength(this.nameMaxLength)]],
    isActive: [this.data.category?.isActive ?? true],
  });

  protected async save(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      focusFirstInvalid(this.host.nativeElement, this.injector);
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    const { name, isActive } = this.form.getRawValue();
    try {
      const saved = this.data.category
        ? await firstValueFrom(this.api.update(this.data.category.id, { name: name.trim(), isActive }))
        : await firstValueFrom(this.api.create(name.trim()));
      this.dialogRef.close(saved);
    } catch (error) {
      const apiError = ApiError.from(error);
      if (apiError.code === 'category_name_in_use') {
        this.form.controls.name.setErrors({ server: apiError.message });
        focusFirstInvalid(this.host.nativeElement, this.injector);
      } else if (!apiError.applyTo(this.form)) {
        this.error.set(apiError); // e.g. "at least one category must stay active"
      }
    } finally {
      this.saving.set(false);
    }
  }
}
