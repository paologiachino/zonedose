import { Injectable } from '@angular/core';

const LOCK_NAME = 'sportlab-refresh';

/**
 * Runs one session refresh at a time, across every tab of the site.
 *
 * The refresh token is rotated, and presenting a replaced one ends the session
 * (the backend takes it for a stolen token). Two tabs refreshing at the same
 * moment would do exactly that, so the refresh runs inside a Web Lock: a named
 * lock the browser grants to one tab of the same origin at a time; the others
 * wait in line.
 *
 * Where the Web Locks API is missing (very old browsers, the test environment)
 * a promise chain gives the same guarantee within the tab.
 */
@Injectable({ providedIn: 'root' })
export class RefreshLock {
  private queue: Promise<unknown> = Promise.resolve();

  runExclusive<T>(task: () => Promise<T>): Promise<T> {
    const locks = globalThis.navigator?.locks;
    if (locks) {
      return locks.request(LOCK_NAME, task);
    }
    // Each task starts after the previous one settles, whatever its outcome
    const result = this.queue.then(task, task);
    this.queue = result.catch(() => undefined);
    return result;
  }
}
