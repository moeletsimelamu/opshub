import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, ElementRef, Injector, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { ApiError } from '../../core/api/api-error';
import { ROLE_LABELS, Role, UserSummary } from '../../core/api/models';
import { UsersApi } from '../../core/api/users-api';
import { Icon } from '../../shared/ui/icon';
import { FieldError, focusFirstInvalid, isInvalid } from '../../shared/ui/field-error';

export interface UserDialogData {
  /** null = create a new user. */
  user: UserSummary | null;
  isSelf: boolean;
}

/** Add or edit a user. Closes with the saved user, or undefined if cancelled. */
@Component({
  selector: 'app-user-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, FieldError, Icon],
  styleUrl: '../../shared/ui/dialog.css',
  templateUrl: './user-dialog.html',
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
export class UserDialog {
  private readonly api = inject(UsersApi);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  protected readonly data = inject<UserDialogData>(DIALOG_DATA);
  protected readonly dialogRef = inject<DialogRef<UserSummary>>(DialogRef);

  protected readonly roles = Object.entries(ROLE_LABELS) as [Role, string][];
  protected readonly isInvalid = isInvalid;
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);

  private readonly fb = inject(NonNullableFormBuilder);
  protected readonly form = this.fb.group({
    email: [{ value: this.data.user?.email ?? '', disabled: !!this.data.user }, [Validators.required, Validators.email, Validators.maxLength(256)]],
    displayName: [this.data.user?.displayName ?? '', [Validators.required, Validators.maxLength(100)]],
    role: [{ value: this.data.user?.role ?? ('Requester' as Role), disabled: this.data.isSelf }, Validators.required],
    isActive: [{ value: this.data.user?.isActive ?? true, disabled: this.data.isSelf }],
    password: ['', this.data.user ? [] : [Validators.required, Validators.minLength(12), Validators.maxLength(128)]],
  });

  protected async save(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      focusFirstInvalid(this.host.nativeElement, this.injector);
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    const value = this.form.getRawValue();
    try {
      const saved = this.data.user
        ? await firstValueFrom(this.api.update(this.data.user.id, { displayName: value.displayName.trim(), role: value.role, isActive: value.isActive }))
        : await firstValueFrom(this.api.create({ email: value.email.trim(), displayName: value.displayName.trim(), role: value.role, password: value.password }));
      this.dialogRef.close(saved);
    } catch (error) {
      const apiError = ApiError.from(error);
      if (apiError.code === 'email_in_use') {
        this.form.controls.email.setErrors({ server: apiError.message });
        focusFirstInvalid(this.host.nativeElement, this.injector);
      } else if (!apiError.applyTo(this.form)) {
        this.error.set(apiError);
      }
    } finally {
      this.saving.set(false);
    }
  }
}
