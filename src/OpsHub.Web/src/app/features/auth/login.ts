import { ChangeDetectionStrategy, Component, ElementRef, Injector, computed, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

import { ApiError } from '../../core/api/api-error';
import { SessionService } from '../../core/auth/session.service';
import { FieldError, focusFirstInvalid, isInvalid } from '../../shared/ui/field-error';
import { Icon } from '../../shared/ui/icon';

@Component({
  selector: 'app-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, FieldError, Icon],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class Login {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  /** Query parameters, bound by the router (withComponentInputBinding). */
  readonly returnUrl = input<string>();
  readonly reason = input<string>();

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected readonly submitting = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly apiUnavailable = this.session.apiUnavailable;
  protected readonly sessionExpired = computed(() => this.reason() === 'expired' && !this.error());
  protected readonly isInvalid = isInvalid;

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      focusFirstInvalid(this.host.nativeElement, this.injector);
      return;
    }

    if (this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    try {
      const { email, password } = this.form.getRawValue();
      await this.session.signIn(email.trim(), password);
      await this.router.navigateByUrl(this.safeReturnUrl() ?? this.session.homeUrl());
    } catch (error) {
      this.error.set(ApiError.from(error));
      this.form.controls.password.reset();
    } finally {
      this.submitting.set(false);
    }
  }

  /** Only follow in-app paths, never another site (prevents an "open redirect"). */
  private safeReturnUrl(): string | null {
    const url = this.returnUrl();
    return url && url.startsWith('/') && !url.startsWith('//') && !url.startsWith('/login') ? url : null;
  }
}
