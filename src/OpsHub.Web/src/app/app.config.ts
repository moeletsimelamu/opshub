import { provideHttpClient, withInterceptors, withXsrfConfiguration } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';

import { routes } from './app.routes';
import { SessionService } from './core/auth/session.service';
import { sessionInterceptor } from './core/auth/session.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      withComponentInputBinding(), // route params (e.g. :id) arrive as component inputs
      withInMemoryScrolling({ scrollPositionRestoration: 'enabled' }),
    ),
    provideHttpClient(
      // Angular copies the API's XSRF-TOKEN cookie into this header on POST/PUT/DELETE.
      // These are Angular's defaults; spelled out so the contract with the API is visible.
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
      withInterceptors([sessionInterceptor]),
    ),
    // Find out who is signed in before the first screen renders.
    provideAppInitializer(() => inject(SessionService).initialise()),
  ],
};
