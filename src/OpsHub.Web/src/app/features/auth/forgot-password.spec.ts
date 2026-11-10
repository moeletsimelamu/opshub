import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { PasswordResetService } from '../../core/auth/password-reset.service';
import { ForgotPassword } from './forgot-password';

/** Asking for a reset link: the confirmation never reveals whether the address has an account. */
describe('ForgotPassword', () => {
  let fixture: ComponentFixture<ForgotPassword>;
  const requestLink = vi.fn();

  beforeEach(async () => {
    requestLink.mockReset();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: PasswordResetService, useValue: { requestLink } }],
    });
    fixture = TestBed.createComponent(ForgotPassword);
    await fixture.whenStable();
  });

  const root = (): HTMLElement => fixture.nativeElement;

  async function submit(email: string): Promise<void> {
    const input = root().querySelector<HTMLInputElement>('#email')!;
    input.value = email;
    input.dispatchEvent(new Event('input'));
    root().querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    await new Promise((r) => setTimeout(r));
    await fixture.whenStable();
  }

  it('needs a valid email address before calling the API', async () => {
    await submit('not-an-email');

    expect(requestLink).not.toHaveBeenCalled();
    expect(root().querySelector('#email-error')?.textContent).toContain('valid email address');
  });

  it('shows a neutral confirmation after sending', async () => {
    requestLink.mockResolvedValue(undefined);

    await submit('  thandi@marula.example ');

    expect(requestLink).toHaveBeenCalledWith('thandi@marula.example');
    const status = root().querySelector('[role=status]')?.textContent ?? '';
    expect(status).toContain('If an account exists for thandi@marula.example');
    expect(root().querySelector('form')).toBeNull();
  });

  it('explains when password reset by email isn’t set up', async () => {
    requestLink.mockRejectedValue(new HttpErrorResponse({
      status: 404,
      error: { title: 'Not Found', status: 404, code: 'password_reset_unavailable', detail: 'Ask an administrator to set a new password for you.' },
    }));

    await submit('thandi@marula.example');

    expect(root().querySelector('[role=alert]')?.textContent).toContain('Ask an administrator');
  });
});
