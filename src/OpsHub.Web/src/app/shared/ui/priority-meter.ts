import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { TICKET_PRIORITIES, TicketPriority } from '../../core/api/models';

/**
 * Priority as a four-step signal meter plus the word (DESIGN.md §4). The number of
 * filled bars carries the meaning; only High and Critical are coloured.
 */
@Component({
  selector: 'app-priority-meter',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="meter" [attr.data-level]="level()">
      <span class="bars" aria-hidden="true">
        @for (bar of bars; track bar) {
          <span class="bar" [class.filled]="bar <= level()" [style.height.px]="3 + bar * 2"></span>
        }
      </span>
      <span [class.sr-only]="compact()">{{ priority() }}</span>
    </span>
  `,
  styles: `
    .meter {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      font-size: var(--text-sm);
      color: var(--priority-muted);
      white-space: nowrap;
    }
    .bars {
      display: inline-flex;
      align-items: flex-end;
      gap: 2px;
      height: 11px;
    }
    .bar {
      width: 3px;
      border-radius: 1px;
      background: var(--priority-empty-bar);
    }
    .bar.filled {
      background: currentColor;
    }
    [data-level='3'] {
      color: var(--priority-high);
      font-weight: var(--weight-medium);
    }
    [data-level='4'] {
      color: var(--priority-critical);
      font-weight: var(--weight-semibold);
    }
  `,
})
export class PriorityMeter {
  readonly priority = input.required<TicketPriority>();
  /** Bars only, with the word kept for screen readers. */
  readonly compact = input(false);

  protected readonly bars = [1, 2, 3, 4];
  protected readonly level = computed(() => TICKET_PRIORITIES.indexOf(this.priority()) + 1);
}
