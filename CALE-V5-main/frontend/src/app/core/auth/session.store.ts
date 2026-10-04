import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, finalize, firstValueFrom, shareReplay, tap } from 'rxjs';
import { AuthApi } from '../../features/auth/api/auth.api';
import { AuthResponse, MeSchoolContext, SessionUser } from './session.models';

const STORAGE_KEY = 'cale.session.v5';
/** Renew well before the 60-minute access cookie runs out, so live-room sockets never reconnect with an expired cookie. */
const KEEP_ALIVE_MS = 30 * 60 * 1000;

/** Only the server rejecting the session ends it; offline, timeouts and cold starts (0, 5xx) keep the user signed in. */
export function isAuthRejection(err: unknown): boolean {
  return err instanceof HttpErrorResponse && (err.status === 401 || err.status === 403);
}

@Injectable({ providedIn: 'root' })
export class SessionStore {
  private readonly authApi = inject(AuthApi);

  readonly token = signal<string | null>(null);
  readonly user = signal<SessionUser | null>(null);
  readonly cookieAuth = signal(false);
  readonly isAuthenticated = computed(() => !!this.user());
  readonly hasCatalogAccess = computed(() => this.catalogAccess());
  readonly hasSimulacroAccess = computed(() => this.simulacroAccess());
  /** Modo gratuito activo (por defecto sí hasta que el servidor diga lo contrario). */
  readonly freeAccess = computed(() => this.user()?.freeAccess !== false);

  private readonly catalogAccess = computed(() => {
    const user = this.user();
    if (!user) {
      return false;
    }
    if (user.role === 'Admin') {
      return true;
    }
    if (user.role === 'School' || user.role === 'Teacher') {
      return this.freeAccess() || !!user.isMembershipActive;
    }
    return false;
  });

  private readonly simulacroAccess = computed(() => {
    const user = this.user();
    if (!user) {
      return false;
    }
    if (user.role === 'Admin' || this.freeAccess()) {
      return true;
    }
    if (user.role === 'School') {
      return !!user.isMembershipActive;
    }
    if (user.role === 'Teacher' || user.role === 'Student') {
      return !!user.schoolId && !!user.isMembershipActive;
    }
    return false;
  });

  private refreshInFlight: Observable<AuthResponse> | null = null;
  private keepAliveTimer: ReturnType<typeof setInterval> | null = null;
  private lastRefreshAt = 0;

  constructor() {
    this.restore();
    if (typeof document !== 'undefined') {
      // Timers pause while a phone sleeps; renew as soon as the app is visible again.
      document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') {
          this.renewIfStale();
        }
      });
    }
  }

  async bootstrap(): Promise<void> {
    // Anonymous visitors have no session cookie — do not POST /refresh (avoids noisy 401).
    if (!this.user()) {
      return;
    }

    try {
      // An expired access cookie is renewed by the unauthorized interceptor and the call retried.
      const me = await firstValueFrom(this.authApi.me());
      this.applyMe(me);
    } catch (err) {
      if (isAuthRejection(err)) {
        this.clear();
        return;
      }
    }
    this.startKeepAlive();
  }

  /** One refresh at a time per tab; concurrent callers share it. */
  refreshSession(): Observable<AuthResponse> {
    if (!this.refreshInFlight) {
      this.refreshInFlight = this.authApi.refresh().pipe(
        tap((res) => this.set(res)),
        finalize(() => {
          this.refreshInFlight = null;
        }),
        shareReplay(1)
      );
    }
    return this.refreshInFlight;
  }

  homeRoute(): string {
    const role = this.user()?.role;
    if (role === 'Admin') {
      return '/admin';
    }
    if (role === 'School') {
      return '/school';
    }
    if (role === 'Teacher') {
      return '/teacher';
    }
    return '/student';
  }

  set(response: AuthResponse): void {
    const user: SessionUser = {
      id: response.userId,
      name: response.name,
      email: response.email,
      role: response.role,
      mustChangePassword: !!response.mustChangePassword
    };
    const usesCookie = !!response.usesCookieAuth;
    this.cookieAuth.set(usesCookie);
    this.token.set(usesCookie ? null : response.token || null);
    this.user.set(user);
    this.persist(user, usesCookie ? null : response.token || null);
    this.lastRefreshAt = Date.now();
    this.startKeepAlive();
  }

  applyMe(me: {
    id: number;
    name: string;
    email: string;
    role: string;
    mustChangePassword?: boolean;
    school?: MeSchoolContext | null;
    freeAccess?: boolean;
    photoUrl?: string | null;
  }): void {
    const current = this.user();
    const user: SessionUser = {
      id: me.id,
      name: me.name,
      email: me.email,
      role: me.role,
      mustChangePassword: !!me.mustChangePassword,
      schoolId: me.school?.schoolId ?? current?.schoolId ?? null,
      isMembershipActive: me.school?.isMembershipActive ?? current?.isMembershipActive,
      planLabel: me.school?.planLabel ?? current?.planLabel ?? null,
      freeAccess: me.freeAccess ?? current?.freeAccess,
      photoUrl: me.photoUrl ?? null
    };
    this.user.set(user);
    this.persist(user, this.cookieAuth() ? null : this.token());
  }

  applySchoolContext(school: MeSchoolContext | null | undefined, freeAccess?: boolean): void {
    const current = this.user();
    if (!current) {
      return;
    }
    const user: SessionUser = {
      ...current,
      schoolId: school?.schoolId ?? null,
      isMembershipActive: !!school?.isMembershipActive,
      planLabel: school?.planLabel ?? null,
      freeAccess: freeAccess ?? current.freeAccess
    };
    this.user.set(user);
    this.persist(user, this.cookieAuth() ? null : this.token());
  }

  patchUser(partial: Partial<SessionUser>): void {
    const current = this.user();
    if (!current) {
      return;
    }
    const user = { ...current, ...partial };
    this.user.set(user);
    this.persist(user, this.cookieAuth() ? null : this.token());
  }

  clear(): void {
    this.stopKeepAlive();
    this.token.set(null);
    this.user.set(null);
    this.cookieAuth.set(false);
    sessionStorage.removeItem(STORAGE_KEY);
    localStorage.removeItem(STORAGE_KEY);
  }

  private startKeepAlive(): void {
    if (this.keepAliveTimer || !this.cookieAuth() || !this.user()) {
      return;
    }
    this.keepAliveTimer = setInterval(() => this.renewIfStale(), 60 * 1000);
  }

  private stopKeepAlive(): void {
    if (this.keepAliveTimer) {
      clearInterval(this.keepAliveTimer);
      this.keepAliveTimer = null;
    }
  }

  private renewIfStale(): void {
    if (!this.cookieAuth() || !this.user() || Date.now() - this.lastRefreshAt < KEEP_ALIVE_MS) {
      return;
    }
    this.refreshSession().subscribe({
      error: (err) => {
        if (isAuthRejection(err)) {
          this.clear();
        }
      }
    });
  }

  private persist(user: SessionUser, token: string | null): void {
    const raw = JSON.stringify({ user, cookieAuth: this.cookieAuth(), token });
    sessionStorage.setItem(STORAGE_KEY, raw);
    localStorage.setItem(STORAGE_KEY, raw);
  }

  private restore(): void {
    const raw =
      sessionStorage.getItem(STORAGE_KEY)
      ?? localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return;
    }
    try {
      const parsed = JSON.parse(raw) as {
        token?: string | null;
        user?: SessionUser;
        cookieAuth?: boolean;
      };
      if (parsed.user) {
        this.user.set(parsed.user);
        this.cookieAuth.set(!!parsed.cookieAuth);
        this.token.set(parsed.cookieAuth ? null : parsed.token ?? null);
      }
    } catch {
      sessionStorage.removeItem(STORAGE_KEY);
      localStorage.removeItem(STORAGE_KEY);
    }
  }
}
