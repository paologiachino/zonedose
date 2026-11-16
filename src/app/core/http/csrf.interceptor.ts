import { HttpInterceptorFn } from '@angular/common/http';

/** Header the backend requires on state-changing requests (backend/app/core/csrf.py). */
export const CSRF_HEADER = 'X-Requested-With';

const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS']);

/**
 * Adds the CSRF header to every state-changing request to our API.
 *
 * A form on another site can make the browser send a POST, but it cannot add a
 * custom header: requiring one is the second barrier after SameSite cookies.
 * Requests to other sites never get it.
 */
export const csrfInterceptor: HttpInterceptorFn = (req, next) => {
  if (SAFE_METHODS.has(req.method) || !isApiUrl(req.url)) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { [CSRF_HEADER]: 'XMLHttpRequest' } }));
};

/** Our API is always called with a relative URL under /api/. */
export function isApiUrl(url: string): boolean {
  return url.startsWith('/api/');
}
