import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../config/env';

export interface AssistantMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface AssistantPendingAction {
  id: string;
  title: string;
}

export interface AssistantChatResult {
  reply: string;
  action: AssistantPendingAction | null;
  remainingToday: number;
}

export interface AssistantActionResult {
  ok: boolean;
  message: string;
}

export interface AssistantStatus {
  enabled: boolean;
  remainingToday: number;
}

@Injectable({ providedIn: 'root' })
export class AssistantApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api/assistant`;

  status() {
    return this.http.get<AssistantStatus>(`${this.base}/status`);
  }

  chat(messages: AssistantMessage[]) {
    return this.http.post<AssistantChatResult>(`${this.base}/chat`, { messages });
  }

  quick(key: string) {
    return this.http.get<AssistantChatResult>(`${this.base}/quick/${encodeURIComponent(key)}`);
  }

  confirm(actionId: string) {
    return this.http.post<AssistantActionResult>(`${this.base}/actions/${actionId}/confirm`, {});
  }

  discard(actionId: string) {
    return this.http.post<void>(`${this.base}/actions/${actionId}/discard`, {});
  }
}
