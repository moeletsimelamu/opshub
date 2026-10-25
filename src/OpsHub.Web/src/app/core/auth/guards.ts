import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { Role } from '../api/models';
import { SessionService } from './session.service';

/**
 * Route guards decide what the UI navigates to. They are a convenience, not security:
 * the API enforces every permission again on every request.
 */

/** Signed-in users only; others go to the login page and come back afterwards. */
export const signedInGuard: CanActivateFn = (_route, state) => {
  const session = inject(SessionService);
  return session.isSignedIn()
    ? true
    : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/** The login page is pointless for someone already signed in. */
export const signedOutGuard: CanActivateFn = () => {
  const session = inject(SessionService);
  return session.isSignedIn() ? inject(Router).parseUrl(session.homeUrl()) : true;
};

/** Restricts a route to some roles, e.g. { roles: ['Administrator'] } in route data. */
export const roleGuard: CanActivateFn = (route) => {
  const session = inject(SessionService);
  const allowed = (route.data['roles'] as Role[] | undefined) ?? [];
  const role = session.user()?.role;
  return role && allowed.includes(role) ? true : inject(Router).parseUrl(session.homeUrl());
};
