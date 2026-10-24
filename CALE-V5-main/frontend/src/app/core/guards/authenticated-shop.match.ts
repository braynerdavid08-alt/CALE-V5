import { inject } from '@angular/core';
import { CanMatchFn, Route, UrlSegment } from '@angular/router';
import { SessionStore } from '../auth/session.store';

/**
 * When the user is logged in, `/tienda` (and aliases) must use AppShell
 * so they stay inside their account chrome instead of the public marketing shell.
 */
export const authenticatedShopMatch: CanMatchFn = (_route: Route, segments: UrlSegment[]) => {
  const session = inject(SessionStore);
  if (!session.isAuthenticated()) {
    return false;
  }

  const head = segments[0]?.path ?? '';
  return head === 'tienda' || head === 'cascos';
};
