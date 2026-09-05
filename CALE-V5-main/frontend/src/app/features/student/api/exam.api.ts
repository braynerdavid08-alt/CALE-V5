import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../../core/config/env';

export interface BankDto {
  id: number;
  name: string;
  description?: string | null;
  isActive: boolean;
  questionCount: number;
}

export interface ExamDto {
  id: number;
  name: string;
  description?: string | null;
  bankId?: number | null;
  questionCount: number;
  timeMinutes: number;
  allowedAttempts: number;
  randomize?: boolean;
  published: boolean;
  startsAt?: string | null;
  endsAt?: string | null;
}

export interface TakeOptionDto {
  id: number;
  text: string;
  imageUrl?: string | null;
}

export interface TakeQuestionDto {
  id: number;
  order: number;
  text: string;
  type: string;
  imageUrl?: string | null;
  options: TakeOptionDto[];
}

export interface StartExamResponse {
  attemptId: number;
  startedAt: string;
  expiresAt?: string | null;
  timeMinutes: number;
  questions: TakeQuestionDto[];
}

export interface FinishResponse {
  attemptId: number;
  totalQuestions: number;
  correctCount: number;
  percent: number;
  passed: boolean;
  timeSeconds: number;
  byTopic: ScoreBreakdownDto[];
  byBlock: ScoreBreakdownDto[];
  bestPercent?: number | null;
}

export interface ScoreBreakdownDto {
  label: string;
  correctCount: number;
  totalQuestions: number;
  percent: number;
}

export interface ReviewOptionDto {
  id: number;
  text: string;
  isCorrect: boolean;
  selected: boolean;
  imageUrl?: string | null;
}

export interface ReviewQuestionDto {
  id: number;
  order: number;
  text: string;
  type: string;
  imageUrl?: string | null;
  explanation?: string | null;
  isCorrect: boolean;
  options: ReviewOptionDto[];
}

export interface ReviewResponse {
  result: FinishResponse;
  questions: ReviewQuestionDto[];
}

@Injectable({ providedIn: 'root' })
export class ExamApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api`;

  banks() {
    return this.http.get<BankDto[]>(`${this.base}/banks?activeOnly=true`);
  }

  published() {
    return this.http.get<ExamDto[]>(`${this.base}/exams/published`);
  }

  start(body: {
    bankId?: number | null;
    examId?: number | null;
    questionCount: number;
    mode: string;
    timeMinutes: number;
  }) {
    return this.http.post<StartExamResponse>(`${this.base}/exams/start`, body);
  }

  answer(attemptId: number, questionId: number, optionId: number) {
    return this.http.post<void>(`${this.base}/exams/${attemptId}/answer`, {
      questionId,
      optionId
    });
  }

  finish(attemptId: number) {
    return this.http.post<FinishResponse>(
      `${this.base}/exams/${attemptId}/finish`,
      {}
    );
  }

  review(attemptId: number) {
    return this.http.get<ReviewResponse>(
      `${this.base}/exams/${attemptId}/review`
    );
  }

  rate(attemptId: number, stars: number, comment?: string) {
    return this.http.post<void>(`${this.base}/ratings`, {
      attemptId,
      stars,
      comment
    });
  }
}
