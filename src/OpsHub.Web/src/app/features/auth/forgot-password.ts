import { ChangeDetectionStrategy, Component, ElementRef, Injector, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { ApiError } from '../../core/api/api-error';
import { PasswordResetService } from '../../core/auth/password-reset.service';
import { FieldError, focusFirstInvalid, isInvalid } from '../../shared/ui/field-error';
import { Icon } from '../../shared/ui/icon';

/** Asks for a reset link. Shows the same confirmation whether or not the address has an account. */
@Component({
  selector: 'app-forgot-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, FieldError, Icon],
  templateUrl: './forgot-password.html',
  styleUrl: './login.css',
})
export class ForgotPassword {
  private readonly passwordReset = inject(PasswordResetService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
  });

  protected readonly submitting = signal(false);
  protected readonly sentTo = signal<string | null>(null);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly isInvalid = isInvalid;

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      focusFirstInvalid(this.host.nativeElement, this.injector);
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    const email = this.form.controls.email.value.trim();
    try {
      await this.passwordReset.requestLink(email);
      this.sentTo.set(email);
    } catch (error) {
      this.error.set(ApiError.from(error));
    } finally {
      this.submitting.set(false);
    }
  }
}
