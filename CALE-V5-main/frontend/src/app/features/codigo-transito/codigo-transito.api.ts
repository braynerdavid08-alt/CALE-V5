import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../core/config/env';

export interface CodigoMeta {
  title?: string;
  sourceBase?: string;
  lawHint?: string;
  articleCount?: number;
  attribution?: string;
  publishedAt?: string;
}

export interface CodigoArticleSummary {
  number: number;
  name: string | null;
  sourceUrl?: string | null;
  preview?: string | null;
  notes?: string[];
  contentHash?: string | null;
}

export interface CodigoNote {
  variant?: string | null;
  text?: string | null;
}

export interface CodigoBlock {
  type?: string;
  variant?: string;
  content?: Array<{ text?: string } | string> | unknown;
  items?: unknown[];
}

export interface CodigoArticle {
  number: number;
  name: string | null;
  sourceUrl?: string | null;
  contentSource?: string | null;
  blocks: CodigoBlock[];
  plainText: string;
  notes: CodigoNote[];
  flags?: string[];
  contentHash?: string | null;
}

export interface CodigoIndexResponse {
  meta: CodigoMeta;
  articles: CodigoArticleSummary[];
}

export interface CodigoArticleResponse {
  meta: CodigoMeta;
  article: CodigoArticle;
  prev: { number: number; name: string | null } | null;
  next: { number: number; name: string | null } | null;
}

@Injectable({ providedIn: 'root' })
export class CodigoTransitoApi {
  private readonly http = inject(HttpClient);
  private readonly base = env.apiUrl;

  index(q?: string) {
    const query = q?.trim() ? `?q=${encodeURIComponent(q.trim())}` : '';
    return this.http.get<CodigoIndexResponse>(`${this.base}/api/codigo-transito${query}`);
  }

  article(number: number) {
    return this.http.get<CodigoArticleResponse>(`${this.base}/api/codigo-transito/${number}`);
  }
}
