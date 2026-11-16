import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';

import { SessionService } from '../../core/session/session.service';
import { LogoutButton } from '../../shared/logout-button';

/** Frame of the signed-in site: header with the user's name and logout, then the page. */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, LogoutButton],
  templateUrl: './shell.html',
})
export class Shell {
  private readonly session = inject(SessionService);
  protected readonly isAdmin = this.session.isAdmin;
  protected readonly name = computed(() => {
    const user = this.session.user();
    return user?.display_name || user?.email;
  });
}
