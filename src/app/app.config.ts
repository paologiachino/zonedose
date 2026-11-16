import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { csrfInterceptor } from './core/http/csrf.interceptor';
import { refreshInterceptor } from './core/http/refresh.interceptor';
import { SessionService } from './core/session/session.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // The first interceptor is the outermost: requests repeated after a refresh
    // still pass through the CSRF one
    provideHttpClient(withInterceptors([refreshInterceptor, csrfInterceptor])),
    // The first page is drawn only after we know who is signed in
    provideAppInitializer(() => inject(SessionService).load()),
  ],
};
