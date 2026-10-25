import { ChangeDetectionStrategy, Component, ElementRef, computed, effect, inject, signal, viewChild } from '@angular/core';
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

  private readonly menuButton = viewChild.required<ElementRef<HTMLButtonElement>>('menuButton');
  private readonly sidebar = viewChild.required<ElementRef<HTMLElement>>('sidebar');

  protected readonly menuOpen = signal(false);
  private menuWasOpen = false;
  /** Navigating closes the menu too; focus then belongs to the new page, not the menu button. */
  private closedByNavigation = false;
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
    // Mobile menu focus management: opening moves focus into the menu; closing it
    // (button, Esc or the dimmed background) returns focus to the menu button.
    effect(() => {
      const open = this.menuOpen();
      if (open === this.menuWasOpen) return;
      this.menuWasOpen = open;
      // A task, not afterNextRender: in a zoneless app afterNextRender registered inside an
      // effect waits for the *next* render, and opening the menu doesn't cause another one.
      // By the time this task runs, the menu's visibility has been applied, so it can take focus.
      setTimeout(() => {
        const button = this.menuButton().nativeElement;
        if (open) {
          // The first *visible* control: the brand link at the top is hidden on phones.
          [...this.sidebar().nativeElement.querySelectorAll<HTMLElement>('a, button')]
            .find((el) => el.offsetParent !== null)
            ?.focus();
        } else if (this.closedByNavigation) {
          // A link in the menu was chosen: continue at the start of the new page's content.
          document.getElementById('main')?.focus();
        } else if (button.offsetParent !== null) {
          // offsetParent is null when the button (or its top bar) is hidden, i.e. on desktop.
          button.focus();
        }
        this.closedByNavigation = false;
      });
    });

    // Close the mobile drawer after navigating.
    this.router.events
      .pipe(
        filter((e) => e instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => {
        if (this.menuOpen()) {
          this.closedByNavigation = true;
          this.menuOpen.set(false);
        }
      });
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
