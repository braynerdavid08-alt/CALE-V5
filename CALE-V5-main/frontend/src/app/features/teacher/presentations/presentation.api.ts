import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { env } from '../../../core/config/env';
import {
  PresentationDetail,
  PresentationListItem,
  PresentationSummary
} from './presentation.models';

@Injectable({ providedIn: 'root' })
export class PresentationApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api/presentations`;

  list() {
    return this.http.get<PresentationListItem[]>(this.base);
  }

  summary() {
    return this.http.get<PresentationSummary>(`${this.base}/summary`);
  }

  get(id: number) {
    return this.http.get<PresentationDetail>(`${this.base}/${id}`);
  }

  create(body: {
    title: string;
    description?: string | null;
    category?: string | null;
    groupId?: number | null;
    templateKey?: string | null;
  }) {
    return this.http.post<PresentationDetail>(this.base, body);
  }

  saveDocument(
    id: number,
    body: {
      title: string;
      description?: string | null;
      category?: string | null;
      groupId?: number | null;
      thumbnailUrl?: string | null;
      slides: {
        id?: number | null;
        title: string;
        notes?: string | null;
        backgroundJson: string;
        elementsJson: string;
      }[];
    }
  ) {
    return this.http.put<PresentationDetail>(`${this.base}/${id}/document`, body);
  }

  updateMeta(
    id: number,
    body: {
      title: string;
      description?: string | null;
      category?: string | null;
      groupId?: number | null;
    }
  ) {
    return this.http.put<void>(`${this.base}/${id}/meta`, body);
  }

  duplicate(id: number) {
    return this.http.post<PresentationDetail>(`${this.base}/${id}/duplicate`, {});
  }

  delete(id: number) {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  upload(file: File) {
    const fd = new FormData();
    fd.append('file', file);
    return this.http.post<{ url: string }>(`${this.base}/upload`, fd);
  }

  downloadImportTemplate(format: 'xlsx' | 'docx') {
    return this.http.get(`${this.base}/import/template`, {
      params: { format },
      responseType: 'blob'
    });
  }

  importFile(file: File, body: { title?: string; description?: string | null; category?: string | null }) {
    const fd = new FormData();
    fd.append('file', file);
    if (body.title) fd.append('title', body.title);
    if (body.description) fd.append('description', body.description);
    if (body.category) fd.append('category', body.category);
    return this.http.post<PresentationDetail>(`${this.base}/import`, fd);
  }

  exportFile(id: number, format: 'xlsx' | 'docx') {
    return this.http.get(`${this.base}/${id}/export`, {
      params: { format },
      responseType: 'blob'
    });
  }
}
