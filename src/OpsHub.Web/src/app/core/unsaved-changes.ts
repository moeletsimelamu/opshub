import { CanDeactivateFn } from '@angular/router';

/** A screen that can hold work the user hasn't saved yet. */
export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
}

/**
 * Asks before leaving a screen with unsaved work (a half-written ticket or reply).
 * The browser's own confirm dialog is used on purpose: it is accessible, keyboard
 * friendly, and blocks navigation until answered.
 */
export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = (component) =>
  !component.hasUnsavedChanges() || window.confirm('You have unsaved changes. Leave this page and discard them?');

/**
 * Covers what the router can't: closing the tab, reloading, or typing another address.
 * Browsers show their own generic "Leave site?" message.
 */
export function warnBeforeUnload(event: BeforeUnloadEvent, hasUnsavedChanges: boolean): void {
  if (hasUnsavedChanges) {
    event.preventDefault();
  }
}
