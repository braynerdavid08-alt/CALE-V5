import { Injectable, computed, inject, signal } from '@angular/core';
import { SessionStore } from '../auth/session.store';
import { STUDENT_TOUR_STEPS } from './student-tour.steps';
import {
  MASCOT_NAME,
  TourPersistState,
  TourPhase,
  TourRuntimeState,
  TourStep
} from './tour.types';

/** v3: compact 4-step tour (faster on mobile / iPhone). */
const KEY_PREFIX = 'luzverde.tour.student.v3.';

@Injectable({ providedIn: 'root' })
export class TourService {
  private readonly session = inject(SessionStore);

  readonly mascotName = MASCOT_NAME;
  readonly steps = STUDENT_TOUR_STEPS;

  private readonly runtime = signal<TourRuntimeState>({
    phase: 'idle',
    stepIndex: 0
  });

  readonly phase = computed(() => this.runtime().phase);
  readonly stepIndex = computed(() => this.runtime().stepIndex);
  readonly active = computed(() => this.runtime().phase !== 'idle');
  readonly currentStep = computed<TourStep | null>(() => {
    const { phase, stepIndex } = this.runtime();
    if (phase !== 'step') return null;
    return this.steps[stepIndex] ?? null;
  });
  readonly stepCount = this.steps.length;

  /** Device-local only; not synced across devices. Keyed by user id. */
  private storageKey(userId: number): string {
    return `${KEY_PREFIX}${userId}`;
  }

  private readPersist(userId: number): TourPersistState | null {
    if (typeof localStorage === 'undefined') return null;
    try {
      const raw = localStorage.getItem(this.storageKey(userId));
      if (!raw) return null;
      const parsed = JSON.parse(raw) as TourPersistState;
      if (parsed?.status === 'done' || parsed?.status === 'skipped') return parsed;
    } catch {
      /* ignore corrupt */
    }
    return null;
  }

  private writePersist(userId: number, status: TourPersistState['status']): void {
    if (typeof localStorage === 'undefined') return;
    const payload: TourPersistState = { status, updatedAt: new Date().toISOString() };
    localStorage.setItem(this.storageKey(userId), JSON.stringify(payload));
  }

  private clearPersist(userId: number): void {
    if (typeof localStorage === 'undefined') return;
    localStorage.removeItem(this.storageKey(userId));
  }

  private userId(): number | null {
    const id = this.session.user()?.id;
    return typeof id === 'number' && id > 0 ? id : null;
  }

  hasFinishedForCurrentUser(): boolean {
    const id = this.userId();
    if (id == null) return true;
    return !!this.readPersist(id);
  }

  /** Auto-start only when student has not done/skipped on this device. */
  maybeAutoStart(): void {
    const user = this.session.user();
    if (!user || user.role !== 'Student' || user.mustChangePassword) return;
    if (this.active()) return;
    if (this.hasFinishedForCurrentUser()) return;
    this.startWelcome();
  }

  startWelcome(): void {
    this.runtime.set({ phase: 'welcome', stepIndex: 0 });
  }

  /** Preferencias → Repetir guía */
  replay(): void {
    const id = this.userId();
    if (id != null) this.clearPersist(id);
    this.startWelcome();
  }

  beginTour(): void {
    this.runtime.set({ phase: 'step', stepIndex: 0 });
  }

  next(): void {
    const { phase, stepIndex } = this.runtime();
    if (phase !== 'step') return;
    if (stepIndex >= this.steps.length - 1) {
      this.runtime.set({ phase: 'celebrate', stepIndex });
      return;
    }
    this.runtime.set({ phase: 'step', stepIndex: stepIndex + 1 });
  }

  /** Skip a missing target without celebrating. */
  skipStep(): void {
    this.next();
  }

  back(): void {
    const { phase, stepIndex } = this.runtime();
    if (phase !== 'step') return;
    if (stepIndex <= 0) {
      this.runtime.set({ phase: 'welcome', stepIndex: 0 });
      return;
    }
    this.runtime.set({ phase: 'step', stepIndex: stepIndex - 1 });
  }

  skip(): void {
    const id = this.userId();
    if (id != null) this.writePersist(id, 'skipped');
    this.runtime.set({ phase: 'idle', stepIndex: 0 });
  }

  complete(): void {
    const id = this.userId();
    if (id != null) this.writePersist(id, 'done');
    this.runtime.set({ phase: 'idle', stepIndex: 0 });
  }

  setPhase(phase: TourPhase): void {
    this.runtime.update((r) => ({ ...r, phase }));
  }
}
