import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { registerLocaleData } from '@angular/common';
import localeIt from '@angular/common/locales/it';
import {
  ApplicationConfig,
  inject,
  LOCALE_ID,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';

import { routes } from './app.routes';
import { csrfInterceptor } from './core/http/csrf.interceptor';
import { refreshInterceptor } from './core/http/refresh.interceptor';
import { SessionService } from './core/session/session.service';

// Dates and numbers formatted the Italian way (DatePipe, DecimalPipe)
registerLocaleData(localeIt);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    { provide: LOCALE_ID, useValue: 'it' },
    // Component inputs receive route parameters, e.g. ?error= on the login page
    provideRouter(routes, withComponentInputBinding()),
    // The first interceptor is the outermost: requests repeated after a refresh
    // still pass through the CSRF one
    provideHttpClient(withInterceptors([refreshInterceptor, csrfInterceptor])),
    // The first page is drawn only after we know who is signed in
    provideAppInitializer(() => inject(SessionService).load()),
  ],
};
