import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionStore } from '../auth/session.store';

export const roleGuard: CanActivateFn = (route) => {
  const session = inject(SessionStore);
  const router = inject(Router);
  const allowed = (route.data['roles'] as string[] | undefined) ?? [];
  const role = session.user()?.role;

  if (!session.isAuthenticated()) {
    return router.parseUrl('/login');
  }
  if (allowed.length === 0 || (role && allowed.includes(role))) {
    return true;
  }
  return router.parseUrl(session.homeRoute());
};
