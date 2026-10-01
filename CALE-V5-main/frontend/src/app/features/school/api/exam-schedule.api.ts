import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../../core/config/env';

export type ExamSlotStatus = 'Available' | 'Full' | 'Closed' | 'Past';
export type ExamBookingStatus = 'Active' | 'Cancelled' | 'NoShow' | 'Completed';

export interface ExamTemplateDto {
  id: number;
  /** .NET DayOfWeek: 0 = domingo … 6 = sábado. */
  dayOfWeek: number;
  time: string;
  capacity: number;
  isActive: boolean;
}

export interface ExamBookingDto {
  id: number;
  studentUserId?: number | null;
  studentName: string;
  status: ExamBookingStatus | string;
  noShow: boolean;
  checkedInAt?: string | null;
  notes?: string | null;
  bookedByStudent: boolean;
  createdAt: string;
}

export interface ExamSlotDto {
  date: string;
  time: string;
  capacity: number;
  occupied: number;
  available: number;
  status: ExamSlotStatus | string;
  source: 'template' | 'extra' | 'none';
  isOverridden: boolean;
  overrideId?: number | null;
  templateId?: number | null;
  note?: string | null;
  bookings: ExamBookingDto[];
}

export interface ExamDayDto {
  date: string;
  isClosed: boolean;
  closureOverrideId?: number | null;
  closureNote?: string | null;
  slots: ExamSlotDto[];
}

export interface ExamWeekDto {
  from: string;
  to: string;
  hasTemplates: boolean;
  days: ExamDayDto[];
}

export interface CreateExamTemplatesRequest {
  daysOfWeek: number[];
  time: string;
  capacity?: number;
}

export interface UpdateExamTemplateRequest {
  time: string;
  capacity: number;
  isActive: boolean;
}

export interface SaveExamSlotRequest {
  date: string;
  time: string;
  capacity?: number | null;
  isClosed: boolean;
  note?: string | null;
  cancelBookings?: boolean;
}

export interface CloseExamDayRequest {
  note?: string | null;
  cancelBookings?: boolean;
}

export interface CreateExamBookingRequest {
  date: string;
  time: string;
  studentUserId?: number | null;
  studentLabel?: string | null;
  notes?: string | null;
}

export interface SchoolAuditEntryDto {
  id: number;
  area: string;
  action: string;
  summary?: string | null;
  oldValue?: string | null;
  newValue?: string | null;
  reason?: string | null;
  studentUserId?: number | null;
  studentName?: string | null;
  actorName?: string | null;
  createdAt: string;
}

export type HoursCategory = 'Theory' | 'Workshop';

export interface HoursLineDto {
  required: number;
  attended: number;
  adjusted: number;
  total: number;
  pending: number;
}

export interface HoursAdjustmentDto {
  id: number;
  category: HoursCategory | string;
  previousHours: number;
  newHours: number;
  deltaHours: number;
  reason: string;
  performedBy?: string | null;
  createdAt: string;
}

export interface StudentHoursDto {
  studentUserId: number;
  studentName: string;
  theory: HoursLineDto;
  workshop: HoursLineDto;
  history: HoursAdjustmentDto[];
}

export interface AdjustStudentHoursRequest {
  category: HoursCategory;
  newHours: number;
  reason: string;
}

export type AgendaItemKind = 'theory' | 'practical' | 'exam';

export interface AgendaStudentDto {
  studentUserId?: number | null;
  name: string;
  status: string;
}

export interface AgendaItemDto {
  kind: AgendaItemKind;
  id: number;
  date: string;
  startTime: string;
  endTime?: string | null;
  title: string;
  instructorName?: string | null;
  status: string;
  capacity: number;
  occupied: number;
  available: number;
  students: AgendaStudentDto[];
}

export interface SchoolAgendaDto {
  from: string;
  to: string;
  items: AgendaItemDto[];
}

@Injectable({ providedIn: 'root' })
export class ExamScheduleApi {
  private readonly http = inject(HttpClient);
  private readonly school = `${env.apiUrl}/api/school`;
  private readonly base = `${this.school}/theory-exams`;

  listTemplates() {
    return this.http.get<ExamTemplateDto[]>(`${this.base}/templates`);
  }

  createTemplates(body: CreateExamTemplatesRequest) {
    return this.http.post<ExamTemplateDto[]>(`${this.base}/templates`, body);
  }

  updateTemplate(id: number, body: UpdateExamTemplateRequest) {
    return this.http.put<ExamTemplateDto>(`${this.base}/templates/${id}`, body);
  }

  deleteTemplate(id: number) {
    return this.http.delete<void>(`${this.base}/templates/${id}`);
  }

  getWeek(from?: string, to?: string) {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<ExamWeekDto>(`${this.base}/week`, { params });
  }

  saveSlot(body: SaveExamSlotRequest) {
    return this.http.put<ExamDayDto>(`${this.base}/slots`, body);
  }

  deleteOverride(id: number) {
    return this.http.delete<ExamDayDto>(`${this.base}/overrides/${id}`);
  }

  closeDay(date: string, body: CloseExamDayRequest) {
    return this.http.put<ExamDayDto>(`${this.base}/days/${date}/close`, body);
  }

  reopenDay(date: string) {
    return this.http.delete<ExamDayDto>(`${this.base}/days/${date}/close`);
  }

  createBooking(body: CreateExamBookingRequest) {
    return this.http.post<ExamSlotDto>(`${this.base}/bookings`, body);
  }

  cancelBooking(id: number, reason?: string | null) {
    return this.http.post<ExamSlotDto>(`${this.base}/bookings/${id}/cancel`, { reason: reason || null });
  }

  listAudit(opts?: { studentUserId?: number; area?: string; take?: number }) {
    let params = new HttpParams();
    if (opts?.studentUserId) params = params.set('studentUserId', opts.studentUserId);
    if (opts?.area) params = params.set('area', opts.area);
    if (opts?.take) params = params.set('take', opts.take);
    return this.http.get<SchoolAuditEntryDto[]>(`${this.base}/audit`, { params });
  }

  getStudentHours(studentUserId: number) {
    return this.http.get<StudentHoursDto>(`${this.school}/apprentices/${studentUserId}/hours`);
  }

  adjustStudentHours(studentUserId: number, body: AdjustStudentHoursRequest) {
    return this.http.post<StudentHoursDto>(`${this.school}/apprentices/${studentUserId}/hours`, body);
  }

  getAgenda(from?: string, to?: string) {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<SchoolAgendaDto>(`${this.school}/agenda`, { params });
  }
}
