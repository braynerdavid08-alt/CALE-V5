import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../../core/config/env';

export interface TheoryTopicDto {
  id: number;
  name: string;
  description?: string | null;
  color: string;
  isActive: boolean;
}

export interface TheoryClassroomDto {
  id: number;
  name: string;
  identifier?: string | null;
  capacity: number;
  location?: string | null;
  isActive: boolean;
}

export interface TheorySettingsDto {
  defaultDurationMinutes: number;
  minCancelHours: number;
  reservationCloseMinutesBefore: number;
  requiredTheoryHours: number;
  weekdaysEnabled: boolean;
  saturdayEnabled: boolean;
  notifyReservationOpen: boolean;
  notifyClassReminder24h: boolean;
  notifyClassReminder1h: boolean;
}

export interface AttendanceRowDto {
  studentUserId: number;
  studentName: string;
  status: string;
  reservationId?: number | null;
}

export interface TheoryClassSessionDto {
  id: number;
  sessionDate: string;
  startTime: string;
  endTime: string;
  topicId: number;
  topicName: string;
  topicColor: string;
  classroomId: number;
  classroomName: string;
  capacity: number;
  reservedCount: number;
  availableSeats: number;
  status: string;
  instructorUserId?: number | null;
  instructorName?: string | null;
  notes?: string | null;
  reservationOpenAt: string;
  reservationCloseAt: string;
  bookingState?: string | null;
  bookingMessage?: string | null;
  myReservationId?: number | null;
  myReservationStatus?: string | null;
}

export interface TheoryWeekScheduleDto {
  weekStart: string;
  weekEnd: string;
  sessions: TheoryClassSessionDto[];
  timeSlots: Array<{ label: string; start: string; end: string }>;
  studentAttendanceDayType?: string | null;
}

export interface TheoryMonthScheduleDto {
  monthStart: string;
  monthEnd: string;
  sessions: TheoryClassSessionDto[];
}

export interface TheorySchoolDashboardDto {
  classesToday: number;
  studentsReserved: number;
  availableSeats: number;
  absencesToday: number;
  scheduledClasses: number;
}

export interface TheoryStudentDashboardDto {
  nextClass?: TheoryClassSessionDto | null;
  upcomingReservations: TheoryClassSessionDto[];
  progressPercent: number;
  hoursCompleted: number;
  hoursRequired: number;
  pendingClasses: number;
  absences: number;
  currentStreak: number;
  bestStreak: number;
  nextAction?: string | null;
  reservationCountdownLabel?: string | null;
  reservationOpensAt?: string | null;
  checkedInToday: boolean;
  todayTasks: Array<{ label: string; done: boolean }>;
  attendanceDayType?: string | null;
}

export interface EnrollmentDto {
  id: number;
  studentUserId: number;
  studentName: string;
  studentEmail: string;
  status: string;
  attendanceDayType?: string | null;
  allowedStartTime?: string | null;
  licenseCategories?: string | null;
  createdAt: string;
  acceptedAt?: string | null;
}

@Injectable({ providedIn: 'root' })
export class TheoryApi {
  private readonly http = inject(HttpClient);
  private readonly schoolBase = `${env.apiUrl}/api/school/theory`;
  private readonly studentBase = `${env.apiUrl}/api/student/theory`;

  // School
  schoolDashboard() {
    return this.http.get<TheorySchoolDashboardDto>(`${this.schoolBase}/dashboard`);
  }

  listTopics(activeOnly = false) {
    return this.http.get<TheoryTopicDto[]>(
      `${this.schoolBase}/topics?activeOnly=${activeOnly}`
    );
  }

  saveTopic(body: Partial<TheoryTopicDto>, id?: number) {
    const payload = {
      name: body.name ?? '',
      description: body.description,
      color: body.color ?? '#3B82F6',
      isActive: body.isActive ?? true
    };
    return id
      ? this.http.put<TheoryTopicDto>(`${this.schoolBase}/topics/${id}`, payload)
      : this.http.post<TheoryTopicDto>(`${this.schoolBase}/topics`, payload);
  }

  listClassrooms(activeOnly = false) {
    return this.http.get<TheoryClassroomDto[]>(
      `${this.schoolBase}/classrooms?activeOnly=${activeOnly}`
    );
  }

  saveClassroom(body: Partial<TheoryClassroomDto>, id?: number) {
    const payload = {
      name: body.name ?? '',
      identifier: body.identifier,
      capacity: body.capacity ?? 15,
      location: body.location,
      isActive: body.isActive ?? true
    };
    return id
      ? this.http.put<TheoryClassroomDto>(`${this.schoolBase}/classrooms/${id}`, payload)
      : this.http.post<TheoryClassroomDto>(`${this.schoolBase}/classrooms`, payload);
  }

  getSettings() {
    return this.http.get<TheorySettingsDto>(`${this.schoolBase}/settings`);
  }

  updateSettings(body: TheorySettingsDto) {
    return this.http.put<TheorySettingsDto>(`${this.schoolBase}/settings`, body);
  }

  schoolSchedule(weekStart?: string) {
    let params = new HttpParams();
    if (weekStart) {
      params = params.set('weekStart', weekStart);
    }
    return this.http.get<TheoryWeekScheduleDto>(`${this.schoolBase}/schedule`, { params });
  }

  schoolMonthSchedule(month?: string) {
    let params = new HttpParams();
    if (month) {
      params = params.set('month', month);
    }
    return this.http.get<TheoryMonthScheduleDto>(`${this.schoolBase}/schedule/month`, { params });
  }

  createSession(body: {
    sessionDate: string;
    startTime: string;
    endTime: string;
    topicId: number;
    classroomId: number;
    capacity?: number;
    notes?: string;
  }) {
    return this.http.post<TheoryClassSessionDto>(`${this.schoolBase}/sessions`, body);
  }

  updateSession(
    id: number,
    body: {
      sessionDate: string;
      startTime: string;
      endTime: string;
      topicId: number;
      classroomId: number;
      capacity?: number;
      notes?: string;
    }
  ) {
    return this.http.put<TheoryClassSessionDto>(`${this.schoolBase}/sessions/${id}`, body);
  }

  cancelSession(id: number, reason?: string) {
    return this.http.post<void>(`${this.schoolBase}/sessions/${id}/cancel`, reason ?? '');
  }

  listAttendanceSessions() {
    return this.http.get<TheoryClassSessionDto[]>(`${this.schoolBase}/sessions/attendance`);
  }

  listAttendance(sessionId: number) {
    return this.http.get<AttendanceRowDto[]>(
      `${this.schoolBase}/sessions/${sessionId}/attendance`
    );
  }

  markAttendance(sessionId: number, body: { studentUserId: number; status: string; notes?: string }) {
    return this.http.post<void>(`${this.schoolBase}/sessions/${sessionId}/attendance`, body);
  }

  markAttendanceBatch(
    sessionId: number,
    rows: Array<{ studentUserId: number; status: string; notes?: string }>
  ) {
    return this.http.post<void>(
      `${this.schoolBase}/sessions/${sessionId}/attendance/batch`,
      { rows }
    );
  }

  listEnrollments() {
    return this.http.get<EnrollmentDto[]>(`${this.schoolBase}/enrollments`);
  }

  updateEnrollment(
    studentUserId: number,
    body: {
      status: string;
      attendanceDayType?: string | null;
      allowedStartTime?: string | null;
      licenseCategories?: string | null;
    }
  ) {
    return this.http.put<EnrollmentDto>(
      `${this.schoolBase}/enrollments/student/${studentUserId}`,
      body
    );
  }

  // Student
  studentDashboard() {
    return this.http.get<TheoryStudentDashboardDto>(`${this.studentBase}/dashboard`);
  }

  studentSchedule(weekStart?: string) {
    let params = new HttpParams();
    if (weekStart) {
      params = params.set('weekStart', weekStart);
    }
    return this.http.get<TheoryWeekScheduleDto>(`${this.studentBase}/schedule`, { params });
  }

  reserve(sessionId: number) {
    return this.http.post<TheoryClassSessionDto>(
      `${this.studentBase}/sessions/${sessionId}/reserve`,
      {}
    );
  }

  cancelReservation(reservationId: number) {
    return this.http.delete<void>(`${this.studentBase}/reservations/${reservationId}`);
  }

  checkIn() {
    return this.http.post<void>(`${this.studentBase}/check-in`, {});
  }
}

export function theoryBookingLabel(state?: string | null, message?: string | null): string {
  if (message) {
    return message;
  }
  switch (state) {
    case 'can_reserve':
      return 'Reservar cupo';
    case 'locked_tomorrow':
      return 'Disponible mañana';
    case 'locked':
      return 'No disponible';
    case 'day_taken':
      return 'Ya tienes clase este día';
    case 'day_limit':
      return 'Máximo de clases del sábado reservadas';
    case 'not_authorized':
      return 'Sin autorización';
    case 'full':
      return 'Sin cupos';
    case 'reserved':
      return 'Reservada';
    case 'started':
      return 'Clase iniciada';
    case 'cancelled':
      return 'Cancelada';
    default:
      return 'Ver clase';
  }
}

export function cupoTone(available: number, capacity: number): 'ok' | 'warn' | 'full' {
  if (available <= 0) {
    return 'full';
  }
  if (available <= Math.max(1, Math.floor(capacity * 0.2))) {
    return 'warn';
  }
  return 'ok';
}
