import { Injectable, computed, inject, signal } from '@angular/core';
import { ShopAnalyticsService } from './shop-analytics.service';

const FAV_KEY = 'cale.shop.favorites';

@Injectable({ providedIn: 'root' })
export class ShopFavoritesService {
  private readonly analytics = inject(ShopAnalyticsService);
  private readonly ids = signal<number[]>(readFavs());

  readonly list = this.ids.asReadonly();
  readonly count = computed(() => this.ids().length);

  isFavorite(id: number): boolean {
    return this.ids().includes(id);
  }

  toggle(id: number): void {
    this.ids.update((list) => {
      const removing = list.includes(id);
      const next = removing ? list.filter((x) => x !== id) : [...list, id];
      writeFavs(next);
      this.analytics.track(removing ? 'unfavorite' : 'favorite', id);
      return next;
    });
  }
}

function readFavs(): number[] {
  try {
    const raw = localStorage.getItem(FAV_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as unknown;
    return Array.isArray(parsed) ? parsed.filter((x) => typeof x === 'number') : [];
  } catch {
    return [];
  }
}

function writeFavs(ids: number[]): void {
  try {
    localStorage.setItem(FAV_KEY, JSON.stringify(ids));
  } catch {
    /* ignore quota */
  }
}
