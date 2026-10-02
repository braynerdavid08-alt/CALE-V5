import { Injectable, signal } from '@angular/core';

const STORAGE_PREFIX = 'cale.textSize.';

/**
 * "Letra grande": scales the whole app (everything is in rem) and raises contrast.
 * Saved per user on this device; schools get it by default until they turn it off.
 */
@Injectable({ providedIn: 'root' })
export class TextSizeService {
  readonly large = signal(false);
  private userKey: string | null = null;

  /** Call when a signed-in user enters the app. */
  init(userId: number | string | undefined, role: string | undefined): void {
    this.userKey = userId != null ? `${STORAGE_PREFIX}${userId}` : null;
    const stored = this.read();
    this.apply(stored === null ? role === 'School' : stored === 'large');
  }

  toggle(): void {
    const next = !this.large();
    this.apply(next);
    if (this.userKey) {
      try {
        localStorage.setItem(this.userKey, next ? 'large' : 'normal');
      } catch {
        /* ignore quota / private mode */
      }
    }
  }

  /** Back to normal size when leaving the signed-in area. */
  reset(): void {
    this.userKey = null;
    this.apply(false);
  }

  private read(): string | null {
    if (!this.userKey) return null;
    try {
      return localStorage.getItem(this.userKey);
    } catch {
      return null;
    }
  }

  private apply(large: boolean): void {
    this.large.set(large);
    if (large) {
      document.documentElement.setAttribute('data-size', 'large');
    } else {
      document.documentElement.removeAttribute('data-size');
    }
  }
}
