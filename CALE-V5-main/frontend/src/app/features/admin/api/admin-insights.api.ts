import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../../core/config/env';

export interface WeeklySummary {
  from: string;
  to: string;
  newUsers: number;
  newUsersByRole: Array<{ role: string; count: number }>;
  activeStudents: number;
  examsFinished: number;
  gamesPlayed: number;
  newRequests: number;
  pendingRequests: number;
  pendingReports: number;
}

export interface StorageReport {
  files: number;
  bytes: number;
  databaseBytes: number | null;
  byOwner: Array<{ group: string; files: number; bytes: number }>;
  largest: Array<{ id: string; contentType: string; bytes: number; owner: string; createdAt: string }>;
}

export interface QuestionAccuracy {
  questionId: number;
  text: string;
  bankName: string;
  answers: number;
  percent: number;
}

@Injectable({ providedIn: 'root' })
export class AdminInsightsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api/admin/insights`;

  weekly(days = 7) {
    return this.http.get<WeeklySummary>(`${this.base}/weekly`, { params: { days } });
  }

  storage() {
    return this.http.get<StorageReport>(`${this.base}/storage`);
  }

  hardestQuestions(take = 30) {
    return this.http.get<QuestionAccuracy[]>(`${this.base}/hardest-questions`, { params: { take } });
  }
}
