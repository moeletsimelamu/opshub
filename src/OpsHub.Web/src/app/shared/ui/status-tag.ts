import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { STATUS_LABELS, TicketStatus } from '../../core/api/models';

const SHAPES: Record<TicketStatus, string> = {
  Open: 'open',
  InProgress: 'progress',
  WaitingForInformation: 'waiting',
  Resolved: 'resolved',
  Closed: 'closed',
};

/**
 * Ticket status as text + a shape glyph + a tint (DESIGN.md §4). The shape and the word
 * carry the meaning; colour only reinforces it, so it works for colour-blind users.
 */
@Component({
  selector: 'app-status-tag',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="tag" [attr.data-status]="shape()"><span class="glyph" aria-hidden="true"></span>{{ label() }}</span>`,
  styles: `
    .tag {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 2px 8px 2px 6px;
      border-radius: var(--radius-sm);
      font-size: var(--text-xs);
      line-height: var(--leading-xs);
      font-weight: var(--weight-medium);
      white-space: nowrap;
    }
    .glyph {
      position: relative;
      width: 9px;
      height: 9px;
      flex: none;
    }
    [data-status='open'] { color: var(--status-open-fg); background: var(--status-open-bg); }
    [data-status='open'] .glyph { border: 1.5px solid currentColor; border-radius: 50%; }
    [data-status='progress'] { color: var(--status-progress-fg); background: var(--status-progress-bg); }
    [data-status='progress'] .glyph {
      border: 1.5px solid currentColor;
      border-radius: 50%;
      background: linear-gradient(90deg, currentColor 50%, transparent 50%);
    }
    [data-status='waiting'] { color: var(--status-waiting-fg); background: var(--status-waiting-bg); }
    [data-status='waiting'] .glyph {
      border-left: 2.5px solid currentColor;
      border-right: 2.5px solid currentColor;
      width: 8px;
    }
    [data-status='resolved'] { color: var(--status-resolved-fg); background: var(--status-resolved-bg); }
    [data-status='resolved'] .glyph {
      width: 5px;
      height: 9px;
      border: solid currentColor;
      border-width: 0 2px 2px 0;
      transform: translateY(-1px) rotate(45deg);
      margin: 0 2px;
    }
    [data-status='closed'] { color: var(--status-closed-fg); background: var(--status-closed-bg); }
    [data-status='closed'] .glyph { border-radius: 50%; background: currentColor; }
  `,
})
export class StatusTag {
  readonly status = input.required<TicketStatus>();

  protected readonly label = computed(() => STATUS_LABELS[this.status()]);
  protected readonly shape = computed(() => SHAPES[this.status()]);
}
