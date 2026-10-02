import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../../core/config/env';

export type ProgressMode = 'all' | 'exam' | 'practice';
export type ProgressBand = 'passed' | 'near' | 'failed';
export type ProgressTrend = 'up' | 'down' | 'flat' | 'none';

export interface ProgressAttemptDto {
  number: number;
  attemptId: number;
  mode: string;
  /** UTC instant. */
  finishedAt: string;
  score: number;
  correctCount: number;
  totalQuestions: number;
  passed: boolean;
  band: ProgressBand | string;
  passThreshold: number;
  durationSeconds: number | null;
}

export interface ProgressWeekDto {
  weekStart: string;
  weekEnd: string;
  count: number;
  open: number;
  close: number;
  high: number;
  low: number;
  average: number;
}

export interface StudentProgressDto {
  mode: ProgressMode;
  take: number | null;
  modeCounts: { all: number; exam: number; practice: number };
  totalAttempts: number;
  passedAttempts: number;
  averageScore: number | null;
  bestScore: number | null;
  lastScore: number | null;
  lastFiveAverage: number | null;
  trendWindow: number;
  recentAverage: number | null;
  previousAverage: number | null;
  trend: number | null;
  trendDirection: ProgressTrend | string;
  bestDurationSeconds: number | null;
  averageDurationSeconds: number | null;
  lastDurationSeconds: number | null;
  attemptsWithoutDuration: number;
  /** Null when the attempts have different question counts (each point keeps its own). */
  approvalThreshold: number | null;
  maxIncorrectAnswers: number;
  attempts: ProgressAttemptDto[];
  weeks: ProgressWeekDto[];
}

@Injectable({ providedIn: 'root' })
export class ProgressApi {
  private readonly http = inject(HttpClient);

  /** Own progress, or a student of the authenticated school when <paramref name="studentUserId"/> is set. */
  get(opts: { mode: ProgressMode; take: number | null; studentUserId?: number | null }) {
    let params = new HttpParams().set('mode', opts.mode);
    if (opts.take) params = params.set('take', opts.take);
    const url = opts.studentUserId
      ? `${env.apiUrl}/api/school/apprentices/${opts.studentUserId}/progress`
      : `${env.apiUrl}/api/student/progress`;
    return this.http.get<StudentProgressDto>(url, { params });
  }
}
