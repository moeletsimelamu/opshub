import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';

import { Role } from '../api/models';
import { roleGuard, signedInGuard } from './guards';
import { SessionService } from './session.service';

/** A stand-in SessionService with just what the guards read. */
function session(role: Role | null) {
  return {
    isSignedIn: () => role !== null,
    user: () => (role ? { id: '1', email: 'x@marula.example', displayName: 'X', role } : null),
    homeUrl: () => (role === 'Requester' ? '/tickets' : '/dashboard'),
  };
}

function setUp(role: Role | null): void {
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: SessionService, useValue: session(role) }],
  });
}

function runRoleGuard(roles: Role[]): boolean | UrlTree {
  const route = { data: { roles } } as unknown as ActivatedRouteSnapshot;
  return TestBed.runInInjectionContext(() => roleGuard(route, {} as RouterStateSnapshot)) as boolean | UrlTree;
}

describe('roleGuard', () => {
  it('lets an administrator into an admin route', () => {
    setUp('Administrator');

    expect(runRoleGuard(['Administrator'])).toBe(true);
  });

  it('sends a requester away from a staff route to their home page', () => {
    setUp('Requester');

    const result = runRoleGuard(['Administrator', 'SupportAgent']);

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/tickets');
  });
});

describe('signedInGuard', () => {
  it('redirects a signed-out visitor to the login page with a returnUrl', () => {
    setUp(null);

    const result = TestBed.runInInjectionContext(() =>
      signedInGuard({} as ActivatedRouteSnapshot, { url: '/tickets/7' } as RouterStateSnapshot),
    ) as UrlTree;

    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/login?returnUrl=%2Ftickets%2F7');
  });

  it('lets a signed-in user through', () => {
    setUp('Requester');

    expect(TestBed.runInInjectionContext(() => signedInGuard({} as ActivatedRouteSnapshot, { url: '/' } as RouterStateSnapshot))).toBe(true);
  });
});
