import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { SessionStore } from '../auth/session.store';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const session = inject(SessionStore);
  const token = session.token();
  const cloned = req.clone({ withCredentials: true });

  if (token) {
    return next(cloned.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    }));
  }

  return next(cloned);
};
