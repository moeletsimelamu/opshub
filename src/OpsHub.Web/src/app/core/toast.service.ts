import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: number;
  message: string;
  tone: 'success' | 'info';
}

/**
 * Short confirmations after an action ("Ticket assigned to Sipho"). Shown in a polite
 * live region so screen readers announce them. Errors that need action are shown as
 * banners on the page instead (DESIGN.md §5).
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  readonly toasts = signal<Toast[]>([]);

  success(message: string): void {
    this.show(message, 'success');
  }

  info(message: string): void {
    this.show(message, 'info');
  }

  dismiss(id: number): void {
    this.toasts.update((list) => list.filter((t) => t.id !== id));
  }

  private show(message: string, tone: Toast['tone']): void {
    const id = this.nextId++;
    this.toasts.update((list) => [...list.slice(-2), { id, message, tone }]);
    setTimeout(() => this.dismiss(id), 4000);
  }
}
