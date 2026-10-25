import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { SessionService } from './session.service';

/**
 * If any API call answers 401 (session expired, user deactivated, signed out in another
 * tab), clear the local user and send them to the login page. The login and "who am I"
 * calls are excluded: a 401 there is an expected answer, not an expired session.
 */
export const sessionInterceptor: HttpInterceptorFn = (request, next) => {
  const session = inject(SessionService);

  return next(request).pipe(
    catchError((error: unknown) => {
      const isAuthCall = request.url.startsWith('/api/auth/login') || request.url.startsWith('/api/auth/me');
      if (error instanceof HttpErrorResponse && error.status === 401 && !isAuthCall) {
        void session.handleSessionExpired();
      }
      return throwError(() => error);
    }),
  );
};
