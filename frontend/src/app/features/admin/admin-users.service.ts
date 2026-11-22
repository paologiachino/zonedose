import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { User, UserRole, UserStatus } from '../../core/session/user';

/** A user as administrators see it (UserAdminOut in the backend). */
export interface AdminUser extends User {
  created_at: string;
  last_login_at: string | null;
  approved_at: string | null;
  approved_by: string | null;
}

const BASE = '/api/admin/users';

/** Calls to the user administration API (backend/app/api/admin.py). */
@Injectable({ providedIn: 'root' })
export class AdminUsersService {
  private readonly http = inject(HttpClient);

  list(status?: UserStatus): Promise<AdminUser[]> {
    const params: Record<string, string> = status ? { status } : {};
    return firstValueFrom(this.http.get<AdminUser[]>(BASE, { params }));
  }

  approve(id: string): Promise<AdminUser> {
    return firstValueFrom(this.http.post<AdminUser>(`${BASE}/${id}/approve`, null));
  }

  disable(id: string): Promise<AdminUser> {
    return firstValueFrom(this.http.post<AdminUser>(`${BASE}/${id}/disable`, null));
  }

  setRole(id: string, role: UserRole): Promise<AdminUser> {
    return firstValueFrom(this.http.post<AdminUser>(`${BASE}/${id}/role`, { role }));
  }
}

/**
 * The backend's refusal as a sentence in Italian. The backend answers in English
 * for developers; the cases an administrator can meet are translated here, and
 * anything else falls back to a generic message.
 */
export function describeAdminError(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'Operazione non riuscita. Riprova.';
  }
  const detail = typeof error.error?.detail === 'string' ? error.error.detail : '';
  switch (error.status) {
    case 409:
      if (detail.includes('last active administrator')) {
        return 'Non puoi rimuovere l’ultimo amministratore attivo.';
      }
      if (detail.includes('only active users')) {
        return 'Solo un utente attivo può diventare amministratore.';
      }
      return 'L’operazione non è compatibile con lo stato attuale dell’utente. Ricarica l’elenco.';
    case 404:
      return 'Utente non trovato. Ricarica l’elenco.';
    case 403:
      return 'Non hai più i permessi di amministratore.';
    case 0:
      return 'Il server non risponde. Riprova tra poco.';
    default:
      return 'Operazione non riuscita. Riprova.';
  }
}
