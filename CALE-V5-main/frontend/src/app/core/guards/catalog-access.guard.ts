import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { SessionStore } from '../auth/session.store';

/** Admin catalog or school/teacher with active paid plan. */
export const catalogAccessGuard: CanActivateFn = () => {
  const session = inject(SessionStore);
  const router = inject(Router);
  const user = session.user();

  if (!session.isAuthenticated() || !user) {
    return router.parseUrl('/login');
  }
  if (session.hasCatalogAccess()) {
    return true;
  }
  if (user.role === 'School') {
    return router.parseUrl('/school/membership');
  }
  if (user.role === 'Teacher') {
    return router.parseUrl('/teacher');
  }
  return router.parseUrl(session.homeRoute());
};

/** Students/teachers linked to a school with active plan (admin bypass). */
export const simulacroAccessGuard: CanActivateFn = () => {
  const session = inject(SessionStore);
  const router = inject(Router);
  const user = session.user();

  if (!session.isAuthenticated() || !user) {
    return router.parseUrl('/login');
  }
  if (session.hasSimulacroAccess()) {
    return true;
  }
  if (user.role === 'School') {
    return router.parseUrl('/school/membership');
  }
  if (user.role === 'Student' || user.role === 'Teacher') {
    return router.parseUrl('/profile');
  }
  return router.parseUrl(session.homeRoute());
};
