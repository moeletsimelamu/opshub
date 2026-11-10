import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { PasswordResetService } from '../../core/auth/password-reset.service';
import { ResetPassword } from './reset-password';

/** Choosing a new password from the emailed link (?user=…&token=…). */
describe('ResetPassword', () => {
  let fixture: ComponentFixture<ResetPassword>;
  const reset = vi.fn();

  async function open(user?: string, token?: string): Promise<void> {
    reset.mockReset();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: PasswordResetService, useValue: { reset } }],
    });
    fixture = TestBed.createComponent(ResetPassword);
    if (user) fixture.componentRef.setInput('user', user);
    if (token) fixture.componentRef.setInput('token', token);
    await fixture.whenStable();
  }

  const root = (): HTMLElement => fixture.nativeElement;

  async function submit(password: string, repeat: string): Promise<void> {
    for (const [id, value] of [['new-password', password], ['confirm-password', repeat]]) {
      const input = root().querySelector<HTMLInputElement>(`#${id}`)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    }
    root().querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    await new Promise((r) => setTimeout(r));
    await fixture.whenStable();
  }

  it('says when the link is incomplete instead of showing the form', async () => {
    await open('user-1');

    expect(root().querySelector('form')).toBeNull();
    expect(root().querySelector('[role=alert]')?.textContent).toContain('This link is incomplete');
  });

  it('won’t submit passwords that don’t match', async () => {
    await open('user-1', 'token-1');

    await submit('a-long-new-password', 'a-different-password');

    expect(reset).not.toHaveBeenCalled();
    expect(root().querySelector('#confirm-password-mismatch')?.textContent).toContain('don’t match');
  });

  it('sets the password and offers to sign in', async () => {
    await open('user-1', 'token-1');
    reset.mockResolvedValue(undefined);

    await submit('a-long-new-password', 'a-long-new-password');

    expect(reset).toHaveBeenCalledWith('user-1', 'token-1', 'a-long-new-password');
    expect(root().querySelector('[role=status]')?.textContent).toContain('Your password has been changed');
  });

  it('explains an expired or used link and links to a new one', async () => {
    await open('user-1', 'token-1');
    reset.mockRejectedValue(new HttpErrorResponse({
      status: 400,
      error: { title: 'Bad Request', status: 400, code: 'invalid_reset_link', detail: 'This reset link is invalid or has expired.' },
    }));

    await submit('a-long-new-password', 'a-long-new-password');

    const alert = root().querySelector('[role=alert]');
    expect(alert?.textContent).toContain('invalid or has expired');
    expect(alert?.querySelector('a[href="/forgot-password"]')).not.toBeNull();
  });
});
