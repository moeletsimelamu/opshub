import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { Dashboard } from '../../core/api/models';
import { TicketsApi } from '../../core/api/tickets-api';
import { DashboardPage, formatDuration } from './dashboard';

function dashboard(overrides: Partial<Dashboard> = {}): Dashboard {
  return {
    generatedAt: '2026-10-25T12:00:00Z',
    totals: { open: 13, needsTriage: 4, waitingForRequester: 3, criticalOpen: 1, awaitingConfirmation: 6, total: 24 },
    byStatus: [
      { status: 'Open', count: 5 },
      { status: 'InProgress', count: 5 },
      { status: 'WaitingForInformation', count: 3 },
      { status: 'Resolved', count: 6 },
      { status: 'Closed', count: 5 },
    ],
    openByPriority: [
      { priority: 'Low', count: 2 },
      { priority: 'Medium', count: 6 },
      { priority: 'High', count: 4 },
      { priority: 'Critical', count: 1 },
    ],
    agentWorkload: [{ id: 'a', displayName: 'Ayesha Patel', activeTickets: 3, urgentTickets: 1, resolvedInWindow: 3 }],
    recentlyUpdated: [],
    resolutionTime: { windowDays: 30, sampleSize: 9, minimumSampleSize: 3, averageHours: 14.1667, medianHours: 5 },
    ...overrides,
  };
}

async function render(data: Dashboard): Promise<HTMLElement> {
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: TicketsApi, useValue: { dashboard: () => of(data) } }],
  });
  const fixture = TestBed.createComponent(DashboardPage);
  await fixture.whenStable();
  return fixture.nativeElement;
}

/** Each tile's label, value and note (separate lines on screen), joined with spaces. */
const stats = (root: HTMLElement) =>
  [...root.querySelectorAll('.stat')].map((tile) =>
    [...tile.children].map((part) => part.textContent?.replace(/\s+/g, ' ').trim()).join(' '),
  );

describe('formatDuration', () => {
  it.each([
    [0.25, '15 min'],
    [0.001, '1 min'],
    [5, '5.0 h'],
    [14.1667, '14.2 h'],
    [47.9, '47.9 h'],
    [72, '3.0 days'],
  ])('formats %f hours as "%s"', (hours, expected) => {
    expect(formatDuration(hours)).toBe(expected);
  });
});

describe('DashboardPage', () => {
  it('shows the API’s figures, including average and median resolution time', async () => {
    const root = await render(dashboard());

    expect(stats(root)).toEqual([
      'Open tickets 13 of 24 logged',
      'Needs triage 4 open, nobody assigned',
      'Waiting for requester 3 6 resolved, awaiting confirmation',
      'Critical open 1 needs attention first',
      'Avg. resolution (30 days) 14.2 h median 5.0 h · 9 tickets',
    ]);
  });

  it('scales bars to the largest value and prints every count', async () => {
    const root = await render(dashboard());

    const rows = [...root.querySelectorAll('[aria-labelledby=by-status-title] .bar-row')];
    expect(rows.map((r) => r.querySelector('.bar-value')?.textContent)).toEqual(['5', '5', '3', '6', '5']);
    expect((rows[3].querySelector('.bar') as HTMLElement).style.width).toBe('100%'); // Resolved is the largest
    expect(rows[2].getAttribute('title')).toBe('Waiting for info: 3 tickets (13% of all tickets)');
  });

  it('withholds the average and explains why when too few tickets were resolved', async () => {
    const root = await render(
      dashboard({ resolutionTime: { windowDays: 30, sampleSize: 1, minimumSampleSize: 3, averageHours: null, medianHours: null } }),
    );

    expect(stats(root)[4]).toBe('Avg. resolution (30 days) – Not enough data yet: needs 3 resolved tickets, 1 so far');
  });

  it('shows an empty state instead of zeros everywhere when there are no tickets', async () => {
    const empty = dashboard({
      totals: { open: 0, needsTriage: 0, waitingForRequester: 0, criticalOpen: 0, awaitingConfirmation: 0, total: 0 },
    });

    const root = await render(empty);

    expect(root.querySelector('app-empty-state')?.textContent).toContain('No tickets yet');
    expect(root.querySelector('.stats')).toBeNull();
  });
});
