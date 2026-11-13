import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { SessionStore } from '../../../core/auth/session.store';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import {
  TournamentApi,
  TournamentBracketMatch,
  TournamentHubEvent,
  TournamentHubHandle,
  TournamentLobby,
  TournamentMatchView,
  tournamentStatusLabel,
  tournamentStatusTone
} from '../api/tournament.api';
import { PlayTopbarComponent } from '../components/play-topbar.component';
import { PlayFxService } from '../play-fx.service';
import {
  applyParticipantPresence,
  isTransientHttpError
} from '../tournament-assignment.nav';
import { tournamentInitials, tournamentPhotoSrc } from '../tournament-avatar';
import {
  filterLiveRealMatches,
  teacherShowsLiveBoard,
  tournamentNeedsRealtime
} from '../tournament-host.logic';

/** Fallback poll when SignalR is down — keep snappy for missed MatchAssigned. */
const LOBBY_POLL_OFFLINE_MS = 3000;
/** Soft poll while SignalR is healthy — avoids N×GET storms on presence. */
const LOBBY_POLL_LIVE_MS = 12000;
const RELOAD_DEBOUNCE_MS = 500;
const TRANSIENT_RETRY_BASE_MS = 800;

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
              @if (needsRealtime(l.status)) {
                <span class="chip" [class.good]="live()" [class.neutral]="!live()">
                  {{ live() ? '● En vivo' : '○ Sin conexión en vivo' }}
                </span>
              }
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
            @if (showLiveBoard(l.status)) {
              <p class="muted">Como organizador podés abrir cualquier partido en vivo desde abajo o desde las llaves para proyectarlo.</p>
            }
          </div>

          @if (showLiveBoard(l.status)) {
            <div class="play-card t-card">
              <h2>Partidos en vivo ({{ liveMatches().length }})</h2>
              @if (!liveMatches().length) {
                <p class="muted pulse">No hay enfrentamientos activos ahora. Puede haber alguien esperando rival.</p>
              } @else {
                <ul class="participants">
                  @for (m of liveMatches(); track m.id) {
                    <li>
                      <span class="who">{{ m.player1Name || '?' }} vs {{ m.player2Name || '?' }}</span>
                      <ui-button [routerLink]="base + '/match/' + m.id" variant="secondary">Ver / proyectar</ui-button>
                    </li>
                  }
                </ul>
              }
            </div>
          }

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
  readonly liveMatches = signal<TournamentBracketMatch[]>([]);
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
  readonly needsRealtime = tournamentNeedsRealtime;

  photoSrc = tournamentPhotoSrc;
  initials = tournamentInitials;

  private hub: TournamentHubHandle | null = null;
  private destroyed = false;
  private loading = false;
  private reloadQueued = false;
  private connecting = false;
  private timer: ReturnType<typeof setInterval> | null = null;
  private pollMs = LOBBY_POLL_OFFLINE_MS;
  private reloadDebounce: ReturnType<typeof setTimeout> | null = null;
  private transientRetry: ReturnType<typeof setTimeout> | null = null;
  private transientAttempts = 0;
  private enteringMatchId: number | null = null;

  ngOnInit(): void {
    if (!Number.isFinite(this.id)) {
      this.error.set('Torneo no válido.');
      return;
    }
    // First load decides whether hub/poll are needed (finished → no realtime).
    this.reload();
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.clearReloadDebounce();
    this.clearTransientRetry();
    this.stopRealtime();
  }

  canCancel(l: TournamentLobby): boolean {
    return l.status !== 'finished' && l.status !== 'cancelled';
  }

  showLiveBoard(status: string): boolean {
    return teacherShowsLiveBoard(this.canManage(), status);
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

  /** Debounced full reload for chatty hub events (LobbyUpdated, RoundStarted, …). */
  private scheduleReload(): void {
    this.clearReloadDebounce();
    this.reloadDebounce = setTimeout(() => {
      this.reloadDebounce = null;
      this.reload();
    }, RELOAD_DEBOUNCE_MS);
  }

  private clearReloadDebounce(): void {
    if (this.reloadDebounce) {
      clearTimeout(this.reloadDebounce);
      this.reloadDebounce = null;
    }
  }

  private clearTransientRetry(): void {
    if (this.transientRetry) {
      clearTimeout(this.transientRetry);
      this.transientRetry = null;
    }
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
        this.transientAttempts = 0;
        this.clearTransientRetry();
        this.error.set(null);
        this.lobby.set(lobby);
        this.maybeEnterMatch(lobby.myMatchId ?? null, lobby.status);
        this.refreshLiveMatches(lobby.status);
        this.syncRealtime(lobby.status);
        if (this.reloadQueued) {
          this.reloadQueued = false;
          this.reload();
        }
      },
      error: (err) => {
        this.loading = false;
        this.reloadQueued = false;
        // Keep last good lobby; only blank-screen on first failure.
        if (!this.lobby()) {
          this.error.set(mapApiError(err));
        } else if (isTransientHttpError(err)) {
          this.scheduleTransientRetry();
        } else {
          this.error.set(mapApiError(err));
        }
      }
    });
  }

  private scheduleTransientRetry(): void {
    if (this.destroyed || this.transientRetry) return;
    const attempt = Math.min(this.transientAttempts, 4);
    this.transientAttempts = attempt + 1;
    const jitter = Math.floor(Math.random() * 400);
    const delay = TRANSIENT_RETRY_BASE_MS * Math.pow(2, attempt) + jitter;
    this.transientRetry = setTimeout(() => {
      this.transientRetry = null;
      if (!this.destroyed) this.reload();
    }, delay);
  }

  private applyPresence(userId: number, isConnected: boolean): void {
    const l = this.lobby();
    if (!l || !Number.isFinite(userId)) return;
    const participants = applyParticipantPresence(l.participants, userId, isConnected);
    if (participants === l.participants) return;
    this.lobby.set({ ...l, participants });
  }

  private refreshLiveMatches(status: string): void {
    if (!this.canManage() || status !== 'in_progress') {
      this.liveMatches.set([]);
      return;
    }
    this.api.bracket(this.id).subscribe({
      next: (b) => this.liveMatches.set(filterLiveRealMatches(b.matches ?? [])),
      error: () => this.liveMatches.set([])
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
    const desired = this.live() ? LOBBY_POLL_LIVE_MS : LOBBY_POLL_OFFLINE_MS;
    if (this.timer && this.pollMs === desired) return;
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
    this.pollMs = desired;
    this.timer = setInterval(() => {
      if (document.visibilityState === 'visible') this.reload();
    }, this.pollMs);
  }

  private setLive(value: boolean): void {
    this.live.set(value);
    if (tournamentNeedsRealtime(this.lobby()?.status ?? '')) {
      this.ensurePolling();
    }
  }

  private stopRealtime(): void {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
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
      const handle = await this.api.watch(this.id, (event, payload) => this.onEvent(event, payload));
      const status = this.lobby()?.status;
      if (this.destroyed || (status != null && !tournamentNeedsRealtime(status))) {
        void handle.close();
        this.connecting = false;
        this.setLive(false);
        return;
      }
      this.hub = handle;
      this.connecting = false;
      this.setLive(true);
      handle.connection.onclose(() => this.setLive(false));
      handle.connection.onreconnecting(() => this.setLive(false));
      handle.connection.onreconnected(() => {
        this.setLive(true);
        this.reload();
      });
      // Anything that happened while connecting.
      this.reload();
    } catch {
      this.connecting = false;
      this.setLive(false);
    }
  }

  private onEvent(event: TournamentHubEvent, payload: unknown): void {
    if (event === 'ParticipantReconnected' || event === 'ParticipantDisconnected') {
      const body = payload as { userId?: number; isConnected?: boolean } | null;
      const userId = Number(body?.userId);
      if (!Number.isFinite(userId)) return;
      const connected =
        typeof body?.isConnected === 'boolean'
          ? body.isConnected
          : event === 'ParticipantReconnected';
      this.applyPresence(userId, connected);
      return;
    }
    if (event === 'MatchAssigned') {
      const match = payload as TournamentMatchView;
      this.scheduleReload();
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
      this.scheduleReload();
      return;
    }
    if (event === 'AchievementUnlocked') return;
    this.scheduleReload();
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
