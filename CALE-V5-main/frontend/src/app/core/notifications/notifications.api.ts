import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs/operators';
import { env } from '../../core/config/env';

export interface NotificationDto {
  id: number;
  title: string;
  message: string;
  type: string;
  category: string;
  isRead: boolean;
  createdAt: string;
  readAt?: string | null;
  groupId?: number | null;
  relatedEntity?: string | null;
  relatedId?: number | null;
  link?: string | null;
  priority: string;
}

export interface NotificationListResponse {
  items: NotificationDto[];
  unreadCount: number;
}

export interface NotificationPreferenceDto {
  academicEnabled: boolean;
  membershipEnabled: boolean;
  adminEnabled: boolean;
  systemEnabled: boolean;
}

export interface BroadcastNotificationRequest {
  title: string;
  message: string;
  type: string;
  priority?: string | null;
  link?: string | null;
  audience: string;
  groupId?: number | null;
  userIds?: number[] | null;
}

@Injectable({ providedIn: 'root' })
export class NotificationsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api/notifications`;

  list(opts?: {
    unreadOnly?: boolean;
    category?: string;
    type?: string;
    skip?: number;
    take?: number;
  }) {
    let params = new HttpParams();
    if (opts?.unreadOnly != null) {
      params = params.set('unreadOnly', String(opts.unreadOnly));
    }
    if (opts?.category) {
      params = params.set('category', opts.category);
    }
    if (opts?.type) {
      params = params.set('type', opts.type);
    }
    if (opts?.skip != null) {
      params = params.set('skip', String(opts.skip));
    }
    if (opts?.take != null) {
      params = params.set('take', String(opts.take));
    }
    return this.http.get<NotificationListResponse>(this.base, { params });
  }

  /** Convenience: recent items only. */
  recent(take = 8) {
    return this.list({ take }).pipe(map((r) => r.items));
  }

  unreadCount() {
    return this.http
      .get<{ count: number }>(`${this.base}/unread-count`)
      .pipe(map((r) => r.count));
  }

  markRead(id: number) {
    return this.http.post<void>(`${this.base}/${id}/read`, {});
  }

  markAllRead() {
    return this.http.post<{ marked: number }>(`${this.base}/read-all`, {});
  }

  archive(id: number) {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  preferences() {
    return this.http.get<NotificationPreferenceDto>(`${this.base}/preferences`);
  }

  updatePreferences(body: NotificationPreferenceDto) {
    return this.http.put<NotificationPreferenceDto>(
      `${this.base}/preferences`,
      body
    );
  }

  broadcast(body: BroadcastNotificationRequest) {
    return this.http.post<{ sent: number; audience: string }>(
      `${this.base}/broadcast`,
      body
    );
  }
}

export function notificationRelativeTime(
  iso: string,
  now = Date.now()
): string {
  const t = new Date(iso).getTime();
  if (Number.isNaN(t)) {
    return '';
  }
  const diffSec = Math.round((now - t) / 1000);
  if (diffSec < 45) {
    return 'Ahora';
  }
  if (diffSec < 3600) {
    const m = Math.max(1, Math.round(diffSec / 60));
    return `Hace ${m} min`;
  }
  if (diffSec < 86400) {
    const h = Math.max(1, Math.round(diffSec / 3600));
    return `Hace ${h} h`;
  }
  if (diffSec < 172800) {
    return 'Ayer';
  }
  return new Date(iso).toLocaleDateString('es-CO', {
    day: 'numeric',
    month: 'long'
  });
}

export function notificationTypeLabel(type: string): string {
  switch (type) {
    case 'announcement':
      return 'Aviso';
    case 'material':
      return 'Material';
    case 'activity':
      return 'Actividad';
    case 'exam':
      return 'Examen';
    case 'exam_result':
      return 'Resultado';
    case 'grade':
      return 'Calificación';
    case 'submission':
      return 'Entrega';
    case 'membership':
      return 'Membresía';
    case 'admin':
      return 'Administración';
    case 'system':
      return 'Sistema';
    default:
      return 'Aviso';
  }
}
