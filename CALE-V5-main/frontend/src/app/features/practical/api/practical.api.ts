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

export type PracticalSessionKind = 'Lesson' | 'Exam';

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
  /** Lesson (default) or Exam (always 1h). */
  kind?: PracticalSessionKind | string | null;
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
  availableInstructors: PracticalInstructorOptionDto[];
  completedLessons?: number;
  requiredLessons?: number;
}

export interface PracticalInstructorOptionDto {
  instructorUserId: number;
  instructorName: string;
  availableLessonCount: number;
}

export interface PracticalAttendanceRowDto {
  studentUserId: number;
  studentName: string;
  status: string;
  reservationId: number;
}

export type PracticalDurationHours = 1 | 2;

export interface TimeSlot {
  start: string;
  /** Internal exclusive-safe end (e.g. 2h from 14:00 → 15:59). */
  end: string;
  /** Human label using wall-clock end (e.g. 14:00 – 16:00). */
  label: string;
  durationHours: PracticalDurationHours;
}

function pad2(n: number): string {
  return String(n).padStart(2, '0');
}

export function parseHour(value: string): number {
  return Number(value.slice(0, 2));
}

/**
 * Internal end: last minute of the occupied block so the next hour stays free.
 * 14:00 + 2h → 15:59 (never touches 16:00).
 */
export function practicalInternalEnd(startHour: number, durationHours: PracticalDurationHours): string {
  const lastOccupiedHour = startHour + durationHours - 1;
  if (lastOccupiedHour >= 23) {
    return '23:59';
  }
  return `${pad2(lastOccupiedHour)}:59`;
}

/** Visible end on the clock (14:00 + 2h → 16:00). */
export function practicalDisplayEnd(startHour: number, durationHours: PracticalDurationHours): string {
  const endHour = startHour + durationHours;
  if (endHour >= 24) {
    return '24:00';
  }
  return `${pad2(endHour)}:00`;
}

export function buildPracticalSlot(
  startHour: number,
  durationHours: PracticalDurationHours
): TimeSlot {
  const start = `${pad2(startHour)}:00`;
  const end = practicalInternalEnd(startHour, durationHours);
  const displayEnd = practicalDisplayEnd(startHour, durationHours);
  return { start, end, label: `${start} – ${displayEnd}`, durationHours };
}

/** Hourly starts for the school grid / pickers: 06:00 … 22:00. */
export const PRACTICAL_START_HOURS: number[] = Array.from({ length: 17 }, (_, i) => 6 + i);

export const PRACTICAL_MAX_DAILY_HOURS = 8;

/** @deprecated Prefer buildPracticalSlot(start, duration). Kept for callers that still import the name. */
export const PRACTICAL_TIME_SLOTS: TimeSlot[] = PRACTICAL_START_HOURS.map((h) =>
  buildPracticalSlot(h, 2)
);

export function slotDurationHours(slot: TimeSlot): number {
  return slot.durationHours;
}

function exclusiveEndMinutes(endTime: string): number {
  const [eh, em] = endTime.slice(0, 5).split(':').map(Number);
  if (em === 59) {
    return (eh + 1) * 60;
  }
  return eh * 60 + em;
}

export function lessonDurationHours(lesson: PracticalLessonSessionDto): number {
  const [sh, sm] = lesson.startTime.slice(0, 5).split(':').map(Number);
  const startMinutes = sh * 60 + sm;
  const endExclusive = exclusiveEndMinutes(lesson.endTime);
  return Math.max(0, (endExclusive - startMinutes) / 60);
}

export function isPracticalExam(lesson: Pick<PracticalLessonSessionDto, 'kind'> | null | undefined): boolean {
  return (lesson?.kind ?? 'Lesson').toLowerCase() === 'exam';
}

export function practicalKindLabel(kind?: string | null): string {
  return (kind ?? 'Lesson').toLowerCase() === 'exam' ? 'Examen de manejo' : 'Clase de manejo';
}

/** Wall-clock range for UI: 14:00 – 16:00 (even if stored end is 15:59). */
export function formatLessonWindow(lesson: Pick<PracticalLessonSessionDto, 'startTime' | 'endTime'>): string {
  const start = lesson.startTime.slice(0, 5);
  const startHour = parseHour(start);
  const duration = lessonDurationHours(lesson as PracticalLessonSessionDto);
  const hours = (duration === 1 || duration === 2 ? duration : Math.round(duration)) as number;
  const displayEnd =
    hours === 1 || hours === 2
      ? practicalDisplayEnd(startHour, hours as PracticalDurationHours)
      : (() => {
          const endExclusive = exclusiveEndMinutes(lesson.endTime);
          return `${pad2(Math.floor(endExclusive / 60))}:${pad2(endExclusive % 60)}`;
        })();
  return `${start} – ${displayEnd}`;
}

/** True when a lesson occupies the hour row (e.g. 2h from 14:00 covers 14:00 and 15:00). */
export function lessonCoversStart(lesson: PracticalLessonSessionDto, hourStart: string): boolean {
  const [sh, sm] = lesson.startTime.slice(0, 5).split(':').map(Number);
  const hour = parseHour(hourStart) * 60;
  const start = sh * 60 + sm;
  const endExclusive = exclusiveEndMinutes(lesson.endTime);
  return hour >= start && hour < endExclusive;
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
    kind?: PracticalSessionKind;
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
    kind?: PracticalSessionKind;
  }) {
    return this.http.post<PracticalLessonSessionDto>(`${this.schoolBase}/lessons`, body);
  }

  bulkCreateLessons(body: {
    fromDate: string;
    toDate: string;
    daysOfWeek: number[];
    startTime: string;
    endTime: string;
    instructorUserId: number;
    vehicleId: number;
    capacity?: number;
    notes?: string | null;
    kind?: PracticalSessionKind;
  }) {
    return this.http.post<{
      created: number;
      skippedExisting: number;
      skippedConflictOrInvalid: number;
    }>(`${this.schoolBase}/lessons/bulk`, body);
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
