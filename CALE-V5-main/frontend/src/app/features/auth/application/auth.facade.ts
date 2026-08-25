import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { SessionStore } from '../../../core/auth/session.store';
import { AuthApi } from '../api/auth.api';

@Injectable({ providedIn: 'root' })
export class AuthFacade {
  private readonly api = inject(AuthApi);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);

  login(email: string, password: string): void {
    this.run(() => this.api.login(email, password).subscribe({
      next: (res) => this.enter(res),
      error: (err) => this.fail(err)
    }));
  }

  register(name: string, email: string, password: string): void {
    this.run(() => this.api.register(name, email, password).subscribe({
      next: (res) => this.enter(res),
      error: (err) => this.fail(err)
    }));
  }

  changePassword(currentPassword: string, newPassword: string): void {
    this.success.set(null);
    this.run(() => this.api.changePassword(currentPassword, newPassword)
      .subscribe({
        next: () => {
          this.loading.set(false);
          this.success.set('Contraseña actualizada.');
        },
        error: (err) => this.fail(err)
      }));
  }

  logout(): void {
    this.session.clear();
    void this.router.navigateByUrl('/login');
  }

  private enter(res: Parameters<SessionStore['set']>[0]): void {
    this.session.set(res);
    this.loading.set(false);
    void this.router.navigateByUrl(this.session.homeRoute());
  }

  private run(start: () => void): void {
    this.loading.set(true);
    this.error.set(null);
    start();
  }

  private fail(err: unknown): void {
    this.loading.set(false);
    this.error.set(mapApiError(err));
  }
}
