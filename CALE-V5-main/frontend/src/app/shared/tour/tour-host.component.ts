import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  OnDestroy,
  effect,
  inject,
  signal,
  viewChild
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { TourService } from '../../core/tour/tour.service';
import { UiButtonComponent } from '../ui/ui-button.component';
import { TourMascotComponent } from './tour-mascot.component';

interface HoleRect {
  top: number;
  left: number;
  width: number;
  height: number;
}

const TARGET_TIMEOUT_MS = 1600;
const SETTLE_MS = 80;
const PAD = 8;

@Component({
  selector: 'app-tour-host',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TourMascotComponent, UiButtonComponent],
  template: `
    @if (tour.active()) {
      <div
        class="tour-root"
        role="dialog"
        aria-modal="true"
        [attr.aria-label]="'Guía de ' + tour.mascotName"
        (keydown)="onKeydown($event)">
        @if (tour.phase() !== 'step' || !hole()) {
          <div class="scrim" aria-hidden="true"></div>
        }

        @if (tour.phase() === 'step' && hole()) {
          <div
            class="hole"
            aria-hidden="true"
            [style.top.px]="hole()!.top"
            [style.left.px]="hole()!.left"
            [style.width.px]="hole()!.width"
            [style.height.px]="hole()!.height"></div>
        }

        @if (tour.phase() === 'welcome') {
          <section class="card welcome" tabindex="-1" #focusEl>
            <button type="button" class="x" aria-label="Omitir guía" (click)="tour.skip()">✕</button>
            <app-tour-mascot pose="explain" size="md" />
            <h2>¡Hola! Soy {{ tour.mascotName }}</h2>
            <p>
              Te muestro lo esencial en {{ tour.stepCount }} pasos cortos.
              Se guarda en este dispositivo para tu cuenta.
            </p>
            <div class="actions">
              <ui-button type="button" (click)="tour.beginTour()">Empezar guía</ui-button>
              <ui-button type="button" variant="ghost" (click)="tour.skip()">Omitir guía</ui-button>
            </div>
          </section>
        }

        @if (tour.phase() === 'step' && tour.currentStep(); as step) {
          <section
            class="card bubble"
            [class.bubble-bottom]="bubbleBottom()"
            tabindex="-1"
            #focusEl
            [attr.aria-live]="'polite'">
            <button type="button" class="x" aria-label="Omitir guía" (click)="tour.skip()">✕</button>
            <div class="bubble-head">
              <app-tour-mascot pose="explain" size="sm" />
              <div>
                <p class="kicker">Paso {{ tour.stepIndex() + 1 }} de {{ tour.stepCount }}</p>
                <h2>{{ step.title }}</h2>
              </div>
            </div>
            <p class="body">{{ step.body }}</p>
            <div class="actions">
              <ui-button type="button" variant="secondary" (click)="tour.back()">Atrás</ui-button>
              <ui-button type="button" (click)="tour.next()">
                {{ tour.stepIndex() >= tour.stepCount - 1 ? 'Terminar' : 'Siguiente' }}
              </ui-button>
            </div>
          </section>
        }

        @if (tour.phase() === 'celebrate') {
          <section class="card welcome" tabindex="-1" #focusEl>
            <app-tour-mascot pose="celebrate" size="md" />
            <h2>¡Listo!</h2>
            <p>
              Ya conoces lo esencial. Puedes repetir esta guía desde Preferencias.
            </p>
            <div class="actions">
              <ui-button type="button" (click)="finish()">Ir a mi panel</ui-button>
            </div>
          </section>
        }
      </div>
    }
  `,
  styles: `
    .tour-root {
      position: fixed;
      inset: 0;
      z-index: 1200;
      pointer-events: auto;
    }
    .scrim {
      position: absolute;
      inset: 0;
      background: rgb(0 0 0 / 0.62);
    }
    .hole {
      position: absolute;
      border-radius: 14px;
      box-shadow: 0 0 0 9999px rgb(0 0 0 / 0.62);
      outline: 2px solid color-mix(in srgb, var(--color-primary) 75%, white);
      pointer-events: auto; /* block clicks on highlighted UI (explanatory tour) */
      z-index: 1;
      transition: top 0.18s ease, left 0.18s ease, width 0.18s ease, height 0.18s ease;
    }
    .card {
      position: absolute;
      z-index: 2;
      width: min(22.5rem, calc(100vw - 1.5rem));
      max-height: min(70dvh, 28rem);
      overflow: auto;
      padding: 1.15rem 1.15rem 1rem;
      border-radius: 18px;
      background: var(--color-surface);
      color: var(--color-text);
      border: 1px solid var(--color-border-strong, var(--color-border));
      box-shadow: 0 22px 50px rgb(0 0 0 / 0.35);
      display: grid;
      gap: 0.65rem;
      justify-items: center;
      text-align: center;
    }
    .welcome {
      left: 50%;
      top: 50%;
      transform: translate(-50%, -50%);
    }
    .bubble {
      left: 50%;
      transform: translateX(-50%);
      /* Keep above the Kazu FAB on phones */
      bottom: max(4.75rem, calc(3.75rem + env(safe-area-inset-bottom, 0px)));
      text-align: left;
      justify-items: stretch;
    }
    .bubble.bubble-bottom {
      top: auto;
      bottom: max(1rem, env(safe-area-inset-bottom, 0px));
    }
    .bubble:not(.bubble-bottom) {
      top: max(1rem, env(safe-area-inset-top, 0px));
      bottom: auto;
    }
    .x {
      position: absolute;
      top: 0.45rem;
      right: 0.45rem;
      width: 2.4rem;
      height: 2.4rem;
      border-radius: 999px;
      border: 1px solid var(--color-border);
      background: transparent;
      color: var(--color-text);
      cursor: pointer;
      font-size: 1rem;
    }
    .bubble-head {
      display: flex;
      gap: 0.65rem;
      align-items: center;
      padding-right: 1.6rem;
    }
    .kicker {
      margin: 0;
      color: var(--color-primary);
      font-size: 0.72rem;
      font-weight: 800;
      letter-spacing: 0.06em;
      text-transform: uppercase;
    }
    h2 {
      margin: 0.1rem 0 0;
      font-size: 1.15rem;
      color: var(--color-text);
    }
    .body, .welcome p {
      margin: 0;
      color: var(--color-text-secondary);
      line-height: 1.45;
      font-size: 0.95rem;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      justify-content: center;
      margin-top: 0.25rem;
    }
    .bubble .actions { justify-content: flex-end; }
  `
})
export class TourHostComponent implements OnDestroy {
  readonly tour = inject(TourService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly hole = signal<HoleRect | null>(null);
  readonly bubbleBottom = signal(true);

  private readonly focusEl = viewChild<ElementRef<HTMLElement>>('focusEl');

  private waitToken = 0;
  private listenersBound = false;
  private onReposition = (): void => this.measureCurrentTarget();

  constructor() {
    effect(
      () => {
        const phase = this.tour.phase();
        const step = this.tour.currentStep();
        if (phase === 'idle') {
          this.teardownListeners();
          this.hole.set(null);
          return;
        }
        queueMicrotask(() => this.focusEl()?.nativeElement?.focus());
        if (phase === 'step' && step) {
          void this.resolveStep(step.route, step.target);
        } else {
          this.hole.set(null);
        }
      },
      { allowSignalWrites: true }
    );

    this.router.events
      .pipe(
        filter((e): e is NavigationEnd => e instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(() => {
        if (this.tour.phase() === 'step') {
          this.measureCurrentTarget();
        }
      });
  }

  ngOnDestroy(): void {
    this.teardownListeners();
  }

  @HostListener('document:keydown', ['$event'])
  onDocKey(ev: KeyboardEvent): void {
    if (!this.tour.active()) return;
    if (ev.key === 'Escape') {
      ev.preventDefault();
      this.tour.skip();
    }
  }

  onKeydown(ev: KeyboardEvent): void {
    if (ev.key === 'Escape') {
      ev.preventDefault();
      this.tour.skip();
    }
  }

  finish(): void {
    this.tour.complete();
    void this.router.navigateByUrl('/student');
  }

  private async resolveStep(route: string, selector: string): Promise<void> {
    const token = ++this.waitToken;
    this.hole.set(null);

    const url = this.router.url.split('?')[0];
    if (!url.startsWith(route)) {
      try {
        await this.router.navigateByUrl(route);
      } catch {
        this.tour.skipStep();
        return;
      }
    }

    const el = await this.waitForTarget(selector, token);
    if (token !== this.waitToken) return;
    if (!el) {
      this.tour.skipStep();
      return;
    }

    el.scrollIntoView({ block: 'center', inline: 'nearest', behavior: 'auto' });
    await new Promise((r) => setTimeout(r, SETTLE_MS));
    if (token !== this.waitToken) return;

    this.measureElement(el);
    this.ensureListeners();
  }

  private waitForTarget(selector: string, token: number): Promise<HTMLElement | null> {
    const started = performance.now();
    return new Promise((resolve) => {
      const tick = (): void => {
        if (token !== this.waitToken) {
          resolve(null);
          return;
        }
        const el = document.querySelector(selector) as HTMLElement | null;
        if (el && el.getClientRects().length > 0) {
          resolve(el);
          return;
        }
        if (performance.now() - started >= TARGET_TIMEOUT_MS) {
          resolve(null);
          return;
        }
        requestAnimationFrame(tick);
      };
      requestAnimationFrame(tick);
    });
  }

  private measureCurrentTarget(): void {
    const step = this.tour.currentStep();
    if (!step) return;
    const el = document.querySelector(step.target) as HTMLElement | null;
    if (!el) return;
    this.measureElement(el);
  }

  private measureElement(el: HTMLElement): void {
    const r = el.getBoundingClientRect();
    const top = Math.max(0, r.top - PAD);
    const left = Math.max(0, r.left - PAD);
    const width = Math.min(window.innerWidth - left, r.width + PAD * 2);
    const height = Math.min(window.innerHeight - top, r.height + PAD * 2);
    this.hole.set({ top, left, width, height });

    const spaceBelow = window.innerHeight - (r.bottom + PAD);
    const spaceAbove = r.top - PAD;
    this.bubbleBottom.set(spaceBelow >= 220 || spaceBelow >= spaceAbove);
  }

  private ensureListeners(): void {
    if (this.listenersBound || typeof window === 'undefined') return;
    window.addEventListener('resize', this.onReposition, { passive: true });
    window.addEventListener('scroll', this.onReposition, { passive: true, capture: true });
    this.listenersBound = true;
  }

  private teardownListeners(): void {
    if (!this.listenersBound || typeof window === 'undefined') return;
    window.removeEventListener('resize', this.onReposition);
    window.removeEventListener('scroll', this.onReposition, true);
    this.listenersBound = false;
  }
}
