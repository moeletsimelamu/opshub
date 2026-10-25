import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { CurrentUser } from '../api/models';

/**
 * Who is signed in, as signals the whole app can read.
 *
 * The session itself lives in an HttpOnly cookie that JavaScript cannot read, so the
 * app never stores a token. It only remembers the user profile the API returned, and
 * asks the API (`/api/auth/me`) on start-up. Roles here decide what the UI shows;
 * the API re-checks every request regardless.
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly currentUser = signal<CurrentUser | null>(null);

  readonly user = this.currentUser.asReadonly();
  readonly isSignedIn = computed(() => this.currentUser() !== null);
  readonly isAdmin = computed(() => this.currentUser()?.role === 'Administrator');
  readonly isStaff = computed(() => {
    const role = this.currentUser()?.role;
    return role === 'Administrator' || role === 'SupportAgent';
  });

  /** True when the API could not be reached at start-up. The login page explains it. */
  readonly apiUnavailable = signal(false);

  /**
   * Runs once before the first route is shown (provideAppInitializer). Fetches an XSRF
   * token so the first POST works, then asks who is signed in.
   */
  async initialise(): Promise<void> {
    try {
      await firstValueFrom(this.http.get('/api/auth/antiforgery'));
      this.apiUnavailable.set(false);
    } catch {
      this.apiUnavailable.set(true);
      return;
    }

    try {
      this.currentUser.set(await firstValueFrom(this.http.get<CurrentUser>('/api/auth/me')));
    } catch (error) {
      if (!(error instanceof HttpErrorResponse && error.status === 401)) {
        throw error;
      }
      this.currentUser.set(null);
    }
  }

  async signIn(email: string, password: string): Promise<CurrentUser> {
    // Refresh the token first: after a sign-out or an expired session the old token
    // belongs to the previous identity and would be rejected.
    await firstValueFrom(this.http.get('/api/auth/antiforgery'));
    const user = await firstValueFrom(this.http.post<CurrentUser>('/api/auth/login', { email, password }));
    this.currentUser.set(user);
    this.apiUnavailable.set(false);
    return user;
  }

  async signOut(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/api/auth/logout', {}));
    } finally {
      this.currentUser.set(null);
      await this.router.navigateByUrl('/login');
    }
  }

  /** Called by the HTTP interceptor when any request comes back 401. */
  async handleSessionExpired(): Promise<void> {
    if (!this.currentUser()) {
      return;
    }

    this.currentUser.set(null);
    const returnUrl = this.router.url;
    await this.router.navigate(['/login'], { queryParams: { returnUrl, reason: 'expired' } });
  }

  /** Where each role starts after signing in. */
  homeUrl(): string {
    return this.isStaff() ? '/tickets?view=triage' : '/tickets';
  }
}
