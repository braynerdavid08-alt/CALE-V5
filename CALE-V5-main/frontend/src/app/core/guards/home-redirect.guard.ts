import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionStore } from '../auth/session.store';

export const homeRedirectGuard: CanActivateFn = () => {
  const session = inject(SessionStore);
  const router = inject(Router);
  if (!session.isAuthenticated()) {
    return router.parseUrl('/login');
  }
  return router.parseUrl(session.homeRoute());
};
