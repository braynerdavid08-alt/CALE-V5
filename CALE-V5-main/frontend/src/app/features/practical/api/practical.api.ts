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

export interface PracticalLessonAssignmentDto {
  studentUserId: number;
  studentName: string;
  licenseCategory?: string | null;
  lessonNumber: number;
  lessonsRequired: number;
  reservationId: number;
  reservationStatus: string;
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
  assignment?: PracticalLessonAssignmentDto | null;
}

export interface PracticalSchedulingStudentDto {
  studentUserId: number;
  studentName: string;
  licenseCategories?: string | null;
  completedLessons: number;
  requiredLessons: number;
  nextLessonNumber: number;
  isEligible: boolean;
  blockReason?: string | null;
}

export interface PracticalStudentDashboardDto {
  eligibility: PracticalEligibilityDto;
  nextLesson?: PracticalLessonSessionDto | null;
  upcomingReservations: PracticalLessonSessionDto[];
  availableLessons: PracticalLessonSessionDto[];
}

export interface PracticalAttendanceRowDto {
  studentUserId: number;
  studentName: string;
  status: string;
  reservationId: number;
}

export interface TimeSlot {
  start: string;
  end: string;
  label: string;
}

function buildTwoHourSlot(startHour: number): TimeSlot {
  const start = `${String(startHour).padStart(2, '0')}:00`;
  const end = startHour >= 22 ? '23:59' : `${String(startHour + 2).padStart(2, '0')}:59`;
  return { start, end, label: `${start} – ${end}` };
}

/** Bloques de 2 horas, igual que clases teóricas: 06:00 hasta 22:00 (fin 23:59). */
export const PRACTICAL_TIME_SLOTS: TimeSlot[] = [6, 8, 10, 12, 14, 16, 18, 20, 22].map(
  buildTwoHourSlot
);

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

  listLessons(weekStart?: string, instructorUserId?: number, vehicleId?: number) {
    let params = new HttpParams();
    if (weekStart) {
      params = params.set('weekStart', weekStart);
    }
    if (instructorUserId) {
      params = params.set('instructorUserId', instructorUserId);
    }
    if (vehicleId) {
      params = params.set('vehicleId', vehicleId);
    }
    return this.http.get<PracticalLessonSessionDto[]>(`${this.schoolBase}/lessons`, { params });
  }

  listSchedulingStudents() {
    return this.http.get<PracticalSchedulingStudentDto[]>(`${this.schoolBase}/students`);
  }

  quickAssign(body: {
    sessionDate: string;
    startTime: string;
    endTime: string;
    instructorUserId: number;
    vehicleId: number;
    studentUserId: number;
  }) {
    return this.http.post<PracticalLessonSessionDto>(`${this.schoolBase}/lessons/quick-assign`, body);
  }

  unassignStudent(lessonId: number) {
    return this.http.post(`${this.schoolBase}/lessons/${lessonId}/unassign`, {});
  }

  duplicateWeek(body: { weekStart: string; instructorUserId: number; vehicleId: number }) {
    return this.http.post<{ created: number }>(`${this.schoolBase}/schedule/duplicate-week`, body);
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

  listAttendanceLessons() {
    return this.http.get<PracticalLessonSessionDto[]>(`${this.schoolBase}/lessons/attendance`);
  }

  listAttendance(lessonId: number) {
    return this.http.get<PracticalAttendanceRowDto[]>(
      `${this.schoolBase}/lessons/${lessonId}/attendance`
    );
  }

  markAttendance(lessonId: number, studentUserId: number, status: string) {
    return this.http.post(`${this.schoolBase}/lessons/${lessonId}/attendance`, {
      studentUserId,
      status
    });
  }

  markAllPresent(lessonId: number, rows: PracticalAttendanceRowDto[]) {
    return this.http.post(`${this.schoolBase}/lessons/${lessonId}/attendance/batch`, {
      rows: rows.map((r) => ({ studentUserId: r.studentUserId, status: 'Present' }))
    });
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
