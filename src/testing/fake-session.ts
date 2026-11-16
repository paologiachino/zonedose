import { computed, Provider, signal } from '@angular/core';

import { SessionService } from '../app/core/session/session.service';
import { User } from '../app/core/session/user';

/** Synthetic users for tests (example.com addresses only). */
export const USERS = {
  active: {
    id: '00000000-0000-4000-8000-000000000001',
    email: 'ada@example.com',
    display_name: 'Ada Example',
    status: 'active',
    role: 'user',
  },
  admin: {
    id: '00000000-0000-4000-8000-000000000002',
    email: 'admin@example.com',
    display_name: 'Admin Example',
    status: 'active',
    role: 'admin',
  },
  pending: {
    id: '00000000-0000-4000-8000-000000000003',
    email: 'new@example.com',
    display_name: null,
    status: 'pending',
    role: 'user',
  },
  disabled: {
    id: '00000000-0000-4000-8000-000000000004',
    email: 'gone@example.com',
    display_name: 'Gone Example',
    status: 'disabled',
    role: 'user',
  },
} as const satisfies Record<string, User>;

/** A SessionService stand-in with a chosen user and no backend. */
export class FakeSession {
  readonly user = signal<User | null>(null);
  readonly isActive = computed(() => this.user()?.status === 'active');
  readonly isAdmin = computed(() => this.isActive() && this.user()?.role === 'admin');
  logoutCalls = 0;

  async logout(): Promise<void> {
    this.logoutCalls++;
    this.user.set(null);
  }

  clear(): void {
    this.user.set(null);
  }
}

export function provideFakeSession(user: User | null): [Provider, FakeSession] {
  const fake = new FakeSession();
  fake.user.set(user);
  return [{ provide: SessionService, useValue: fake }, fake];
}
