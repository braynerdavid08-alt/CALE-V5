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

  registerTeacher(name: string, email: string, password: string) {
    return this.http.post<AuthResponse>(`${this.base}/register-teacher`, {
      name,
      email,
      password
    });
  }

  registerSchool(body: Record<string, string>) {
    return this.http.post<AuthResponse>(`${this.base}/register-school`, body);
  }

  schoolPlans() {
    return this.http.get<SchoolPlanDto[]>(`${this.base}/school-plans`);
  }

  me() {
    return this.http.get<MeResponse>(`${this.base}/me`);
  }

  updateMe(name: string) {
    return this.http.put<MeResponse>(`${this.base}/me`, { name });
  }

  changePassword(currentPassword: string, newPassword: string) {
    return this.http.post<void>(`${this.base}/change-password`, {
      currentPassword,
      newPassword
    });
  }
}
