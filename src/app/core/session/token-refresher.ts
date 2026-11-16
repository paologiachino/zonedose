import { HttpClient } from '@angular/common/http';
import { inject, Injectable, InjectionToken } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { RefreshLock } from './refresh-lock';

export const REFRESH_URL = '/api/auth/refresh';

/** Current time in milliseconds; replaced in tests. */
export const CLOCK = new InjectionToken<() => number>('CLOCK', {
  providedIn: 'root',
  factory: () => () => Date.now(),
});

/** localStorage key with the time of the last successful refresh, shared by the tabs. */
export const LAST_REFRESH_KEY = 'sportlab.lastRefreshAt';

/**
 * Renews the session cookies when the access token has expired.
 *
 * `renewAfter(sentAt)` is called by a request that was sent at `sentAt` and got
 * 401. Inside the lock it first checks whether another request, in this tab or
 * in another one, has already refreshed after `sentAt`: the cookies are shared
 * by every tab, so the request only has to be repeated. Otherwise it calls the
 * backend. A refresh run twice in a row is harmless (each uses the newest
 * cookie); only two refreshes at the same moment are not, and the lock rules
 * them out.
 */
@Injectable({ providedIn: 'root' })
export class TokenRefresher {
  private readonly http = inject(HttpClient);
  private readonly lock = inject(RefreshLock);
  private readonly now = inject(CLOCK);

  /** True when the session cookies are fresh, false when the session is over. */
  renewAfter(sentAt: number): Promise<boolean> {
    return this.lock.runExclusive(async () => {
      if (readLastRefresh() > sentAt) {
        return true;
      }
      try {
        await firstValueFrom(this.http.post(REFRESH_URL, null));
      } catch {
        // 401 (session ended or expired) or backend unreachable: signed out
        return false;
      }
      writeLastRefresh(this.now());
      return true;
    });
  }
}

// localStorage can be unavailable (private mode, blocked storage): without it
// tabs only lose the shortcut and refresh once more, which is still safe.
function readLastRefresh(): number {
  try {
    return Number(globalThis.localStorage?.getItem(LAST_REFRESH_KEY) ?? 0) || 0;
  } catch {
    return 0;
  }
}

function writeLastRefresh(time: number): void {
  try {
    globalThis.localStorage?.setItem(LAST_REFRESH_KEY, String(time));
  } catch {
    // see readLastRefresh
  }
}
