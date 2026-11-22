import { Component, computed, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

import { SessionService } from '../../core/session/session.service';
import { LogoutButton } from '../../shared/logout-button';

const MESSAGES = {
  pending: {
    title: 'Account in attesa di approvazione',
    text: 'Un amministratore deve approvare il tuo account prima che tu possa usare il sito. Riprova più tardi.',
  },
  disabled: {
    title: 'Account disattivato',
    text: 'Il tuo account è stato disattivato. Per riattivarlo rivolgiti a un amministratore.',
  },
} as const;

/** Page for a signed-in account that cannot use the site yet (pending) or anymore (disabled). */
@Component({
  selector: 'app-account-status',
  imports: [LogoutButton],
  templateUrl: './account-status.html',
})
export class AccountStatus {
  protected readonly session = inject(SessionService);
  private readonly status: keyof typeof MESSAGES = inject(ActivatedRoute).snapshot.data['status'];
  protected readonly message = MESSAGES[this.status];
  protected readonly email = computed(() => this.session.user()?.email);
}
