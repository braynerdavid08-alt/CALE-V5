import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../../core/config/env';

export interface FormationStageDto {
  id: string;
  title: string;
  status: string;
  detail?: string | null;
  blockReason?: string | null;
  canBook: boolean;
  ctaLabel?: string | null;
  ctaPath?: string | null;
  hoursCompleted?: number | null;
  hoursRequired?: number | null;
  lessonsCompleted?: number | null;
  lessonsRequired?: number | null;
}

export interface FormationUpcomingDto {
  kind: string;
  title: string;
  date: string;
  startTime: string;
  endTime?: string | null;
  path: string;
}

export interface StudentFormationStatusDto {
  currentStage: string;
  currentStageLabel: string;
  nextRecommendedAction?: string | null;
  nextRecommendedPath?: string | null;
  stages: FormationStageDto[];
  upcoming: FormationUpcomingDto[];
  balanceDue: number;
  hasActiveSchool: boolean;
}

@Injectable({ providedIn: 'root' })
export class StudentFormationApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api/student/formation`;

  status() {
    return this.http.get<StudentFormationStatusDto>(this.base);
  }
}
