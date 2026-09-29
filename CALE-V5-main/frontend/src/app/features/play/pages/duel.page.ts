import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';
import { SessionStore } from '../../../core/auth/session.store';
import { mapApiError } from '../../../core/http/map-api-error';
import { pollWhileVisible } from '../../../core/rxjs/poll-while-visible';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { AnswerFeedback, Badge, DuelState, PlayApi } from '../api/play.api';
import { PlayBadgesToastComponent } from '../components/play-badges-toast.component';
import { PlayQuestionComponent } from '../components/play-question.component';
import { PlayTopbarComponent } from '../components/play-topbar.component';
import { PlayFxService } from '../play-fx.service';
import { shareCard } from '../share-card';

const POLL_MS = 1500;

@Component({
  selector: 'app-duel-page',
  standalone: true,
  imports: [
    FormsModule,
    UiButtonComponent,
    PlayBadgesToastComponent,
    PlayQuestionComponent,
    PlayTopbarComponent
  ],
  styleUrl: './play-page.css',
  styles: [`
    .lobby { display: grid; gap: 1rem; grid-template-columns: repeat(auto-fit, minmax(16rem, 1fr)); }
    .lobby .play-card { display: grid; gap: 0.75rem; align-content: start; }
    .lobby h2 { margin: 0; font-size: var(--text-lg); }
    .lobby p { margin: 0; color: var(--color-text-secondary); line-height: 1.45; }
    .code-input {
      width: 100%;
      padding: 0.75rem 0.9rem;
      font: inherit;
      font-size: 1.4rem;
      font-weight: 800;
      letter-spacing: 0.25em;
      text-align: center;
      text-transform: uppercase;
      color: var(--color-text);
      background: var(--color-surface-raised);
      border: 2px solid var(--color-border);
      border-radius: var(--radius-md);
    }
    .code-input:focus { outline: none; border-color: var(--color-primary); }
    .code {
      margin: 0.25rem 0;
      font-size: clamp(2.4rem, 9vw, 3.4rem);
      font-weight: 800;
      letter-spacing: 0.2em;
      color: var(--color-primary);
    }
    .pulse { animation: pulse 1.4s ease-in-out infinite; }
    .versus { display: grid; grid-template-columns: 1fr auto 1fr; gap: 0.75rem; align-items: center; }
    .player { display: grid; gap: 0.3rem; }
    .player.right { text-align: right; }
    .player strong { font-size: var(--text-md); }
    .player small { color: var(--color-text-secondary); }
    .vs { font-weight: 800; color: var(--color-text-secondary); }
    .mini { height: 0.5rem; border-radius: 999px; background: var(--color-chip); overflow: hidden; }
    .mini span { display: block; height: 100%; background: var(--color-primary); transition: width 0.3s ease; }
    .player.right .mini span { margin-left: auto; background: #f59e0b; }
    .timer { font-weight: 800; font-variant-numeric: tabular-nums; }
    .timer.low { color: var(--color-danger); }
    @keyframes pulse { 50% { opacity: 0.45; } }
  `],
  template: `
    <section class="play-page">
      <play-topbar title="Duelo 1 vs 1" subtitle="Reta a un compañero: 7 preguntas, 3 minutos. Gana quien acierte más (y si empatan, el más rápido)." />

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      @if (!state()) {
        <div class="lobby">
          <div class="play-card">
            <h2>Crear un duelo</h2>
            <p>Te damos un código de 6 letras para que se lo compartas a tu compañero.</p>
            <ui-button type="button" [loading]="busy()" (click)="create()">Crear duelo</ui-button>
          </div>
          <div class="play-card">
            <h2>Unirme con código</h2>
            <p>¿Tu compañero ya creó el duelo? Escribe su código.</p>
            <input
              class="code-input"
              maxlength="6"
              autocomplete="off"
              aria-label="Código del duelo"
              placeholder="ABC123"
              [(ngModel)]="joinCode" />
            <ui-button type="button" variant="secondary" [loading]="busy()" [disabled]="joinCode.trim().length < 6" (click)="join()">
              Unirme
            </ui-button>
          </div>
        </div>
      } @else {
        @if (state(); as s) {
        @switch (s.status) {
          @case ('waiting') {
            <div class="play-card result-hero">
              <p class="muted">Comparte este código con tu compañero</p>
              <p class="code">{{ s.code }}</p>
              <p class="pulse">Esperando a que se una tu rival…</p>
              <div class="play-actions center">
                <ui-button type="button" variant="secondary" (click)="copyCode()">{{ copied() ? '¡Copiado!' : 'Copiar código' }}</ui-button>
                <ui-button type="button" variant="ghost" (click)="cancel()">Cancelar</ui-button>
              </div>
            </div>
          }
          @case ('playing') {
            <div class="play-card">
              <div class="versus">
                <div class="player">
                  <strong>Tú</strong>
                  <small>{{ s.me.answered }}/{{ s.questions.length }} respondidas</small>
                  <span class="mini"><span [style.width.%]="(s.me.answered / (s.questions.length || 1)) * 100"></span></span>
                </div>
                <span class="timer" [class.low]="(s.secondsLeft ?? 99) <= 20">⏱ {{ formatSeconds(s.secondsLeft) }}</span>
                <div class="player right">
                  <strong>{{ s.opponent?.name ?? 'Rival' }}</strong>
                  <small>{{ s.opponent?.answered ?? 0 }}/{{ s.questions.length }} respondidas</small>
                  <span class="mini"><span [style.width.%]="((s.opponent?.answered ?? 0) / (s.questions.length || 1)) * 100"></span></span>
                </div>
              </div>
            </div>
            @if (question(); as q) {
              <div class="play-card">
                <div class="play-progress">
                  <span>Pregunta {{ questionIndex() + 1 }} de {{ s.questions.length }}</span>
                </div>
                <play-question
                  [question]="q"
                  [feedback]="feedback()"
                  [selectedId]="selected()"
                  [busy]="busy()"
                  (answered)="answer($event)" />
              </div>
            } @else {
              <div class="play-card result-hero">
                <h2>¡Terminaste!</h2>
                <p class="pulse">Esperando a que {{ s.opponent?.name ?? 'tu rival' }} termine…</p>
              </div>
            }
          }
          @case ('finished') {
            <div class="play-card result-hero">
              <p class="big">{{ resultEmoji() }}</p>
              <h2>{{ resultTitle() }}</h2>
              <div class="versus">
                <div class="player">
                  <strong>Tú</strong>
                  <small>{{ s.me.correct }} aciertos · {{ formatSeconds(s.me.timeSeconds) }}</small>
                </div>
                <span class="vs">VS</span>
                <div class="player right">
                  <strong>{{ s.opponent?.name ?? 'Rival' }}</strong>
                  <small>{{ s.opponent?.correct ?? 0 }} aciertos · {{ formatSeconds(s.opponent?.timeSeconds) }}</small>
                </div>
              </div>
              <div class="play-actions center">
                <ui-button type="button" (click)="reset()">Nuevo duelo</ui-button>
                @if (s.result === 'won') {
                  <ui-button type="button" variant="secondary" [loading]="sharing()" (click)="share()">Compartir victoria</ui-button>
                }
                <ui-button routerLink="/student/play/ranking" variant="ghost">Ver ranking</ui-button>
              </div>
              @if (shareMsg()) {
                <p class="muted">{{ shareMsg() }}</p>
              }
            </div>
          }
        }
        }
      }
      <play-badges-toast [badges]="newBadges()" (closed)="newBadges.set([])" />
    </section>
  `
})
export class DuelPage implements OnInit, OnDestroy {
  private readonly api = inject(PlayApi);
  private readonly fx = inject(PlayFxService);
  private readonly route = inject(ActivatedRoute);
  private readonly session = inject(SessionStore);

  readonly state = signal<DuelState | null>(null);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);
  readonly feedback = signal<AnswerFeedback | null>(null);
  readonly selected = signal<number | null>(null);
  readonly copied = signal(false);
  readonly newBadges = signal<Badge[]>([]);
  readonly sharing = signal(false);
  readonly shareMsg = signal<string | null>(null);
  /** Keeps the answered question on screen while the feedback is visible. */
  private readonly heldIndex = signal<number | null>(null);
  joinCode = '';

  private poll: Subscription | null = null;
  private lastStatus: string | null = null;

  readonly questionIndex = computed(() => this.heldIndex() ?? this.state()?.myAnswers.length ?? 0);
  readonly question = computed(() => this.state()?.questions[this.questionIndex()] ?? null);
  readonly resultTitle = computed(() => {
    switch (this.state()?.result) {
      case 'won': return '¡Ganaste el duelo!';
      case 'lost': return 'Esta vez ganó tu rival';
      default: return '¡Empate!';
    }
  });
  readonly resultEmoji = computed(() => {
    switch (this.state()?.result) {
      case 'won': return '🏆';
      case 'lost': return '💪';
      default: return '🤝';
    }
  });

  ngOnInit(): void {
    const code = this.route.snapshot.queryParamMap.get('code');
    if (code) {
      this.joinCode = code.toUpperCase();
      this.join();
    }
  }

  ngOnDestroy(): void {
    this.stopPolling();
    const s = this.state();
    if (s?.status === 'waiting') {
      this.api.cancelDuel(s.code).subscribe({ error: () => undefined });
    }
  }

  create(): void {
    this.run(this.api.createDuel());
  }

  join(): void {
    const code = this.joinCode.trim().toUpperCase();
    if (code.length < 6) return;
    this.run(this.api.joinDuel(code));
  }

  answer(optionId: number): void {
    const s = this.state();
    const q = this.question();
    if (!s || !q || this.busy() || this.feedback()) return;
    this.selected.set(optionId);
    this.heldIndex.set(this.questionIndex());
    this.busy.set(true);
    this.api.answerDuel(s.code, q.id, optionId).subscribe({
      next: (res) => {
        this.busy.set(false);
        this.feedback.set(res);
        this.fx.play(res.correct ? 'correct' : 'wrong');
        setTimeout(() => {
          this.feedback.set(null);
          this.selected.set(null);
          this.heldIndex.set(null);
          this.apply(res.state);
        }, 900);
      },
      error: (err) => {
        this.busy.set(false);
        this.selected.set(null);
        this.heldIndex.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }

  cancel(): void {
    const s = this.state();
    if (s) this.api.cancelDuel(s.code).subscribe({ error: () => undefined });
    this.reset();
  }

  reset(): void {
    this.stopPolling();
    this.state.set(null);
    this.lastStatus = null;
    this.joinCode = '';
    this.feedback.set(null);
    this.selected.set(null);
    this.heldIndex.set(null);
    this.shareMsg.set(null);
    this.error.set(null);
  }

  async copyCode(): Promise<void> {
    const code = this.state()?.code;
    if (!code) return;
    try {
      await navigator.clipboard.writeText(code);
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 1800);
    } catch {
      this.copied.set(false);
    }
  }

  formatSeconds(value?: number | null): string {
    if (value == null) return '—';
    const m = Math.floor(value / 60);
    const s = value % 60;
    return `${m}:${s.toString().padStart(2, '0')}`;
  }

  async share(): Promise<void> {
    const s = this.state();
    if (!s) return;
    this.sharing.set(true);
    this.shareMsg.set(null);
    try {
      const result = await shareCard({
        kicker: 'Duelo 1 vs 1',
        headline: `🏆 ${s.me.correct}/${s.questions.length}`,
        title: `¡Gané el duelo contra ${s.opponent?.name ?? 'mi rival'}!`,
        details: ['¿Te atreves a retarme?'],
        name: this.session.user()?.name,
        tone: 'success'
      });
      if (result === 'downloaded') this.shareMsg.set('Imagen descargada. Ya puedes subirla a tus redes.');
    } catch {
      this.shareMsg.set('No se pudo generar la imagen en este navegador.');
    } finally {
      this.sharing.set(false);
    }
  }

  private run(request: ReturnType<PlayApi['duel']>): void {
    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: (s) => {
        this.busy.set(false);
        this.apply(s);
        this.startPolling();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  private apply(s: DuelState): void {
    const previous = this.lastStatus;
    this.lastStatus = s.status;
    if (this.heldIndex() === null) {
      this.state.set(s);
    } else {
      this.state.update((cur) => (cur ? { ...s, myAnswers: cur.myAnswers } : s));
    }

    if (previous === 'waiting' && s.status === 'playing') {
      this.fx.play('start');
    }
    if (s.status === 'finished' && previous !== 'finished') {
      this.stopPolling();
      if (s.result === 'won') this.fx.celebrate();
      else this.fx.play(s.result === 'draw' ? 'start' : 'wrong');
      if (s.newBadges.length) {
        this.newBadges.set(s.newBadges);
        this.fx.play('badge');
      }
    }
  }

  private startPolling(): void {
    this.stopPolling();
    const code = this.state()?.code;
    if (!code || this.state()?.status === 'finished') return;
    this.poll = pollWhileVisible(POLL_MS, () => this.api.duel(code), { leading: false }).subscribe({
      next: (s) => this.apply(s),
      error: (err) => {
        this.stopPolling();
        this.error.set(mapApiError(err));
      }
    });
  }

  private stopPolling(): void {
    this.poll?.unsubscribe();
    this.poll = null;
  }
}
