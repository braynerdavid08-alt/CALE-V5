import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { switchMap } from 'rxjs';
import { env } from '../../core/config/env';
import { compressImage } from '../../core/media/compress-image';

export interface SchoolCommercialStatsDto {
  pageViews30d: number;
  whatsAppClicks30d: number;
  mapsClicks30d: number;
  phoneClicks30d: number;
}

export interface SchoolCommercialProfileDto {
  schoolUserId: number;
  publicationStatus: string;
  about: string;
  licenseCategories: string;
  coverMediaId: string | null;
  logoMediaId: string | null;
  coverUrl: string | null;
  logoUrl: string | null;
  slug: string | null;
  whatsApp: string | null;
  publicEmail: string | null;
  isSheetComplete: boolean;
  isPublished: boolean;
  isCommerciallySuspended: boolean;
  commercialSuspensionReason: string | null;
  presenceEligible: boolean;
  presenceDenyReason: string | null;
  publishedAt: string | null;
  updatedAt: string;
  publicPath: string | null;
  stats?: SchoolCommercialStatsDto | null;
}

export interface SchoolCommercialPostDto {
  id: number;
  body: string;
  mediaId: string | null;
  mediaUrl: string | null;
  status: string;
  publishedAt: string | null;
  updatedAt: string;
}

export interface SchoolCommercialPromotionDto {
  id: number;
  title: string;
  body: string;
  mediaId: string | null;
  mediaUrl: string | null;
  startsOn: string;
  endsOn: string;
  status: string;
  updatedAt: string;
}

@Injectable({ providedIn: 'root' })
export class SchoolCommercialApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api/school/commercial-profile`;

  getProfile() {
    return this.http.get<SchoolCommercialProfileDto>(this.base);
  }

  saveProfile(body: {
    about: string;
    licenseCategories: string;
    coverMediaId: string | null;
    logoMediaId: string | null;
    whatsApp?: string | null;
    publicEmail?: string | null;
    slug?: string | null;
  }) {
    return this.http.put<SchoolCommercialProfileDto>(this.base, body);
  }

  setPublication(publish: boolean) {
    return this.http.post<SchoolCommercialProfileDto>(`${this.base}/publication`, { publish });
  }

  listPosts() {
    return this.http.get<SchoolCommercialPostDto[]>(`${this.base}/posts`);
  }

  createPost(body: string, mediaId: string | null) {
    return this.http.post<SchoolCommercialPostDto>(`${this.base}/posts`, { body, mediaId });
  }

  setPostPublication(postId: number, publish: boolean) {
    return this.http.post<SchoolCommercialPostDto>(
      `${this.base}/posts/${postId}/publication`,
      { publish }
    );
  }

  deletePost(postId: number) {
    return this.http.delete(`${this.base}/posts/${postId}`);
  }

  listPromotions() {
    return this.http.get<SchoolCommercialPromotionDto[]>(`${this.base}/promotions`);
  }

  createPromotion(body: {
    title: string;
    body: string;
    mediaId: string | null;
    startsOn: string;
    endsOn: string;
  }) {
    return this.http.post<SchoolCommercialPromotionDto>(`${this.base}/promotions`, body);
  }

  setPromotionPublication(promoId: number, publish: boolean) {
    return this.http.post<SchoolCommercialPromotionDto>(
      `${this.base}/promotions/${promoId}/publication`,
      { publish }
    );
  }

  deletePromotion(promoId: number) {
    return this.http.delete(`${this.base}/promotions/${promoId}`);
  }

  uploadMedia(file: File) {
    return compressImage(file).pipe(
      switchMap((small) => {
        const data = new FormData();
        data.append('file', small);
        return this.http.post<{ url: string }>(`${env.apiUrl}/api/media/upload`, data);
      })
    );
  }
}

export function mediaIdFromUrl(url: string | null | undefined): string | null {
  if (!url) return null;
  const m = /\/api\/media\/([0-9a-fA-F-]{36})/.exec(url);
  return m?.[1] ?? null;
}
