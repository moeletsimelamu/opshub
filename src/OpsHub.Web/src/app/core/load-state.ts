import { Observable, catchError, map, of, scan, startWith } from 'rxjs';

import { ApiError } from './api/api-error';

/**
 * The three states every screen that loads data must handle (DESIGN.md principle 5).
 * While reloading, the previous data is kept so the page doesn't flash empty.
 */
export type LoadState<T> =
  | { kind: 'loading'; data: T | null }
  | { kind: 'ready'; data: T }
  | { kind: 'error'; error: ApiError; data: T | null };

/** Turns a request into a stream of LoadState values: loading → ready | error. */
export function toLoadState<T>(request: Observable<T>): Observable<LoadState<T>> {
  return request.pipe(
    map((data): LoadState<T> => ({ kind: 'ready', data })),
    catchError((error: unknown) => of<LoadState<T>>({ kind: 'error', error: ApiError.from(error), data: null })),
    startWith<LoadState<T>>({ kind: 'loading', data: null }),
  );
}

/**
 * Keeps the last successful data on screen while the next request is loading, so the page
 * doesn't flash empty. Deliberately NOT kept after an error: stale results (possibly for a
 * different filter) must never look current, so a failed load shows the error instead.
 */
export function keepPreviousData<T>() {
  return (source: Observable<LoadState<T>>) =>
    source.pipe(
      scan(
        (previous: LoadState<T>, next: LoadState<T>): LoadState<T> =>
          next.kind === 'loading' ? { ...next, data: next.data ?? previous.data } : next,
        { kind: 'loading', data: null } as LoadState<T>,
      ),
    );
}
