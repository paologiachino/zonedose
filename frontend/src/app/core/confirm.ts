import { Injectable } from '@angular/core';

/**
 * Asks the person to confirm an action. The browser's own dialog for now: it is
 * accessible and needs no code; behind a service so tests can answer it and a
 * nicer dialog can replace it later without touching the pages.
 */
@Injectable({ providedIn: 'root' })
export class Confirm {
  ask(message: string): boolean {
    return globalThis.confirm(message);
  }
}
