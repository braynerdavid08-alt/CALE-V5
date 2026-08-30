import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../../core/config/env';
import { PracticalEligibilityDto } from '../../theory/api/theory.api';

export interface PracticalVehicleDto {
  id: number;
  label: string;
  plate?: string | null;
  isActive: boolean;
}

export interface PracticalLessonSessionDto {
  id: number;
  sessionDate: string;
  startTime: string;
  endTime: string;
  instructorUserId: number;
  instructorName: string;
  vehicleId: number;
  vehicleLabel: string;
  capacity: number;
  reservedCount: number;
  availableSeats: number;
  status: string;
  notes?: string | null;
  bookingState?: string | null;
  bookingMessage?: string | null;
  myReservationId?: number | null;
}

export interface PracticalStudentDashboardDto {
  eligibility: PracticalEligibilityDto;
  nextLesson?: PracticalLessonSessionDto | null;
  upcomingReservations: PracticalLessonSessionDto[];
  availableLessons: PracticalLessonSessionDto[];
}

@Injectable({ providedIn: 'root' })
export class PracticalApi {
  private readonly http = inject(HttpClient);
  private readonly schoolBase = `${env.apiUrl}/api/school/practical`;
  private readonly studentBase = `${env.apiUrl}/api/student/practical`;

  listVehicles(activeOnly = false) {
    return this.http.get<PracticalVehicleDto[]>(
      `${this.schoolBase}/vehicles?activeOnly=${activeOnly}`
    );
  }

  saveVehicle(body: { label: string; plate?: string | null; isActive: boolean }, id?: number) {
    return id
      ? this.http.put<PracticalVehicleDto>(`${this.schoolBase}/vehicles/${id}`, body)
      : this.http.post<PracticalVehicleDto>(`${this.schoolBase}/vehicles`, body);
  }

  listLessons(weekStart?: string) {
    let params = new HttpParams();
    if (weekStart) {
      params = params.set('weekStart', weekStart);
    }
    return this.http.get<PracticalLessonSessionDto[]>(`${this.schoolBase}/lessons`, { params });
  }

  createLesson(body: {
    sessionDate: string;
    startTime: string;
    endTime: string;
    instructorUserId: number;
    vehicleId: number;
    capacity?: number;
    notes?: string | null;
  }) {
    return this.http.post<PracticalLessonSessionDto>(`${this.schoolBase}/lessons`, body);
  }

  cancelLesson(id: number) {
    return this.http.post(`${this.schoolBase}/lessons/${id}/cancel`, {});
  }

  studentDashboard() {
    return this.http.get<PracticalStudentDashboardDto>(`${this.studentBase}/dashboard`);
  }

  reserve(lessonId: number) {
    return this.http.post<PracticalLessonSessionDto>(
      `${this.studentBase}/lessons/${lessonId}/reserve`,
      {}
    );
  }

  cancelReservation(reservationId: number) {
    return this.http.delete(`${this.studentBase}/reservations/${reservationId}`);
  }
}
