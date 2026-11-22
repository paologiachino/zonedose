import { Component, inject, signal } from '@angular/core';

import { SessionService } from '../core/session/session.service';

/** "Esci" button, used in the header and on the waiting pages. */
@Component({
  selector: 'app-logout-button',
  template: `
    <button
      type="button"
      class="rounded-md border border-slate-300 px-3 py-1.5 text-sm hover:bg-slate-100 disabled:opacity-50"
      [disabled]="busy()"
      (click)="logout()"
    >
      Esci
    </button>
  `,
})
export class LogoutButton {
  private readonly session = inject(SessionService);
  protected readonly busy = signal(false);

  protected async logout(): Promise<void> {
    this.busy.set(true);
    await this.session.logout();
    this.busy.set(false);
  }
}
