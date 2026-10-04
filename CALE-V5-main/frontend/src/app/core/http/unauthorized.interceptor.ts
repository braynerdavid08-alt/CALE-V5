import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { isSafeReturnUrl, stashReturnUrl } from '../auth/return-url';
import { isAuthRejection, SessionStore } from '../auth/session.store';

/**
 * On 401, try cookie refresh once before clearing session and redirecting to login.
 */
export const unauthorizedInterceptor: HttpInterceptorFn = (req, next) => {
  const session = inject(SessionStore);
  const router = inject(Router);

  const sendToLogin = (here: string) => {
    const safe = isSafeReturnUrl(here) ? here : null;
    if (safe) {
      stashReturnUrl(safe);
    }
    void router.navigate(['/login'], {
      replaceUrl: true,
      queryParams: safe ? { returnUrl: safe } : undefined,
      state: { reason: 'session_expired' }
    });
  };

  return next(req).pipe(
    catchError((err: unknown) => {
      if (
        err instanceof HttpErrorResponse
        && err.status === 403
        && err.error?.detail === 'password_change_required'
      ) {
        session.patchUser({ mustChangePassword: true });
        const here = router.url.split('?')[0];
        if (here !== '/profile') {
          void router.navigate(['/profile'], {
            queryParams: { mustChange: 1 },
            replaceUrl: true
          });
        }
        return throwError(() => err);
      }

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

      // The game show works without login: a stale session must not kick a student out mid-game.
      const inGameShow = /^\/game-show\/(play|screen)\//.test(router.url);
      if (inGameShow && !session.cookieAuth()) {
        return throwError(() => err);
      }

      if (!session.cookieAuth()) {
        const here = router.url;
        session.clear();
        sendToLogin(here);
        return throwError(() => err);
      }

      return session.refreshSession().pipe(
        switchMap(() => next(req.clone({ withCredentials: true }))),
        catchError((refreshErr) => {
          if (!isAuthRejection(refreshErr)) {
            return throwError(() => refreshErr);
          }
          const here = router.url;
          session.clear();
          if (!inGameShow) sendToLogin(here);
          return throwError(() => refreshErr);
        })
      );
    })
  );
};
