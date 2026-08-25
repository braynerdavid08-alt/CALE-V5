import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionStore } from '../auth/session.store';

export const authGuard: CanActivateFn = () => {
  const session = inject(SessionStore);
  if (session.isAuthenticated()) {
    return true;
  }
  return inject(Router).parseUrl('/login');
};
