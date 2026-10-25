import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';

import { ROLE_LABELS } from '../core/api/models';
import { SessionService } from '../core/auth/session.service';
import { Icon } from '../shared/ui/icon';
import { ToastHost } from '../shared/ui/toast-host';

/**
 * The signed-in frame: sidebar navigation on wide screens, a top bar with a drawer on
 * narrow ones (DESIGN.md §6–7). Navigation items depend on the role, and only link to
 * screens that exist.
 */
@Component({
  selector: 'app-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Icon, ToastHost],
  templateUrl: './shell.html',
  styleUrl: './shell.css',
})
export class Shell {
  protected readonly session = inject(SessionService);
  private readonly router = inject(Router);

  protected readonly menuOpen = signal(false);
  protected readonly signingOut = signal(false);

  protected readonly roleLabel = computed(() => {
    const role = this.session.user()?.role;
    return role ? ROLE_LABELS[role] : '';
  });

  protected readonly initials = computed(() =>
    (this.session.user()?.displayName ?? '')
      .split(' ')
      .map((part) => part[0])
      .slice(0, 2)
      .join('')
      .toUpperCase(),
  );

  constructor() {
    // Close the mobile drawer after navigating.
    this.router.events
      .pipe(
        filter((e) => e instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.menuOpen.set(false));
  }

  /**
   * With <base href="/">, a plain href="#main" would navigate to "/#main" and leave the
   * current page, so move focus to the main region directly.
   */
  protected skipToContent(event: Event): void {
    event.preventDefault();
    document.getElementById('main')?.focus();
  }

  protected async signOut(): Promise<void> {
    this.signingOut.set(true);
    try {
      await this.session.signOut();
    } finally {
      this.signingOut.set(false);
    }
  }
}
