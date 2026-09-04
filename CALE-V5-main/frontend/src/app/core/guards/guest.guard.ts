import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionStore } from '../auth/session.store';
import { isSafeReturnUrl, peekReturnUrl, takeReturnUrl } from '../auth/return-url';

export const guestGuard: CanActivateFn = (route) => {
  const session = inject(SessionStore);
  if (!session.isAuthenticated()) {
    return true;
  }

  const fromQuery = route.queryParamMap.get('returnUrl');
  const candidate = isSafeReturnUrl(fromQuery) ? fromQuery : peekReturnUrl();
  if (isSafeReturnUrl(candidate)) {
    takeReturnUrl(candidate);
    return inject(Router).parseUrl(candidate);
  }

  return inject(Router).parseUrl(session.homeRoute());
};
