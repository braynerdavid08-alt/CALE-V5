import { ApplicationConfig, APP_INITIALIZER, inject, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { SessionStore } from './core/auth/session.store';
import { authInterceptor } from './core/http/auth.interceptor';
import { observabilityInterceptor } from './core/http/observability.interceptor';
import { unauthorizedInterceptor } from './core/http/unauthorized.interceptor';
import { wireInterceptor } from './core/http/wire.interceptor';
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
      withInterceptors([observabilityInterceptor, authInterceptor, unauthorizedInterceptor, wireInterceptor])
    )
  ]
};

/** Register PWA service worker for installable home-screen app. */
export function registerPwaServiceWorker(): void {
  if (typeof window === 'undefined' || !('serviceWorker' in navigator)) {
    return;
  }

  // When a new SW takes control, reload once so the desktop app is not stuck on old JS.
  let refreshing = false;
  navigator.serviceWorker.addEventListener('controllerchange', () => {
    if (refreshing) {
      return;
    }
    refreshing = true;
    window.location.reload();
  });

  window.addEventListener('load', () => {
    navigator.serviceWorker
      .register('/sw.js', { updateViaCache: 'none' })
      .then((reg) => {
        const poke = (): void => {
          void reg.update();
        };
        // Installed PWAs often stay open; check for deploys when the user returns.
        document.addEventListener('visibilitychange', () => {
          if (document.visibilityState === 'visible') {
            poke();
          }
        });
        window.addEventListener('focus', poke);
        // Fallback poll (desktop app may sit on one route for a long time).
        window.setInterval(poke, 5 * 60 * 1000);

        reg.addEventListener('updatefound', () => {
          const installing = reg.installing;
          if (!installing) {
            return;
          }
          installing.addEventListener('statechange', () => {
            if (installing.state === 'installed' && navigator.serviceWorker.controller) {
              // New version downloaded; activate immediately (sw.js already skipWaiting).
              installing.postMessage({ type: 'SKIP_WAITING' });
            }
          });
        });
      })
      .catch(() => undefined);
  });
}
