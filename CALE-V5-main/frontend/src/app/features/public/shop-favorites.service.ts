import { Injectable, computed, signal } from '@angular/core';

const FAV_KEY = 'cale.shop.favorites';

@Injectable({ providedIn: 'root' })
export class ShopFavoritesService {
  private readonly ids = signal<number[]>(readFavs());

  readonly list = this.ids.asReadonly();
  readonly count = computed(() => this.ids().length);

  isFavorite(id: number): boolean {
    return this.ids().includes(id);
  }

  toggle(id: number): void {
    this.ids.update((list) => {
      const next = list.includes(id) ? list.filter((x) => x !== id) : [...list, id];
      writeFavs(next);
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
