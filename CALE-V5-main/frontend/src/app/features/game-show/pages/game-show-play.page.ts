import { Component, NgZone, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { HubConnection, HubConnectionState } from '@microsoft/signalr';
import { SessionStore } from '../../../core/auth/session.store';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  GameShowApi,
  GameShowLobbyDto,
  GameShowTeamDto,
  TEAM_COLORS,
  teamNameOf,
  teamsOf
} from '../api/game-show.api';
import { phaseHasTurnClock, remainingFromServerSnapshot, secondsUntilDeadline } from '../api/game-show-deadline';
import { GameShowSfxService } from '../api/game-show-sfx.service';
import { GameShowRulesComponent } from '../components/game-show-rules.component';

@Component({
  selector: 'app-game-show-play-page',
  standalone: true,
  imports: [FormsModule, RouterLink, UiButtonComponent, UiErrorComponent, GameShowRulesComponent],
  template: `
    @if (lobby(); as L) {
      <section class="play">
        @if (L.status === 'Lobby' && !rulesSeen() && !removed() && !replaced()) {
          <div class="rules-overlay" role="presentation">
            <app-game-show-rules
              [modal]="true"
              primaryLabel="Entendido, listo para jugar"
              (primary)="ackRules()" />
          </div>
        }
        <header>
          <h1>{{ L.title }}</h1>
          <div class="score">
            @for (t of teams(L); track t.key) {
              <span [class.mine]="myTeam() === t.key" [style.--team]="teamColor(t.key)">{{ t.name }} {{ t.score }}</span>
            }
          </div>
          <p class="muted">Juegas con <strong>{{ myTeamName(L) }}</strong></p>
        </header>
        @if (removed()) {
          <p class="removed">El profesor te quitó de esta partida. Si fue un error, vuelve a entrar con el código.</p>
        }
        @if (replaced()) {
          <div class="removed">
            <p>Abriste esta partida en otra pestaña o dispositivo, así que aquí se pausó.</p>
            <ui-button type="button" (click)="useThisTab()">Seguir aquí</ui-button>
          </div>
        }
        <ui-error [message]="error()" />
        @if (connection()) { <p class="conn">{{ connection() }}</p> }
        @if (flash()) { <p class="flash">{{ flash() }}</p> }

        @if (L.currentRound; as R) {
          <p class="q">{{ R.questionText }}</p>
          <ol class="board">
            @for (a of R.answers; track a.id) {
              <li [class.on]="a.isRevealed">{{ a.isRevealed ? (a.text + ' · ' + a.points) : '████████' }}</li>
            }
          </ol>
          <p class="muted">
            @if (timerSec() !== null) {
              <span class="timer" [class.urgent]="(timerSec() ?? 0) <= 3">⏱ {{ timerSec() }}s</span>
              ·
            }
            @if (R.phase === 'Control' || R.phase === 'Playing') {
              Strikes: {{ R.strikes }} / 3 · Banco: {{ R.roundPointsForController }} ·
            }
            {{ phaseLabel(L, R.phase) }}
          </p>

          @if (R.phase === 'WaitingBuzz') {
            <ui-button type="button" class="buzz" [disabled]="buzzing()" (click)="buzz()">
              {{ buzzing() ? 'Tomando turno…' : '¡RESPONDER!' }}
            </ui-button>
          }
          @if (R.phase === 'StealBuzz') {
            @if (R.controllingTeam !== myTeam()) {
              <p class="steal">¡Oportunidad de robo! El primer equipo que presione intenta robar el banco ({{ R.roundPointsForController }}).</p>
              <ui-button type="button" class="buzz" [disabled]="buzzing()" (click)="buzz()">
                {{ buzzing() ? 'Tomando turno…' : '¡ROBAR!' }}
              </ui-button>
            } @else {
              <p class="muted">Los otros equipos compiten por robar. Espera el resultado.</p>
            }
          }

          @if (canAnswer(L) && myTurn(L)) {
            @if (R.phase === 'Steal') {
              <p class="steal">¡Tienen UNA sola oportunidad para robar el banco ({{ R.roundPointsForController }})!</p>
            }
            @if (R.phase === 'FaceOff' || R.phase === 'FaceOffSecond') {
              <p class="steal">Enfrentamiento: acierta para tomar el control. Un fallo pasa el turno.</p>
            }
            <p class="steal">Turno de tu equipo — cualquier compañero puede escribir la respuesta</p>
            <label class="field">Tu respuesta
              <input class="input" [(ngModel)]="answer" name="answer" autofocus (keyup.enter)="send()" />
            </label>
            <ui-button type="button" (click)="send()">Enviar respuesta</ui-button>
            <ui-button type="button" variant="secondary" (click)="listenVoice()">Hablar (opcional)</ui-button>
          } @else if (R.phase === 'Steal') {
            <p class="muted">{{ waitingAnswerHint(L) }}</p>
          } @else if (R.phase === 'FaceOff' || R.phase === 'FaceOffSecond' || R.phase === 'Control' || R.phase === 'Playing') {
            <p class="muted">{{ waitingAnswerHint(L) }}</p>
          } @else if (R.phase === 'Finished') {
            <p class="muted">Ronda terminada. Espera la siguiente.</p>
          }
        } @else {
          <p>Esperando en el lobby… Código {{ L.joinCode }}</p>
          @if (L.status === 'Lobby' && !removed()) {
            <div class="pick">
              <p class="steal">Escoge tu equipo</p>
              <div class="pick-grid">
                @for (t of teams(L); track t.key) {
                  <button
                    type="button"
                    class="team-btn"
                    [class.on]="myTeam() === t.key"
                    [style.--team]="teamColor(t.key)"
                    [disabled]="choosing()"
                    (click)="chooseTeam(t.key)">
                    <strong>{{ t.name }}</strong>
                    <small>{{ playersIn(L, t.key) }} {{ playersIn(L, t.key) === 1 ? 'jugador' : 'jugadores' }}</small>
                  </button>
                }
              </div>
            </div>
          }
        }
        <a routerLink="/live/join" (click)="leave()">Salir de la partida</a>
      </section>
    } @else {
      <ui-error [message]="error() || 'Cargando…'" />
    }
  `,
  styles: [`
    .play { max-width: 40rem; margin: 0 auto; padding: 1rem; display: grid; gap: 0.85rem; }
    .score { display: flex; flex-wrap: wrap; gap: 0.5rem 1rem; font-weight: 800; font-size: 1.1rem; }
    .score span { padding: 0.15rem 0.6rem; border-radius: 999px; border: 2px solid transparent; }
    .score .mine { border-color: var(--team, var(--color-primary)); color: var(--color-text); }
    .removed { padding: 0.75rem 1rem; border-radius: 12px; border: 1px solid var(--color-danger, #dc2626); color: var(--color-danger, #dc2626); font-weight: 700; }
    .pick { display: grid; gap: 0.5rem; }
    .pick-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 9rem), 1fr)); gap: 0.6rem; }
    .team-btn { display: grid; gap: 0.2rem; padding: 0.9rem 0.75rem; border-radius: 14px; border: 2px solid var(--color-border); border-top: 6px solid var(--team); background: var(--color-surface); color: var(--color-text); cursor: pointer; font: inherit; text-align: left; }
    .team-btn.on { border-color: var(--team); background: color-mix(in srgb, var(--team) 18%, var(--color-surface)); }
    .team-btn small { color: var(--color-text-secondary); }
    .team-btn:disabled { opacity: 0.7; cursor: wait; }
    .q { font-size: 1.35rem; font-weight: 800; }
    .board { list-style: none; padding: 0; display: grid; gap: 0.4rem; }
    .board li { padding: 0.75rem 1rem; border-radius: 12px; background: var(--color-surface); border: 1px solid var(--color-border); font-weight: 700; letter-spacing: 0.04em; transition: background 0.25s ease, border-color 0.25s ease; }
    .board li.on { background: color-mix(in srgb, var(--color-success) 18%, transparent); border-color: color-mix(in srgb, var(--color-success) 45%, transparent); }
    .buzz { min-height: 4.5rem; font-size: 1.4rem; font-weight: 900; }
    .flash { color: var(--color-primary); font-weight: 900; font-size: 1.2rem; }
    .steal { font-weight: 800; }
    .conn { color: var(--color-text-secondary); font-weight: 700; }
    .muted { color: var(--color-text-secondary); }
    .timer { font-weight: 900; color: var(--color-primary); font-variant-numeric: tabular-nums; }
    .timer.urgent { color: #dc2626; }
    .field { display: grid; gap: 0.35rem; font-weight: 600; }
    .input { min-height: 3rem; font-size: 1.1rem; }
    .rules-overlay {
      position: fixed;
      inset: 0;
      z-index: 40;
      display: grid;
      place-items: center;
      padding: 1rem;
      overflow: auto;
      background: rgba(15, 23, 42, 0.55);
      backdrop-filter: blur(4px);
    }
  `]
})
export class GameShowPlayPage implements OnInit, OnDestroy {
  private readonly api = inject(GameShowApi);
  private readonly route = inject(ActivatedRoute);
  private readonly zone = inject(NgZone);
  private readonly sfx = inject(GameShowSfxService);

  readonly lobby = signal<GameShowLobbyDto | null>(null);
  readonly error = signal<string | null>(null);
  readonly flash = signal<string | null>(null);
  readonly connection = signal<string | null>(null);
  readonly myTeam = signal('A');
  readonly buzzing = signal(false);
  readonly choosing = signal(false);
  readonly removed = signal(false);
  /** This seat was opened in another tab; this one stopped listening to the game. */
  readonly replaced = signal(false);
  readonly rulesSeen = signal(false);
  readonly timerSec = signal<number | null>(null);
  answer = '';
  private hub: HubConnection | null = null;
  private sessionId = 0;
  private token = '';
  private myPlayerId: number | null = null;
  private lastActivePlayerId: number | null | undefined = undefined;
  private flashTimer: ReturnType<typeof setTimeout> | null = null;
  private pollTimer: ReturnType<typeof setInterval> | null = null;
  private tickTimer: ReturnType<typeof setInterval> | null = null;
  private timeoutPostedFor: string | null = null;
  private turnSecondsSnapshot: number | null = null;
  private turnSecondsCapturedAtMs = 0;
  private restartTimer: ReturnType<typeof setTimeout> | null = null;
  private eventSyncTimer: ReturnType<typeof setTimeout> | null = null;
  private lastSyncAt = 0;
  private syncInFlight = false;
  private started = false;
  private destroyed = false;
  private readonly session = inject(SessionStore);

  ngOnInit(): void {
    this.sessionId = Number(this.route.snapshot.paramMap.get('sessionId'));
    this.token = this.api.loadPlayerToken(this.sessionId) || '';
    if (!this.token) {
      this.tryRejoin();
      return;
    }
    this.startPlaying();
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    if (this.flashTimer) clearTimeout(this.flashTimer);
    if (this.pollTimer) clearInterval(this.pollTimer);
    if (this.tickTimer) clearInterval(this.tickTimer);
    if (this.restartTimer) clearTimeout(this.restartTimer);
    if (this.eventSyncTimer) clearTimeout(this.eventSyncTimer);
    document.removeEventListener('visibilitychange', this.onWake);
    window.removeEventListener('online', this.onWake);
    window.removeEventListener('focus', this.onWake);
    void this.hub?.stop();
  }

  ackRules(): void {
    this.rulesSeen.set(true);
  }

  leave(): void {
    this.api.forgetLastGame();
  }

  useThisTab(): void {
    window.location.reload();
  }

  private startPlaying(): void {
    if (this.started) return;
    this.started = true;
    const savedTeam = localStorage.getItem(`cale.game-show.team.${this.sessionId}`);
    if (savedTeam) this.myTeam.set(savedTeam);
    const savedPlayer = localStorage.getItem(`cale.game-show.playerId.${this.sessionId}`);
    if (savedPlayer) this.myPlayerId = Number(savedPlayer) || null;
    this.sfx.unlock();
    this.reload();
    this.connectHub();
    // Poll fast only while the realtime link is down; otherwise a slow safety sync.
    this.pollTimer = setInterval(() => {
      if (document.visibilityState === 'hidden') return;
      const live = this.hub?.state === HubConnectionState.Connected;
      if (!live || Date.now() - this.lastSyncAt >= 10_000) this.reloadQuiet();
    }, 2500);
    this.tickTimer = setInterval(() => this.tickDeadline(), 250);
    document.addEventListener('visibilitychange', this.onWake);
    window.addEventListener('online', this.onWake);
    window.addEventListener('focus', this.onWake);
  }

  /** Phone unlocked / tab back / network back: resync at once and revive the realtime link. */
  private readonly onWake = () => {
    if (document.visibilityState === 'hidden' || this.replaced()) return;
    this.zone.run(() => {
      this.reloadQuiet();
      this.ensureHub();
    });
  };

  private tryRejoin(): void {
    if (!this.session.user()) {
      this.error.set('Se perdió tu lugar en la partida. Vuelve a unirte con el código.');
      return;
    }
    this.connection.set('Recuperando tu lugar en la partida…');
    this.api.rejoin(this.sessionId).subscribe({
      next: (res) => {
        this.connection.set(null);
        this.token = res.playerToken;
        this.api.savePlayerToken(res.sessionId, res.playerToken);
        localStorage.setItem(`cale.game-show.team.${res.sessionId}`, res.team || 'A');
        localStorage.setItem(`cale.game-show.playerId.${res.sessionId}`, String(res.playerId));
        this.error.set(null);
        if (this.started) {
          void this.hub?.invoke('JoinAsPlayer', this.sessionId, this.token).catch(() => {});
          this.apply(res.lobby);
        } else {
          this.startPlaying();
        }
      },
      error: (err) => {
        this.connection.set(null);
        this.error.set(mapApiError(err) || 'Se perdió tu lugar en la partida. Vuelve a unirte con el código.');
      }
    });
  }

  private isSeatLost(err: unknown): boolean {
    return err instanceof HttpErrorResponse
      && (err.status === 403 || err.status === 404)
      && (err.error?.detail === 'forbidden' || err.error?.detail === 'player_not_found');
  }

  private ensureHub(): void {
    const hub = this.hub;
    if (!hub || this.destroyed || this.replaced()) return;
    if (hub.state === HubConnectionState.Disconnected) {
      this.connection.set('Reconectando…');
      void hub.start()
        .then(() => hub.invoke('JoinAsPlayer', this.sessionId, this.token))
        .then(() => this.zone.run(() => this.connection.set(null)))
        .catch(() => this.scheduleRestart());
    }
  }

  private scheduleRestart(): void {
    if (this.destroyed || this.replaced() || this.restartTimer) return;
    this.restartTimer = setTimeout(() => {
      this.restartTimer = null;
      this.ensureHub();
    }, 5000);
  }

  myTeamName(L: GameShowLobbyDto): string {
    return teamNameOf(L, this.myTeam());
  }

  teams(L: GameShowLobbyDto): GameShowTeamDto[] {
    return teamsOf(L);
  }

  teamColor(key: string): string {
    return TEAM_COLORS[key] ?? '#2563eb';
  }

  playersIn(L: GameShowLobbyDto, key: string): number {
    return L.players.filter((p) => p.team === key).length;
  }

  chooseTeam(team: string): void {
    if (this.choosing() || team === this.myTeam()) return;
    this.choosing.set(true);
    this.error.set(null);
    this.api.chooseTeam(this.sessionId, this.token, team).subscribe({
      next: (lobby) => {
        this.choosing.set(false);
        this.apply(lobby);
      },
      error: (err) => {
        this.choosing.set(false);
        this.error.set(mapApiError(err));
        this.reloadQuiet();
      }
    });
  }

  myTurn(L: GameShowLobbyDto): boolean {
    const r = L.currentRound;
    if (!r) return false;
    // Any phone on the answering team may type (not only ActivePlayer spotlight).
    if (r.phase === 'Steal') {
      const stealer = r.stealingTeam ?? (r.controllingTeam === 'A' ? 'B' : 'A');
      return stealer === this.myTeam();
    }
    if (
      r.phase === 'FaceOff'
      || r.phase === 'FaceOffSecond'
      || r.phase === 'Control'
      || r.phase === 'Playing'
    ) {
      return r.controllingTeam === this.myTeam();
    }
    return false;
  }

  canAnswer(L: GameShowLobbyDto): boolean {
    const p = L.currentRound?.phase;
    return (
      p === 'FaceOff'
      || p === 'FaceOffSecond'
      || p === 'Control'
      || p === 'Playing'
      || p === 'Steal'
    );
  }

  waitingAnswerHint(L: GameShowLobbyDto): string {
    if (this.myTurn(L)) {
      return 'Turno de tu equipo: cualquiera puede responder desde su celular.';
    }
    const name = L.currentRound?.activePlayerName;
    if (name) return `Espera: turno de ${teamNameOf(L, L.currentRound?.controllingTeam)} (${name}).`;
    if (L.currentRound?.phase === 'Steal') {
      return `${teamNameOf(L, L.currentRound.stealingTeam)} intenta robar la ronda. Espera el resultado.`;
    }
    return 'Espera el turno de tu equipo.';
  }

  phaseLabel(L: GameShowLobbyDto, phase: string): string {
    const r = L.currentRound;
    const controller = teamNameOf(L, r?.controllingTeam);
    const who = r?.activePlayerName ? r.activePlayerName : controller;
    switch (phase) {
      case 'WaitingBuzz': return 'Presiona RESPONDER para el enfrentamiento';
      case 'StealBuzz': return 'Robo abierto';
      case 'FaceOff': return `Enfrentamiento: responde ${who}`;
      case 'FaceOffSecond': return `Enfrentamiento (siguiente equipo): responde ${who}`;
      case 'Control':
      case 'Playing': return `Control: responde ${who}`;
      case 'Steal': return this.myTurn(L) ? 'Robo: ¡es tu oportunidad!' : `Robo: responde ${who}`;
      case 'Finished': return 'Ronda terminada';
      default: return phase;
    }
  }

  buzz(): void {
    if (this.buzzing()) return;
    this.buzzing.set(true);
    this.error.set(null);
    this.sfx.unlock();
    this.api.buzz(this.sessionId, this.token).subscribe({
      next: (lobby) => {
        this.buzzing.set(false);
        this.apply(lobby);
        this.showFlash('¡Tu equipo responde!');
      },
      error: (err) => {
        this.buzzing.set(false);
        this.error.set(mapApiError(err));
        // Sync if someone else already claimed the buzz or phase moved on.
        this.reload();
      }
    });
  }

  send(): void {
    const text = this.answer.trim();
    if (!text) return;
    this.api.answer(this.sessionId, this.token, text).subscribe({
      next: (lobby) => {
        this.answer = '';
        this.apply(lobby);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.reload();
      }
    });
  }

  listenVoice(): void {
    const w = window as unknown as {
      webkitSpeechRecognition?: new () => {
        lang: string;
        start(): void;
        onresult: ((ev: { results: { [i: number]: { [j: number]: { transcript: string } } } }) => void) | null;
      };
      SpeechRecognition?: new () => {
        lang: string;
        start(): void;
        onresult: ((ev: { results: { [i: number]: { [j: number]: { transcript: string } } } }) => void) | null;
      };
    };
    const SR = w.webkitSpeechRecognition || w.SpeechRecognition;
    if (!SR) {
      this.error.set('Tu navegador no soporta dictado por voz.');
      return;
    }
    const rec = new SR();
    rec.lang = 'es-CO';
    rec.onresult = (ev) => {
      this.answer = ev.results[0][0].transcript;
    };
    rec.start();
  }

  private reload(): void {
    this.api.get(this.sessionId, this.token).subscribe({
      next: (lobby) => this.apply(lobby),
      error: (err) => {
        if (this.isSeatLost(err)) {
          this.tryRejoin();
          return;
        }
        this.error.set(mapApiError(err));
      }
    });
  }

  /**
   * Hub events are normally followed by a LobbyUpdated broadcast; only fetch if none arrived,
   * so one game action doesn't cost every player several GETs.
   */
  private syncAfterEvent(): void {
    if (this.eventSyncTimer) return;
    const requestedAt = Date.now();
    this.eventSyncTimer = setTimeout(() => {
      this.eventSyncTimer = null;
      if (this.destroyed || this.lastSyncAt >= requestedAt) return;
      this.reloadQuiet();
    }, 700);
  }

  private reloadQuiet(): void {
    if (this.syncInFlight) return;
    this.syncInFlight = true;
    this.api.get(this.sessionId, this.token).subscribe({
      next: (lobby) => {
        this.syncInFlight = false;
        this.apply(lobby);
      },
      error: (err) => {
        this.syncInFlight = false;
        // Keep the last good board on network blips; only a lost seat needs action.
        if (this.isSeatLost(err)) this.tryRejoin();
      }
    });
  }

  /** El backend indica el equipo del jugador dueño del token; localStorage es solo respaldo. */
  private apply(lobby: GameShowLobbyDto): void {
    this.lastSyncAt = Date.now();
    const prev = this.lobby();
    const viewerPlayerId = lobby.viewerPlayerId ?? prev?.viewerPlayerId ?? this.myPlayerId;
    const viewerTeam = lobby.viewerTeam ?? prev?.viewerTeam ?? this.myTeam();
    const merged: GameShowLobbyDto = {
      ...lobby,
      viewerPlayerId,
      viewerTeam
    };
    this.lobby.set(merged);
    if (viewerTeam) {
      this.myTeam.set(viewerTeam);
      localStorage.setItem(`cale.game-show.team.${this.sessionId}`, viewerTeam);
    }
    if (viewerPlayerId != null) {
      this.myPlayerId = viewerPlayerId;
      localStorage.setItem(`cale.game-show.playerId.${this.sessionId}`, String(viewerPlayerId));
    }

    const activeId = merged.currentRound?.activePlayerId ?? null;
    if (
      activeId != null
      && activeId === this.myPlayerId
      && activeId !== this.lastActivePlayerId
      && this.myTurn(merged)
    ) {
      if (merged.settings?.enableSounds !== false) this.sfx.play('yourTurn');
      this.showFlash('¡Te toca responder!');
    }
    this.lastActivePlayerId = activeId;

    const deadline = merged.currentRound?.answerDeadlineUtc ?? null;
    this.turnSecondsSnapshot =
      merged.currentRound?.secondsRemaining ?? secondsUntilDeadline(deadline);
    this.turnSecondsCapturedAtMs = Date.now();
    if (deadline !== this.timeoutPostedFor && (this.turnSecondsSnapshot ?? 1) > 0) {
      this.timeoutPostedFor = null;
    }
    this.tickDeadline();
  }

  private tickDeadline(): void {
    const lobby = this.lobby();
    const round = lobby?.currentRound;
    if (!round || !phaseHasTurnClock(round.phase)) {
      this.timerSec.set(null);
      return;
    }
    const remaining = remainingFromServerSnapshot(
      this.turnSecondsSnapshot,
      this.turnSecondsCapturedAtMs
    );
    this.timerSec.set(remaining);
    if (remaining === 0 && round.answerDeadlineUtc && this.timeoutPostedFor !== round.answerDeadlineUtc) {
      this.timeoutPostedFor = round.answerDeadlineUtc;
      this.api.timeout(this.sessionId, this.token).subscribe({
        next: (next) => this.apply(next),
        error: () => this.reloadQuiet()
      });
    }
  }

  private showFlash(message: string): void {
    this.flash.set(message);
    if (this.flashTimer) clearTimeout(this.flashTimer);
    const ms = this.lobby()?.settings?.correctFlashMs ?? 2500;
    this.flashTimer = setTimeout(() => this.flash.set(null), ms);
  }

  private connectHub(): void {
    this.hub = this.api.buildHub();
    const onLobby = (lobby: GameShowLobbyDto) => this.zone.run(() => this.apply(lobby));
    const onFlash = (message: string) => this.zone.run(() => {
      this.showFlash(message);
      this.syncAfterEvent();
    });

    this.hub.on('LobbyUpdated', onLobby);
    this.hub.on('YourTurn', (payload: { playerId?: number; displayName?: string }) => this.zone.run(() => {
      if (payload?.playerId != null && payload.playerId === this.myPlayerId) {
        this.lastActivePlayerId = payload.playerId;
        if (this.lobby()?.settings?.enableSounds !== false) this.sfx.play('yourTurn');
        this.showFlash('¡Te toca responder!');
      } else if (payload?.displayName) {
        this.showFlash(`Responde ${payload.displayName}`);
      }
      this.syncAfterEvent();
    }));
    this.hub.on('PlayerRemoved', (payload: { playerId?: number }) => this.zone.run(() => {
      if (payload?.playerId == null || payload.playerId !== this.myPlayerId) return;
      this.removed.set(true);
      this.api.forgetLastGame();
      if (this.pollTimer) clearInterval(this.pollTimer);
      void this.hub?.stop();
    }));
    this.hub.on('ReplacedByAnotherTab', () => this.zone.run(() => {
      this.replaced.set(true);
      this.connection.set(null);
      if (this.pollTimer) clearInterval(this.pollTimer);
      if (this.tickTimer) clearInterval(this.tickTimer);
      if (this.restartTimer) clearTimeout(this.restartTimer);
      this.pollTimer = null;
      this.tickTimer = null;
      this.restartTimer = null;
      void this.hub?.stop();
    }));
    this.hub.on('StealBuzzWon', () => onFlash('¡Un equipo tomó el robo!'));
    this.hub.on('BuzzWon', () => onFlash('¡Turno de enfrentamiento!'));
    this.hub.on('FaceOffPass', () => onFlash('Turno del otro equipo'));
    this.hub.on('FaceOffWon', () => onFlash('¡Control de la ronda!'));
    this.hub.on('FaceOffReopen', () => onFlash('Nadie acertó — buzzer de nuevo'));
    this.hub.on('CorrectAnswer', () => onFlash('¡Correcto!'));
    this.hub.on('AlreadyRevealed', () => this.zone.run(() => this.showFlash('⚠️ Esta respuesta ya fue descubierta')));
    this.hub.on('Strike', () => onFlash('❌ Strike'));
    this.hub.on('StealOpportunity', () => onFlash('¡Oportunidad de robo!'));
    this.hub.on('StealSucceeded', () => onFlash('Robo exitoso'));
    this.hub.on('StealFailed', () => onFlash('Robo fallido'));
    this.hub.on('RoundWon', () => onFlash('¡Ronda ganada!'));
    this.hub.onreconnecting(() => this.zone.run(() => this.connection.set('Reconectando…')));
    this.hub.onreconnected(() => this.zone.run(() => {
      this.connection.set(null);
      void this.hub?.invoke('JoinAsPlayer', this.sessionId, this.token).catch(() => {});
      this.reloadQuiet();
    }));
    this.hub.onclose(() => this.zone.run(() => {
      if (this.destroyed || this.replaced()) return;
      this.connection.set('Reconectando… sigues en la partida.');
      this.scheduleRestart();
    }));
    void this.hub.start()
      .then(() => this.hub!.invoke('JoinAsPlayer', this.sessionId, this.token))
      .catch(() => this.zone.run(() => {
        this.connection.set('Reconectando… sigues en la partida.');
        this.scheduleRestart();
      }));
  }
}
