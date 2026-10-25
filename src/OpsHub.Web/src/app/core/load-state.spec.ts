import { HttpErrorResponse } from '@angular/common/http';
import { Subject, of, throwError, toArray, firstValueFrom, switchMap } from 'rxjs';

import { LoadState, keepPreviousData, toLoadState } from './load-state';

describe('toLoadState', () => {
  it('emits loading, then ready with the data', async () => {
    const states = await firstValueFrom(toLoadState(of([1, 2])).pipe(toArray()));

    expect(states).toEqual([
      { kind: 'loading', data: null },
      { kind: 'ready', data: [1, 2] },
    ]);
  });

  it('emits loading, then error with an ApiError (never throws)', async () => {
    const states = await firstValueFrom(toLoadState(throwError(() => new HttpErrorResponse({ status: 404 }))).pipe(toArray()));

    expect(states[1].kind).toBe('error');
    expect(states[1].kind === 'error' && states[1].error.status).toBe(404);
  });
});

describe('keepPreviousData', () => {
  /** Simulates a screen whose data source is re-requested each time `requests` emits. */
  function run(responses: ('ok' | 'fail')[]): LoadState<string>[] {
    const requests = new Subject<'ok' | 'fail'>();
    const seen: LoadState<string>[] = [];
    requests
      .pipe(
        switchMap((r) => toLoadState(r === 'ok' ? of(`data-${seen.length}`) : throwError(() => new HttpErrorResponse({ status: 500 })))),
        keepPreviousData(),
      )
      .subscribe((s) => seen.push(s));
    responses.forEach((r) => requests.next(r));
    return seen;
  }

  it('keeps the previous data on screen while reloading', () => {
    const states = run(['ok', 'ok']);
    const secondLoading = states[2];

    expect(secondLoading.kind).toBe('loading');
    expect(secondLoading.data).toBe(states[1].data);
  });

  it('drops the previous data when a reload fails, so stale results never look current', () => {
    const states = run(['ok', 'fail']);
    const last = states[states.length - 1];

    expect(last.kind).toBe('error');
    expect(last.data).toBeNull();
  });
});
