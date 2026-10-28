import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../config/env';

export interface AssistantExamSlotUi {
  time: string;
  availableCapacity: number;
  endTime?: string | null;
  detail?: string | null;
  sessionId?: number | null;
  bookTool?: string | null;
}

export interface AssistantExamDayUi {
  date: string;
  availableSlots: AssistantExamSlotUi[];
}

/** Structured card for exam / theory / practical slots (not free-form LLM HTML). */
export interface AssistantExamAvailabilityUi {
  type: 'exam_availability' | 'theory_week' | 'practical_slots' | string;
  title: string;
  dateRangeFrom: string;
  dateRangeTo: string;
  canBook: boolean;
  blockReason?: string | null;
  myBookingSummary?: string | null;
  selectable: boolean;
  days: AssistantExamDayUi[];
}

export interface AssistantMessage {
  role: 'user' | 'assistant';
  content: string;
  examAvailability?: AssistantExamAvailabilityUi | null;
  /** How many day cards are visible (expandable). */
  daysVisible?: number;
}

export interface AssistantPendingAction {
  id: string;
  title: string;
}

export interface AssistantChatResult {
  reply: string;
  action: AssistantPendingAction | null;
  remainingToday: number;
  examAvailability?: AssistantExamAvailabilityUi | null;
}

export interface AssistantActionResult {
  ok: boolean;
  message: string;
}

export interface AssistantStatus {
  enabled: boolean;
  remainingToday: number;
  limitToday?: number;
  nearLimit?: boolean;
  atLimit?: boolean;
}

export interface AssistantSchoolUsageSummary {
  day: string;
  schoolUserId: number;
  totalMessages: number;
  studentCount: number;
  students: Array<{
    userId: number;
    userName: string;
    role: string;
    messagesUsed: number;
    messagesLimit: number;
    messagesRemaining: number;
  }>;
}

@Injectable({ providedIn: 'root' })
export class AssistantApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api/assistant`;

  status() {
    return this.http.get<AssistantStatus>(`${this.base}/status`);
  }

  chat(messages: AssistantMessage[]) {
    const payload = messages.map(({ role, content }) => ({ role, content }));
    return this.http.post<AssistantChatResult>(`${this.base}/chat`, { messages: payload });
  }

  quick(key: string) {
    return this.http.get<AssistantChatResult>(`${this.base}/quick/${encodeURIComponent(key)}`);
  }

  confirm(actionId: string) {
    return this.http.post<AssistantActionResult>(`${this.base}/actions/${actionId}/confirm`, {});
  }

  /** Prepare agendar_examen from a tapped slot (still requires confirm). */
  prepareExam(date: string, time: string) {
    return this.http.post<AssistantChatResult>(`${this.base}/actions/prepare-exam`, { date, time });
  }

  /** Prepare theory/practical reservation from a tapped session card. */
  prepareSession(sessionId: number, tool: string) {
    return this.http.post<AssistantChatResult>(`${this.base}/actions/prepare-session`, {
      sessionId,
      tool
    });
  }

  discard(actionId: string) {
    return this.http.post<void>(`${this.base}/actions/${actionId}/discard`, {});
  }

  schoolUsage() {
    return this.http.get<AssistantSchoolUsageSummary>(`${this.base}/usage/school`);
  }

  adminUsage(schoolUserId?: number) {
    const q = schoolUserId != null ? `?schoolUserId=${schoolUserId}` : '';
    return this.http.get<AssistantSchoolUsageSummary | AssistantSchoolUsageSummary[]>(
      `${this.base}/usage/admin${q}`
    );
  }
}
