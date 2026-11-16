import { Component, computed, input } from '@angular/core';

/** Messages for the error codes the backend puts in /login?error=... */
const ERROR_MESSAGES: Record<string, string> = {
  cancelled: 'Hai annullato l’accesso con Google.',
  failed: 'Accesso non riuscito. Riprova.',
  unverified_email: 'L’indirizzo email del tuo account Google non è verificato.',
  unavailable: 'L’accesso non è disponibile in questo momento. Riprova più tardi.',
};

/** Sign-in page: one button that starts the Google sign-in. */
@Component({
  selector: 'app-login',
  templateUrl: './login.html',
})
export class Login {
  /** The `error` query parameter, bound by the router (withComponentInputBinding). */
  readonly error = input<string>();

  protected readonly errorMessage = computed(() => {
    const code = this.error();
    return code ? (ERROR_MESSAGES[code] ?? ERROR_MESSAGES['failed']) : null;
  });
}
