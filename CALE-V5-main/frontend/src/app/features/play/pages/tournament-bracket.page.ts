import { NgTemplateOutlet } from '@angular/common';
import { Component, DestroyRef, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EMPTY, catchError } from 'rxjs';
import { SessionStore } from '../../../core/auth/session.store';
import { mapApiError } from '../../../core/http/map-api-error';
import { pollWhileVisible } from '../../../core/rxjs/poll-while-visible';
import {
  FINISH_REASON_LABELS,
  TournamentApi,
  TournamentBracket,
  TournamentBracketMatch,
  TournamentHubHandle,
  tournamentStatusLabel,
  tournamentStatusTone
} from '../api/tournament.api';
import { PlayTopbarComponent } from '../components/play-topbar.component';
import { tournamentInitials, tournamentPhotoSrc } from '../tournament-avatar';
import {
  tournamentFootText,
  tournamentMatchLink,
  tournamentNeedsRealtime,
  tournamentSlotName
} from '../tournament-host.logic';

const FALLBACK_REFRESH_MS = 8000;

interface BracketRound {
  number: number;
  name: string;
  matches: TournamentBracketMatch[];
}

@Component({
  selector: 'app-tournament-bracket-page',
  standalone: true,
  imports: [NgTemplateOutlet, RouterLink, PlayTopbarComponent],
  styleUrls: ['./play-page.css', './tournament-page.css'],
  template: `
    <section class="play-page wide">
      <play-topbar
        title="Llaves del torneo"
        subtitle="Eliminación directa: quien gana avanza a la siguiente ronda."
        [back]="base + '/' + id"
        backLabel="Volver a la sala" />

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      @if (!bracket()) {
        @if (!error()) {
          <div class="play-card result-hero"><p class="pulse">Cargando llaves…</p></div>
        }
      } @else {
        @if (bracket(); as b) {
          <div class="t-toolbar">
            <span class="chip" [class.good]="tone(b.status) === 'good'" [class.warn]="tone(b.status) === 'warn'">
              {{ label(b.status) }}
            </span>
            @if (needsRealtime(b.status)) {
              <span class="chip" [class.good]="live()" [class.neutral]="!live()">
                {{ live() ? '● En vivo' : '○ Se actualiza cada pocos segundos' }}
              </span>
            }
          </div>

          @if (b.championName) {
            <div class="play-card champion">
              <p class="big">🏆</p>
              <span class="t-avatar lg" aria-hidden="true">
                @if (photoSrc(b.championPhotoUrl); as src) {
                  <img [src]="src" alt="" />
                } @else {
                  {{ initials(b.championName) }}
                }
              </span>
              <span class="muted">Campeón</span>
              <strong>{{ b.championName }}</strong>
            </div>
          }

          @if (!rounds().length) {
            <div class="play-card result-hero">
              <h2>Las llaves aún no se arman</h2>
              <p>Aparecerán cuando el organizador inicie el torneo.</p>
            </div>
          } @else {
            <div class="bracket-scroll" tabindex="0" aria-label="Llaves del torneo (desliza horizontalmente)">
              <div class="bracket">
                @for (r of rounds(); track r.number) {
                  <div class="round">
                    <h3>{{ r.name }}</h3>
                    <div class="round-matches">
                      @for (m of r.matches; track m.id) {
                        @if (matchLink(m); as link) {
                          <a class="bm" [class.active]="m.status === 'active'" [routerLink]="link">
                            <ng-container *ngTemplateOutlet="card; context: { $implicit: m }" />
                          </a>
                        } @else {
                          <div class="bm" [class.active]="m.status === 'active'">
                            <ng-container *ngTemplateOutlet="card; context: { $implicit: m }" />
                          </div>
                        }
                      }
                    </div>
                  </div>
                }
              </div>
            </div>
          }
        }
      }

      <ng-template #card let-m>
        <div
          class="bm-row"
          [class.winner]="m.winnerUserId != null && m.winnerUserId === m.player1UserId"
          [class.empty]="!m.player1Name"
          [class.me]="m.player1UserId === myId()">
          <span class="t-avatar sm" aria-hidden="true">
            @if (slotPhoto(m, 1); as src) {
              <img [src]="src" alt="" />
            } @else {
              {{ initials(slotName(m, 1)) }}
            }
          </span>
          <strong>{{ slotName(m, 1) }}</strong>
          <span class="bm-score">{{ m.player1Correct }}</span>
        </div>
        <div
          class="bm-row"
          [class.winner]="m.winnerUserId != null && m.winnerUserId === m.player2UserId"
          [class.empty]="!m.player2Name"
          [class.me]="m.player2UserId === myId()">
          <span class="t-avatar sm" aria-hidden="true">
            @if (slotPhoto(m, 2); as src) {
              <img [src]="src" alt="" />
            } @else {
              {{ initials(slotName(m, 2)) }}
            }
          </span>
          <strong>{{ slotName(m, 2) }}</strong>
          <span class="bm-score">{{ m.player2Correct }}</span>
        </div>
        @if (footText(m); as foot) {
          <div class="bm-foot">{{ foot }}</div>
        }
      </ng-template>
    </section>
  `
})
export class TournamentBracketPage implements OnInit, OnDestroy {
  private readonly api = inject(TournamentApi);
  private readonly session = inject(SessionStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);

  readonly base: string = this.route.snapshot.data['base'] ?? '/student/play/tournaments';
  readonly id = Number(this.route.snapshot.paramMap.get('id'));

  readonly bracket = signal<TournamentBracket | null>(null);
  readonly error = signal<string | null>(null);
  readonly live = signal(false);

  readonly myId = computed(() => this.session.user()?.id ?? -1);

  readonly rounds = computed<BracketRound[]>(() => {
    const matches = this.bracket()?.matches ?? [];
    const byRound = new Map<number, BracketRound>();
    for (const m of matches) {
      let round = byRound.get(m.roundNumber);
      if (!round) {
        round = { number: m.roundNumber, name: m.roundName, matches: [] };
        byRound.set(m.roundNumber, round);
      }
      round.matches.push(m);
    }
    return [...byRound.values()]
      .sort((a, b) => a.number - b.number)
      .map((r) => ({ ...r, matches: r.matches.sort((a, b) => a.slotIndex - b.slotIndex) }));
  });

  readonly label = tournamentStatusLabel;
  readonly tone = tournamentStatusTone;
  readonly needsRealtime = tournamentNeedsRealtime;
  photoSrc = tournamentPhotoSrc;
  initials = tournamentInitials;

  private hub: TournamentHubHandle | null = null;
  private destroyed = false;
  private loading = false;
  private reloadQueued = false;
  private connecting = false;
  private pollingStarted = false;

  ngOnInit(): void {
    if (!Number.isFinite(this.id)) {
      this.error.set('Torneo no válido.');
      return;
    }

    // One-shot load; hub/poll only if the tournament can still change.
    this.api.bracket(this.id).subscribe({
      next: (b) => {
        this.error.set(null);
        this.bracket.set(b);
        this.syncRealtime(b.status);
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.stopRealtime();
  }

  slotName(m: TournamentBracketMatch, player: 1 | 2): string {
    return tournamentSlotName(m, player);
  }

  slotPhoto(m: TournamentBracketMatch, player: 1 | 2): string | null {
    const isBye = player === 1 ? m.player1IsBye : m.player2IsBye;
    if (isBye) return null;
    return this.photoSrc(player === 1 ? m.player1PhotoUrl : m.player2PhotoUrl);
  }

  footText(m: TournamentBracketMatch): string {
    return tournamentFootText(m, FINISH_REASON_LABELS);
  }

  /** Anyone who can see the bracket can open live/finished matches (teachers host/spectate). */
  matchLink(m: TournamentBracketMatch): string | null {
    return tournamentMatchLink(this.base, m);
  }

  private reload(): void {
    if (this.loading) {
      this.reloadQueued = true;
      return;
    }
    this.loading = true;
    this.api.bracket(this.id).subscribe({
      next: (b) => {
        this.loading = false;
        this.error.set(null);
        this.bracket.set(b);
        this.syncRealtime(b.status);
        if (this.reloadQueued) {
          this.reloadQueued = false;
          this.reload();
        }
      },
      error: (err) => {
        this.loading = false;
        this.reloadQueued = false;
        this.error.set(mapApiError(err));
      }
    });
  }

  private syncRealtime(status: string): void {
    if (!tournamentNeedsRealtime(status)) {
      this.stopRealtime();
      return;
    }
    this.ensurePolling();
    if (!this.hub && !this.connecting) {
      void this.connect();
    }
  }

  private ensurePolling(): void {
    if (this.pollingStarted) return;
    this.pollingStarted = true;
    // Fallback refresh while the socket is down (only for open tournaments).
    pollWhileVisible(FALLBACK_REFRESH_MS, () => {
      const status = this.bracket()?.status;
      if (status && !tournamentNeedsRealtime(status)) return EMPTY;
      if (this.live() && this.bracket()) return EMPTY;
      return this.api.bracket(this.id).pipe(
        catchError((err) => {
          this.error.set(mapApiError(err));
          return EMPTY;
        })
      );
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((b) => {
        this.error.set(null);
        this.bracket.set(b);
        if (!tournamentNeedsRealtime(b.status)) this.stopRealtime();
      });
  }

  private stopRealtime(): void {
    if (this.hub) {
      void this.hub.close();
      this.hub = null;
    }
    this.connecting = false;
    this.live.set(false);
  }

  private async connect(): Promise<void> {
    this.connecting = true;
    try {
      const handle = await this.api.watch(this.id, (event) => {
        if (event !== 'AchievementUnlocked' && event !== 'ParticipantDisconnected' && event !== 'ParticipantReconnected') {
          this.reload();
        }
      });
      const status = this.bracket()?.status;
      if (this.destroyed || (status != null && !tournamentNeedsRealtime(status))) {
        void handle.close();
        this.connecting = false;
        this.live.set(false);
        return;
      }
      this.hub = handle;
      this.connecting = false;
      this.live.set(true);
      handle.connection.onclose(() => this.live.set(false));
      handle.connection.onreconnecting(() => this.live.set(false));
      handle.connection.onreconnected(() => {
        this.live.set(true);
        this.reload();
      });
      this.reload();
    } catch {
      this.connecting = false;
      this.live.set(false);
    }
  }
}
