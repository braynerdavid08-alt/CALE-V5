import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionStore } from '../auth/session.store';
import { stashReturnUrl } from '../auth/return-url';

export const authGuard: CanActivateFn = (_route, state) => {
  const session = inject(SessionStore);
  if (session.isAuthenticated()) {
    return true;
  }
  stashReturnUrl(state.url);
  return inject(Router).createUrlTree(['/login'], {
    queryParams: { returnUrl: state.url }
  });
};
