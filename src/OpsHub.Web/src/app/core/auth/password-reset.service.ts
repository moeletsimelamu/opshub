import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** "Forgot your password?": the three anonymous /api/auth endpoints behind it. */
@Injectable({ providedIn: 'root' })
export class PasswordResetService {
  private readonly http = inject(HttpClient);

  /** Whether the sign-in page should offer a reset: only when the server can send email. */
  async isAvailable(): Promise<boolean> {
    try {
      return (await firstValueFrom(this.http.get<{ passwordReset: boolean }>('/api/auth/options'))).passwordReset;
    } catch {
      return false; // can't tell: don't offer something that may not work
    }
  }

  /** The server answers the same for every address, so this never says whether an account exists. */
  async requestLink(email: string): Promise<void> {
    await firstValueFrom(this.http.get('/api/auth/antiforgery')); // POSTs need a current XSRF token
    await firstValueFrom(this.http.post('/api/auth/forgot-password', { email }));
  }

  async reset(userId: string, token: string, newPassword: string): Promise<void> {
    await firstValueFrom(this.http.get('/api/auth/antiforgery'));
    await firstValueFrom(this.http.post('/api/auth/reset-password', { userId, token, newPassword }));
  }
}
