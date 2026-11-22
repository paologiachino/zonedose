import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { Confirm } from '../../core/confirm';
import { landingPath } from '../../core/session/guards';
import { SessionService } from '../../core/session/session.service';
import { UserStatus } from '../../core/session/user';
import { AdminUser, AdminUsersService, describeAdminError } from './admin-users.service';

const FILTERS: { status: UserStatus | undefined; label: string }[] = [
  { status: 'pending', label: 'In attesa' },
  { status: 'active', label: 'Attivi' },
  { status: 'disabled', label: 'Disattivati' },
  { status: undefined, label: 'Tutti' },
];

export const STATUS_LABELS: Record<UserStatus, string> = {
  pending: 'In attesa',
  active: 'Attivo',
  disabled: 'Disattivato',
};

/** User administration: list with a status filter, and approve / disable / role actions. */
@Component({
  selector: 'app-admin-users',
  imports: [DatePipe, RouterLink],
  templateUrl: './admin-users.html',
})
export class AdminUsers {
  private readonly api = inject(AdminUsersService);
  private readonly session = inject(SessionService);
  private readonly confirm = inject(Confirm);
  private readonly router = inject(Router);

  /** `?status=` in the address, bound by the router: the filter can be bookmarked. */
  readonly status = input<string>();

  protected readonly filters = FILTERS;
  protected readonly statusLabels = STATUS_LABELS;
  protected readonly users = signal<AdminUser[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  /** Id of the user whose action is running: their buttons are disabled meanwhile. */
  protected readonly busyId = signal<string | null>(null);
  protected readonly myId = computed(() => this.session.user()?.id);

  protected readonly currentFilter = computed(() => {
    const status = this.status();
    return FILTERS.some((f) => f.status === status) ? (status as UserStatus) : undefined;
  });

  constructor() {
    // Reloads the list whenever the filter in the address changes
    effect(() => void this.load(this.currentFilter()));
  }

  protected displayName(user: AdminUser): string {
    return user.display_name || user.email;
  }

  protected approve(user: AdminUser): Promise<void> {
    return this.run(user, () => this.api.approve(user.id));
  }

  protected disable(user: AdminUser): Promise<void> {
    const self = user.id === this.myId();
    const question = self
      ? 'Vuoi disattivare il tuo account? Uscirai subito dal sito.'
      : `Disattivare ${this.displayName(user)}? Le sue sessioni verranno chiuse.`;
    if (!this.confirm.ask(question)) {
      return Promise.resolve();
    }
    return this.run(user, () => this.api.disable(user.id));
  }

  protected makeAdmin(user: AdminUser): Promise<void> {
    return this.run(user, () => this.api.setRole(user.id, 'admin'));
  }

  protected removeAdmin(user: AdminUser): Promise<void> {
    const self = user.id === this.myId();
    const question = self
      ? 'Vuoi rinunciare al ruolo di amministratore? Non potrai più gestire gli utenti.'
      : `Togliere il ruolo di amministratore a ${this.displayName(user)}?`;
    if (!this.confirm.ask(question)) {
      return Promise.resolve();
    }
    return this.run(user, () => this.api.setRole(user.id, 'user'));
  }

  private async load(status: UserStatus | undefined): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.users.set(await this.api.list(status));
    } catch (error) {
      this.error.set(describeAdminError(error));
    } finally {
      this.loading.set(false);
    }
  }

  /** Runs one action and puts the changed user back in the list, or shows why it failed. */
  private async run(user: AdminUser, action: () => Promise<AdminUser>): Promise<void> {
    this.busyId.set(user.id);
    this.error.set(null);
    try {
      const changed = await action();
      if (changed.id === this.myId()) {
        // My own role or status changed: the session decides where I can stay
        await this.session.load();
        if (!this.session.isAdmin()) {
          await this.router.navigateByUrl(landingPath(this.session.user()));
          return;
        }
      }
      const filter = this.currentFilter();
      this.users.update((list) =>
        list
          // A user who no longer matches the filter leaves the list
          .filter((u) => u.id !== changed.id || !filter || changed.status === filter)
          .map((u) => (u.id === changed.id ? changed : u)),
      );
    } catch (error) {
      this.error.set(describeAdminError(error));
    } finally {
      this.busyId.set(null);
    }
  }
}
