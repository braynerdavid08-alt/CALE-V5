import { Injectable, computed, signal } from '@angular/core';
import { MOTIVATION_CATALOG } from './motivation.catalog';
import {
  MotivationAudience,
  MotivationMoment,
  MotivationTip
} from './motivation.model';

/** Fixed tip for the current browser tab / login session. */
const SESSION_KEY = 'cale.motivation.session.v1';

@Injectable({ providedIn: 'root' })
export class MotivationService {
  private readonly tipId = signal<string>(MOTIVATION_CATALOG[0].id);
  private readonly role = signal<string | null>(null);
  private locked = false;

  readonly current = computed(
    () => this.findById(this.tipId()) ?? MOTIVATION_CATALOG[0]
  );

  /**
   * Locks one tip for this login session (role-aware).
   * Does not rotate or change until {@link clearSession}.
   */
  ensureSessionTip(role?: string | null): MotivationTip {
    this.role.set(role ?? null);

    if (this.locked) {
      const current = this.findById(this.tipId());
      if (current && this.pool().some((t) => t.id === current.id)) {
        return current;
      }
    }

    const restored = this.readSession(role);
    if (restored) {
      this.tipId.set(restored);
      this.locked = true;
      return this.findById(restored) ?? MOTIVATION_CATALOG[0];
    }

    const tip = this.pickFresh();
    this.tipId.set(tip.id);
    this.locked = true;
    this.writeSession(role, tip.id);
    return tip;
  }

  setRole(role?: string | null): void {
    this.ensureSessionTip(role);
  }

  clearSession(): void {
    this.locked = false;
    try {
      sessionStorage.removeItem(SESSION_KEY);
    } catch {
      /* ignore */
    }
  }

  private pickFresh(): MotivationTip {
    const pool = this.pool();
    return pool[Math.floor(Math.random() * pool.length)] ?? MOTIVATION_CATALOG[0];
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

  private writeSession(role: string | null | undefined, id: string): void {
    try {
      sessionStorage.setItem(
        SESSION_KEY,
        JSON.stringify({ id, role: role ?? null })
      );
    } catch {
      /* ignore */
    }
  }

  private readSession(role?: string | null): string | null {
    try {
      const raw = sessionStorage.getItem(SESSION_KEY);
      if (!raw) {
        return null;
      }
      const parsed = JSON.parse(raw) as { id?: string; role?: string | null };
      if (!parsed.id || !this.findById(parsed.id)) {
        return null;
      }
      if ((parsed.role ?? null) !== (role ?? null)) {
        return null;
      }
      return parsed.id;
    } catch {
      return null;
    }
  }

  private findById(id: string): MotivationTip | undefined {
    return MOTIVATION_CATALOG.find((t) => t.id === id);
  }
}
