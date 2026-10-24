import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../core/config/env';
import { SessionStore } from '../../core/auth/session.store';

const VISITOR_KEY = 'cale.shop.vid';

export type ShopInterestType =
  | 'view_shop'
  | 'view_product'
  | 'favorite'
  | 'unfavorite'
  | 'whatsapp';

/** Fire-and-forget shop interest (views, favorites, WhatsApp). */
@Injectable({ providedIn: 'root' })
export class ShopAnalyticsService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionStore);
  private readonly sent = new Set<string>();

  track(eventType: ShopInterestType, productId?: number | null): void {
    const visitorKey = this.visitorKey();
    const dedupe = `${eventType}:${productId ?? 0}:${Math.floor(Date.now() / 60_000)}`;
    if (eventType.startsWith('view_') && this.sent.has(dedupe)) {
      return;
    }
    if (eventType.startsWith('view_')) {
      this.sent.add(dedupe);
    }

    this.http
      .post(
        `${env.apiUrl}/api/public/shop/interest`,
        { eventType, visitorKey, productId: productId ?? null },
        { withCredentials: true }
      )
      .subscribe({ error: () => { /* ignore analytics failures */ } });
  }

  private visitorKey(): string {
    const userId = this.session.user()?.id;
    if (userId) {
      return `u${userId}`;
    }
    try {
      let id = localStorage.getItem(VISITOR_KEY);
      if (!id || id.length < 8) {
        id = `a${crypto.randomUUID().replace(/-/g, '').slice(0, 24)}`;
        localStorage.setItem(VISITOR_KEY, id);
      }
      return id.slice(0, 64);
    } catch {
      return `t${Date.now().toString(36)}${Math.random().toString(36).slice(2, 10)}`;
    }
  }
}
