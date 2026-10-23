import {
  Component,
  ElementRef,
  Input,
  OnDestroy,
  OnInit,
  ViewChild,
  inject,
  signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router } from '@angular/router';
import { filter, Subscription } from 'rxjs';
import {
  AssistantApi,
  AssistantMessage,
  AssistantPendingAction
} from '../core/assistant/assistant.api';
import { BRAND } from '../core/brand';
import { mapApiError } from '../core/http/map-api-error';

interface QuickQuestion {
  key: string;
  label: string;
}

/** Answered from the database without the AI, so they don't spend messages. */
const QUICK: Record<string, QuickQuestion[]> = {
  Student: [
    { key: 'progreso', label: '¿Cuántas horas me faltan?' },
    { key: 'clases_teoricas', label: 'Clases teóricas de la semana' },
    { key: 'cupos_examen', label: 'Cupos para el examen' },
    { key: 'clases_manejo', label: 'Clases de manejo' }
  ],
  School: [
    { key: 'resumen', label: '¿Cómo van mis estudiantes?' },
    { key: 'listos_examen', label: 'Listos para el examen' },
    { key: 'agenda', label: 'Agenda de esta semana' },
    { key: 'con_saldo', label: 'Con saldo pendiente' }
  ]
};

const HIDE_KEY = 'cale.assistant.hideUntil';

/** Full-screen flows where the floating button gets in the way. */
const HIDDEN_PATHS = [
  /^\/live\//,
  /^\/game-show\//,
  /^\/student\/simulator/,
  /^\/student\/exam/,
  /^\/student\/evaluations/,
  /^\/student\/cursos\/leccion\//,
  /^\/teacher\/live\//,
  /^\/teacher\/presentations\/[^/]+\/present/,
  /^\/teacher\/game-show\//
];

@Component({
  selector: 'app-assistant-widget',
  standalone: true,
  imports: [FormsModule],
  template: `
    @if (enabled() && !routeHidden() && !hiddenForToday()) {
      @if (!open()) {
        <button type="button" class="fab" (click)="toggle()" aria-label="Abrir el asistente">
          <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 5h16v11H8l-4 4z" /><path d="M8 9h8M8 12h5" /></svg>
          <span class="fab-label">Ayuda</span>
        </button>
      } @else {
        <button type="button" class="backdrop" (click)="close()" aria-label="Cerrar el asistente"></button>
        <section class="panel" role="dialog" aria-label="Asistente" aria-modal="true">
          <header>
            <div>
              <strong>Asistente {{ brand.name }}</strong>
              <small>Consultas y agenda. Siempre te pido confirmar.</small>
            </div>
            <div class="header-actions">
              <button type="button" class="ghost" (click)="hideToday()" title="Ocultar el botón hasta mañana">
                Hoy no
              </button>
              <button type="button" class="close" (click)="close()" aria-label="Cerrar el asistente">✕</button>
            </div>
          </header>

          <div class="messages" #scroller aria-live="polite">
            @if (!messages().length) {
              <p class="bubble bot">
                Hola. ¿Qué necesitas? Toca una consulta rápida (no gasta mensajes) o escríbeme.
              </p>
            }
            @for (m of messages(); track $index) {
              <p class="bubble" [class.bot]="m.role === 'assistant'" [class.me]="m.role === 'user'">{{ m.content }}</p>
            }
            @if (action(); as a) {
              <div class="action">
                <p><strong>¿Confirmas?</strong> {{ a.title }}</p>
                <div class="action-btns">
                  <button type="button" class="yes" (click)="confirm(a)" [disabled]="busy()">Sí, confirmar</button>
                  <button type="button" class="no" (click)="discard(a)" [disabled]="busy()">No</button>
                </div>
              </div>
            }
            @if (busy()) {
              <p class="bubble bot typing">Pensando…</p>
            }
            @if (error()) {
              <p class="bubble err">{{ error() }}</p>
            }
          </div>

          @if (quick.length) {
            <div class="chips" aria-label="Consultas rápidas">
              @for (q of quick; track q.key) {
                <button type="button" (click)="ask(q)" [disabled]="busy()">{{ q.label }}</button>
              }
            </div>
          }
          <form class="composer" (ngSubmit)="send(draft)">
            <textarea
              rows="2"
              name="draft"
              [(ngModel)]="draft"
              placeholder="Escribe tu pregunta…"
              aria-label="Escribe tu pregunta"
              maxlength="1000"
              (keydown.enter)="onEnter($event)"></textarea>
            <button type="submit" [disabled]="busy() || !draft.trim()">Enviar</button>
          </form>
          <small class="quota">Te quedan {{ remaining() }} mensajes hoy.</small>
          <small class="quota ai-note">
            Respuestas con IA: pueden tener errores. No escribas documentos, contraseñas ni datos de salud.
          </small>
        </section>
      }
    }
  `,
  styles: `
    :host {
      position: fixed;
      right: max(0.75rem, env(safe-area-inset-right));
      bottom: max(0.75rem, env(safe-area-inset-bottom));
      z-index: 45;
      pointer-events: none;
    }
    :host > * { pointer-events: auto; }

    .fab {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 0.35rem;
      width: 3rem;
      height: 3rem;
      padding: 0;
      border: 0;
      border-radius: 999px;
      background: color-mix(in srgb, var(--color-primary) 92%, #000);
      color: var(--color-on-primary, #04130a);
      font: inherit;
      font-weight: 800;
      font-size: 0.85rem;
      cursor: pointer;
      box-shadow: 0 4px 14px rgb(0 0 0 / 0.28);
      opacity: 0.92;
    }
    .fab:hover, .fab:focus-visible { opacity: 1; }
    .fab svg {
      width: 1.25rem;
      height: 1.25rem;
      fill: none;
      stroke: currentColor;
      stroke-width: 2;
      stroke-linejoin: round;
      stroke-linecap: round;
      flex-shrink: 0;
    }
    .fab-label { display: none; }

    .backdrop {
      position: fixed;
      inset: 0;
      border: 0;
      padding: 0;
      margin: 0;
      background: rgb(0 0 0 / 0.45);
      cursor: pointer;
      z-index: 1;
    }

    .panel {
      position: relative;
      z-index: 2;
      width: min(24rem, calc(100vw - 1.5rem));
      height: min(34rem, calc(100dvh - 5rem));
      display: flex;
      flex-direction: column;
      background: var(--color-surface, #111);
      color: var(--color-text, inherit);
      border: 1px solid var(--color-border);
      border-radius: 1rem;
      box-shadow: 0 16px 48px rgb(0 0 0 / 0.45);
      overflow: hidden;
    }

    header {
      display: flex;
      gap: 0.5rem;
      align-items: flex-start;
      justify-content: space-between;
      padding: 0.75rem 0.85rem;
      border-bottom: 1px solid var(--color-border);
    }
    header div:first-child { display: grid; gap: 0.1rem; min-width: 0; }
    header strong { font-size: 1rem; }
    header small { color: var(--color-text-secondary); font-size: 0.78rem; line-height: 1.3; }
    .header-actions { display: flex; gap: 0.35rem; align-items: center; flex-shrink: 0; }
    .ghost {
      border: 0;
      background: transparent;
      color: var(--color-text-secondary);
      font: inherit;
      font-size: 0.75rem;
      font-weight: 700;
      cursor: pointer;
      padding: 0.35rem 0.4rem;
      white-space: nowrap;
    }
    .close {
      min-width: 2.5rem;
      min-height: 2.5rem;
      border: 1px solid var(--color-border);
      border-radius: 0.55rem;
      background: transparent;
      color: inherit;
      font-size: 1rem;
      cursor: pointer;
    }
    .messages { flex: 1; overflow-y: auto; padding: 0.85rem; display: flex; flex-direction: column; gap: 0.55rem; }
    .bubble {
      margin: 0;
      padding: 0.6rem 0.8rem;
      border-radius: 0.85rem;
      max-width: 88%;
      white-space: pre-wrap;
      line-height: 1.4;
      font-size: 0.95rem;
    }
    .bot { align-self: flex-start; background: var(--color-background); border: 1px solid var(--color-border); }
    .me { align-self: flex-end; background: color-mix(in srgb, var(--color-primary) 22%, transparent); }
    .typing { opacity: 0.75; font-style: italic; }
    .err { align-self: stretch; max-width: none; color: var(--color-danger, #f87171); border: 1px solid currentColor; }
    .chips {
      display: flex;
      gap: 0.4rem;
      overflow-x: auto;
      padding: 0.5rem 0.7rem 0;
      border-top: 1px solid var(--color-border);
      scrollbar-width: thin;
    }
    .chips button {
      flex: none;
      min-height: 2.4rem;
      padding: 0.3rem 0.75rem;
      border-radius: 999px;
      border: 1px solid var(--color-border);
      background: transparent;
      color: inherit;
      font: inherit;
      font-size: 0.85rem;
      cursor: pointer;
      white-space: nowrap;
    }
    .chips + .composer { border-top: 0; }
    .action { border: 2px solid var(--color-primary); border-radius: 0.85rem; padding: 0.75rem; display: grid; gap: 0.55rem; }
    .action p { margin: 0; line-height: 1.4; font-size: 0.95rem; }
    .action-btns { display: flex; gap: 0.45rem; }
    .action-btns button { flex: 1; min-height: 2.6rem; border-radius: 0.65rem; font: inherit; font-weight: 800; cursor: pointer; }
    .yes { border: 0; background: var(--color-primary); color: var(--color-on-primary, #04130a); }
    .no { border: 1px solid var(--color-border); background: transparent; color: inherit; }
    .composer { display: flex; gap: 0.45rem; padding: 0.65rem; border-top: 1px solid var(--color-border); }
    .composer textarea {
      flex: 1;
      resize: none;
      padding: 0.55rem 0.7rem;
      border-radius: 0.65rem;
      border: 1px solid var(--color-border);
      background: var(--color-background);
      color: inherit;
      font: inherit;
      font-size: 0.95rem;
    }
    .composer button {
      min-width: 4.75rem;
      min-height: 2.6rem;
      border: 0;
      border-radius: 0.65rem;
      background: var(--color-primary);
      color: var(--color-on-primary, #04130a);
      font: inherit;
      font-weight: 800;
      cursor: pointer;
    }
    button:disabled { opacity: 0.55; cursor: default; }
    .quota { padding: 0 0.8rem 0.45rem; color: var(--color-text-secondary); font-size: 0.75rem; }
    .ai-note { display: block; margin-top: -0.25rem; padding-bottom: 0.55rem; font-size: 0.68rem; line-height: 1.3; }

    @media (min-width: 721px) {
      .fab {
        width: auto;
        height: auto;
        min-height: 2.75rem;
        padding: 0 0.95rem;
      }
      .fab-label { display: inline; }
    }

    @media (max-width: 720px) {
      :host {
        right: auto;
        left: max(0.65rem, env(safe-area-inset-left));
        bottom: max(4.25rem, calc(3.5rem + env(safe-area-inset-bottom)));
        z-index: 35;
      }
      .fab {
        width: 2.75rem;
        height: 2.75rem;
        opacity: 0.82;
        box-shadow: 0 3px 10px rgb(0 0 0 / 0.22);
      }
      .panel {
        position: fixed;
        left: 0;
        right: 0;
        bottom: 0;
        width: 100vw;
        height: min(78dvh, calc(100dvh - 3.5rem));
        border-radius: 1rem 1rem 0 0;
        border: 0;
        border-top: 1px solid var(--color-border);
      }
      .ghost { font-size: 0.72rem; }
    }
  `
})
export class AssistantWidgetComponent implements OnInit, OnDestroy {
  @Input() role: string | undefined;
  @ViewChild('scroller') private scroller?: ElementRef<HTMLElement>;

  private readonly api = inject(AssistantApi);
  private readonly router = inject(Router);
  private navSub?: Subscription;

  readonly brand = BRAND;
  readonly enabled = signal(false);
  readonly open = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly messages = signal<AssistantMessage[]>([]);
  readonly action = signal<AssistantPendingAction | null>(null);
  readonly remaining = signal(0);
  readonly routeHidden = signal(false);
  readonly hiddenForToday = signal(false);
  draft = '';

  get quick(): QuickQuestion[] {
    return QUICK[this.role ?? ''] ?? [];
  }

  ngOnInit(): void {
    this.hiddenForToday.set(this.isHiddenForToday());
    this.syncRoute(this.router.url);
    this.navSub = this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => {
        this.syncRoute(e.urlAfterRedirects);
        if (this.routeHidden()) {
          this.open.set(false);
        }
      });

    this.api.status().subscribe({
      next: (s) => {
        this.enabled.set(s.enabled);
        this.remaining.set(s.remainingToday);
      },
      error: () => this.enabled.set(false)
    });
  }

  ngOnDestroy(): void {
    this.navSub?.unsubscribe();
  }

  toggle(): void {
    this.open.update((v) => !v);
    this.scrollSoon();
  }

  close(): void {
    this.open.set(false);
  }

  hideToday(): void {
    const until = new Date();
    until.setHours(23, 59, 59, 999);
    try {
      localStorage.setItem(HIDE_KEY, until.toISOString());
    } catch {
      /* ignore quota / private mode */
    }
    this.hiddenForToday.set(true);
    this.open.set(false);
  }

  onEnter(event: Event): void {
    const e = event as KeyboardEvent;
    if (e.shiftKey) return;
    e.preventDefault();
    this.send(this.draft);
  }

  send(text: string): void {
    const content = text.trim();
    if (!content || this.busy()) return;
    const pending = this.action();
    if (pending) {
      this.api.discard(pending.id).subscribe({ error: () => undefined });
      this.action.set(null);
    }
    this.draft = '';
    this.error.set(null);
    this.messages.update((list) => [...list, { role: 'user', content }]);
    this.busy.set(true);
    this.scrollSoon();
    this.api.chat(this.messages().slice(-10)).subscribe({
      next: (r) => {
        this.busy.set(false);
        this.messages.update((list) => [...list, { role: 'assistant', content: r.reply }]);
        this.action.set(r.action);
        this.remaining.set(r.remainingToday);
        this.scrollSoon();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
        this.scrollSoon();
      }
    });
  }

  ask(q: QuickQuestion): void {
    if (this.busy()) return;
    this.error.set(null);
    this.messages.update((list) => [...list, { role: 'user', content: q.label }]);
    this.busy.set(true);
    this.scrollSoon();
    this.api.quick(q.key).subscribe({
      next: (r) => {
        this.busy.set(false);
        this.messages.update((list) => [...list, { role: 'assistant', content: r.reply }]);
        this.remaining.set(r.remainingToday);
        this.scrollSoon();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
        this.scrollSoon();
      }
    });
  }

  confirm(a: AssistantPendingAction): void {
    this.busy.set(true);
    this.error.set(null);
    this.api.confirm(a.id).subscribe({
      next: (r) => {
        this.busy.set(false);
        this.action.set(null);
        this.messages.update((list) => [...list, { role: 'assistant', content: r.message }]);
        this.scrollSoon();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  discard(a: AssistantPendingAction): void {
    this.action.set(null);
    this.api.discard(a.id).subscribe({ error: () => undefined });
    this.messages.update((list) => [...list, { role: 'assistant', content: 'Listo, no hice ningún cambio.' }]);
    this.scrollSoon();
  }

  private syncRoute(url: string): void {
    const path = url.split('?')[0] ?? url;
    this.routeHidden.set(HIDDEN_PATHS.some((re) => re.test(path)));
  }

  private isHiddenForToday(): boolean {
    try {
      const raw = localStorage.getItem(HIDE_KEY);
      if (!raw) return false;
      const until = Date.parse(raw);
      if (Number.isNaN(until) || until <= Date.now()) {
        localStorage.removeItem(HIDE_KEY);
        return false;
      }
      return true;
    } catch {
      return false;
    }
  }

  private scrollSoon(): void {
    setTimeout(() => {
      const el = this.scroller?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    });
  }
}
