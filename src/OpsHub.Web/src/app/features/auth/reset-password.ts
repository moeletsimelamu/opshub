import { ChangeDetectionStrategy, Component, ElementRef, Injector, computed, inject, input, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { ApiError } from '../../core/api/api-error';
import { PasswordResetService } from '../../core/auth/password-reset.service';
import { FieldError, focusFirstInvalid, isInvalid } from '../../shared/ui/field-error';
import { Icon } from '../../shared/ui/icon';

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('newPassword')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return confirm && password !== confirm ? { passwordMismatch: true } : null;
}

/** Opened from the emailed link (?user=…&token=…): choose a new password. */
@Component({
  selector: 'app-reset-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, FieldError, Icon],
  templateUrl: './reset-password.html',
  styleUrl: './login.css',
})
export class ResetPassword {
  private readonly passwordReset = inject(PasswordResetService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  /** Query parameters, bound by the router (withComponentInputBinding). */
  readonly user = input<string>();
  readonly token = input<string>();

  protected readonly linkIsComplete = computed(() => !!this.user() && !!this.token());

  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      newPassword: ['', [Validators.required, Validators.minLength(12), Validators.maxLength(128)]],
      confirmPassword: ['', Validators.required],
    },
    { validators: passwordsMatch },
  );

  protected readonly submitting = signal(false);
  protected readonly done = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly isInvalid = isInvalid;

  protected readonly mismatch = signal(false);

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    this.mismatch.set(this.form.hasError('passwordMismatch'));
    if (this.form.invalid) {
      focusFirstInvalid(this.host.nativeElement, this.injector);
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    try {
      await this.passwordReset.reset(this.user()!, this.token()!, this.form.controls.newPassword.value);
      this.done.set(true);
    } catch (error) {
      const apiError = ApiError.from(error);
      if (!apiError.applyTo(this.form)) {
        this.error.set(apiError); // e.g. an expired or already used link
      }
    } finally {
      this.submitting.set(false);
    }
  }
}
