import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { switchMap } from 'rxjs';
import { env } from '../../core/config/env';
import { compressImage } from '../../core/media/compress-image';
import {
  AdminHomepageDto,
  PublicHomeDto,
  PublicInstructorCardDto,
  PublicSchoolCardDto,
  PublicTestimonialsDto,
  UpdateHomepageRequest
} from './public.models';

@Injectable({ providedIn: 'root' })
export class PublicHomeApi {
  private readonly http = inject(HttpClient);
  private readonly base = env.apiUrl;

  getHome() {
    return this.http.get<PublicHomeDto>(`${this.base}/api/public/home`);
  }

  listSchools(take = 48) {
    return this.http.get<PublicSchoolCardDto[]>(
      `${this.base}/api/public/schools`,
      { params: { take } }
    );
  }

  listInstructors(take = 48) {
    return this.http.get<PublicInstructorCardDto[]>(
      `${this.base}/api/public/instructors`,
      { params: { take } }
    );
  }

  listTestimonials(take = 6) {
    return this.http.get<PublicTestimonialsDto>(
      `${this.base}/api/public/testimonials`,
      { params: { take } }
    );
  }

  getAdminHomepage() {
    return this.http.get<AdminHomepageDto>(`${this.base}/api/admin/homepage`);
  }

  saveAdminHomepage(body: UpdateHomepageRequest) {
    return this.http.put<AdminHomepageDto>(
      `${this.base}/api/admin/homepage`,
      body
    );
  }

  uploadMedia(file: File) {
    return compressImage(file).pipe(
      switchMap((small) => {
        const data = new FormData();
        data.append('file', small);
        return this.http.post<{ url: string }>(`${this.base}/api/media/upload`, data);
      })
    );
  }
}
