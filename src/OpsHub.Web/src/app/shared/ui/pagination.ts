import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

import { Icon } from './icon';

/** "Showing 21–40 of 87" with previous/next (DESIGN.md §5). */
@Component({
  selector: 'app-pagination',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  template: `
    <nav class="pagination" aria-label="Pagination">
      <p class="summary tabular" aria-live="polite">
        @if (total() === 0) {
          No results
        } @else {
          Showing {{ from() }}–{{ to() }} of {{ total() }}
        }
      </p>
      @if (totalPages() > 1) {
        <div class="buttons">
          <button type="button" class="btn btn-secondary btn-sm" [disabled]="page() <= 1" (click)="pageChange.emit(page() - 1)">
            <app-icon name="chevron-left" /> Previous
          </button>
          <span class="page tabular">Page {{ page() }} of {{ totalPages() }}</span>
          <button type="button" class="btn btn-secondary btn-sm" [disabled]="page() >= totalPages()" (click)="pageChange.emit(page() + 1)">
            Next <app-icon name="chevron-right" />
          </button>
        </div>
      }
    </nav>
  `,
  styles: `
    .pagination {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-3);
      align-items: center;
      justify-content: space-between;
      font-size: var(--text-sm);
      color: var(--color-text-muted);
    }
    .summary {
      margin: 0;
    }
    .buttons {
      display: flex;
      gap: var(--space-3);
      align-items: center;
    }
    .page {
      line-height: 1;
    }
    @media (max-width: 639px) {
      .page { display: none; }
    }
  `,
})
export class Pagination {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly total = input.required<number>();
  readonly totalPages = input.required<number>();
  readonly pageChange = output<number>();

  protected readonly from = computed(() => (this.page() - 1) * this.pageSize() + 1);
  protected readonly to = computed(() => Math.min(this.page() * this.pageSize(), this.total()));
}
