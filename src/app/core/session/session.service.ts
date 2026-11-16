import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { User } from './user';

/**
 * Who is signed in, for the whole application.
 *
 * The session itself lives in HttpOnly cookies that JavaScript cannot read: the
 * only way to know the user is to ask the backend (GET /api/auth/me). The answer
 * is kept in a signal, so every template that shows it updates by itself.
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly http = inject(HttpClient);
  private readonly current = signal<User | null>(null);

  /** The signed-in user, or null when nobody is signed in. */
  readonly user = this.current.asReadonly();
  readonly isActive = computed(() => this.current()?.status === 'active');
  readonly isAdmin = computed(() => this.isActive() && this.current()?.role === 'admin');

  /**
   * Asks the backend who is signed in; called once when the application starts.
   * Any failure (not signed in, session ended, backend unreachable) leaves the
   * user signed out: the backend checks every request anyway.
   */
  async load(): Promise<void> {
    try {
      this.current.set(await firstValueFrom(this.http.get<User>('/api/auth/me')));
    } catch {
      this.current.set(null);
    }
  }

  /** Ends the session on the backend; the user is signed out here in any case. */
  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/api/auth/logout', null));
    } finally {
      this.current.set(null);
    }
  }

  /** Forgets the user locally, when the backend says the session is over. */
  clear(): void {
    this.current.set(null);
  }
}
