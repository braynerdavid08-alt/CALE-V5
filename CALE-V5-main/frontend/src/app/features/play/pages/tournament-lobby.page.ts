import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { SessionStore } from '../../../core/auth/session.store';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import {
  TournamentApi,
  TournamentHubEvent,
  TournamentHubHandle,
  TournamentLobby,
  TournamentMatchView,
  tournamentStatusLabel,
  tournamentStatusTone
} from '../api/tournament.api';
import { PlayTopbarComponent } from '../components/play-topbar.component';
import { PlayFxService } from '../play-fx.service';
import { tournamentInitials, tournamentPhotoSrc } from '../tournament-avatar';

/** Keep polling even while SignalR is "live" so a missed MatchAssigned cannot leave students stuck. */
const LOBBY_POLL_MS = 3000;

@Component({
  selector: 'app-tournament-lobby-page',
  standalone: true,
  imports: [DatePipe, RouterLink, UiButtonComponent, PlayTopbarComponent],
  styleUrls: ['./play-page.css', './tournament-page.css'],
  template: `
    <section class="play-page">
      <play-topbar
        [title]="lobby()?.name ?? 'Sala del torneo'"
        [subtitle]="lobby()?.description ?? ''"
        [back]="base"
        backLabel="Volver a torneos" />

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      @if (!lobby()) {
        @if (!error()) {
          <div class="play-card result-hero"><p class="pulse">Cargando sala…</p></div>
        }
      } @else {
        @if (lobby(); as l) {
          <div class="play-card t-card">
            <div class="t-head">
              <span class="chip" [class.good]="tone(l.status) === 'good'" [class.warn]="tone(l.status) === 'warn'">
                {{ label(l.status) }}
              </span>
              <span class="chip" [class.good]="live()" [class.neutral]="!live()">
                {{ live() ? '● En vivo' : '○ Sin conexión en vivo' }}
              </span>
            </div>
            <div class="t-meta">
              <span>📅 {{ l.scheduledAt | date: 'EEEE d MMM y, h:mm a' }}</span>
              <span>👥 {{ l.participants.length }}/{{ l.capacity }} inscritos</span>
              <span>Organiza: {{ l.organizerName }}</span>
              @if (l.currentRoundNumber > 0) {
                <span>Ronda {{ l.currentRoundNumber }}</span>
              }
            </div>

            <div class="play-actions">
              @if (isStudent()) {
                @if (l.myMatchId && l.status === 'in_progress') {
                  <ui-button [routerLink]="base + '/match/' + l.myMatchId">Ir a mi partida</ui-button>
                } @else if (l.canJoin) {
                  <ui-button [loading]="busy() === 'join'" (click)="join()">Inscribirme</ui-button>
                } @else if (l.amIRegistered) {
                  <span class="chip good">✔ Estás inscrito</span>
                }
              }

              @if (canManage()) {
                @if (l.status === 'registration_open') {
                  <ui-button variant="secondary" [loading]="busy() === 'close'" (click)="closeRegistration()">
                    Cerrar inscripciones
                  </ui-button>
                }
                @if (l.status === 'registration_open' || l.status === 'registration_closed') {
                  <ui-button [loading]="busy() === 'start'" [disabled]="l.participants.length < 4" (click)="start()">
                    Iniciar torneo
                  </ui-button>
                }
                @if (canCancel(l)) {
                  <ui-button variant="danger" [loading]="busy() === 'cancel'" (click)="cancel()">
                    Cancelar torneo
                  </ui-button>
                }
              }

              @if (l.status === 'in_progress' || l.status === 'finished') {
                <ui-button [routerLink]="base + '/' + l.id + '/bracket'" variant="secondary">Ver llaves</ui-button>
              }
            </div>

            @if (isStudent() && l.status === 'registration_open' && !l.amIRegistered && !l.canJoin) {
              <p class="muted">Este torneo ya no admite más inscripciones.</p>
            }
            @if (isStudent() && l.amIRegistered && l.status === 'registration_open') {
              <p class="muted pulse">Esperando a que el organizador inicie el torneo…</p>
            }
            @if (isStudent() && l.amIRegistered && l.status === 'in_progress' && !l.myMatchId) {
              <p class="muted pulse">Torneo en curso. Te llevaremos al VS en cuanto toque tu enfrentamiento…</p>
            }
            @if (isStudent() && l.amIRegistered && l.myMatchId && l.status === 'in_progress') {
              <p class="muted pulse">Abriendo tu enfrentamiento…</p>
            }
          </div>

          @if (l.status === 'finished' && l.championName) {
            <div class="play-card champion">
              <p class="big">🏆</p>
              <span class="t-avatar lg" aria-hidden="true">
                @if (photoSrc(l.championPhotoUrl); as src) {
                  <img [src]="src" alt="" />
                } @else {
                  {{ initials(l.championName) }}
                }
              </span>
              <span class="muted">Campeón</span>
              <strong>{{ l.championName }}</strong>
            </div>
          }

          <div class="play-card t-card">
            <h2>Participantes ({{ l.participants.length }})</h2>
            @if (!l.participants.length) {
              <p>Aún no hay inscritos.</p>
            } @else {
              <ul class="participants">
                @for (p of l.participants; track p.userId) {
                  <li [class.me]="p.userId === myId()" [class.out]="p.isEliminated">
                    <span class="dot" [class.on]="p.isConnected" [attr.title]="p.isConnected ? 'Conectado' : 'Desconectado'"></span>
                    <span class="t-avatar" aria-hidden="true">
                      @if (photoSrc(p.photoUrl); as src) {
                        <img [src]="src" alt="" />
                      } @else {
                        {{ initials(p.name) }}
                      }
                    </span>
                    <span class="who">{{ p.name }}{{ p.userId === myId() ? ' (tú)' : '' }}</span>
                    @if (p.bracketSlot != null) {
                      <span class="slot">Llave {{ p.bracketSlot + 1 }}</span>
                    }
                  </li>
                }
              </ul>
            }
          </div>
        }
      }
    </section>
  `
})
export class TournamentLobbyPage implements OnInit, OnDestroy {
  private readonly api = inject(TournamentApi);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private readonly fx = inject(PlayFxService);
  private readonly route = inject(ActivatedRoute);

  readonly base: string = this.route.snapshot.data['base'] ?? '/student/play/tournaments';
  readonly id = Number(this.route.snapshot.paramMap.get('id'));

  readonly lobby = signal<TournamentLobby | null>(null);
  readonly error = signal<string | null>(null);
  readonly busy = signal<'join' | 'close' | 'start' | 'cancel' | null>(null);
  readonly live = signal(false);

  readonly myId = computed(() => this.session.user()?.id ?? -1);
  readonly isStudent = computed(() => this.session.user()?.role === 'Student');
  readonly canManage = computed(() => {
    const role = this.session.user()?.role;
    return role === 'Teacher' || role === 'School' || role === 'Admin';
  });

  readonly label = tournamentStatusLabel;
  readonly tone = tournamentStatusTone;

  photoSrc = tournamentPhotoSrc;
  initials = tournamentInitials;

  private hub: TournamentHubHandle | null = null;
  private destroyed = false;
  private loading = false;
  private reloadQueued = false;
  private timer: ReturnType<typeof setInterval> | null = null;
  private enteringMatchId: number | null = null;

  ngOnInit(): void {
    if (!Number.isFinite(this.id)) {
      this.error.set('Torneo no válido.');
      return;
    }
    this.reload();
    void this.connect();
    this.timer = setInterval(() => {
      if (document.visibilityState === 'visible') this.reload();
    }, LOBBY_POLL_MS);
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    if (this.timer) clearInterval(this.timer);
    void this.hub?.close();
    this.hub = null;
  }

  canCancel(l: TournamentLobby): boolean {
    return l.status !== 'finished' && l.status !== 'cancelled';
  }

  join(): void {
    this.act('join', this.api.join(this.id), (lobby) => this.lobby.set(lobby));
  }

  closeRegistration(): void {
    this.act('close', this.api.closeRegistration(this.id), () => this.reload());
  }

  start(): void {
    if (!confirm('¿Iniciar el torneo ahora? Se cerrarán las inscripciones y se armarán las llaves.')) return;
    this.act('start', this.api.start(this.id), () => {
      void this.router.navigate([this.base, this.id, 'bracket']);
    });
  }

  cancel(): void {
    if (!confirm('¿Cancelar este torneo? Esta acción no se puede deshacer.')) return;
    this.act('cancel', this.api.cancel(this.id), () => this.reload());
  }

  private act<T>(
    kind: 'join' | 'close' | 'start' | 'cancel',
    request: Observable<T>,
    done: (result: T) => void
  ): void {
    if (this.busy()) return;
    this.busy.set(kind);
    this.error.set(null);
    request.subscribe({
      next: (res) => {
        this.busy.set(null);
        done(res);
      },
      error: (err) => {
        this.busy.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }

  private reload(): void {
    // Hub payloads are built for one viewer, so always re-read the lobby for ourselves.
    if (this.loading) {
      this.reloadQueued = true;
      return;
    }
    this.loading = true;
    this.api.lobby(this.id).subscribe({
      next: (lobby) => {
        this.loading = false;
        this.error.set(null);
        this.lobby.set(lobby);
        this.maybeEnterMatch(lobby.myMatchId ?? null, lobby.status);
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

  private async connect(): Promise<void> {
    try {
      const handle = await this.api.watch(this.id, (event, payload) => this.onEvent(event, payload));
      if (this.destroyed) {
        void handle.close();
        return;
      }
      this.hub = handle;
      this.live.set(true);
      handle.connection.onclose(() => this.live.set(false));
      handle.connection.onreconnecting(() => this.live.set(false));
      handle.connection.onreconnected(() => {
        this.live.set(true);
        this.reload();
      });
      // Anything that happened while connecting.
      this.reload();
    } catch {
      this.live.set(false);
    }
  }

  private onEvent(event: TournamentHubEvent, payload: unknown): void {
    if (event === 'MatchAssigned') {
      const match = payload as TournamentMatchView;
      this.reload();
      if (match?.matchId) {
        this.enterMatch(match.matchId, true);
      }
      return;
    }
    if (event === 'MatchReady') {
      const ready = payload as { userId?: number; matchId?: number };
      if (ready?.userId === this.myId() && ready.matchId) {
        this.enterMatch(ready.matchId, true);
      }
      this.reload();
      return;
    }
    if (event === 'TournamentStarted' || event === 'RoundStarted') {
      this.reload();
      return;
    }
    if (event === 'AchievementUnlocked') return;
    this.reload();
  }

  /** Auto-open the VS page whenever the student has an active match. */
  private maybeEnterMatch(matchId: number | null, status: string): void {
    if (!this.isStudent() || status !== 'in_progress' || !matchId) return;
    this.enterMatch(matchId, false);
  }

  private enterMatch(matchId: number, playStartSound: boolean): void {
    if (!this.isStudent() || !matchId || this.destroyed) return;
    if (this.enteringMatchId === matchId) return;
    this.enteringMatchId = matchId;
    if (playStartSound) this.fx.play('start');
    void this.router.navigate([this.base, 'match', matchId]).then((ok) => {
      if (!ok) this.enteringMatchId = null;
    });
  }
}
