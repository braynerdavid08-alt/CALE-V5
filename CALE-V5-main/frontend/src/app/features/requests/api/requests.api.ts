import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { switchMap, tap } from 'rxjs/operators';
import { compressImage } from '../../../core/media/compress-image';
import { env } from '../../../core/config/env';

export const QUESTION_TYPE_MC = 'Seleccion multiple';
export const QUESTION_TYPE_TF = 'Verdadero/Falso';

export interface QuestionDraftOption {
  text: string;
  isCorrect: boolean;
  imageUrl: string;
}

export interface QuestionDraft {
  text: string;
  type: string;
  topic: string;
  imageUrl: string;
  explanation: string;
  options: QuestionDraftOption[];
}

export type UserRequestKind = 'question' | 'idea' | 'report';
export type UserRequestStatus = 'Pending' | 'Accepted' | 'Rejected';

export interface UserRequestDto {
  id: number;
  userId: number;
  userName: string;
  userRole: string;
  kind: UserRequestKind;
  status: UserRequestStatus;
  title: string | null;
  message: string | null;
  question: {
    text: string;
    type: string;
    topic: string | null;
    imageUrl: string | null;
    explanation: string | null;
    options: Array<{ text: string | null; isCorrect: boolean; imageUrl: string | null }>;
  } | null;
  adminNote: string | null;
  reviewedAt: string | null;
  createdQuestionId: number | null;
  createdAt: string;
  reportedQuestionId?: number | null;
}

export interface SimilarQuestion {
  questionId: number;
  text: string;
  bankName: string;
  percent: number;
}

export interface BlockedUser {
  userId: number;
  name: string;
  email: string;
  reason: string | null;
  createdAt: string;
}

export interface MyRequestStatus {
  blocked: boolean;
  reason: string | null;
}

export interface UserRequestCounts {
  pending: number;
  accepted: number;
  rejected: number;
}

export function emptyDraft(): QuestionDraft {
  return {
    text: '',
    type: QUESTION_TYPE_MC,
    topic: '',
    imageUrl: '',
    explanation: '',
    options: [
      { text: '', isCorrect: true, imageUrl: '' },
      { text: '', isCorrect: false, imageUrl: '' },
      { text: '', isCorrect: false, imageUrl: '' },
      { text: '', isCorrect: false, imageUrl: '' }
    ]
  };
}

export function draftFromDto(q: NonNullable<UserRequestDto['question']>): QuestionDraft {
  return {
    text: q.text,
    type: q.type,
    topic: q.topic ?? '',
    imageUrl: q.imageUrl ?? '',
    explanation: q.explanation ?? '',
    options: q.options.map((o) => ({ text: o.text ?? '', isCorrect: o.isCorrect, imageUrl: o.imageUrl ?? '' }))
  };
}

/** Same rules as the backend; returns a user-facing error or null. */
export function validateDraft(d: QuestionDraft): string | null {
  if (d.text.trim().length < 5) return 'Escribe el enunciado de la pregunta.';
  if (d.options.length < 2) return 'Agrega al menos dos respuestas.';
  if (d.options.filter((o) => o.isCorrect).length !== 1) return 'Marca exactamente una respuesta correcta.';
  if (d.options.some((o) => !o.text.trim() && !o.imageUrl)) return 'Cada respuesta necesita texto o imagen.';
  return null;
}

export function draftBody(d: QuestionDraft) {
  return {
    text: d.text.trim(),
    type: d.type,
    topic: d.topic.trim() || null,
    imageUrl: d.imageUrl || null,
    explanation: d.explanation.trim() || null,
    options: d.options.map((o) => ({ text: o.text.trim(), isCorrect: o.isCorrect, imageUrl: o.imageUrl || null }))
  };
}

@Injectable({ providedIn: 'root' })
export class RequestsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api/requests`;

  create(body: {
    kind: UserRequestKind;
    title?: string | null;
    message?: string | null;
    question?: unknown;
    questionId?: number;
  }) {
    return this.http.post<UserRequestDto>(this.base, body);
  }

  mine() {
    return this.http.get<UserRequestDto[]>(`${this.base}/mine`);
  }

  myStatus() {
    return this.http.get<MyRequestStatus>(`${this.base}/mine/status`);
  }

  similar(text: string) {
    return this.http.post<SimilarQuestion[]>(`${this.base}/admin/similar`, { text });
  }

  blockedUsers() {
    return this.http.get<BlockedUser[]>(`${this.base}/admin/blocked`);
  }

  blockUser(userId: number, reason: string | null) {
    return this.http.post<{ rejected: number }>(`${this.base}/admin/blocked/${userId}`, { reason });
  }

  unblockUser(userId: number) {
    return this.http.delete<void>(`${this.base}/admin/blocked/${userId}`);
  }

  cancel(id: number) {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  adminList(status?: string, kind?: string) {
    const params: Record<string, string> = {};
    if (status) params['status'] = status;
    if (kind) params['kind'] = kind;
    return this.http.get<UserRequestDto[]>(`${this.base}/admin`, { params });
  }

  /** Last known pending count; feeds the admin menu badge. */
  readonly pendingCount = signal(0);

  adminCounts() {
    return this.http
      .get<UserRequestCounts>(`${this.base}/admin/counts`)
      .pipe(tap((c) => this.pendingCount.set(c.pending)));
  }

  accept(id: number, body: { note?: string | null; bankId?: number | null; blockId?: number | null; question?: unknown }) {
    return this.http.post<UserRequestDto>(`${this.base}/${id}/accept`, body);
  }

  reject(id: number, note: string | null) {
    return this.http.post<UserRequestDto>(`${this.base}/${id}/reject`, { note });
  }

  upload(file: File) {
    return compressImage(file).pipe(
      switchMap((small) => {
        const data = new FormData();
        data.append('file', small);
        return this.http.post<{ url: string }>(`${env.apiUrl}/api/media/upload`, data);
      })
    );
  }
}
