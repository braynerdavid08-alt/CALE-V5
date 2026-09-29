import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { SessionStore } from '../../../core/auth/session.store';
import { mapApiError } from '../../../core/http/map-api-error';
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { Badge, GameSaved, PlayApi, Sign } from '../api/play.api';
import { PlayBadgesToastComponent } from '../components/play-badges-toast.component';
import { PlayTopbarComponent } from '../components/play-topbar.component';
import { PlayFxService } from '../play-fx.service';
import { shareCard } from '../share-card';

const GAME_SECONDS = 60;
const OPTION_COUNT = 4;

interface SignRound {
  sign: Sign;
  options: Sign[];
}

@Component({
  selector: 'app-signs-game-page',
  standalone: true,
  imports: [UiButtonComponent, UiLoadingComponent, PlayBadgesToastComponent, PlayTopbarComponent],
  styleUrl: './play-page.css',
  styles: [`
    .intro { text-align: center; display: grid; gap: 0.75rem; justify-items: center; }
    .intro .emoji { font-size: 3.5rem; line-height: 1; }
    .intro p { margin: 0; max-width: 32rem; color: var(--color-text-secondary); line-height: 1.5; }
    .hud { display: flex; justify-content: space-between; align-items: center; gap: 0.75rem; font-weight: 700; }
    .timer { font-size: 1.4rem; font-variant-numeric: tabular-nums; }
    .timer.low { color: var(--color-danger); }
    .score { font-size: 1.4rem; color: var(--color-success); }
    .sign-img {
      display: grid;
      place-items: center;
      height: clamp(9rem, 30vw, 13rem);
      margin: 0.5rem 0 1rem;
    }
    .sign-img img { max-height: 100%; max-width: 70%; object-fit: contain; }
    .sign-img.flash-ok img { animation: ok 0.3s ease; }
    .sign-img.flash-bad img { animation: bad 0.3s ease; }
    .opts { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.6rem; }
    .opt {
      min-height: 3.6rem;
      padding: 0.6rem 0.8rem;
      font: inherit;
      font-weight: 600;
      color: var(--color-text);
      background: var(--color-surface);
      border: 2px solid var(--color-border);
      border-radius: var(--radius-md);
      cursor: pointer;
      transition: border-color 0.12s ease, transform 0.1s ease;
    }
    .opt:hover { border-color: var(--color-primary); transform: translateY(-1px); }
    .opt.ok { border-color: var(--color-success); background: var(--color-success-soft); }
    .opt.bad { border-color: var(--color-danger); background: var(--color-danger-soft); }
    @media (max-width: 520px) { .opts { grid-template-columns: 1fr; } }
    @keyframes ok { 50% { transform: scale(1.08); } }
    @keyframes bad { 25% { transform: translateX(-8px); } 75% { transform: translateX(8px); } }
  `],
  template: `
    <section class="play-page">
      <play-topbar title="Señal relámpago" subtitle="¿Cuántas señales reconoces en 60 segundos?" />

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      @if (loading()) {
        <ui-loading />
      } @else {
        @switch (phase()) {
          @case ('intro') {
            <div class="play-card intro">
              <span class="emoji" aria-hidden="true">⚡🚦</span>
              <h2>¿Listo?</h2>
              <p>Verás una señal de tránsito y 4 nombres. Toca el correcto lo más rápido que puedas. Los errores no restan, pero te hacen perder tiempo.</p>
              <ui-button type="button" (click)="start()">¡Empezar!</ui-button>
            </div>
          }
          @case ('playing') {
            @if (round(); as r) {
              <div class="play-card">
                <div class="hud">
                  <span class="timer" [class.low]="secondsLeft() <= 10">⏱ {{ secondsLeft() }} s</span>
                  <span class="score">✔ {{ correct() }}</span>
                </div>
                <div class="sign-img" [class.flash-ok]="flash() === 'ok'" [class.flash-bad]="flash() === 'bad'">
                  <img [src]="media(r.sign.imageUrl)" alt="Señal de tránsito" />
                </div>
                <div class="opts">
                  @for (o of r.options; track o.code) {
                    <button
                      type="button"
                      class="opt"
                      [class.ok]="picked() && o.code === r.sign.code"
                      [class.bad]="picked() === o.code && o.code !== r.sign.code"
                      [disabled]="!!picked()"
                      (click)="pick(o)">
                      {{ o.name }}
                    </button>
                  }
                </div>
              </div>
            }
          }
          @case ('done') {
            <div class="play-card result-hero">
              <p class="big">{{ correct() }}</p>
              <h2>{{ saved()?.isRecord ? '¡Nuevo récord!' : 'Señales reconocidas' }}</h2>
              <p>
                Respondiste {{ total() }} señales.
                @if (saved(); as s) { Tu mejor marca es {{ s.best }}. }
              </p>
              <div class="play-actions center">
                <ui-button type="button" (click)="start()">Jugar otra vez</ui-button>
                <ui-button type="button" variant="secondary" [loading]="sharing()" (click)="share()">Compartir</ui-button>
                <ui-button routerLink="/student/play/ranking" variant="ghost">Ver ranking</ui-button>
              </div>
              @if (shareMsg()) {
                <p class="muted">{{ shareMsg() }}</p>
              }
            </div>
          }
        }
      }
      <play-badges-toast [badges]="newBadges()" (closed)="newBadges.set([])" />
    </section>
  `
})
export class SignsGamePage implements OnInit, OnDestroy {
  private readonly api = inject(PlayApi);
  private readonly fx = inject(PlayFxService);
  private readonly session = inject(SessionStore);

  readonly media = resolveMediaUrl;
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly phase = signal<'intro' | 'playing' | 'done'>('intro');
  readonly round = signal<SignRound | null>(null);
  readonly picked = signal<string | null>(null);
  readonly flash = signal<'ok' | 'bad' | null>(null);
  readonly secondsLeft = signal(GAME_SECONDS);
  readonly correct = signal(0);
  readonly total = signal(0);
  readonly saved = signal<GameSaved | null>(null);
  readonly newBadges = signal<Badge[]>([]);
  readonly sharing = signal(false);
  readonly shareMsg = signal<string | null>(null);

  private signs: Sign[] = [];
  private deck: Sign[] = [];
  private timer: ReturnType<typeof setInterval> | null = null;
  private endsAt = 0;

  ngOnInit(): void {
    this.api.signs().subscribe({
      next: (signs) => {
        this.signs = signs.filter((s) => s.imageUrl && s.name);
        this.loading.set(false);
        if (this.signs.length < OPTION_COUNT) {
          this.error.set('Todavía no hay suficientes señales para jugar.');
        }
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }

  ngOnDestroy(): void {
    this.stopTimer();
  }

  start(): void {
    if (this.signs.length < OPTION_COUNT) return;
    this.correct.set(0);
    this.total.set(0);
    this.saved.set(null);
    this.shareMsg.set(null);
    this.deck = shuffle([...this.signs]);
    this.nextRound();
    this.phase.set('playing');
    this.fx.play('start');
    this.endsAt = Date.now() + GAME_SECONDS * 1000;
    this.secondsLeft.set(GAME_SECONDS);
    this.stopTimer();
    this.timer = setInterval(() => {
      const left = Math.max(0, Math.ceil((this.endsAt - Date.now()) / 1000));
      if (left !== this.secondsLeft()) {
        this.secondsLeft.set(left);
        if (left > 0 && left <= 5) this.fx.play('tick');
      }
      if (left <= 0) this.finish();
    }, 200);
  }

  pick(option: Sign): void {
    const r = this.round();
    if (!r || this.picked() || this.phase() !== 'playing') return;
    const ok = option.code === r.sign.code;
    this.picked.set(option.code);
    this.flash.set(ok ? 'ok' : 'bad');
    this.total.update((n) => n + 1);
    if (ok) this.correct.update((n) => n + 1);
    this.fx.play(ok ? 'correct' : 'wrong');
    setTimeout(() => {
      if (this.phase() === 'playing') this.nextRound();
    }, ok ? 350 : 800);
  }

  async share(): Promise<void> {
    this.sharing.set(true);
    this.shareMsg.set(null);
    try {
      const result = await shareCard({
        kicker: 'Señal relámpago',
        headline: `⚡ ${this.correct()}`,
        title: 'señales reconocidas en 60 segundos',
        details: ['¿Puedes superarme?'],
        name: this.session.user()?.name,
        tone: 'primary'
      });
      if (result === 'downloaded') this.shareMsg.set('Imagen descargada. Ya puedes subirla a tus redes.');
    } catch {
      this.shareMsg.set('No se pudo generar la imagen en este navegador.');
    } finally {
      this.sharing.set(false);
    }
  }

  private nextRound(): void {
    if (!this.deck.length) this.deck = shuffle([...this.signs]);
    const sign = this.deck.pop()!;
    const sameFamily = shuffle(this.signs.filter((s) => s.family === sign.family && s.code !== sign.code));
    const others = shuffle(this.signs.filter((s) => s.family !== sign.family));
    const distractors = [...sameFamily, ...others]
      .filter((s, i, arr) => s.name !== sign.name && arr.findIndex((x) => x.name === s.name) === i)
      .slice(0, OPTION_COUNT - 1);
    this.round.set({ sign, options: shuffle([sign, ...distractors]) });
    this.picked.set(null);
    this.flash.set(null);
  }

  private finish(): void {
    if (this.phase() !== 'playing') return;
    this.stopTimer();
    this.phase.set('done');
    this.api.saveSigns(this.correct(), this.total()).subscribe({
      next: (res) => {
        this.saved.set(res);
        if (res.isRecord && res.score > 0) {
          this.fx.celebrate();
        } else {
          this.fx.play('start');
        }
        if (res.newBadges.length) {
          this.newBadges.set(res.newBadges);
          this.fx.play('badge');
        }
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private stopTimer(): void {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }
}

function shuffle<T>(items: T[]): T[] {
  for (let i = items.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [items[i], items[j]] = [items[j], items[i]];
  }
  return items;
}
