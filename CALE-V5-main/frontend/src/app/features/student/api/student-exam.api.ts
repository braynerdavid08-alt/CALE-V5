import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../../core/config/env';

export interface StudentExamSlotDto {
  date: string;
  time: string;
  available: number;
  status: 'Available' | 'Full' | string;
}

export interface StudentExamDayDto {
  date: string;
  slots: StudentExamSlotDto[];
}

export interface StudentExamBookingDto {
  id: number;
  date: string;
  time: string;
  status: string;
  canCancel: boolean;
  cancelDeadline?: string | null;
}

export interface StudentExamAvailabilityDto {
  from: string;
  to: string;
  canBook: boolean;
  blockReason?: string | null;
  myBooking?: StudentExamBookingDto | null;
  minCancelHours: number;
  days: StudentExamDayDto[];
}

@Injectable({ providedIn: 'root' })
export class StudentExamApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api/student/theory/exams`;

  availability(from?: string, to?: string) {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<StudentExamAvailabilityDto>(`${this.base}/availability`, { params });
  }

  book(date: string, time: string) {
    return this.http.post<StudentExamBookingDto>(`${this.base}/book`, { date, time });
  }

  mine() {
    return this.http.get<StudentExamBookingDto[]>(`${this.base}/mine`);
  }

  cancel(id: number) {
    return this.http.post<void>(`${this.base}/${id}/cancel`, {});
  }
}
