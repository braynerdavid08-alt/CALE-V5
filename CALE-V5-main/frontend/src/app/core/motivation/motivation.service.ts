import { Injectable, computed, signal } from '@angular/core';
import { MOTIVATION_CATALOG } from './motivation.catalog';
import {
  MotivationAudience,
  MotivationMoment,
  MotivationTip
} from './motivation.model';

const STORAGE_KEY = 'cale.motivation.v1';
const ROTATE_MS = 12000;

@Injectable({ providedIn: 'root' })
export class MotivationService {
  private readonly tipId = signal<string>(this.bootstrapId());
  private readonly role = signal<string | null>(null);
  private readonly paused = signal(false);
  private timer: ReturnType<typeof setInterval> | null = null;

  readonly current = computed(
    () => this.findById(this.tipId()) ?? MOTIVATION_CATALOG[0]
  );

  setRole(role?: string | null): void {
    this.role.set(role ?? null);
    this.refreshContext();
    this.ensureTimer();
  }

  setPaused(paused: boolean): void {
    this.paused.set(paused);
  }

  pick(): MotivationTip {
    const pool = this.pool();
    const lastId = this.tipId();
    const candidates = pool.filter((t) => t.id !== lastId);
    const source = candidates.length ? candidates : pool;
    const tip = source[Math.floor(Math.random() * source.length)];
    this.commit(tip.id);
    return tip;
  }

  next(): MotivationTip {
    const pool = this.pool();
    const index = pool.findIndex((t) => t.id === this.tipId());
    const tip = pool[(Math.max(index, 0) + 1) % pool.length];
    this.commit(tip.id);
    return tip;
  }

  refreshContext(): MotivationTip {
    const pool = this.pool();
    const current = this.findById(this.tipId());
    if (current && pool.some((t) => t.id === current.id)) {
      return current;
    }
    return this.pick();
  }

  private ensureTimer(): void {
    if (this.timer) {
      return;
    }
    this.timer = setInterval(() => {
      if (!this.paused()) {
        this.pick();
      }
    }, ROTATE_MS);
  }

  private pool(): MotivationTip[] {
    const audience = this.toAudience(this.role());
    const moment = this.currentMoment();
    const matched = MOTIVATION_CATALOG.filter(
      (t) =>
        (t.audience === 'all' || t.audience === audience)
        && (t.moment === 'any' || t.moment === moment)
    );

    if (matched.length) {
      return matched;
    }

    return MOTIVATION_CATALOG.filter(
      (t) => t.audience === 'all' || t.audience === audience
    );
  }

  private toAudience(role?: string | null): MotivationAudience {
    if (
      role === 'Admin'
      || role === 'School'
      || role === 'Teacher'
      || role === 'Student'
    ) {
      return role;
    }
    return 'all';
  }

  private currentMoment(): MotivationMoment {
    const hour = new Date().getHours();
    if (hour >= 5 && hour < 12) return 'morning';
    if (hour >= 12 && hour < 19) return 'afternoon';
    return 'night';
  }

  private commit(id: string): void {
    this.tipId.set(id);
    try {
      localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({ id, at: Date.now() })
      );
    } catch {
      /* ignore */
    }
  }

  private bootstrapId(): string {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (raw) {
        const parsed = JSON.parse(raw) as { id?: string };
        if (parsed.id && this.findById(parsed.id)) {
          return parsed.id;
        }
      }
    } catch {
      /* ignore */
    }
    return MOTIVATION_CATALOG[0].id;
  }

  private findById(id: string): MotivationTip | undefined {
    return MOTIVATION_CATALOG.find((t) => t.id === id);
  }
}
