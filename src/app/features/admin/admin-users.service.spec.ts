import { HttpErrorResponse } from '@angular/common/http';

import { describeAdminError } from './admin-users.service';

function httpError(status: number, detail?: string): HttpErrorResponse {
  return new HttpErrorResponse({ status, error: detail ? { detail } : null });
}

describe('describeAdminError', () => {
  it.each([
    [httpError(409, 'the last active administrator cannot be removed'), 'ultimo amministratore'],
    [httpError(409, 'only active users can become administrators'), 'Solo un utente attivo'],
    [httpError(409, 'the user is already active'), 'Ricarica l’elenco'],
    [httpError(404, 'User not found'), 'Utente non trovato'],
    [httpError(403, 'Administrators only'), 'permessi di amministratore'],
    [httpError(0), 'non risponde'],
    [httpError(500), 'Operazione non riuscita'],
    [new Error('anything'), 'Operazione non riuscita'],
  ])('translates %o', (error, text) => {
    expect(describeAdminError(error)).toContain(text);
  });
});
