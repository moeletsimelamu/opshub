import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * What would be here, and what to do about it (DESIGN.md §5). No illustrations.
 * Put an optional action button inside the element.
 */
@Component({
  selector: 'app-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="title">{{ heading() }}</p>
    @if (description()) {
      <p class="description">{{ description() }}</p>
    }
    <div class="actions"><ng-content /></div>
  `,
  styles: `
    :host {
      display: grid;
      justify-items: center;
      gap: var(--space-2);
      padding: var(--space-7) var(--space-5);
      text-align: center;
    }
    .title {
      font-weight: var(--weight-semibold);
    }
    .description {
      max-width: 48ch;
      color: var(--color-text-muted);
      font-size: var(--text-sm);
      line-height: var(--leading-sm);
    }
    .actions:empty {
      display: none;
    }
    .actions {
      margin-top: var(--space-2);
    }
  `,
})
export class EmptyState {
  readonly heading = input.required<string>();
  readonly description = input<string>();
}
