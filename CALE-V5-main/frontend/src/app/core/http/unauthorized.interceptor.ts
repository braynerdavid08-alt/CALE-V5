import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthApi } from '../../features/auth/api/auth.api';
import { SessionStore } from '../auth/session.store';

let refreshInFlight: ReturnType<AuthApi['refresh']> | null = null;

/**
 * On 401, try cookie refresh once before clearing session and redirecting to login.
 */
export const unauthorizedInterceptor: HttpInterceptorFn = (req, next) => {
  const session = inject(SessionStore);
  const router = inject(Router);
  const authApi = inject(AuthApi);

  return next(req).pipe(
    catchError((err: unknown) => {
      if (!(err instanceof HttpErrorResponse) || err.status !== 401) {
        return throwError(() => err);
      }

      const path = req.url.toLowerCase();
      const isAuthBootstrap =
        path.includes('/api/auth/login')
        || path.includes('/api/auth/register')
        || path.includes('/api/auth/confirm')
        || path.includes('/api/auth/resend')
        || path.includes('/api/auth/refresh');

      if (isAuthBootstrap || !session.user()) {
        return throwError(() => err);
      }

      if (!session.cookieAuth()) {
        const here = router.url;
        session.clear();
        if (here && here !== '/login') {
          try {
            sessionStorage.setItem('cale.auth.returnUrl', here);
          } catch { /* ignore */ }
        }
        void router.navigate(['/login'], {
          replaceUrl: true,
          queryParams: here && here.startsWith('/') && here !== '/login'
            ? { returnUrl: here }
            : undefined,
          state: { reason: 'session_expired' }
        });
        return throwError(() => err);
      }

      if (!refreshInFlight) {
        refreshInFlight = authApi.refresh();
      }

      return refreshInFlight.pipe(
        switchMap((res) => {
          refreshInFlight = null;
          session.set(res);
          return next(req.clone({ withCredentials: true }));
        }),
        catchError((refreshErr) => {
          refreshInFlight = null;
          const here = router.url;
          session.clear();
          if (here && here !== '/login') {
            try {
              sessionStorage.setItem('cale.auth.returnUrl', here);
            } catch { /* ignore */ }
          }
          void router.navigate(['/login'], {
            replaceUrl: true,
            queryParams: here && here.startsWith('/') && here !== '/login'
              ? { returnUrl: here }
              : undefined,
            state: { reason: 'session_expired' }
          });
          return throwError(() => refreshErr);
        })
      );
    })
  );
};
