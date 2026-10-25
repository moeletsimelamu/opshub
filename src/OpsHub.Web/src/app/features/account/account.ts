import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, ElementRef, Injector, computed, inject, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { ApiError } from '../../core/api/api-error';
import { ROLE_LABELS } from '../../core/api/models';
import { SessionService } from '../../core/auth/session.service';
import { ToastService } from '../../core/toast.service';
import { FieldError, focusFirstInvalid, isInvalid } from '../../shared/ui/field-error';
import { Icon } from '../../shared/ui/icon';

/**
 * Cross-field rule: the two new-password boxes must match. It is a form-level error (not
 * an error on one field), so it clears correctly whichever of the two boxes is corrected.
 */
function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const newPassword = group.get('newPassword')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return confirm && newPassword !== confirm ? { passwordMismatch: true } : null;
}

/** Your profile and password (POST /api/auth/change-password). */
@Component({
  selector: 'app-account',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, FieldError, Icon],
  templateUrl: './account.html',
  styleUrl: './account.css',
})
export class Account {
  private readonly http = inject(HttpClient);
  private readonly toasts = inject(ToastService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  protected readonly session = inject(SessionService);

  protected readonly roleLabel = computed(() => {
    const role = this.session.user()?.role;
    return role ? ROLE_LABELS[role] : '';
  });

  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      currentPassword: ['', Validators.required],
      newPassword: ['', [Validators.required, Validators.minLength(12), Validators.maxLength(128)]],
      confirmPassword: ['', Validators.required],
    },
    { validators: passwordsMatch },
  );

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly isInvalid = isInvalid;

  protected showMismatch(): boolean {
    return this.form.hasError('passwordMismatch') && this.form.controls.confirmPassword.touched;
  }

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      focusFirstInvalid(this.host.nativeElement, this.injector);
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    const { currentPassword, newPassword } = this.form.getRawValue();
    try {
      await firstValueFrom(this.http.post('/api/auth/change-password', { currentPassword, newPassword }));
      this.form.reset();
      this.toasts.success('Password changed. Any other signed-in devices have been signed out.');
    } catch (error) {
      const apiError = ApiError.from(error);
      // "Current password is incorrect" and password-policy errors appear next to their fields.
      if (apiError.applyTo(this.form)) {
        focusFirstInvalid(this.host.nativeElement, this.injector);
      } else {
        this.error.set(apiError);
      }
    } finally {
      this.saving.set(false);
    }
  }
}
