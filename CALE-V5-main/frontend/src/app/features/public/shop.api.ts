import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { switchMap } from 'rxjs';
import { env } from '../../core/config/env';
import { compressImage } from '../../core/media/compress-image';
import {
  AdminShopDto,
  AdminShopProductDto,
  PublicShopDto,
  PublicShopProductDto,
  UpdateShopSettingsRequest,
  UpsertShopProductRequest
} from './shop.models';

@Injectable({ providedIn: 'root' })
export class ShopApi {
  private readonly http = inject(HttpClient);
  private readonly base = env.apiUrl;

  getPublic() {
    return this.http.get<PublicShopDto>(`${this.base}/api/public/shop`);
  }

  getPublicBySlug(slug: string) {
    return this.http.get<PublicShopProductDto>(
      `${this.base}/api/public/shop/${encodeURIComponent(slug)}`
    );
  }

  getAdmin() {
    return this.http.get<AdminShopDto>(`${this.base}/api/admin/shop`);
  }

  saveSettings(body: UpdateShopSettingsRequest) {
    return this.http.put<AdminShopDto>(`${this.base}/api/admin/shop/settings`, body);
  }

  createProduct(body: UpsertShopProductRequest) {
    return this.http.post<AdminShopProductDto>(`${this.base}/api/admin/shop/products`, body);
  }

  updateProduct(id: number, body: UpsertShopProductRequest) {
    return this.http.put<AdminShopProductDto>(`${this.base}/api/admin/shop/products/${id}`, body);
  }

  deleteProduct(id: number) {
    return this.http.delete<void>(`${this.base}/api/admin/shop/products/${id}`);
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
