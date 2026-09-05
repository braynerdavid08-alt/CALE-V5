import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from '../../features/auth/api/auth.api';
import { AuthResponse, MeSchoolContext, SessionUser } from './session.models';

const STORAGE_KEY = 'cale.session.v5';

@Injectable({ providedIn: 'root' })
export class SessionStore {
  private readonly authApi = inject(AuthApi);

  readonly token = signal<string | null>(null);
  readonly user = signal<SessionUser | null>(null);
  readonly cookieAuth = signal(false);
  readonly isAuthenticated = computed(() => !!this.user());
  readonly hasCatalogAccess = computed(() => this.catalogAccess());
  readonly hasSimulacroAccess = computed(() => this.simulacroAccess());

  private readonly catalogAccess = computed(() => {
    const user = this.user();
    if (!user) {
      return false;
    }
    if (user.role === 'Admin') {
      return true;
    }
    if (user.role === 'School' || user.role === 'Teacher') {
      return !!user.isMembershipActive;
    }
    return false;
  });

  private readonly simulacroAccess = computed(() => {
    const user = this.user();
    if (!user) {
      return false;
    }
    if (user.role === 'Admin') {
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

  constructor() {
    this.restore();
  }

  async bootstrap(): Promise<void> {
    // Anonymous visitors have no session cookie — do not POST /refresh (avoids noisy 401).
    if (!this.user()) {
      return;
    }

    try {
      const me = await firstValueFrom(this.authApi.me());
      this.applyMe(me);
      return;
    } catch {
      /* access expired — try refresh cookie below */
    }

    try {
      const refreshed = await firstValueFrom(this.authApi.refresh());
      this.set(refreshed);
      const me = await firstValueFrom(this.authApi.me());
      this.applyMe(me);
    } catch {
      // Stale localStorage without a valid refresh cookie → clear ghost session.
      this.clear();
    }
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
  }

  applyMe(me: {
    id: number;
    name: string;
    email: string;
    role: string;
    mustChangePassword?: boolean;
    school?: MeSchoolContext | null;
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
      planLabel: me.school?.planLabel ?? current?.planLabel ?? null
    };
    this.user.set(user);
    this.persist(user, this.cookieAuth() ? null : this.token());
  }

  applySchoolContext(school: MeSchoolContext | null | undefined): void {
    const current = this.user();
    if (!current) {
      return;
    }
    const user: SessionUser = {
      ...current,
      schoolId: school?.schoolId ?? null,
      isMembershipActive: !!school?.isMembershipActive,
      planLabel: school?.planLabel ?? null
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
    this.token.set(null);
    this.user.set(null);
    this.cookieAuth.set(false);
    sessionStorage.removeItem(STORAGE_KEY);
    localStorage.removeItem(STORAGE_KEY);
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
