import { Injectable, computed, signal } from '@angular/core';
import { AuthResponse, MeSchoolContext, SessionUser } from './session.models';

const STORAGE_KEY = 'cale.session.v5';

@Injectable({ providedIn: 'root' })
export class SessionStore {
  readonly token = signal<string | null>(null);
  readonly user = signal<SessionUser | null>(null);
  readonly isAuthenticated = computed(() => !!this.token());
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
    this.token.set(response.token);
    this.user.set(user);
    this.persist({ token: response.token, user });
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
    const token = this.token();
    if (token) {
      this.persist({ token, user });
    }
  }

  patchUser(partial: Partial<SessionUser>): void {
    const current = this.user();
    if (!current) {
      return;
    }
    const user = { ...current, ...partial };
    this.user.set(user);
    const token = this.token();
    if (token) {
      this.persist({ token, user });
    }
  }

  clear(): void {
    this.token.set(null);
    this.user.set(null);
    sessionStorage.removeItem(STORAGE_KEY);
    localStorage.removeItem(STORAGE_KEY);
  }

  private persist(payload: { token: string; user: SessionUser }): void {
    // sessionStorage: no persiste al cerrar el navegador (menos exposición que localStorage).
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(payload));
    localStorage.removeItem(STORAGE_KEY);
  }

  private restore(): void {
    // Migra/limpia restos antiguos en localStorage.
    localStorage.removeItem(STORAGE_KEY);

    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return;
    }
    try {
      const parsed = JSON.parse(raw) as {
        token?: string;
        user?: SessionUser;
      };
      if (parsed.token && parsed.user) {
        this.token.set(parsed.token);
        this.user.set(parsed.user);
      }
    } catch {
      sessionStorage.removeItem(STORAGE_KEY);
    }
  }
}
