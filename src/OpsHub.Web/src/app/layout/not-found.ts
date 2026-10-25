import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

import { EmptyState } from '../shared/ui/empty-state';

@Component({
  selector: 'app-not-found',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EmptyState, RouterLink],
  template: `
    <div class="page">
      <h1 class="sr-only">Page not found</h1>
      <app-empty-state heading="This page doesn’t exist" description="The link may be out of date, or the address mistyped.">
        <a routerLink="/tickets" class="btn btn-secondary">Go to tickets</a>
      </app-empty-state>
    </div>
  `,
})
export class NotFound {}
