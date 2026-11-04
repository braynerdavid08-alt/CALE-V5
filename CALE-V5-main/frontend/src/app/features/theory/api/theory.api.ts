import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../../core/config/env';

export interface TheoryTopicDto {
  id: number;
  name: string;
  description?: string | null;
  color: string;
  category: string;
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

export interface LicenseCategoryPolicyDto {
  code: string;
  label: string;
  requiredTheoryHours?: number | null;
  requiredWorkshopHours?: number | null;
}

export interface TheorySettingsDto {
  defaultDurationMinutes: number;
  minCancelHours: number;
  reservationCloseMinutesBefore: number;
  requiredTheoryHours: number;
  requiredWorkshopHours: number;
  theoryExamId?: number | null;
  weekdaysEnabled: boolean;
  saturdayEnabled: boolean;
  maxWeekdayClassesPerDay: number;
  maxSaturdayClassesPerDay: number;
  maxDailyTheoryMinutes: number;
  weekdayReservationOpenDaysBefore: number;
  saturdayReservationOpenDaysBefore: number;
  studentBookingWindowStart?: string | null;
  studentBookingWindowEnd?: string | null;
  bookingPolicySummary: string;
  licenseCategoryPolicies: LicenseCategoryPolicyDto[];
  savedBookingPresets: TheoryBookingPresetDto[];
  hiddenBookingPresetKeys: string[];
  notifyReservationOpen: boolean;
  notifyClassReminder24h: boolean;
  notifyClassReminder1h: boolean;
  notifyExamReminder24h: boolean;
}

export interface TheoryBookingPresetDto {
  id: string;
  name: string;
  weekdaysEnabled: boolean;
  saturdayEnabled: boolean;
  maxWeekdayClassesPerDay: number;
  maxSaturdayClassesPerDay: number;
  maxDailyTheoryMinutes: number;
  weekdayReservationOpenDaysBefore: number;
  saturdayReservationOpenDaysBefore: number;
  studentBookingWindowStart?: string | null;
  studentBookingWindowEnd?: string | null;
}

export interface PracticalEligibilityDto {
  canBookPractical: boolean;
  theoryExamPassed: boolean;
  theoryHoursComplete: boolean;
  workshopHoursComplete: boolean;
  theoryHoursCompleted: number;
  theoryHoursRequired: number;
  workshopHoursCompleted: number;
  workshopHoursRequired: number;
  theoryExamAuthorized: boolean;
  practicalAuthorized: boolean;
  blockReason?: string | null;
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
  workshopHoursCompleted: number;
  workshopHoursRequired: number;
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
  practicalEligibility?: PracticalEligibilityDto | null;
  nextExamAppointment?: StudentExamAppointmentDto | null;
  platformExam?: StudentPlatformExamDto | null;
  balanceDue?: number;
}

export interface StudentExamAppointmentDto {
  id: number;
  examDate: string;
  slotTime: string;
}

export interface StudentPlatformExamDto {
  id: number;
  name: string;
}

export interface BulkAuthorizeResultDto {
  authorizedCount: number;
  skippedCount: number;
  skippedInactive?: number;
  skippedBalanceDue?: number;
  skippedAlreadyAuthorized?: number;
  skippedHoursIncomplete?: number;
  skippedExamPassed?: number;
  skippedExamNotPassed?: number;
  skippedAlreadyPractical?: number;
  skippedTheoryExamNotConfigured?: number;
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
  theoryExamAuthorized: boolean;
  practicalAuthorized: boolean;
  createdAt: string;
  acceptedAt?: string | null;
  practicalEligibility?: PracticalEligibilityDto | null;
  balanceDue?: number;
}

export interface TheoryExamOptionDto {
  id: number;
  name: string;
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
      category: body.category ?? 'Theory',
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

  listExamOptions() {
    return this.http.get<TheoryExamOptionDto[]>(`${this.schoolBase}/exam-options`);
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

  bulkCreateSessions(body: {
    fromDate: string;
    toDate: string;
    daysOfWeek: number[];
    startTime: string;
    endTime: string;
    topicId: number;
    classroomId: number;
    capacity?: number;
    notes?: string;
  }) {
    return this.http.post<{ created: number; skippedExisting: number; skippedInvalidDay: number }>(
      `${this.schoolBase}/sessions/bulk`,
      body
    );
  }

  bulkCancelSessions(body: {
    fromDate: string;
    toDate: string;
    daysOfWeek: number[];
    startTime: string;
    endTime: string;
    topicId: number;
    classroomId: number;
  }) {
    return this.http.post<{ cancelled: number; withReservations: number; skippedInvalidDay: number }>(
      `${this.schoolBase}/sessions/bulk-cancel`,
      body
    );
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
      instructorUserId?: number | null;
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

  listEnrollments(studentIds?: number[]) {
    const params = studentIds?.length ? { studentIds: studentIds.join(',') } : undefined;
    return this.http.get<EnrollmentDto[]>(`${this.schoolBase}/enrollments`, { params });
  }

  updateEnrollment(
    studentUserId: number,
    body: {
      status: string;
      attendanceDayType?: string | null;
      allowedStartTime?: string | null;
      licenseCategories?: string | null;
      theoryExamAuthorized?: boolean | null;
      practicalAuthorized?: boolean | null;
    }
  ) {
    return this.http.put<EnrollmentDto>(
      `${this.schoolBase}/enrollments/student/${studentUserId}`,
      body
    );
  }

  bulkAuthorize(body: { theoryExam: boolean; practical: boolean }) {
    return this.http.post<BulkAuthorizeResultDto>(
      `${this.schoolBase}/enrollments/bulk-authorize`,
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

  rescheduleReservation(reservationId: number, newSessionId: number) {
    return this.http.post<TheoryClassSessionDto>(
      `${this.studentBase}/reservations/${reservationId}/reschedule`,
      { newSessionId }
    );
  }

  joinWaitlist(sessionId: number) {
    return this.http.post<ScheduleWaitlistEntryDto>(
      `${this.studentBase}/sessions/${sessionId}/waitlist`,
      {}
    );
  }

  myWaitlist() {
    return this.http.get<ScheduleWaitlistEntryDto[]>(`${this.studentBase}/waitlist`);
  }

  confirmWaitlist(waitlistId: number) {
    return this.http.post<TheoryClassSessionDto>(
      `${this.studentBase}/waitlist/${waitlistId}/confirm`,
      {}
    );
  }

  leaveWaitlist(waitlistId: number) {
    return this.http.delete<void>(`${this.studentBase}/waitlist/${waitlistId}`);
  }

  cancelReservation(reservationId: number) {
    return this.http.delete<void>(`${this.studentBase}/reservations/${reservationId}`);
  }

  checkIn() {
    return this.http.post<void>(`${this.studentBase}/check-in`, {});
  }
}

export interface ScheduleWaitlistEntryDto {
  id: number;
  kind: string;
  targetId: number;
  position: number;
  status: string;
  offeredAt?: string | null;
  offerExpiresAt?: string | null;
  targetDate?: string | null;
  targetStartTime?: string | null;
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
      return 'Máximo de clases del día reservadas';
    case 'minutes_limit':
      return 'Límite de horas diarias alcanzado';
    case 'not_authorized':
      return 'Sin autorización';
    case 'full':
      return 'Entrar a lista de espera';
    case 'reserved':
      return 'Cancelar reserva';
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
