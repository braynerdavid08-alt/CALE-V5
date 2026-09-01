import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../../core/config/env';

export interface TheoryStudentDashboardDto {
  progressPercent: number;
  hoursCompleted: number;
  hoursRequired: number;
  workshopHoursCompleted: number;
  workshopHoursRequired: number;
  pendingClasses: number;
  absences: number;
  checkedInToday: boolean;
  nextAction?: string | null;
  platformExam?: { id: number; name: string } | null;
  practicalEligibility?: { canStart: boolean; reason?: string | null } | null;
}

@Injectable({ providedIn: 'root' })
export class StudentTheoryApi {
  private readonly http = inject(HttpClient);
  private readonly base = env.apiUrl;

  dashboard() {
    return this.http.get<TheoryStudentDashboardDto>(
      `${this.base}/api/student/theory/dashboard`
    );
  }
}
