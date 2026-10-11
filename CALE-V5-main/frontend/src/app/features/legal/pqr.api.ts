import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../core/config/env';

export type PqrStatus = 'PENDIENTE' | 'EN_REVISION' | 'RESPONDIDA' | 'RECHAZADA' | 'ESCALADA';

export interface PqrKind {
  key: string;
  label: string;
  isDataRight: boolean;
  deadlineDays: number;
}

export interface PqrEvent {
  action: string;
  fromStatus: PqrStatus | null;
  toStatus: PqrStatus | null;
  note: string | null;
  visibleToRequester: boolean;
  createdAt: string;
}

export interface Pqr {
  id: number;
  radicado: string;
  kind: string;
  kindLabel: string;
  status: PqrStatus;
  subject: string;
  message: string;
  response: string | null;
  dueAt: string;
  extended: boolean;
  createdAt: string;
  closedAt: string | null;
  events: PqrEvent[];
}

export interface AdminPqr extends Pqr {
  isDataRight: boolean;
  userId: number | null;
  userRole: string | null;
  userActive: boolean | null;
  contactName: string;
  contactEmail: string;
  overdue: boolean;
}

export interface PqrCreated {
  radicado: string;
  trackingCode: string;
  dueAt: string;
  emailSent: boolean;
}

export interface CreatePqr {
  kind: string;
  name?: string;
  email?: string;
  subject: string;
  message: string;
}

export const PQR_STATUS_LABEL: Record<PqrStatus, string> = {
  PENDIENTE: 'Pendiente',
  EN_REVISION: 'En revisión',
  RESPONDIDA: 'Respondida',
  RECHAZADA: 'Rechazada con justificación',
  ESCALADA: 'Escalada'
};

export const PQR_EVENT_LABEL: Record<string, string> = {
  Created: 'Solicitud radicada',
  StatusChanged: 'Cambio de estado',
  Extended: 'Plazo prorrogado',
  Note: 'Nota interna'
};

@Injectable({ providedIn: 'root' })
export class PqrApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api/pqr`;
  private readonly admin = `${env.apiUrl}/api/admin/pqr`;

  kinds() {
    return this.http.get<PqrKind[]>(`${this.base}/kinds`);
  }

  create(body: CreatePqr) {
    return this.http.post<PqrCreated>(this.base, body);
  }

  track(code: string) {
    return this.http.get<Pqr>(`${this.base}/track/${encodeURIComponent(code.trim())}`);
  }

  mine() {
    return this.http.get<Pqr[]>(`${this.base}/mine`);
  }

  adminList(status?: string, kind?: string) {
    const params: Record<string, string> = {};
    if (status) params['status'] = status;
    if (kind) params['kind'] = kind;
    return this.http.get<AdminPqr[]>(this.admin, { params });
  }

  update(id: number, status: PqrStatus, response: string | null, internalNote: string | null) {
    return this.http.put<void>(`${this.admin}/${id}`, { status, response, internalNote });
  }

  extend(id: number, reason: string) {
    return this.http.post<void>(`${this.admin}/${id}/extend`, { reason });
  }
}
