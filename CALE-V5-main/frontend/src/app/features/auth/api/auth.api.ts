import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../../core/config/env';
import {
  AuthResponse,
  MeResponse
} from '../../../core/auth/session.models';

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api/auth`;

  login(email: string, password: string) {
    return this.http.post<AuthResponse>(`${this.base}/login`, {
      email,
      password
    });
  }

  register(name: string, email: string, password: string) {
    return this.http.post<AuthResponse>(`${this.base}/register`, {
      name,
      email,
      password
    });
  }

  me() {
    return this.http.get<MeResponse>(`${this.base}/me`);
  }

  changePassword(currentPassword: string, newPassword: string) {
    return this.http.post<void>(`${this.base}/change-password`, {
      currentPassword,
      newPassword
    });
  }
}
