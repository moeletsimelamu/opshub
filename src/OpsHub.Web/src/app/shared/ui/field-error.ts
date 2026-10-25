import { Component, Injector, afterNextRender, input } from '@angular/core';
import { AbstractControl } from '@angular/forms';

import { Icon } from './icon';

/**
 * Shows the first error of a form control, once the user has left the field or tried to
 * submit. Server-side errors (from the API's ProblemDetails) are stored as `server`.
 * Give it the same id the input references in aria-describedby.
 *
 * Uses default change detection on purpose: reactive-form state isn't a signal, so this
 * re-renders whenever its parent form does.
 */
@Component({
  selector: 'app-field-error',
  imports: [Icon],
  template: `
    @if (message(); as text) {
      <p class="field-error" [id]="errorId()"><app-icon name="alert" [size]="14" />{{ text }}</p>
    }
  `,
})
export class FieldError {
  readonly control = input.required<AbstractControl | null>();
  readonly errorId = input.required<string>();
  /** Field name used in messages, e.g. "Title". */
  readonly label = input('This field');

  message(): string | null {
    const control = this.control();
    if (!control || !control.errors || !control.touched) {
      return null;
    }

    const errors = control.errors;
    if (errors['server']) return String(errors['server']);
    // `min` is how a select with a "Choose…" placeholder (value 0) reports "nothing chosen".
    if (errors['required'] || errors['min']) return `${this.label()} is required.`;
    if (errors['email']) return 'Enter a valid email address, like name@marula.example.';
    if (errors['minlength']) return `${this.label()} must be at least ${errors['minlength'].requiredLength} characters.`;
    if (errors['maxlength']) return `${this.label()} must be at most ${errors['maxlength'].requiredLength} characters.`;
    return `${this.label()} is not valid.`;
  }
}

/** True when the control should be announced as invalid (for aria-invalid). */
export function isInvalid(control: AbstractControl | null): boolean {
  return !!control && control.invalid && control.touched;
}

/**
 * After a failed submit, move keyboard focus to the first invalid field so keyboard and
 * screen-reader users land on the problem. Runs after Angular has rendered aria-invalid.
 */
export function focusFirstInvalid(host: HTMLElement, injector: Injector): void {
  afterNextRender(() => host.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus(), { injector });
}
