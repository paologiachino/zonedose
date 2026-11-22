import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';

import { SessionService } from './session.service';
import { User, UserStatus } from './user';

/*
 * Route guards: functions the router runs before opening a page, which may send
 * the browser elsewhere. They only shape the interface: the backend checks every
 * request anyway (401, 403), so a modified frontend gains nothing.
 */

/** Where a user belongs: the login page, the waiting pages or the site. */
export function landingPath(user: User | null): string {
  if (user === null) {
    return '/login';
  }
  switch (user.status) {
    case 'pending':
      return '/pending';
    case 'disabled':
      return '/disabled';
    case 'active':
      return '/';
  }
}

function redirectToLanding(): UrlTree {
  return inject(Router).parseUrl(landingPath(inject(SessionService).user()));
}

/** The login page: only for people who are not signed in. */
export const signedOutGuard: CanActivateFn = () =>
  inject(SessionService).user() === null || redirectToLanding();

/** The page for one account status (pending, disabled). */
export function statusGuard(status: Exclude<UserStatus, 'active'>): CanActivateFn {
  return () => inject(SessionService).user()?.status === status || redirectToLanding();
}

/** The site itself: approved users only. */
export const activeGuard: CanActivateFn = () =>
  inject(SessionService).isActive() || redirectToLanding();

/** Administration pages: active administrators only. */
export const adminGuard: CanActivateFn = () => {
  const session = inject(SessionService);
  if (session.isAdmin()) {
    return true;
  }
  return session.isActive() ? inject(Router).parseUrl('/') : redirectToLanding();
};
