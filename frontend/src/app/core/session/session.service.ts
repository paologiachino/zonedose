import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
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
  private readonly router = inject(Router);
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

  /**
   * Ends the session on the backend and goes to the login page. The user is
   * signed out here even if the backend cannot be reached.
   */
  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/api/auth/logout', null));
    } catch {
      // Nothing more to do: the page forgets the user anyway
    }
    this.current.set(null);
    await this.router.navigateByUrl('/login');
  }

  /**
   * Forgets the user, e.g. when the session ended. Only someone who was signed
   * in is sent to the login page: for a visitor who never was, the route guards
   * already decide, and a redirect here would drop /login?error=... at startup.
   */
  clear(): void {
    if (this.current() === null) {
      return;
    }
    this.current.set(null);
    void this.router.navigateByUrl('/login');
  }
}
