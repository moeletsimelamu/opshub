import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { SessionService } from '../../core/auth/session.service';
import { Login } from './login';

describe('Login', () => {
  let fixture: ComponentFixture<Login>;
  const signIn = vi.fn();

  beforeEach(async () => {
    signIn.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: SessionService, useValue: { signIn, apiUnavailable: signal(false), homeUrl: () => '/dashboard' } },
      ],
    });
    fixture = TestBed.createComponent(Login);
    await fixture.whenStable();
  });

  const root = (): HTMLElement => fixture.nativeElement;

  async function fill(email: string, password: string): Promise<void> {
    const emailInput = root().querySelector<HTMLInputElement>('#email')!;
    const passwordInput = root().querySelector<HTMLInputElement>('#password')!;
    emailInput.value = email;
    emailInput.dispatchEvent(new Event('input'));
    passwordInput.value = password;
    passwordInput.dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  async function submit(): Promise<void> {
    root().querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    // Let the async sign-in settle, then re-render.
    await new Promise((r) => setTimeout(r));
    await fixture.whenStable();
  }

  it('shows field errors and does not call the API when the form is empty', async () => {
    await submit();

    expect(signIn).not.toHaveBeenCalled();
    const errors = [...root().querySelectorAll('.field-error')].map((e) => e.textContent?.trim());
    expect(errors).toEqual(['Email is required.', 'Password is required.']);
    expect(root().querySelector('#email')?.getAttribute('aria-invalid')).toBe('true');
  });

  it('signs in and goes to the role’s home page', async () => {
    signIn.mockResolvedValue({ id: '1', email: 'lerato@marula.example', displayName: 'Lerato', role: 'Administrator' });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    await fill('lerato@marula.example', 'a-long-password');
    await submit();

    expect(signIn).toHaveBeenCalledWith('lerato@marula.example', 'a-long-password');
    expect(navigate).toHaveBeenCalledWith('/dashboard');
  });

  it('shows the API’s generic message on a failed sign-in and clears the password', async () => {
    signIn.mockRejectedValue(
      new HttpErrorResponse({ status: 401, error: { title: 'Sign-in failed', status: 401, detail: 'The email or password is incorrect.' } }),
    );

    await fill('lerato@marula.example', 'wrong-password-1');
    await submit();

    expect(root().querySelector('.banner-error')?.textContent).toContain('The email or password is incorrect.');
    expect(root().querySelector<HTMLInputElement>('#password')!.value).toBe('');
  });
});
