import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../../core/config/env';
import {
  AuthResponse,
  MeResponse
} from '../../../core/auth/session.models';

export interface SchoolPlanDto {
  code: string;
  label: string;
  priceCop: number;
  monthlyEquivalentCop: number;
  durationMonths: number;
  maxTeachers: number;
  maxStudents: number;
}

export interface PendingEmailConfirmationResponse {
  email: string;
  message: string;
  requiresEmailConfirmation?: boolean;
  emailSent?: boolean;
  token?: string;
  userId?: number;
  name?: string;
  role?: string;
  mustChangePassword?: boolean;
}

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
    return this.http.post<PendingEmailConfirmationResponse>(`${this.base}/register`, {
      name,
      email,
      password
    });
  }

  registerTeacher(name: string, email: string, password: string) {
    return this.http.post<PendingEmailConfirmationResponse>(
      `${this.base}/register-teacher`,
      { name, email, password }
    );
  }

  registerSchool(body: Record<string, string>) {
    return this.http.post<PendingEmailConfirmationResponse>(
      `${this.base}/register-school`,
      body
    );
  }

  confirmEmail(email: string, code: string) {
    return this.http.post<AuthResponse>(`${this.base}/confirm-email`, {
      email,
      code
    });
  }

  resendConfirmation(email: string) {
    return this.http.post<PendingEmailConfirmationResponse>(
      `${this.base}/resend-confirmation`,
      { email }
    );
  }

  schoolPlans() {
    return this.http.get<SchoolPlanDto[]>(`${this.base}/school-plans`);
  }

  me() {
    return this.http.get<MeResponse>(`${this.base}/me`);
  }

  updateMe(name: string, email?: string) {
    return this.http.put<MeResponse>(`${this.base}/me`, {
      name,
      ...(email ? { email } : {})
    });
  }

  changePassword(currentPassword: string, newPassword: string) {
    return this.http.post<void>(`${this.base}/change-password`, {
      currentPassword,
      newPassword
    });
  }
}
