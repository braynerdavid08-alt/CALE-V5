import { Component, ElementRef, Input, OnInit, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
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

@Component({
  selector: 'app-assistant-widget',
  standalone: true,
  imports: [FormsModule],
  template: `
    @if (enabled()) {
      @if (!open()) {
        <button type="button" class="fab" (click)="toggle()" aria-label="Abrir el asistente">
          <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 5h16v11H8l-4 4z" /><path d="M8 9h8M8 12h5" /></svg>
          <span>Asistente</span>
        </button>
      } @else {
        <section class="panel" role="dialog" aria-label="Asistente">
          <header>
            <div>
              <strong>Asistente {{ brand.name }}</strong>
              <small>Te ayudo a consultar y agendar. Siempre te pido confirmar.</small>
            </div>
            <button type="button" class="close" (click)="toggle()" aria-label="Cerrar el asistente">✕</button>
          </header>

          <div class="messages" #scroller aria-live="polite">
            @if (!messages().length) {
              <p class="bubble bot">
                Hola. ¿Qué necesitas hoy? Toca una consulta rápida (no gasta mensajes) o escríbeme para reservar, cancelar o agendar.
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
            Respuestas generadas con inteligencia artificial: pueden tener errores. No reemplazan a tu instructor,
            a tu escuela ni a las autoridades de tránsito. No escribas documentos, contraseñas ni datos de salud.
          </small>
        </section>
      }
    }
  `,
  styles: `
    :host { position: fixed; right: 1rem; bottom: 1rem; z-index: 60; }
    .fab {
      display: inline-flex; align-items: center; gap: 0.5rem;
      min-height: 3.25rem; padding: 0 1.15rem; border: 0; border-radius: 999px;
      background: var(--color-primary); color: var(--color-on-primary, #04130a);
      font: inherit; font-weight: 800; font-size: 1.05rem; cursor: pointer;
      box-shadow: 0 8px 24px rgb(0 0 0 / 0.35);
    }
    .fab svg { width: 1.5rem; height: 1.5rem; fill: none; stroke: currentColor; stroke-width: 2; stroke-linejoin: round; stroke-linecap: round; }
    .panel {
      width: min(26rem, calc(100vw - 2rem)); height: min(40rem, calc(100dvh - 2rem));
      display: flex; flex-direction: column;
      background: var(--color-surface, #111); color: var(--color-text, inherit);
      border: 1px solid var(--color-border); border-radius: 1rem;
      box-shadow: 0 16px 48px rgb(0 0 0 / 0.45); overflow: hidden;
    }
    header { display: flex; gap: 0.75rem; align-items: flex-start; justify-content: space-between; padding: 0.9rem 1rem; border-bottom: 1px solid var(--color-border); }
    header div { display: grid; gap: 0.15rem; }
    header strong { font-size: 1.1rem; }
    header small { color: var(--color-text-secondary); }
    .close { min-width: 2.75rem; min-height: 2.75rem; border: 1px solid var(--color-border); border-radius: 0.6rem; background: transparent; color: inherit; font-size: 1.1rem; cursor: pointer; }
    .messages { flex: 1; overflow-y: auto; padding: 1rem; display: flex; flex-direction: column; gap: 0.6rem; }
    .bubble { margin: 0; padding: 0.7rem 0.9rem; border-radius: 0.9rem; max-width: 88%; white-space: pre-wrap; line-height: 1.45; font-size: 1.02rem; }
    .bot { align-self: flex-start; background: var(--color-background); border: 1px solid var(--color-border); }
    .me { align-self: flex-end; background: color-mix(in srgb, var(--color-primary) 22%, transparent); }
    .typing { opacity: 0.75; font-style: italic; }
    .err { align-self: stretch; max-width: none; color: var(--color-danger, #f87171); border: 1px solid currentColor; }
    .chips { display: flex; gap: 0.5rem; overflow-x: auto; padding: 0.6rem 0.75rem 0; border-top: 1px solid var(--color-border); scrollbar-width: thin; }
    .chips button { flex: none; min-height: 2.75rem; padding: 0.4rem 0.85rem; border-radius: 999px; border: 1px solid var(--color-border); background: transparent; color: inherit; font: inherit; cursor: pointer; white-space: nowrap; }
    .chips + .composer { border-top: 0; }
    .action { border: 2px solid var(--color-primary); border-radius: 0.9rem; padding: 0.85rem; display: grid; gap: 0.65rem; }
    .action p { margin: 0; line-height: 1.45; font-size: 1.02rem; }
    .action-btns { display: flex; gap: 0.5rem; }
    .action-btns button { flex: 1; min-height: 3rem; border-radius: 0.7rem; font: inherit; font-weight: 800; cursor: pointer; }
    .yes { border: 0; background: var(--color-primary); color: var(--color-on-primary, #04130a); }
    .no { border: 1px solid var(--color-border); background: transparent; color: inherit; }
    .composer { display: flex; gap: 0.5rem; padding: 0.75rem; border-top: 1px solid var(--color-border); }
    .composer textarea { flex: 1; resize: none; padding: 0.6rem 0.75rem; border-radius: 0.7rem; border: 1px solid var(--color-border); background: var(--color-background); color: inherit; font: inherit; font-size: 1.02rem; }
    .composer button { min-width: 5.5rem; min-height: 3rem; border: 0; border-radius: 0.7rem; background: var(--color-primary); color: var(--color-on-primary, #04130a); font: inherit; font-weight: 800; cursor: pointer; }
    button:disabled { opacity: 0.55; cursor: default; }
    .quota { padding: 0 0.9rem 0.6rem; color: var(--color-text-secondary); font-size: 0.8rem; }
    .ai-note { display: block; margin-top: -0.3rem; font-size: 0.72rem; line-height: 1.35; }
    @media (max-width: 600px) {
      :host { right: 0.75rem; bottom: 0.75rem; }
      :host:has(.panel) { inset: 0; }
      .panel { width: 100vw; height: 100dvh; border-radius: 0; border: 0; }
    }
  `
})
export class AssistantWidgetComponent implements OnInit {
  @Input() role: string | undefined;
  @ViewChild('scroller') private scroller?: ElementRef<HTMLElement>;

  private readonly api = inject(AssistantApi);
  readonly brand = BRAND;
  readonly enabled = signal(false);
  readonly open = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly messages = signal<AssistantMessage[]>([]);
  readonly action = signal<AssistantPendingAction | null>(null);
  readonly remaining = signal(0);
  draft = '';

  get quick(): QuickQuestion[] {
    return QUICK[this.role ?? ''] ?? [];
  }

  ngOnInit(): void {
    this.api.status().subscribe({
      next: (s) => {
        this.enabled.set(s.enabled);
        this.remaining.set(s.remainingToday);
      },
      error: () => this.enabled.set(false)
    });
  }

  toggle(): void {
    this.open.update((v) => !v);
    this.scrollSoon();
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

  private scrollSoon(): void {
    setTimeout(() => {
      const el = this.scroller?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    });
  }
}
