import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HubConnection } from '@microsoft/signalr';
import { buildQrDataUrl } from '../../../core/qr/build-qr-data-url';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { mapApiError } from '../../../core/http/map-api-error';
import { GameShowApi, GameShowLobbyDto, GameShowStatsDto } from '../api/game-show.api';
import { GameShowSfxService } from '../api/game-show-sfx.service';
import { phaseHasTurnClock, secondsUntilDeadline } from '../api/game-show-deadline';

@Component({
  selector: 'app-game-show-screen-page',
  standalone: true,
  imports: [UiErrorComponent],
  template: `
    @if (lobby(); as L) {
      <section class="screen" (click)="sfx.unlock()">
        <p class="brand">100 ESTUDIANTES DIJERON</p>
        <div class="score">
          <div [class.active]="isControlling(L, 'A')" [class.winner]="isWinner(L, 'A')">
            <span>{{ L.teamAName }}</span><strong>{{ L.teamAScore }}</strong>
          </div>
          <div [class.active]="isControlling(L, 'B')" [class.winner]="isWinner(L, 'B')">
            <span>{{ L.teamBName }}</span><strong>{{ L.teamBScore }}</strong>
          </div>
        </div>
        @if (flash()) { <p class="flash">{{ flash() }}</p> }

        @if (L.status === 'Ended') {
          <div class="finale">
            <p class="finale-label">Partida terminada</p>
            <h1>{{ winnerText(L) }}</h1>
            @if (stats(); as S) {
              <div class="stats">
                <div><span>Aciertos</span><strong>{{ S.correctAnswers }}</strong></div>
                <div><span>Errores</span><strong>{{ S.wrongAnswers }}</strong></div>
                <div><span>Robos</span><strong>{{ S.stealsSucceeded }}</strong></div>
                <div><span>Rondas</span><strong>{{ S.roundCount }}</strong></div>
              </div>
            }
          </div>
        } @else if (L.currentRound) {
          <h1>{{ L.currentRound.questionText }}</h1>
          <p class="phase">
            {{ phase(L.currentRound.phase) }}
            @if (L.currentRound.controllingTeam) {
              · Turno de {{ teamName(L, L.currentRound.controllingTeam) }}
            }
            @if (L.currentRound.phase === 'Control' || L.currentRound.phase === 'Playing' || L.currentRound.phase === 'Steal') {
              · Banco {{ L.currentRound.roundPointsForController }}
            }
          </p>
          @if (timerSec() !== null && (
            L.currentRound.phase === 'FaceOff'
            || L.currentRound.phase === 'FaceOffSecond'
            || L.currentRound.phase === 'Control'
            || L.currentRound.phase === 'Playing'
            || L.currentRound.phase === 'Steal'
          )) {
            <p class="timer" [class.urgent]="(timerSec() ?? 0) <= 3">{{ timerSec() }}</p>
          }
          @if (L.currentRound.phase === 'FaceOff' || L.currentRound.phase === 'FaceOffSecond') {
            <p class="steal">⚡ ENFRENTAMIENTO — un fallo pasa el turno (sin strikes)</p>
          }
          @if (L.currentRound.phase === 'Steal') {
            <p class="steal">🔥 OPORTUNIDAD DE ROBO · {{ teamName(L, otherTeam(L.currentRound.controllingTeam)) }} · banco {{ L.currentRound.roundPointsForController }}</p>
          }
          @if ((L.currentRound.phase === 'Control' || L.currentRound.phase === 'Playing') && L.currentRound.roundPointsForController > 0) {
            <p class="bank">BANCO: {{ L.currentRound.roundPointsForController }}</p>
          }
          @if (L.currentRound.phase === 'Control' || L.currentRound.phase === 'Playing') {
            <p class="strikes">
              @for (x of strikeSlots(); track $index) {
                <span [class.hit]="$index < L.currentRound.strikes">✕</span>
              }
            </p>
          }
          <ol>
            @for (a of L.currentRound.answers; track a.id) {
              <li [class.revealed]="a.isRevealed">
                <em>{{ a.rank }}</em>
                <span>{{ a.isRevealed ? a.text : '████████████████' }}</span>
                <b>{{ a.isRevealed ? a.points : '' }}</b>
              </li>
            }
          </ol>
        } @else {
          <div class="lobby-join">
            <div>
              <p class="lobby-label">Código para Aula en vivo</p>
              <h1 class="lobby-code">{{ L.joinCode }}</h1>
              <p>Esperando jugadores… ({{ L.players.length }} conectados)</p>
              <p class="lobby-url">Entran en la app → Aula en vivo</p>
            </div>
            @if (qrUrl()) {
              <img class="qr" [src]="qrUrl()" [alt]="'QR ' + L.joinCode" width="280" height="280" />
            }
          </div>
        }
        <footer class="foot">
          <span>App → <strong>Aula en vivo</strong></span>
          @if (connection()) { <span class="conn">{{ connection() }}</span> }
          <span>Código <strong>{{ L.joinCode }}</strong></span>
        </footer>
      </section>
    } @else {
      <ui-error [message]="error() || 'Cargando pantalla…'" />
    }
  `,
  styles: [`
    :host { display: block; min-height: 100vh; background: radial-gradient(circle at top, #123a6b, #070d18 55%); color: #fff; }
    .screen { padding: 2rem clamp(1rem, 4vw, 3rem); display: grid; gap: 1.25rem; }
    .brand { letter-spacing: 0.18em; font-weight: 900; color: #5eb0ff; margin: 0; }
    .score { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .score > div { background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.12); border-radius: 18px; padding: 1.1rem 1.4rem; display: flex; justify-content: space-between; align-items: baseline; font-size: clamp(1.2rem, 3vw, 2rem); transition: background 0.3s ease, border-color 0.3s ease, box-shadow 0.3s ease; }
    .score > div.active { background: rgba(94,176,255,0.18); border-color: rgba(94,176,255,0.6); box-shadow: 0 0 2rem rgba(94,176,255,0.25); }
    .score > div.winner { background: rgba(250, 204, 21, 0.18); border-color: rgba(250, 204, 21, 0.7); }
    .score strong { font-size: clamp(2rem, 6vw, 4rem); }
    h1 { font-size: clamp(1.6rem, 4.5vw, 3rem); margin: 0; line-height: 1.15; }
    .phase { opacity: 0.8; font-size: 1.2rem; }
    .timer {
      margin: 0;
      font-size: clamp(3rem, 10vw, 6rem);
      font-weight: 900;
      letter-spacing: 0.06em;
      color: #7dd3fc;
      text-align: center;
      line-height: 1;
    }
    .timer.urgent { color: #f87171; animation: pulse 0.6s ease infinite alternate; }
    @keyframes pulse { from { transform: scale(1); } to { transform: scale(1.08); } }
    .steal { margin: 0; font-size: clamp(1.2rem, 3vw, 2rem); font-weight: 900; color: #fdba74; }
    .bank {
      margin: 0;
      text-align: center;
      font-size: clamp(1.4rem, 3.5vw, 2.2rem);
      font-weight: 900;
      letter-spacing: 0.08em;
      color: #7dd3fc;
    }
    .strikes { margin: 0; display: flex; gap: 0.75rem; }
    .strikes span { font-size: clamp(2rem, 6vw, 4rem); font-weight: 900; line-height: 1; color: rgba(255,255,255,0.14); transition: color 0.3s ease, transform 0.3s ease; }
    .strikes span.hit { color: #f87171; transform: scale(1.1); }
    ol { list-style: none; padding: 0; margin: 0; display: grid; gap: 0.75rem; }
    li { display: grid; grid-template-columns: 3rem 1fr 5rem; gap: 1rem; align-items: center; padding: 1rem 1.25rem; border-radius: 16px; background: rgba(0,0,0,0.35); border: 1px solid rgba(94,176,255,0.25); font-size: clamp(1.2rem, 3vw, 2rem); font-weight: 800; letter-spacing: 0.04em; transition: background 0.35s ease, border-color 0.35s ease, transform 0.35s ease; }
    li.revealed { background: rgba(40, 160, 100, 0.25); border-color: rgba(80, 220, 150, 0.45); transform: translateX(0.35rem); }
    .flash { font-size: clamp(1.5rem, 4vw, 2.5rem); font-weight: 900; color: #7dd3fc; text-align: center; }
    .finale { text-align: center; display: grid; gap: 1rem; padding: 1rem 0 2rem; }
    .finale-label { margin: 0; letter-spacing: 0.14em; text-transform: uppercase; color: #5eb0ff; font-weight: 800; }
    .stats {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: 0.75rem;
      max-width: 900px;
      margin: 0 auto;
    }
    .stats > div {
      background: rgba(255,255,255,0.06);
      border: 1px solid rgba(255,255,255,0.12);
      border-radius: 14px;
      padding: 1rem;
      display: grid;
      gap: 0.35rem;
    }
    .stats span { opacity: 0.75; font-size: 0.95rem; }
    .stats strong { font-size: clamp(1.6rem, 4vw, 2.4rem); }
    .lobby-join {
      display: grid;
      grid-template-columns: 1fr auto;
      gap: 2rem;
      align-items: center;
      min-height: 45vh;
    }
    .lobby-label { margin: 0; letter-spacing: 0.12em; text-transform: uppercase; color: #5eb0ff; font-weight: 800; }
    .lobby-code {
      margin: 0.4rem 0 0.75rem;
      font-size: clamp(3rem, 10vw, 6.5rem);
      letter-spacing: 0.18em;
      font-weight: 900;
    }
    .lobby-url { opacity: 0.8; word-break: break-all; font-size: clamp(1rem, 2.2vw, 1.4rem); }
    .qr {
      width: min(280px, 42vw);
      height: auto;
      aspect-ratio: 1;
      border-radius: 18px;
      background: #fff;
      padding: 0.85rem;
      box-sizing: border-box;
    }
    .foot { display: flex; justify-content: space-between; gap: 1rem; flex-wrap: wrap; opacity: 0.75; font-size: clamp(1rem, 2vw, 1.4rem); letter-spacing: 0.04em; }
    .conn { color: #fdba74; font-weight: 800; }
    @media (max-width: 800px) {
      .lobby-join { grid-template-columns: 1fr; }
      .qr { justify-self: start; }
      .stats { grid-template-columns: 1fr 1fr; }
    }
  `]
})
export class GameShowScreenPage implements OnInit, OnDestroy {
  private readonly api = inject(GameShowApi);
  private readonly route = inject(ActivatedRoute);
  readonly sfx = inject(GameShowSfxService);
  readonly lobby = signal<GameShowLobbyDto | null>(null);
  readonly stats = signal<GameShowStatsDto | null>(null);
  readonly error = signal<string | null>(null);
  readonly flash = signal<string | null>(null);
  readonly connection = signal<string | null>(null);
  readonly qrUrl = signal('');
  readonly timerSec = signal<number | null>(null);
  private hub: HubConnection | null = null;
  private sessionId = 0;
  private flashTimer: ReturnType<typeof setTimeout> | null = null;
  private tickTimer: ReturnType<typeof setInterval> | null = null;
  private lastQrCode = '';
  private lastPhase: string | null = null;
  private lastStatus: string | null = null;
  private timeoutPostedFor: string | null = null;
  private lastUrgentTick = false;

  ngOnInit(): void {
    this.sessionId = Number(this.route.snapshot.paramMap.get('sessionId'));
    this.reload();
    this.tickTimer = setInterval(() => this.tickDeadline(), 250);
    this.hub = this.api.buildHub();
    this.hub.on('LobbyUpdated', (lobby: GameShowLobbyDto) => this.applyLobby(lobby));
    this.hub.on('BuzzWon', () => {
      this.sfx.play('buzz');
      this.showFlash('¡ENFRENTAMIENTO!');
    });
    this.hub.on('FaceOffPass', () => {
      this.sfx.play('strike');
      this.showFlash('TURNO DEL OTRO EQUIPO');
    });
    this.hub.on('FaceOffWon', () => {
      this.sfx.play('correct');
      this.showFlash('¡CONTROL DE LA RONDA!');
    });
    this.hub.on('FaceOffReopen', () => {
      this.sfx.play('tick');
      this.showFlash('NADIE ACIERTÓ — BUZZER');
    });
    this.hub.on('CorrectAnswer', () => {
      this.sfx.play('correct');
      this.showFlash('¡CORRECTO!');
    });
    this.hub.on('AlreadyRevealed', () => this.showFlash('⚠️ YA FUE DESCUBIERTA'));
    this.hub.on('Strike', () => {
      this.sfx.play('strike');
      this.showFlash('STRIKE');
    });
    this.hub.on('StealOpportunity', () => {
      this.sfx.play('steal');
      this.showFlash('¡OPORTUNIDAD DE ROBO!');
    });
    this.hub.on('StealSucceeded', () => {
      this.sfx.play('correct');
      this.showFlash('¡ROBO EXITOSO!');
    });
    this.hub.on('StealFailed', () => {
      this.sfx.play('stealFail');
      this.showFlash('ROBO FALLIDO');
    });
    this.hub.on('RoundWon', () => {
      this.sfx.play('end');
      this.showFlash('¡RONDA GANADA!');
    });
    this.hub.on('RoundStarted', () => {
      this.sfx.play('round');
    });
    this.hub.on('GameEnded', () => {
      this.sfx.play('end');
      this.showFlash('PARTIDA TERMINADA');
      this.loadStats();
    });
    this.hub.onreconnecting(() => this.connection.set('Reconectando…'));
    this.hub.onreconnected(() => {
      this.connection.set(null);
      void this.hub?.invoke('JoinAsScreen', this.sessionId);
      this.reload();
    });
    this.hub.onclose(() => this.connection.set('Conexión perdida. Recarga la pantalla.'));
    void this.hub.start().then(() => this.hub!.invoke('JoinAsScreen', this.sessionId));
  }

  ngOnDestroy(): void {
    if (this.flashTimer) clearTimeout(this.flashTimer);
    if (this.tickTimer) clearInterval(this.tickTimer);
    void this.hub?.stop();
  }

  phase(p: string): string {
    if (p === 'WaitingBuzz') return 'Presionen RESPONDER';
    if (p === 'FaceOff') return 'Enfrentamiento';
    if (p === 'FaceOffSecond') return 'Enfrentamiento (2.º equipo)';
    if (p === 'Control' || p === 'Playing') return 'Control de ronda';
    if (p === 'Steal') return 'Robo';
    if (p === 'Finished') return 'Ronda terminada';
    return p;
  }

  teamName(L: GameShowLobbyDto, team: string | null): string {
    if (team === 'A') return L.teamAName;
    if (team === 'B') return L.teamBName;
    return 'Sin definir';
  }

  otherTeam(team: string | null): string | null {
    if (!team) return null;
    return team === 'A' ? 'B' : 'A';
  }

  isControlling(L: GameShowLobbyDto, team: string): boolean {
    return L.currentRound?.controllingTeam === team;
  }

  isWinner(L: GameShowLobbyDto, team: string): boolean {
    if (L.status !== 'Ended') return false;
    if (team === 'A') return L.teamAScore > L.teamBScore;
    return L.teamBScore > L.teamAScore;
  }

  winnerText(L: GameShowLobbyDto): string {
    if (L.teamAScore > L.teamBScore) return `Gana ${L.teamAName}`;
    if (L.teamBScore > L.teamAScore) return `Gana ${L.teamBName}`;
    return 'Empate';
  }

  strikeSlots(): readonly number[] {
    return STRIKE_SLOTS;
  }

  joinUrl(code: string): string {
    const origin = typeof window !== 'undefined' ? window.location.origin : '';
    return `${origin}/live/join/${code}`;
  }

  private showFlash(message: string): void {
    this.flash.set(message);
    if (this.flashTimer) clearTimeout(this.flashTimer);
    this.flashTimer = setTimeout(() => this.flash.set(null), 2500);
  }

  private reload(): void {
    this.api.get(this.sessionId).subscribe({
      next: (lobby) => this.applyLobby(lobby),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private loadStats(): void {
    this.api.stats(this.sessionId).subscribe({
      next: (s) => this.stats.set(s),
      error: () => this.stats.set(null)
    });
  }

  private applyLobby(lobby: GameShowLobbyDto): void {
    const phase = lobby.currentRound?.phase ?? null;
    if (lobby.status === 'Ended' && this.lastStatus !== 'Ended') {
      this.loadStats();
    }
    this.lastPhase = phase;
    this.lastStatus = lobby.status;
    this.lobby.set(lobby);
    const deadline = lobby.currentRound?.answerDeadlineUtc ?? null;
    if (deadline !== this.timeoutPostedFor && (secondsUntilDeadline(deadline) ?? 1) > 0) {
      this.timeoutPostedFor = null;
    }
    this.tickDeadline();
    this.refreshQr(lobby.joinCode);
  }

  private tickDeadline(): void {
    const lobby = this.lobby();
    const round = lobby?.currentRound;
    if (!round || !phaseHasTurnClock(round.phase)) {
      this.timerSec.set(null);
      this.lastUrgentTick = false;
      return;
    }
    const remaining = secondsUntilDeadline(round.answerDeadlineUtc);
    this.timerSec.set(remaining);
    if (remaining !== null && remaining <= 3 && remaining > 0 && !this.lastUrgentTick) {
      this.sfx.play('tick');
      this.lastUrgentTick = true;
    }
    if (remaining !== null && remaining > 3) {
      this.lastUrgentTick = false;
    }
    if (remaining === 0 && round.answerDeadlineUtc && this.timeoutPostedFor !== round.answerDeadlineUtc) {
      this.timeoutPostedFor = round.answerDeadlineUtc;
      this.api.timeout(this.sessionId).subscribe({
        next: (next) => this.applyLobby(next),
        error: () => this.reload()
      });
    }
  }

  private refreshQr(code: string): void {
    if (!code || code === this.lastQrCode) return;
    this.lastQrCode = code;
    try {
      this.qrUrl.set(buildQrDataUrl(this.joinUrl(code), 280));
    } catch {
      this.qrUrl.set('');
    }
  }
}

const STRIKE_SLOTS = [0, 1, 2] as const;
