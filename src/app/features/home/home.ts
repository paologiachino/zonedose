import { Component, computed, inject } from '@angular/core';

import { SessionService } from '../../core/session/session.service';

/** Placeholder home page, replaced when there is data to show. */
@Component({
  selector: 'app-home',
  templateUrl: './home.html',
})
export class Home {
  private readonly session = inject(SessionService);
  protected readonly firstName = computed(() => {
    const user = this.session.user();
    return user?.display_name?.split(' ')[0] ?? null;
  });
}
