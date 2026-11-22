import { HttpErrorResponse, HttpInterceptorFn, HttpStatusCode } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';

import { SessionService } from '../session/session.service';
import { CLOCK, REFRESH_URL, TokenRefresher } from '../session/token-refresher';
import { isApiUrl } from './csrf.interceptor';

/**
 * Endpoints that must never trigger a refresh: the refresh itself (it would
 * loop), logout (nothing to keep), and the Google redirects (page navigations).
 * /api/auth/me is not here: after a long pause its 401 only means that the
 * access token expired, and the refresh token may still be valid.
 */
const NO_REFRESH = [REFRESH_URL, '/api/auth/logout', '/api/auth/google/'];

/**
 * On 401 from our API, renews the session once and repeats the request.
 * If the renewal fails, the session is over: the signed-in user is cleared and
 * the original 401 reaches the caller.
 */
export const refreshInterceptor: HttpInterceptorFn = (req, next) => {
  if (!isApiUrl(req.url) || NO_REFRESH.some((path) => req.url.startsWith(path))) {
    return next(req);
  }
  const refresher = inject(TokenRefresher);
  const session = inject(SessionService);
  const sentAt = inject(CLOCK)();

  return next(req).pipe(
    catchError((error: unknown) => {
      if (!isUnauthorized(error)) {
        return throwError(() => error);
      }
      return from(refresher.renewAfter(sentAt)).pipe(
        switchMap((renewed) => {
          if (!renewed) {
            session.clear();
            return throwError(() => error);
          }
          // Repeated once: a second 401 is passed on, never refreshed again
          return next(req);
        }),
      );
    }),
  );
};

function isUnauthorized(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === HttpStatusCode.Unauthorized;
}
