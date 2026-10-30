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
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import {
  AssistantApi,
  AssistantExamAvailabilityUi,
  AssistantExamDayUi,
  AssistantMessage,
  AssistantPendingAction
} from '../core/assistant/assistant.api';
import { renderAssistantMarkdown } from '../core/assistant/assistant-markdown';
import { BRAND } from '../core/brand';
import { MASCOT_NAME } from '../core/tour/tour.types';
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
const INITIAL_DAYS = 4;
const DAY_NAMES = ['domingo', 'lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado'];

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
    @if ((enabled() || connecting()) && !routeHidden() && !hiddenForToday()) {
      @if (!open()) {
        <button
          type="button"
          class="fab"
          [class.fab-connecting]="connecting()"
          data-tour="student-kazu"
          [disabled]="connecting()"
          (click)="toggle()"
          [attr.aria-label]="connecting() ? ('Conectando a ' + mascotName) : ('Abrir a ' + mascotName)"
          [attr.title]="connecting() ? 'Conectando…' : mascotName">
          <img
            class="fab-avatar"
            [src]="connecting() ? '/mascot/kuzu-explain-static.gif' : '/mascot/kuzu-explain-128.gif'"
            [alt]="''"
            width="40"
            height="40"
            decoding="async" />
          <span class="fab-label">{{ connecting() ? '…' : mascotName }}</span>
        </button>
      } @else {
        <button type="button" class="backdrop" (click)="close()" [attr.aria-label]="'Cerrar a ' + mascotName"></button>
        <section class="panel" role="dialog" [attr.aria-label]="mascotName" aria-modal="true">
          <header>
            <img class="head-avatar" src="/mascot/kuzu-explain-128.gif" [alt]="''" width="40" height="40" decoding="async" />
            <div>
              <strong>{{ mascotName }}</strong>
              <small>
                Asistente de {{ brand.name }}. Consultas y agenda. Siempre te pido confirmar.
                @if (remaining() > 0) {
                  · {{ remaining() }} mensajes hoy
                }
                @if (nearLimit()) {
                  · te acercas al límite diario
                }
                @if (atLimit()) {
                  · cuota agotada; contacta al administrador si necesitas más
                }
              </small>
            </div>
            <div class="header-actions">
              <button type="button" class="ghost" (click)="hideToday()" title="Ocultar el botón hasta mañana">
                Hoy no
              </button>
              <button type="button" class="close" (click)="close()" [attr.aria-label]="'Cerrar a ' + mascotName">✕</button>
            </div>
          </header>

          <div class="messages" #scroller aria-live="polite">
            @if (!messages().length) {
              <p class="bubble bot">
                Hola, soy {{ mascotName }}. ¿Qué necesitas? Toca una consulta rápida (no gasta mensajes) o escríbeme.
              </p>
            }
            @for (m of messages(); track $index) {
              @if (m.role === 'user') {
                <p class="bubble me">{{ m.content }}</p>
              } @else {
                <div class="bot-block">
                  @if (m.content && !m.examAvailability) {
                    <div class="bubble bot md" [innerHTML]="asHtml(m.content)"></div>
                  } @else if (m.content && m.examAvailability && tipBesideCard(m.content)) {
                    <div class="bubble bot md tip" [innerHTML]="asHtml(m.content)"></div>
                  }
                  @if (m.examAvailability; as card) {
                    <div class="exam-card" [attr.data-msg]="$index">
                      <div class="exam-head">
                        <strong>{{ card.title }}</strong>
                        <span class="exam-range">{{ formatRange(card.dateRangeFrom, card.dateRangeTo) }}</span>
                      </div>
                      @if (card.myBookingSummary) {
                        <p class="exam-note">{{ scheduleSummaryLabel(card) }}{{ card.myBookingSummary }}</p>
                      }
                      @if (card.blockReason) {
                        <p class="exam-warn">{{ card.blockReason }}</p>
                      }
                      @if (card.selectable && card.days.length) {
                        <p class="exam-hint">Toca un horario para preparar la reserva. Aún no se agenda hasta que confirmes.</p>
                      }
                      @if (!card.days.length) {
                        <p class="exam-empty">{{ emptySlotsLabel(card.type) }}</p>
                      } @else {
                        @for (day of visibleDays(m); track day.date) {
                          <article class="day">
                            <header class="day-head">
                              <div>
                                <strong class="day-name">{{ weekdayName(day.date) }}</strong>
                                <span class="day-date">{{ formatFullDate(day.date) }}</span>
                              </div>
                              <span class="day-count">{{ day.availableSlots.length }} horario{{ day.availableSlots.length === 1 ? '' : 's' }}</span>
                            </header>
                            <div class="slots" role="list">
                              @for (slot of day.availableSlots; track slotTrack(slot)) {
                                @if (isSlotSelectable(card, slot)) {
                                  <button
                                    type="button"
                                    class="slot"
                                    role="listitem"
                                    [disabled]="busy()"
                                    (click)="selectSlot(day.date, slot)"
                                    [attr.aria-label]="slotAria(slot)">
                                    <span class="slot-time">{{ slotLabel(slot) }}</span>
                                    @if (slot.detail) {
                                      <span class="slot-detail">{{ slot.detail }}</span>
                                    }
                                    <span class="slot-cap">{{ slot.availableCapacity }} cupo{{ slot.availableCapacity === 1 ? '' : 's' }}</span>
                                  </button>
                                } @else {
                                  <div class="slot static" role="listitem">
                                    <span class="slot-time">{{ slotLabel(slot) }}</span>
                                    @if (slot.detail) {
                                      <span class="slot-detail">{{ slot.detail }}</span>
                                    }
                                    <span class="slot-cap">{{ slot.availableCapacity }} cupo{{ slot.availableCapacity === 1 ? '' : 's' }}</span>
                                  </div>
                                }
                              }
                            </div>
                          </article>
                        }
                        @if (card.days.length > INITIAL_DAYS) {
                          <div class="more-row">
                            @if ((m.daysVisible ?? INITIAL_DAYS) < card.days.length) {
                              <button type="button" class="more" (click)="showMoreDays($index)">
                                Ver más días ({{ card.days.length - (m.daysVisible ?? INITIAL_DAYS) }})
                              </button>
                            } @else {
                              <button type="button" class="more" (click)="showFewerDays($index)">Mostrar menos</button>
                            }
                          </div>
                        }
                      }
                    </div>
                  }
                </div>
              }
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
      width: 3.35rem;
      height: 3.35rem;
      padding: 0.2rem;
      border: 2px solid color-mix(in srgb, var(--color-primary) 55%, #fff);
      border-radius: 999px;
      background: color-mix(in srgb, var(--color-surface) 88%, var(--color-primary));
      color: var(--color-on-primary, #04130a);
      font: inherit;
      font-weight: 800;
      font-size: 0.85rem;
      cursor: pointer;
      box-shadow: 0 6px 18px rgb(0 0 0 / 0.32);
      opacity: 0.96;
      overflow: hidden;
    }
    .fab:hover:not(:disabled), .fab:focus-visible:not(:disabled) { opacity: 1; transform: translateY(-1px); }
    .fab:disabled { cursor: wait; }
    .fab-connecting {
      opacity: 0.78;
      animation: fab-pulse 1.1s ease-in-out infinite;
    }
    @keyframes fab-pulse {
      0%, 100% { transform: scale(1); }
      50% { transform: scale(1.04); }
    }
    .fab-avatar {
      width: 100%;
      height: 100%;
      object-fit: contain;
      display: block;
      pointer-events: none;
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
      gap: 0.55rem;
      align-items: flex-start;
      justify-content: space-between;
      padding: 0.75rem 0.85rem;
      border-bottom: 1px solid var(--color-border);
    }
    .head-avatar {
      flex: none;
      width: 2.5rem;
      height: 2.5rem;
      object-fit: contain;
      border-radius: 999px;
      background: color-mix(in srgb, var(--color-primary) 16%, var(--color-surface));
    }
    header > div:nth-child(2) { display: grid; gap: 0.1rem; min-width: 0; flex: 1; }
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
    .messages {
      flex: 1;
      overflow-x: hidden;
      overflow-y: auto;
      padding: 0.85rem;
      display: flex;
      flex-direction: column;
      gap: 0.55rem;
      min-height: 0;
    }
    .bot-block {
      align-self: stretch;
      display: flex;
      flex-direction: column;
      gap: 0.45rem;
      max-width: 100%;
      min-width: 0;
    }
    .bubble {
      margin: 0;
      padding: 0.7rem 0.9rem;
      border-radius: 0.85rem;
      max-width: 88%;
      white-space: pre-wrap;
      overflow-wrap: anywhere;
      word-break: break-word;
      line-height: 1.55;
      font-size: 0.95rem;
    }
    .bot {
      align-self: flex-start;
      background: var(--color-background);
      border: 1px solid var(--color-border);
      letter-spacing: 0.01em;
    }
    .bot-block > .bubble.bot { max-width: 100%; }
    .bubble.md {
      white-space: normal;
      display: grid;
      gap: 0.45rem;
      min-width: 0;
    }
    .bubble.md :is(p, ul, ol, h2, h3, h4) {
      margin: 0;
      min-width: 0;
      overflow-wrap: anywhere;
      word-break: break-word;
    }
    .bubble.md .asst-title {
      margin: 0.15rem 0 0;
      font-size: 0.98rem;
      line-height: 1.35;
    }
    .bubble.md .asst-title strong { font-weight: 800; }
    .bubble.md ul, .bubble.md ol {
      padding-left: 1.1rem;
      display: grid;
      gap: 0.35rem;
    }
    .bubble.md li { padding-left: 0.1rem; }
    .bubble.md ul.asst-sub {
      margin-top: 0.25rem;
      gap: 0.15rem;
      color: var(--color-text-secondary);
      font-size: 0.9em;
    }
    .bubble.md strong { font-weight: 800; }
    .me { align-self: flex-end; background: color-mix(in srgb, var(--color-primary) 22%, transparent); }
    .typing { opacity: 0.75; font-style: italic; }
    .err { align-self: stretch; max-width: none; color: var(--color-danger, #f87171); border: 1px solid currentColor; }

    .exam-card {
      align-self: stretch;
      display: grid;
      gap: 0.55rem;
      padding: 0.7rem;
      border-radius: 0.85rem;
      border: 1px solid var(--color-border);
      background: var(--color-background);
      min-width: 0;
      max-width: 100%;
      box-sizing: border-box;
    }
    .exam-head {
      display: grid;
      gap: 0.15rem;
      min-width: 0;
    }
    .exam-head strong {
      font-size: 0.95rem;
      line-height: 1.25;
    }
    .exam-range {
      color: var(--color-text-secondary);
      font-size: 0.8rem;
    }
    .exam-note, .exam-warn, .exam-empty, .exam-hint {
      margin: 0;
      font-size: 0.85rem;
      line-height: 1.35;
    }
    .exam-warn { color: var(--color-warning, #d97706); }
    .exam-empty, .exam-hint { color: var(--color-text-secondary); }
    .bubble.tip { font-size: 0.9rem; }

    .day {
      display: grid;
      gap: 0.4rem;
      padding: 0.55rem 0.6rem;
      border-radius: 0.7rem;
      border: 1px solid color-mix(in srgb, var(--color-border) 85%, transparent);
      background: color-mix(in srgb, var(--color-surface, #111) 88%, var(--color-background));
      min-width: 0;
    }
    .day-head {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 0.5rem;
    }
    .day-head > div { display: grid; gap: 0.05rem; min-width: 0; }
    .day-name {
      font-size: 0.92rem;
      text-transform: capitalize;
    }
    .day-date {
      color: var(--color-text-secondary);
      font-size: 0.78rem;
    }
    .day-count {
      flex-shrink: 0;
      font-size: 0.72rem;
      font-weight: 700;
      color: var(--color-text-secondary);
      padding: 0.15rem 0.4rem;
      border-radius: 999px;
      border: 1px solid var(--color-border);
    }
    .slots {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(7.5rem, 1fr));
      gap: 0.35rem;
    }
    .slot {
      display: grid;
      gap: 0.1rem;
      align-content: center;
      min-height: 2.6rem;
      padding: 0.35rem 0.4rem;
      border-radius: 0.55rem;
      border: 1px solid color-mix(in srgb, var(--color-primary) 45%, var(--color-border));
      background: color-mix(in srgb, var(--color-primary) 12%, transparent);
      color: inherit;
      font: inherit;
      cursor: pointer;
      text-align: left;
      min-width: 0;
    }
    .slot:not(.static):hover:not(:disabled),
    .slot:not(.static):focus-visible {
      border-color: var(--color-primary);
      background: color-mix(in srgb, var(--color-primary) 22%, transparent);
    }
    .slot.static {
      cursor: default;
      background: transparent;
      border-color: var(--color-border);
    }
    .slot-time {
      font-weight: 800;
      font-variant-numeric: tabular-nums;
      font-size: 0.92rem;
      line-height: 1.2;
    }
    .slot-detail {
      font-size: 0.68rem;
      color: var(--color-text-secondary);
      line-height: 1.2;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .slot-cap {
      font-size: 0.7rem;
      color: var(--color-text-secondary);
      line-height: 1.2;
    }
    .more-row { display: flex; justify-content: center; }
    .more {
      border: 0;
      background: transparent;
      color: var(--color-primary);
      font: inherit;
      font-size: 0.85rem;
      font-weight: 800;
      cursor: pointer;
      padding: 0.35rem 0.5rem;
      text-decoration: underline;
      text-underline-offset: 0.15em;
    }

    .chips {
      display: flex;
      gap: 0.4rem;
      overflow-x: auto;
      padding: 0.5rem 0.7rem 0;
      border-top: 1px solid var(--color-border);
      scrollbar-width: thin;
      flex-shrink: 0;
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
    .action {
      border: 2px solid var(--color-primary);
      border-radius: 0.85rem;
      padding: 0.75rem;
      display: grid;
      gap: 0.55rem;
      max-width: 100%;
      box-sizing: border-box;
    }
    .action p { margin: 0; line-height: 1.4; font-size: 0.95rem; overflow-wrap: anywhere; }
    .action-btns { display: flex; gap: 0.45rem; }
    .action-btns button { flex: 1; min-height: 2.6rem; border-radius: 0.65rem; font: inherit; font-weight: 800; cursor: pointer; }
    .yes { border: 0; background: var(--color-primary); color: var(--color-on-primary, #04130a); }
    .no { border: 1px solid var(--color-border); background: transparent; color: inherit; }
    .composer {
      display: flex;
      gap: 0.45rem;
      padding: 0.65rem;
      border-top: 1px solid var(--color-border);
      flex-shrink: 0;
    }
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
      min-width: 0;
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
      flex-shrink: 0;
    }
    button:disabled { opacity: 0.55; cursor: default; }
    .quota { padding: 0 0.8rem 0.45rem; color: var(--color-text-secondary); font-size: 0.75rem; flex-shrink: 0; }
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
        max-width: 100vw;
        height: min(78dvh, calc(100dvh - 3.5rem));
        border-radius: 1rem 1rem 0 0;
        border: 0;
        border-top: 1px solid var(--color-border);
      }
      .ghost { font-size: 0.72rem; }
      .slots { grid-template-columns: repeat(auto-fill, minmax(6.5rem, 1fr)); }
    }
  `
})
export class AssistantWidgetComponent implements OnInit, OnDestroy {
  readonly INITIAL_DAYS = INITIAL_DAYS;

  @Input() role: string | undefined;
  @ViewChild('scroller') private scroller?: ElementRef<HTMLElement>;

  private readonly api = inject(AssistantApi);
  private readonly router = inject(Router);
  private readonly sanitizer = inject(DomSanitizer);
  private navSub?: Subscription;

  readonly brand = BRAND;
  readonly mascotName = MASCOT_NAME;
  /** True until /status responds — FAB visible so tour/UI don't wait on a blank corner. */
  readonly connecting = signal(true);
  readonly enabled = signal(false);
  readonly open = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly messages = signal<AssistantMessage[]>([]);
  readonly action = signal<AssistantPendingAction | null>(null);
  readonly remaining = signal(0);
  readonly nearLimit = signal(false);
  readonly atLimit = signal(false);
  readonly routeHidden = signal(false);
  readonly hiddenForToday = signal(false);
  draft = '';

  get quick(): QuickQuestion[] {
    return QUICK[this.role ?? ''] ?? [];
  }

  /** Escape-first Markdown → trusted HTML for bot bubbles only. */
  asHtml(content: string): SafeHtml {
    return this.sanitizer.bypassSecurityTrustHtml(renderAssistantMarkdown(content));
  }

  /** Short tip beside a schedule card; hide long dumps that repeat the card. */
  tipBesideCard(content: string): boolean {
    const t = content.trim();
    if (!t || t.length > 280) return false;
    if (t.includes('|') || /cupo|Rango consultado|Toca un horario/i.test(t)) return false;
    return true;
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
        this.connecting.set(false);
        this.enabled.set(s.enabled);
        this.remaining.set(s.remainingToday);
        this.nearLimit.set(!!s.nearLimit);
        this.atLimit.set(!!s.atLimit || s.remainingToday <= 0);
      },
      error: () => {
        this.connecting.set(false);
        this.enabled.set(false);
      }
    });
  }

  ngOnDestroy(): void {
    this.navSub?.unsubscribe();
  }

  toggle(): void {
    if (this.connecting() || !this.enabled()) {
      return;
    }
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

  visibleDays(m: AssistantMessage): AssistantExamDayUi[] {
    const card = m.examAvailability;
    if (!card) return [];
    const n = m.daysVisible ?? INITIAL_DAYS;
    return card.days.slice(0, n);
  }

  showMoreDays(index: number): void {
    this.messages.update((list) =>
      list.map((m, i) => {
        if (i !== index || !m.examAvailability) return m;
        const next = Math.min(
          (m.daysVisible ?? INITIAL_DAYS) + INITIAL_DAYS,
          m.examAvailability.days.length
        );
        return { ...m, daysVisible: next };
      })
    );
    this.scrollSoon();
  }

  showFewerDays(index: number): void {
    this.messages.update((list) =>
      list.map((m, i) => (i === index ? { ...m, daysVisible: INITIAL_DAYS } : m))
    );
  }

  weekdayName(iso: string): string {
    const d = parseIsoDate(iso);
    return d ? DAY_NAMES[d.getUTCDay()] : iso;
  }

  formatFullDate(iso: string): string {
    const d = parseIsoDate(iso);
    if (!d) return iso;
    const day = d.getUTCDate();
    const month = d.toLocaleString('es-CO', { month: 'long', timeZone: 'UTC' });
    const year = d.getUTCFullYear();
    return `${day} de ${month} de ${year}`;
  }

  formatRange(from: string, to: string): string {
    return `${this.formatFullDate(from)} — ${this.formatFullDate(to)}`;
  }

  scheduleSummaryLabel(card: AssistantExamAvailabilityUi): string {
    return card.type === 'exam_availability' ? 'Cita actual: ' : '';
  }

  emptySlotsLabel(type: string): string {
    if (type === 'theory_week') return 'No hay clases teóricas publicadas en este rango.';
    if (type === 'practical_slots') return 'No hay cupos de manejo publicados.';
    return 'No hay cupos libres en este rango.';
  }

  slotTrack(slot: { time: string; sessionId?: number | null; endTime?: string | null }): string {
    return `${slot.sessionId ?? 't'}-${slot.time}-${slot.endTime ?? ''}`;
  }

  slotLabel(slot: { time: string; endTime?: string | null }): string {
    return slot.endTime ? `${slot.time}–${slot.endTime}` : slot.time;
  }

  slotAria(slot: { time: string; endTime?: string | null; availableCapacity: number; detail?: string | null }): string {
    const range = this.slotLabel(slot);
    const detail = slot.detail ? `, ${slot.detail}` : '';
    return `Reservar ${range}${detail}, ${slot.availableCapacity} cupos`;
  }

  isSlotSelectable(
    card: AssistantExamAvailabilityUi,
    slot: { sessionId?: number | null; bookTool?: string | null }
  ): boolean {
    if (!card.selectable) return false;
    if (card.type === 'exam_availability') return true;
    return !!slot.sessionId && !!slot.bookTool;
  }

  selectSlot(
    date: string,
    slot: { time: string; sessionId?: number | null; bookTool?: string | null }
  ): void {
    if (this.busy()) return;
    const pending = this.action();
    if (pending) {
      this.api.discard(pending.id).subscribe({ error: () => undefined });
      this.action.set(null);
    }
    this.error.set(null);
    this.busy.set(true);
    this.scrollSoon();

    const req$ =
      slot.sessionId && slot.bookTool
        ? this.api.prepareSession(slot.sessionId, slot.bookTool)
        : this.api.prepareExam(date, slot.time);

    req$.subscribe({
      next: (r) => {
        this.busy.set(false);
        this.messages.update((list) => [
          ...list,
          { role: 'assistant', content: r.reply }
        ]);
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
        this.pushAssistant(r.reply, r.examAvailability ?? null);
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
        this.pushAssistant(r.reply, r.examAvailability ?? null);
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
    if (this.busy()) {
      return;
    }
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
    if (this.busy()) {
      return;
    }
    const pending = this.action();
    if (!pending || pending.id !== a.id) {
      return;
    }
    this.action.set(null);
    this.api.discard(a.id).subscribe({ error: () => undefined });
    this.messages.update((list) => [...list, { role: 'assistant', content: 'Listo, no hice ningún cambio.' }]);
    this.scrollSoon();
  }

  private pushAssistant(content: string, exam: AssistantExamAvailabilityUi | null): void {
    this.messages.update((list) => [
      ...list,
      {
        role: 'assistant',
        content,
        examAvailability: exam,
        daysVisible: exam ? INITIAL_DAYS : undefined
      }
    ]);
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

function parseIsoDate(iso: string): Date | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso.trim());
  if (!m) return null;
  const y = Number(m[1]);
  const mo = Number(m[2]) - 1;
  const d = Number(m[3]);
  const dt = new Date(Date.UTC(y, mo, d));
  if (dt.getUTCFullYear() !== y || dt.getUTCMonth() !== mo || dt.getUTCDate() !== d) return null;
  return dt;
}
