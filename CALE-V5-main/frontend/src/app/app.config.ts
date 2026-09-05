import { ApplicationConfig, APP_INITIALIZER, inject, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { SessionStore } from './core/auth/session.store';
import { authInterceptor } from './core/http/auth.interceptor';
import { observabilityInterceptor } from './core/http/observability.interceptor';
import { unauthorizedInterceptor } from './core/http/unauthorized.interceptor';
import { routes } from './app.routes';

function initSession() {
  const session = inject(SessionStore);
  return () => session.bootstrap();
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    {
      provide: APP_INITIALIZER,
      useFactory: initSession,
      multi: true
    },
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([observabilityInterceptor, authInterceptor, unauthorizedInterceptor])
    )
  ]
};

/** Register PWA service worker for installable home-screen app. */
export function registerPwaServiceWorker(): void {
  if (typeof window === 'undefined' || !('serviceWorker' in navigator)) {
    return;
  }
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch(() => undefined);
  });
}
