import { Component, NgZone, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HubConnection } from '@microsoft/signalr';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { mapApiError } from '../../../core/http/map-api-error';
import { GameShowApi, GameShowLobbyDto } from '../api/game-show.api';
import { phaseHasTurnClock, secondsUntilDeadline } from '../api/game-show-deadline';
import { GameShowSfxService } from '../api/game-show-sfx.service';

@Component({
  selector: 'app-game-show-play-page',
  standalone: true,
  imports: [FormsModule, RouterLink, UiButtonComponent, UiErrorComponent],
  template: `
    @if (lobby(); as L) {
      <section class="play">
        <header>
          <h1>{{ L.title }}</h1>
          <div class="score">
            <span [class.mine]="myTeam() === 'A'">{{ L.teamAName }} {{ L.teamAScore }}</span>
            <span [class.mine]="myTeam() === 'B'">{{ L.teamBName }} {{ L.teamBScore }}</span>
          </div>
          <p class="muted">Juegas con {{ myTeamName(L) }}</p>
        </header>
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

          @if (canAnswer(L) && myTurn(L)) {
            @if (R.phase === 'Steal') {
              <p class="steal">¡Tienen UNA sola oportunidad para robar el banco ({{ R.roundPointsForController }})!</p>
            }
            @if (R.phase === 'FaceOff' || R.phase === 'FaceOffSecond') {
              <p class="steal">Enfrentamiento: acierta para tomar el control. Un fallo pasa el turno.</p>
            }
            <p class="steal">Te toca responder</p>
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
        }
        <a routerLink="/live/join">Salir</a>
      </section>
    } @else {
      <ui-error [message]="error() || 'Cargando…'" />
    }
  `,
  styles: [`
    .play { max-width: 40rem; margin: 0 auto; padding: 1rem; display: grid; gap: 0.85rem; }
    .score { display: flex; gap: 1rem; font-weight: 800; font-size: 1.2rem; }
    .score .mine { color: var(--color-primary); }
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

  ngOnInit(): void {
    this.sessionId = Number(this.route.snapshot.paramMap.get('sessionId'));
    this.token = this.api.loadPlayerToken(this.sessionId) || '';
    if (!this.token) {
      this.error.set('No hay sesión de jugador. Vuelve a unirte con el código.');
      return;
    }
    const savedTeam = localStorage.getItem(`cale.game-show.team.${this.sessionId}`);
    if (savedTeam) this.myTeam.set(savedTeam);
    const savedPlayer = localStorage.getItem(`cale.game-show.playerId.${this.sessionId}`);
    if (savedPlayer) this.myPlayerId = Number(savedPlayer) || null;
    this.sfx.unlock();
    this.reload();
    this.connectHub();
    // Mobile networks often drop SignalR; poll so play stays in sync with the engine.
    this.pollTimer = setInterval(() => this.reloadQuiet(), 2500);
    this.tickTimer = setInterval(() => this.tickDeadline(), 250);
  }

  ngOnDestroy(): void {
    if (this.flashTimer) clearTimeout(this.flashTimer);
    if (this.pollTimer) clearInterval(this.pollTimer);
    if (this.tickTimer) clearInterval(this.tickTimer);
    void this.hub?.stop();
  }

  myTeamName(L: GameShowLobbyDto): string {
    return this.myTeam() === 'B' ? L.teamBName : L.teamAName;
  }

  myTurn(L: GameShowLobbyDto): boolean {
    const r = L.currentRound;
    if (!r) return false;
    const me = L.viewerPlayerId ?? this.myPlayerId;
    if (me == null || r.activePlayerId == null || r.activePlayerId !== me) {
      return false;
    }
    if (r.phase === 'Steal') {
      const other = r.controllingTeam === 'A' ? 'B' : 'A';
      return other === this.myTeam();
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
    const name = L.currentRound?.activePlayerName;
    if (name) return `Espera: responde ${name}`;
    if (L.currentRound?.phase === 'Steal') {
      return 'El otro equipo intenta robar la ronda. Espera el resultado.';
    }
    return 'Espera el turno de tu equipo.';
  }

  phaseLabel(L: GameShowLobbyDto, phase: string): string {
    const r = L.currentRound;
    const controller = r?.controllingTeam === 'B' ? L.teamBName : L.teamAName;
    const who = r?.activePlayerName ? r.activePlayerName : controller;
    switch (phase) {
      case 'WaitingBuzz': return 'Presiona RESPONDER para el enfrentamiento';
      case 'FaceOff': return `Enfrentamiento: responde ${who}`;
      case 'FaceOffSecond': return `Enfrentamiento (2.º): responde ${who}`;
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
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private reloadQuiet(): void {
    this.api.get(this.sessionId, this.token).subscribe({
      next: (lobby) => this.apply(lobby),
      error: () => { /* keep last good lobby while polling */ }
    });
  }

  /** El backend indica el equipo del jugador dueño del token; localStorage es solo respaldo. */
  private apply(lobby: GameShowLobbyDto): void {
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
    if (deadline !== this.timeoutPostedFor && (secondsUntilDeadline(deadline) ?? 1) > 0) {
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
    const remaining = secondsUntilDeadline(round.answerDeadlineUtc);
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
      this.reloadQuiet();
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
      this.reloadQuiet();
    }));
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
      void this.hub?.invoke('JoinAsPlayer', this.sessionId, this.token);
      this.reload();
    }));
    this.hub.onclose(() => this.zone.run(() => this.connection.set('Conexión perdida. Recarga la página.')));
    void this.hub.start()
      .then(() => this.hub!.invoke('JoinAsPlayer', this.sessionId, this.token))
      .catch(() => this.zone.run(() => this.connection.set('Sin SignalR — sincronizando por red…')));
  }
}
