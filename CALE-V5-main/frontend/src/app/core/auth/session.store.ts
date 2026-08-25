import { Injectable, computed, signal } from '@angular/core';
import { AuthResponse, SessionUser } from './session.models';

const STORAGE_KEY = 'cale.session.v5';

@Injectable({ providedIn: 'root' })
export class SessionStore {
  readonly token = signal<string | null>(null);
  readonly user = signal<SessionUser | null>(null);
  readonly isAuthenticated = computed(() => !!this.token());

  constructor() {
    this.restore();
  }

  homeRoute(): string {
    const role = this.user()?.role;
    if (role === 'Admin') {
      return '/admin';
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
      role: response.role
    };
    this.token.set(response.token);
    this.user.set(user);
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ token: response.token, user })
    );
  }

  clear(): void {
    this.token.set(null);
    this.user.set(null);
    localStorage.removeItem(STORAGE_KEY);
  }

  private restore(): void {
    const raw = localStorage.getItem(STORAGE_KEY);
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
      localStorage.removeItem(STORAGE_KEY);
    }
  }
}
