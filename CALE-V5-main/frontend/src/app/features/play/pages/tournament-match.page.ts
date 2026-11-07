import { Component, DestroyRef, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EMPTY, catchError } from 'rxjs';
import { SessionStore } from '../../../core/auth/session.store';
import { mapApiError } from '../../../core/http/map-api-error';
import { pollWhileVisible } from '../../../core/rxjs/poll-while-visible';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { AnswerFeedback, Badge, PlayQuestion } from '../api/play.api';
import {
  FINISH_REASON_LABELS,
  TournamentApi,
  TournamentHubEvent,
  TournamentHubHandle,
  TournamentMatchView
} from '../api/tournament.api';
import { PlayBadgesToastComponent } from '../components/play-badges-toast.component';
import { PlayQuestionComponent } from '../components/play-question.component';
import { PlayTopbarComponent } from '../components/play-topbar.component';
import { PlayFxService } from '../play-fx.service';
import { tournamentInitials, tournamentPhotoSrc } from '../tournament-avatar';

/** Safety net: the hub pushes MatchUpdated, polling covers dropped sockets and timeouts. */
const POLL_MS = 3000;
const FEEDBACK_MS = 1100;

interface HeldAnswer {
  question: PlayQuestion;
  feedback: AnswerFeedback;
  selected: number | null;
}

@Component({
  selector: 'app-tournament-match-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, PlayBadgesToastComponent, PlayQuestionComponent, PlayTopbarComponent],
  styleUrls: ['./play-page.css', './tournament-page.css'],
  template: `
    <section class="play-page">
      <play-topbar
        [title]="state()?.roundName || 'Enfrentamiento'"
        [subtitle]="state()?.tournamentName ?? ''"
        [back]="backLink()"
        backLabel="Volver a la sala" />

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      @if (!state()) {
        @if (!error()) {
          <div class="play-card result-hero"><p class="pulse">Cargando enfrentamiento…</p></div>
        }
      } @else {
        @if (state(); as s) {
          <div class="play-card">
            <div class="versus">
              <div class="player">
                <span class="t-avatar lg" aria-hidden="true">
                  @if (photoSrc(s.player1PhotoUrl); as src) {
                    <img [src]="src" alt="" />
                  } @else {
                    {{ initials(s.player1Name) }}
                  }
                </span>
                <strong>{{ s.player1Name }}{{ s.player1UserId === myId() ? ' (tú)' : '' }}</strong>
                <span class="score">{{ s.player1Correct }}</span>
                <small>aciertos</small>
              </div>
              @if (s.status === 'active') {
                <span class="timer" [class.low]="secondsLeft() <= 5">⏱ {{ secondsLeft() }} s</span>
              } @else {
                <span class="vs">VS</span>
              }
              <div class="player right">
                <span class="t-avatar lg" aria-hidden="true">
                  @if (photoSrc(s.player2PhotoUrl); as src) {
                    <img [src]="src" alt="" />
                  } @else {
                    {{ initials(s.player2Name) }}
                  }
                </span>
                <strong>{{ s.player2Name }}{{ s.player2UserId === myId() ? ' (tú)' : '' }}</strong>
                <span class="score">{{ s.player2Correct }}</span>
                <small>aciertos</small>
              </div>
            </div>
          </div>

          @switch (s.status) {
            @case ('pending') {
              <div class="play-card result-hero">
                <h2>Todavía no empieza</h2>
                <p class="pulse">Esperando a que termine la ronda anterior…</p>
              </div>
            }
            @case ('active') {
              @if (!s.amIPlayer) {
                <div class="play-card result-hero">
                  <h2>Estás viendo este enfrentamiento</h2>
                  <p class="pulse">{{ progressText() }}</p>
                </div>
              } @else {
                @if (shownQuestion(); as q) {
                  <div class="play-card">
                    <div class="play-progress">
                      <span>{{ progressText() }}</span>
                      <span class="bar">
                        <span [style.width.%]="(secondsLeft() / (s.secondsPerQuestion || 1)) * 100"></span>
                      </span>
                    </div>
                    <play-question
                      [question]="q"
                      [reportable]="false"
                      [feedback]="held()?.feedback ?? null"
                      [selectedId]="held()?.selected ?? selected()"
                      [busy]="busy()"
                      (answered)="answer($event)" />
                  </div>
                } @else {
                  <div class="play-card result-hero">
                    <h2>¡Respuesta enviada!</h2>
                    <p class="pulse">Esperando a {{ opponentName() }}…</p>
                  </div>
                }
              }
            }
            @case ('finished') {
              <div class="play-card result-hero">
                @if (s.amIPlayer) {
                  <p class="big">{{ iWon() ? '🏆' : '💪' }}</p>
                  <h2>{{ iWon() ? '¡Avanzas de ronda!' : 'Quedaste eliminado' }}</h2>
                } @else {
                  <p class="big">🏁</p>
                  <h2>Enfrentamiento finalizado</h2>
                }
                <p>
                  Ganó {{ winnerName() }}
                  @if (reasonText()) {
                    {{ reasonText() }}
                  }
                </p>
                <div class="play-actions center">
                  <ui-button [routerLink]="base + '/' + s.tournamentId + '/bracket'">Ver llaves</ui-button>
                  <ui-button [routerLink]="base + '/' + s.tournamentId" variant="secondary">Volver a la sala</ui-button>
                </div>
              </div>
            }
          }
        }
      }
      <play-badges-toast [badges]="newBadges()" (closed)="newBadges.set([])" />
    </section>
  `
})
export class TournamentMatchPage implements OnInit, OnDestroy {
  private readonly api = inject(TournamentApi);
  private readonly session = inject(SessionStore);
  private readonly fx = inject(PlayFxService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);

  readonly base: string = this.route.snapshot.data['base'] ?? '/student/play/tournaments';
  readonly matchId = Number(this.route.snapshot.paramMap.get('matchId'));

  readonly state = signal<TournamentMatchView | null>(null);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);
  readonly selected = signal<number | null>(null);
  readonly held = signal<HeldAnswer | null>(null);
  readonly newBadges = signal<Badge[]>([]);

  private readonly fetchedAt = signal(Date.now());
  private readonly now = signal(Date.now());

  readonly myId = computed(() => this.session.user()?.id ?? -1);
  photoSrc = tournamentPhotoSrc;
  initials = tournamentInitials;

  readonly backLink = computed(() => {
    const s = this.state();
    return s ? `${this.base}/${s.tournamentId}` : this.base;
  });

  /** Server countdown corrected by the time elapsed since the last fetch. */
  readonly secondsLeft = computed(() => {
    const s = this.state();
    if (!s || s.secondsLeft == null) return 0;
    const elapsed = Math.floor((this.now() - this.fetchedAt()) / 1000);
    return Math.max(0, s.secondsLeft - elapsed);
  });

  /** Current question only while I have not answered it yet. */
  private readonly pending = computed<PlayQuestion | null>(() => {
    const s = this.state();
    if (!s || s.status !== 'active' || !s.amIPlayer || !s.currentQuestion || s.currentMatchQuestionId == null) {
      return null;
    }
    const q = s.currentQuestion;
    return s.myAnswers.some((a) => a.questionId === q.id) ? null : q;
  });

  readonly shownQuestion = computed(() => this.held()?.question ?? this.pending());

  readonly iWon = computed(() => {
    const s = this.state();
    return !!s && s.winnerUserId != null && s.winnerUserId === this.myId();
  });

  readonly opponentName = computed(() => {
    const s = this.state();
    if (!s) return 'tu rival';
    return s.player1UserId === this.myId() ? s.player2Name : s.player1Name;
  });

  readonly winnerName = computed(() => {
    const s = this.state();
    if (!s) return '';
    if (s.winnerUserId != null && s.winnerUserId === s.player1UserId) return s.player1Name;
    if (s.winnerUserId != null && s.winnerUserId === s.player2UserId) return s.player2Name;
    return 'nadie';
  });

  readonly reasonText = computed(() => {
    const reason = this.state()?.finishReason;
    return reason ? FINISH_REASON_LABELS[reason] ?? '' : '';
  });

  readonly progressText = computed(() => {
    const s = this.state();
    if (!s) return '';
    return s.currentQuestionIndex >= s.totalQuestions
      ? '⚡ Muerte súbita: la próxima respuesta decide'
      : `Pregunta ${s.currentQuestionIndex + 1} de ${s.totalQuestions}`;
  });

  private hub: TournamentHubHandle | null = null;
  private hubTournamentId: number | null = null;
  private destroyed = false;
  private fetching = false;
  private refetchQueued = false;
  private lastStatus: string | null = null;
  private clock: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    if (!Number.isFinite(this.matchId)) {
      this.error.set('Enfrentamiento no válido.');
      return;
    }
    this.clock = setInterval(() => this.now.set(Date.now()), 500);
    this.refresh();

    pollWhileVisible(
      POLL_MS,
      () => this.api.match(this.matchId).pipe(catchError(() => EMPTY)),
      { leading: false }
    )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((s) => this.apply(s));
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    if (this.clock) clearInterval(this.clock);
    void this.hub?.close();
    this.hub = null;
  }

  answer(optionId: number): void {
    const s = this.state();
    const q = this.pending();
    if (!s || !q || s.currentMatchQuestionId == null || this.busy() || this.held()) return;
    this.busy.set(true);
    this.selected.set(optionId);
    this.error.set(null);
    this.api.answer(s.matchId, s.currentMatchQuestionId, optionId).subscribe({
      next: (res) => {
        this.busy.set(false);
        this.fx.play(res.correct ? 'correct' : 'wrong');
        if (res.newBadges?.length) {
          this.newBadges.set(res.newBadges);
          this.fx.play('badge');
        }
        // Keep the answered question on screen while the feedback is visible.
        this.held.set({
          question: q,
          selected: optionId,
          feedback: {
            correct: res.correct,
            correctOptionId: res.correctOptionId,
            explanation: res.explanation
          }
        });
        this.apply(res.state);
        setTimeout(() => {
          this.held.set(null);
          this.selected.set(null);
        }, FEEDBACK_MS);
      },
      error: (err) => {
        this.busy.set(false);
        this.selected.set(null);
        this.error.set(mapApiError(err));
        // The question may have timed out or advanced: resync.
        this.refresh();
      }
    });
  }

  private refresh(): void {
    if (this.fetching) {
      this.refetchQueued = true;
      return;
    }
    this.fetching = true;
    this.api.match(this.matchId).subscribe({
      next: (s) => {
        this.fetching = false;
        this.apply(s);
        if (this.refetchQueued) {
          this.refetchQueued = false;
          this.refresh();
        }
      },
      error: (err) => {
        this.fetching = false;
        this.refetchQueued = false;
        this.error.set(mapApiError(err));
      }
    });
  }

  private apply(s: TournamentMatchView): void {
    const previous = this.lastStatus;
    this.lastStatus = s.status;
    this.state.set(s);
    this.fetchedAt.set(Date.now());
    this.now.set(Date.now());

    if (previous === 'active' && s.status === 'finished' && s.amIPlayer) {
      if (s.winnerUserId === this.myId()) this.fx.celebrate();
      else this.fx.play('wrong');
    }
    if (this.hubTournamentId === null) {
      this.hubTournamentId = s.tournamentId;
      void this.connect(s.tournamentId);
    }
  }

  private async connect(tournamentId: number): Promise<void> {
    try {
      const handle = await this.api.watch(tournamentId, (event, payload) => this.onEvent(event, payload));
      if (this.destroyed) {
        void handle.close();
        return;
      }
      this.hub = handle;
      handle.connection.onreconnected(() => this.refresh());
      this.refresh();
    } catch {
      /* polling keeps the match in sync */
    }
  }

  private onEvent(event: TournamentHubEvent, payload: unknown): void {
    switch (event) {
      case 'MatchUpdated':
      case 'MatchFinished': {
        // Payloads are built for the player who answered; always re-read our own view.
        const id = (payload as { matchId?: number; id?: number } | null);
        const target = id?.matchId ?? id?.id;
        if (target === undefined || target === this.matchId) this.refresh();
        break;
      }
      case 'AchievementUnlocked':
        if (Array.isArray(payload) && payload.length) {
          this.newBadges.set(payload as Badge[]);
          this.fx.play('badge');
        }
        break;
      case 'RoundStarted':
      case 'TournamentFinished':
      case 'ChampionDeclared':
        this.refresh();
        break;
      default:
        break;
    }
  }
}
