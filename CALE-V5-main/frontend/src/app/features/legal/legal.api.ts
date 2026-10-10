import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../core/config/env';

export type LegalDocKey = 'terms' | 'privacy' | 'data-processing' | 'ai-policy' | 'school-terms';

export interface LegalDocumentSummary {
  key: LegalDocKey;
  title: string;
  version: string | null;
  publishedAt: string | null;
  isDraft: boolean;
}

export interface LegalDocument {
  key: LegalDocKey;
  title: string;
  version: string;
  content: string;
  contentHash: string;
  publishedAt: string | null;
  isDraft: boolean;
}

export interface LegalRequirement {
  key: LegalDocKey;
  title: string;
  action: 'Accepted' | 'Acknowledged' | 'Authorized';
  status: 'ACCEPTED' | 'OUTDATED' | 'NOT_VERIFIED';
  currentVersion: string | null;
  acceptedVersion: string | null;
  acceptedAt: string | null;
}

export interface MyLegalStatus {
  allAccepted: boolean;
  documents: LegalRequirement[];
}

export interface LegalConsent {
  acceptTerms: boolean;
  acknowledgePrivacy: boolean;
  authorizeDataProcessing: boolean;
  acceptSchoolTerms?: boolean;
}

export interface LegalAcceptance {
  id: number;
  userId: number;
  userName: string | null;
  userEmail: string | null;
  documentKey: LegalDocKey;
  version: string;
  action: string;
  method: string;
  ipAddress: string | null;
  userAgent: string | null;
  createdAt: string;
}

export interface LegalVersion {
  id: number;
  documentKey: LegalDocKey;
  version: string;
  title: string;
  content: string;
  contentHash: string;
  status: 'Draft' | 'Published' | 'Retired';
  requiresReacceptance: boolean;
  hasPlaceholders: boolean;
  changeSummary: string | null;
  createdAt: string;
  publishedAt: string | null;
  acceptances: number;
}

export interface SaveLegalVersion {
  documentKey: LegalDocKey;
  version: string;
  title: string;
  content: string;
  changeSummary: string | null;
  requiresReacceptance: boolean;
}

export interface LegalSummary {
  key: LegalDocKey;
  title: string;
  currentVersion: string | null;
  isDraft: boolean;
  accepted: number;
  outdated: number;
  notVerified: number;
}

export type GuardianStatus = 'PENDING' | 'CONFIRMED' | 'REJECTED' | 'REVOKED';

export const GUARDIAN_RELATIONSHIPS = ['Madre', 'Padre', 'Tutor legal'] as const;

export interface Guardian {
  name: string;
  relationship: string;
  email: string;
}

export interface MyAgeStatus {
  required: boolean;
  declared: boolean;
  isMinor: boolean;
  guardianStatus: GuardianStatus | null;
  guardianName: string | null;
  guardianRelationship: string | null;
  guardianEmailMasked: string | null;
  guardianLinkExpiresAt: string | null;
  canResend: boolean;
}

export interface GuardianView {
  minorFirstName: string;
  guardianName: string;
  guardianRelationship: string;
  status: GuardianStatus;
  linkExpired: boolean;
  documents: LegalDocumentSummary[];
}

export interface GuardianEvent {
  action: string;
  documentVersions: string | null;
  ipAddress: string | null;
  note: string | null;
  createdAt: string;
}

export interface AdminMinor {
  userId: number;
  userName: string | null;
  userEmail: string | null;
  userActive: boolean;
  ageToday: number;
  guardianStatus: GuardianStatus;
  guardianName: string | null;
  guardianRelationship: string | null;
  guardianEmail: string | null;
  guardianLinkExpiresAt: string | null;
  guardianDecidedAt: string | null;
  createdAt: string;
  events: GuardianEvent[];
}

export const GUARDIAN_STATUS_LABEL: Record<GuardianStatus, string> = {
  PENDING: 'Esperando al representante',
  CONFIRMED: 'Autorizado',
  REJECTED: 'No autorizado',
  REVOKED: 'Autorización revocada'
};

/** Edad cumplida hoy a partir de una fecha "yyyy-MM-dd". */
export function ageFromBirthDate(value: string, today = new Date()): number | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!match) return null;
  const [y, m, d] = [Number(match[1]), Number(match[2]), Number(match[3])];
  let age = today.getFullYear() - y;
  if (today.getMonth() + 1 < m || (today.getMonth() + 1 === m && today.getDate() < d)) age--;
  return age;
}

/** Texto de cada casilla: qué significa marcarla, según el documento. */
export const CONSENT_LABELS: Record<LegalDocKey, string> = {
  terms: 'Acepto los Términos y Condiciones',
  privacy: 'He leído el Aviso de Privacidad',
  'data-processing': 'Autorizo el tratamiento de mis datos personales según la Política de Tratamiento de Datos',
  'ai-policy': 'He leído la Política de uso de Inteligencia Artificial',
  'school-terms': 'Acepto las Condiciones para escuelas (CEA)'
};

@Injectable({ providedIn: 'root' })
export class LegalApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api/legal`;
  private readonly admin = `${env.apiUrl}/api/admin/legal`;

  documents() {
    return this.http.get<LegalDocumentSummary[]>(`${this.base}/documents`);
  }

  document(key: string) {
    return this.http.get<LegalDocument>(`${this.base}/documents/${encodeURIComponent(key)}`);
  }

  myStatus() {
    return this.http.get<MyLegalStatus>(`${this.base}/me`);
  }

  myHistory() {
    return this.http.get<LegalAcceptance[]>(`${this.base}/me/history`);
  }

  accept(consent: LegalConsent) {
    return this.http.post<MyLegalStatus>(`${this.base}/me/accept`, consent);
  }

  summary() {
    return this.http.get<LegalSummary[]>(`${this.admin}/summary`);
  }

  versions() {
    return this.http.get<LegalVersion[]>(`${this.admin}/versions`);
  }

  createDraft(body: SaveLegalVersion) {
    return this.http.post<LegalVersion>(`${this.admin}/versions`, body);
  }

  updateDraft(id: number, body: SaveLegalVersion) {
    return this.http.put<LegalVersion>(`${this.admin}/versions/${id}`, body);
  }

  publish(id: number) {
    return this.http.post<LegalVersion>(`${this.admin}/versions/${id}/publish`, {});
  }

  acceptances(document?: string) {
    const query = document ? `?document=${encodeURIComponent(document)}` : '';
    return this.http.get<LegalAcceptance[]>(`${this.admin}/acceptances${query}`);
  }

  acceptancesCsv() {
    return this.http.get(`${this.admin}/acceptances.csv`, { responseType: 'blob' });
  }

  myAge() {
    return this.http.get<MyAgeStatus>(`${this.base}/me/age`);
  }

  declareAge(birthDate: string, guardian: Guardian | null) {
    return this.http.post<MyAgeStatus>(`${this.base}/me/age`, { birthDate, guardian });
  }

  updateGuardian(guardian: Guardian) {
    return this.http.post<MyAgeStatus>(`${this.base}/me/guardian`, guardian);
  }

  guardianView(token: string) {
    return this.http.get<GuardianView>(`${this.base}/guardian/${encodeURIComponent(token)}`);
  }

  guardianDecision(token: string, decision: 'authorize' | 'reject' | 'revoke', confirmIsGuardian: boolean) {
    return this.http.post<GuardianView>(`${this.base}/guardian/${encodeURIComponent(token)}/decision`, {
      decision,
      confirmIsGuardian
    });
  }

  minors() {
    return this.http.get<AdminMinor[]>(`${this.admin}/minors`);
  }

  resendGuardian(userId: number) {
    return this.http.post<void>(`${this.admin}/minors/${userId}/resend`, {});
  }
}
