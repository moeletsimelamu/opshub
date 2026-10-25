import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { ToastService } from '../../core/toast.service';
import { Icon } from './icon';

/** Renders toasts in a polite live region so screen readers announce them. */
@Component({
  selector: 'app-toast-host',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  template: `
    <div class="host" role="status" aria-live="polite" aria-atomic="false">
      @for (toast of toasts.toasts(); track toast.id) {
        <div class="toast" [attr.data-tone]="toast.tone">
          <app-icon [name]="toast.tone === 'success' ? 'check' : 'alert'" />
          <span class="message">{{ toast.message }}</span>
          <button type="button" class="close" (click)="toasts.dismiss(toast.id)">
            <app-icon name="x" label="Dismiss notification" />
          </button>
        </div>
      }
    </div>
  `,
  styles: `
    .host {
      position: fixed;
      right: var(--space-4);
      bottom: var(--space-4);
      z-index: 1100;
      display: grid;
      gap: var(--space-2);
      width: min(360px, calc(100vw - 2 * var(--space-4)));
    }
    .toast {
      display: flex;
      gap: var(--space-3);
      align-items: center;
      padding: var(--space-3) var(--space-3) var(--space-3) var(--space-4);
      background: var(--color-text);
      color: #fff;
      border-radius: var(--radius-md);
      box-shadow: var(--shadow-overlay);
      font-size: var(--text-sm);
      line-height: var(--leading-sm);
      animation: enter var(--duration-base) var(--ease-out);
    }
    .toast[data-tone='success'] app-icon {
      color: #9fd8a1;
    }
    .message {
      flex: 1;
    }
    .close {
      display: inline-flex;
      padding: var(--space-1);
      border: 0;
      border-radius: var(--radius-sm);
      background: none;
      color: #fff;
      cursor: pointer;
    }
    .close:hover {
      background: rgb(255 255 255 / 0.12);
    }
    @keyframes enter {
      from { opacity: 0; transform: translateY(6px); }
    }
  `,
})
export class ToastHost {
  protected readonly toasts = inject(ToastService);
}
