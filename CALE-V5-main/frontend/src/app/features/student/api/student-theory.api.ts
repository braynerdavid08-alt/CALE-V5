import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../../core/config/env';
import { PracticalEligibilityDto } from '../../theory/api/theory.api';

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
  nextClass?: { sessionDate: string; startTime: string; topicName?: string | null } | null;
  nextExamAppointment?: { id: number; examDate: string; slotTime: string } | null;
  platformExam?: { id: number; name: string } | null;
  practicalEligibility?: PracticalEligibilityDto | null;
  balanceDue?: number;
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
