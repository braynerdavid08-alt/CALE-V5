import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { SessionStore } from '../auth/session.store';

/**
 * If the JWT expired or was rejected, clear the local session and send the user
 * to login instead of leaving every page with a generic red banner.
 */
export const unauthorizedInterceptor: HttpInterceptorFn = (req, next) => {
  const session = inject(SessionStore);
  const router = inject(Router);

  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse && err.status === 401) {
        const path = req.url.toLowerCase();
        const isAuthBootstrap =
          path.includes('/api/auth/login')
          || path.includes('/api/auth/register')
          || path.includes('/api/auth/confirm')
          || path.includes('/api/auth/resend');

        if (!isAuthBootstrap && session.isAuthenticated()) {
          session.clear();
          void router.navigateByUrl('/login', {
            replaceUrl: true,
            state: { reason: 'session_expired' }
          });
        }
      }

      return throwError(() => err);
    })
  );
};
