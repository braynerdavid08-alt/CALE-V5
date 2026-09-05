import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionStore } from '../auth/session.store';
import { stashReturnUrl } from '../auth/return-url';

export const authGuard: CanActivateFn = (_route, state) => {
  const session = inject(SessionStore);
  const router = inject(Router);
  if (!session.isAuthenticated()) {
    stashReturnUrl(state.url);
    return router.createUrlTree(['/login'], {
      queryParams: { returnUrl: state.url }
    });
  }

  const mustChange = !!session.user()?.mustChangePassword;
  const onProfile = state.url.split('?')[0] === '/profile';
  if (mustChange && !onProfile) {
    stashReturnUrl(state.url);
    return router.createUrlTree(['/profile'], {
      queryParams: { mustChange: 1 }
    });
  }

  return true;
};
