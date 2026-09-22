import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HubConnection } from '@microsoft/signalr';
import { buildQrDataUrl } from '../../../core/qr/build-qr-data-url';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  GameShowApi,
  GameShowLobbyDto,
  GameShowPlayerStandingDto
} from '../api/game-show.api';
import { GameShowSfxService } from '../api/game-show-sfx.service';
import { phaseHasTurnClock, remainingFromServerSnapshot, secondsUntilDeadline } from '../api/game-show-deadline';
import { phraseForLightning, phraseForSteal, phraseForStrike3 } from '../api/game-show-phrases';

@Component({
  selector: 'app-game-show-screen-page',
  standalone: true,
  imports: [UiErrorComponent],
  template: `
    @if (lobby(); as L) {
      <section class="screen" [class.lightning]="L.isLightning" (click)="sfx.unlock()">
        @if (confetti()) {
          <div class="confetti" aria-hidden="true">
            @for (p of confettiPieces; track p) {
              <i [style.--i]="p" [style.--h]="(p * 47) % 360"></i>
            }
          </div>
        }

        <p class="brand">100 ESTUDIANTES DIJERON</p>
        @if (L.isLightning) {
          <p class="lightning-banner">⚡ RELÁMPAGO · BANCO ×2
            @if (lightningSec() !== null) { · {{ lightningSec() }}s }
          </p>
        }

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
            @if (mvpOf(L); as mvp) {
              <div class="mvp" [style.--accent]="mvp.accentColor">
                <p class="mvp-label">MVP de la noche</p>
                <div class="avatar" [style.background]="mvp.accentColor">{{ initials(mvp.displayName) }}</div>
                <h2>{{ mvp.displayName }}</h2>
                <p class="mvp-meta">{{ mvp.correctAnswers }} aciertos · {{ mvp.stealsWon }} robos · {{ mvp.buzzWins }} buzz</p>
              </div>
            }
            @if (standingsOf(L); as rows) {
              <ol class="ranking">
                @for (p of rows; track p.playerId; let i = $index) {
                  <li>
                    <em>{{ i + 1 }}</em>
                    <span class="dot" [style.background]="p.accentColor"></span>
                    <span>{{ p.displayName }}</span>
                    <b>{{ p.correctAnswers }}/{{ p.stealsWon }}/{{ p.buzzWins }}</b>
                  </li>
                }
              </ol>
            }
          </div>
        } @else if (L.currentRound) {
          @if (L.currentRound.phase === 'Finished' && L.roundChampion; as C) {
            <div class="champ" [style.--accent]="accentFor(L, C.playerId)">
              <p>Campeón de ronda</p>
              <div class="avatar lg" [style.background]="accentFor(L, C.playerId)">{{ initials(C.displayName) }}</div>
              <h2>{{ C.displayName }}</h2>
              <p>{{ C.correctAnswers }} aciertos en esta ronda</p>
            </div>
          }

          <h1>{{ L.currentRound.questionText }}</h1>
          <p class="phase">
            {{ phase(L.currentRound.phase) }}
            @if (L.currentRound.controllingTeam) {
              · Turno de {{ teamName(L, L.currentRound.controllingTeam) }}
            }
            @if (L.currentRound.phase === 'Control' || L.currentRound.phase === 'Playing' || L.currentRound.phase === 'Steal') {
              · Banco {{ displayBank(L) }}
              @if (L.isLightning) { <span class="x2"> ×2</span> }
            }
          </p>

          @if (L.currentRound.activePlayerName && L.currentRound.phase !== 'WaitingBuzz' && L.currentRound.phase !== 'Finished') {
            <div class="active-player" [style.--accent]="L.currentRound.activePlayerAccent || '#5eb0ff'">
              <div class="avatar" [style.background]="L.currentRound.activePlayerAccent || '#5eb0ff'">
                {{ initials(L.currentRound.activePlayerName) }}
              </div>
              <div>
                <p class="ap-label">Responde ahora</p>
                <p class="ap-name">{{ L.currentRound.activePlayerName }}</p>
              </div>
            </div>
          }

          @if (timerSec() !== null && (
            L.currentRound.phase === 'FaceOff'
            || L.currentRound.phase === 'FaceOffSecond'
            || L.currentRound.phase === 'Control'
            || L.currentRound.phase === 'Playing'
            || L.currentRound.phase === 'Steal'
          )) {
            <p class="timer" [class.urgent]="(timerSec() ?? 0) <= 3">{{ timerSec() }}s</p>
          }
          @if (L.currentRound.phase === 'FaceOff' || L.currentRound.phase === 'FaceOffSecond') {
            <p class="steal">⚡ ENFRENTAMIENTO — un fallo pasa el turno (sin strikes)</p>
          }
          @if (L.currentRound.phase === 'Steal') {
            <p class="steal">🔥 OPORTUNIDAD DE ROBO · {{ teamName(L, otherTeam(L.currentRound.controllingTeam)) }} · banco {{ displayBank(L) }}</p>
          }
          @if ((L.currentRound.phase === 'Control' || L.currentRound.phase === 'Playing') && L.currentRound.roundPointsForController > 0) {
            <p class="bank">BANCO: {{ displayBank(L) }}@if (L.isLightning) { <span class="x2"> ×2</span> }</p>
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
              <li [class.revealed]="a.isRevealed" [class.top]="a.isRevealed && a.rank === 1">
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
              @if (L.packLeaderboard?.length) {
                <div class="board-hist">
                  <p class="lobby-label">Desafío del pack · mejores partidas</p>
                  <ol>
                    @for (e of L.packLeaderboard; track e.sessionId; let i = $index) {
                      <li>
                        <em>{{ i + 1 }}</em>
                        <span>{{ e.title }}</span>
                        <b>{{ e.teamAScore }}–{{ e.teamBScore }}</b>
                      </li>
                    }
                  </ol>
                </div>
              }
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
    .screen { padding: 2rem clamp(1rem, 4vw, 3rem); display: grid; gap: 1.25rem; position: relative; overflow: hidden; }
    .screen.lightning { background: radial-gradient(circle at top, #1e3a5f, #0a1628 50%, #1a0a00 100%); }
    .brand { letter-spacing: 0.18em; font-weight: 900; color: #5eb0ff; margin: 0; }
    .lightning-banner {
      margin: 0; text-align: center; font-weight: 900; letter-spacing: 0.08em;
      color: #fbbf24; font-size: clamp(1.1rem, 3vw, 1.8rem);
      animation: pulse 0.7s ease infinite alternate;
    }
    .score { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .score > div { background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.12); border-radius: 18px; padding: 1.1rem 1.4rem; display: flex; justify-content: space-between; align-items: baseline; font-size: clamp(1.2rem, 3vw, 2rem); transition: background 0.3s ease, border-color 0.3s ease, box-shadow 0.3s ease; }
    .score > div.active { background: rgba(94,176,255,0.18); border-color: rgba(94,176,255,0.6); box-shadow: 0 0 2rem rgba(94,176,255,0.25); }
    .score > div.winner { background: rgba(250, 204, 21, 0.18); border-color: rgba(250, 204, 21, 0.7); }
    .score strong { font-size: clamp(2rem, 6vw, 4rem); }
    h1 { font-size: clamp(1.6rem, 4.5vw, 3rem); margin: 0; line-height: 1.15; }
    h2 { margin: 0; font-size: clamp(1.8rem, 5vw, 3.2rem); }
    .phase { opacity: 0.8; font-size: 1.2rem; }
    .x2 { color: #fbbf24; font-weight: 900; }
    .timer {
      margin: 0; font-size: clamp(3rem, 10vw, 6rem); font-weight: 900; letter-spacing: 0.06em;
      color: #7dd3fc; text-align: center; line-height: 1;
    }
    .timer.urgent { color: #f87171; animation: pulse 0.6s ease infinite alternate; }
    @keyframes pulse { from { transform: scale(1); } to { transform: scale(1.08); } }
    .steal { margin: 0; font-size: clamp(1.2rem, 3vw, 2rem); font-weight: 900; color: #fdba74; }
    .bank { margin: 0; text-align: center; font-size: clamp(1.4rem, 3.5vw, 2.2rem); font-weight: 900; letter-spacing: 0.08em; color: #7dd3fc; }
    .strikes { margin: 0; display: flex; gap: 0.75rem; }
    .strikes span { font-size: clamp(2rem, 6vw, 4rem); font-weight: 900; line-height: 1; color: rgba(255,255,255,0.14); transition: color 0.3s ease, transform 0.3s ease; }
    .strikes span.hit { color: #f87171; transform: scale(1.1); }
    ol { list-style: none; padding: 0; margin: 0; display: grid; gap: 0.75rem; }
    li { display: grid; grid-template-columns: 3rem 1fr 5rem; gap: 1rem; align-items: center; padding: 1rem 1.25rem; border-radius: 16px; background: rgba(0,0,0,0.35); border: 1px solid rgba(94,176,255,0.25); font-size: clamp(1.2rem, 3vw, 2rem); font-weight: 800; letter-spacing: 0.04em; transition: background 0.35s ease, border-color 0.35s ease, transform 0.35s ease; }
    li.revealed { background: rgba(40, 160, 100, 0.25); border-color: rgba(80, 220, 150, 0.45); transform: translateX(0.35rem); }
    li.top { background: rgba(250, 204, 21, 0.22); border-color: rgba(250, 204, 21, 0.65); box-shadow: 0 0 1.5rem rgba(250, 204, 21, 0.25); }
    .flash { font-size: clamp(1.5rem, 4vw, 2.5rem); font-weight: 900; color: #7dd3fc; text-align: center; }
    .active-player {
      display: flex; align-items: center; gap: 1rem; padding: 0.85rem 1.2rem;
      border-radius: 16px; border: 2px solid var(--accent); background: color-mix(in srgb, var(--accent) 22%, transparent);
    }
    .ap-label { margin: 0; opacity: 0.75; font-size: 0.95rem; letter-spacing: 0.08em; text-transform: uppercase; }
    .ap-name { margin: 0; font-size: clamp(1.4rem, 3.5vw, 2.2rem); font-weight: 900; }
    .avatar {
      width: 3.2rem; height: 3.2rem; border-radius: 50%; display: grid; place-items: center;
      font-weight: 900; font-size: 1.1rem; color: #fff; flex-shrink: 0;
      box-shadow: 0 0 0 3px rgba(255,255,255,0.2);
    }
    .avatar.lg { width: 6rem; height: 6rem; font-size: 2rem; margin: 0 auto; }
    .champ, .mvp { text-align: center; display: grid; gap: 0.5rem; padding: 1rem; border-radius: 20px; border: 2px solid var(--accent, #fbbf24); background: rgba(255,255,255,0.06); }
    .mvp-label, .champ > p:first-child { margin: 0; letter-spacing: 0.12em; text-transform: uppercase; color: #fbbf24; font-weight: 800; }
    .mvp-meta { margin: 0; opacity: 0.8; }
    .ranking { max-width: 720px; margin: 0.5rem auto 0; }
    .ranking li { grid-template-columns: 2.5rem 1rem 1fr auto; font-size: clamp(1rem, 2.5vw, 1.4rem); }
    .dot { width: 0.75rem; height: 0.75rem; border-radius: 50%; display: inline-block; }
    .finale { text-align: center; display: grid; gap: 1rem; padding: 1rem 0 2rem; }
    .finale-label { margin: 0; letter-spacing: 0.14em; text-transform: uppercase; color: #5eb0ff; font-weight: 800; }
    .lobby-join { display: grid; grid-template-columns: 1fr auto; gap: 2rem; align-items: center; min-height: 45vh; }
    .lobby-label { margin: 0; letter-spacing: 0.12em; text-transform: uppercase; color: #5eb0ff; font-weight: 800; }
    .lobby-code { margin: 0.4rem 0 0.75rem; font-size: clamp(3rem, 10vw, 6.5rem); letter-spacing: 0.18em; font-weight: 900; }
    .lobby-url { opacity: 0.8; word-break: break-all; font-size: clamp(1rem, 2.2vw, 1.4rem); }
    .board-hist { margin-top: 1.5rem; }
    .board-hist ol { margin-top: 0.75rem; }
    .board-hist li { font-size: 1.1rem; grid-template-columns: 2.5rem 1fr 5rem; }
    .qr { width: min(280px, 42vw); height: auto; aspect-ratio: 1; border-radius: 18px; background: #fff; padding: 0.85rem; box-sizing: border-box; }
    .foot { display: flex; justify-content: space-between; gap: 1rem; flex-wrap: wrap; opacity: 0.75; font-size: clamp(1rem, 2vw, 1.4rem); letter-spacing: 0.04em; }
    .conn { color: #fdba74; font-weight: 800; }
    .confetti { pointer-events: none; position: absolute; inset: 0; overflow: hidden; z-index: 5; }
    .confetti i {
      position: absolute; top: -10%; left: calc(var(--i) * 3.1%);
      width: 10px; height: 18px; background: hsl(var(--h) 85% 55%);
      animation: fall 2.4s linear forwards;
      transform: rotate(calc(var(--i) * 20deg));
    }
    @keyframes fall {
      to { transform: translateY(110vh) rotate(720deg); opacity: 0.2; }
    }
    @media (max-width: 800px) {
      .lobby-join { grid-template-columns: 1fr; }
      .qr { justify-self: start; }
    }
  `]
})
export class GameShowScreenPage implements OnInit, OnDestroy {
  private readonly api = inject(GameShowApi);
  private readonly route = inject(ActivatedRoute);
  readonly sfx = inject(GameShowSfxService);
  readonly lobby = signal<GameShowLobbyDto | null>(null);
  readonly error = signal<string | null>(null);
  readonly flash = signal<string | null>(null);
  readonly connection = signal<string | null>(null);
  readonly qrUrl = signal('');
  readonly timerSec = signal<number | null>(null);
  readonly lightningSec = signal<number | null>(null);
  readonly confetti = signal(false);
  readonly confettiPieces = Array.from({ length: 28 }, (_, i) => i + 1);
  private hub: HubConnection | null = null;
  private sessionId = 0;
  private flashTimer: ReturnType<typeof setTimeout> | null = null;
  private confettiTimer: ReturnType<typeof setTimeout> | null = null;
  private tickTimer: ReturnType<typeof setInterval> | null = null;
  private lastQrCode = '';
  private lastPhase: string | null = null;
  private lastStatus: string | null = null;
  private timeoutPostedFor: string | null = null;
  private lastUrgentTick = false;
  private lastLightning = false;
  private seenTop1 = new Set<number>();
  private turnSecondsSnapshot: number | null = null;
  private turnSecondsCapturedAtMs = 0;
  private lightningSecondsSnapshot: number | null = null;
  private lightningSecondsCapturedAtMs = 0;

  ngOnInit(): void {
    this.sessionId = Number(this.route.snapshot.paramMap.get('sessionId'));
    this.reload();
    this.tickTimer = setInterval(() => this.tickDeadline(), 250);
    this.hub = this.api.buildHub();
    this.hub.on('LobbyUpdated', (lobby: GameShowLobbyDto) => this.applyLobby(lobby));
    this.hub.on('BuzzWon', () => {
      this.playSfx('buzz');
      this.showFlash('¡ENFRENTAMIENTO!');
    });
    this.hub.on('FaceOffPass', () => {
      this.playSfx('strike');
      this.showFlash('TURNO DEL OTRO EQUIPO');
    });
    this.hub.on('FaceOffWon', (p: { rank?: number }) => {
      this.playSfx('correct');
      this.showFlash('¡CONTROL DE LA RONDA!');
      if (p?.rank === 1) this.burstConfetti();
    });
    this.hub.on('FaceOffReopen', () => {
      this.playSfx('tick');
      this.showFlash('NADIE ACIERTÓ — BUZZER');
    });
    this.hub.on('CorrectAnswer', (p: { rank?: number }) => {
      this.playSfx('correct');
      this.showFlash('¡CORRECTO!');
      if (p?.rank === 1) this.burstConfetti();
    });
    this.hub.on('AnswerRevealed', (p: { rank?: number }) => {
      this.playSfx('correct');
      if (p?.rank === 1) this.burstConfetti();
    });
    this.hub.on('AlreadyRevealed', () => this.showFlash('⚠️ YA FUE DESCUBIERTA'));
    this.hub.on('Strike', (p: { strikes?: number }) => {
      const n = p?.strikes ?? 1;
      this.playStrikeSfx(n);
      this.showFlash(n >= 3 ? phraseForStrike3() : `STRIKE ${n}`);
    });
    this.hub.on('StealOpportunity', () => {
      this.playSfx('steal');
      this.showFlash(phraseForSteal());
    });
    this.hub.on('StealSucceeded', () => {
      this.playSfx('correct');
      this.showFlash('¡ROBO EXITOSO!');
    });
    this.hub.on('StealFailed', () => {
      this.playSfx('stealFail');
      this.showFlash('ROBO FALLIDO');
    });
    this.hub.on('RoundWon', () => {
      this.playSfx('end');
      this.showFlash('¡RONDA GANADA!');
    });
    this.hub.on('RoundStarted', () => {
      this.playSfx('round');
    });
    this.hub.on('LightningStarted', () => {
      this.playSfx('lightning');
      this.showFlash(phraseForLightning());
    });
    this.hub.on('GameEnded', () => {
      this.playSfx('end');
      this.showFlash('PARTIDA TERMINADA');
      this.reload();
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
    if (this.confettiTimer) clearTimeout(this.confettiTimer);
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

  displayBank(L: GameShowLobbyDto): number {
    const bank = L.currentRound?.roundPointsForController ?? 0;
    return L.isLightning ? bank * 2 : bank;
  }

  standingsOf(L: GameShowLobbyDto): GameShowPlayerStandingDto[] | null {
    const rows = L.playerStandings;
    return rows && rows.length ? rows.slice(0, 8) : null;
  }

  mvpOf(L: GameShowLobbyDto): GameShowPlayerStandingDto | null {
    const rows = L.playerStandings;
    if (!rows?.length) return null;
    return rows[0] ?? null;
  }

  accentFor(L: GameShowLobbyDto, playerId: number): string {
    return L.players.find((p) => p.id === playerId)?.accentColor
      || L.playerStandings?.find((p) => p.playerId === playerId)?.accentColor
      || '#5eb0ff';
  }

  initials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (!parts.length) return '?';
    if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase();
    return (parts[0]![0]! + parts[1]![0]!).toUpperCase();
  }

  strikeSlots(): readonly number[] {
    return STRIKE_SLOTS;
  }

  joinUrl(code: string): string {
    const origin = typeof window !== 'undefined' ? window.location.origin : '';
    return `${origin}/live/join/${code}`;
  }

  private burstConfetti(): void {
    if (this.lobby()?.settings?.enableAnimations === false) return;
    if (this.lobby()?.settings?.enableSounds !== false) this.sfx.play('confetti');
    this.confetti.set(true);
    if (this.confettiTimer) clearTimeout(this.confettiTimer);
    const ms = this.lobby()?.settings?.celebrationMs ?? 2600;
    this.confettiTimer = setTimeout(() => this.confetti.set(false), ms);
  }

  private showFlash(message: string): void {
    this.flash.set(message);
    if (this.flashTimer) clearTimeout(this.flashTimer);
    const ms = this.lobby()?.settings?.correctFlashMs ?? 2800;
    this.flashTimer = setTimeout(() => this.flash.set(null), ms);
  }

  private playSfx(kind: Parameters<GameShowSfxService['play']>[0]): void {
    if (this.lobby()?.settings?.enableSounds === false) return;
    this.sfx.play(kind);
  }

  private playStrikeSfx(n: number): void {
    if (this.lobby()?.settings?.enableSounds === false) return;
    this.sfx.playStrike(n);
  }

  private reload(): void {
    this.api.get(this.sessionId).subscribe({
      next: (lobby) => this.applyLobby(lobby),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private applyLobby(lobby: GameShowLobbyDto): void {
    const phase = lobby.currentRound?.phase ?? null;
    if (lobby.isLightning && !this.lastLightning) {
      this.playSfx('lightning');
      this.showFlash(phraseForLightning());
    }
    this.lastLightning = !!lobby.isLightning;

    if (phase === 'Finished' && this.lastPhase !== 'Finished' && lobby.roundChampion) {
      this.showFlash(`Campeón: ${lobby.roundChampion.displayName}`);
    }

    for (const a of lobby.currentRound?.answers ?? []) {
      if (a.isRevealed && a.rank === 1 && !this.seenTop1.has(a.id)) {
        this.seenTop1.add(a.id);
        this.burstConfetti();
      }
    }

    this.lastPhase = phase;
    this.lastStatus = lobby.status;
    this.lobby.set(lobby);
    const deadline = lobby.currentRound?.answerDeadlineUtc ?? null;
    const serverRemaining = lobby.currentRound?.secondsRemaining;
    this.turnSecondsSnapshot =
      serverRemaining ?? secondsUntilDeadline(deadline);
    this.turnSecondsCapturedAtMs = Date.now();
    if (lobby.lightningUntilUtc) {
      this.lightningSecondsSnapshot = secondsUntilDeadline(lobby.lightningUntilUtc);
      this.lightningSecondsCapturedAtMs = Date.now();
    } else {
      this.lightningSecondsSnapshot = null;
    }
    if (deadline !== this.timeoutPostedFor && (this.turnSecondsSnapshot ?? 1) > 0) {
      this.timeoutPostedFor = null;
    }
    this.tickDeadline();
    this.refreshQr(lobby.joinCode);
  }

  private tickDeadline(): void {
    const lobby = this.lobby();
    if (this.lightningSecondsSnapshot != null) {
      this.lightningSec.set(
        remainingFromServerSnapshot(this.lightningSecondsSnapshot, this.lightningSecondsCapturedAtMs)
      );
    } else {
      this.lightningSec.set(null);
    }

    const round = lobby?.currentRound;
    if (!round || !phaseHasTurnClock(round.phase)) {
      this.timerSec.set(null);
      this.lastUrgentTick = false;
      return;
    }
    const remaining = remainingFromServerSnapshot(
      this.turnSecondsSnapshot,
      this.turnSecondsCapturedAtMs
    );
    this.timerSec.set(remaining);
    if (remaining !== null && remaining <= 3 && remaining > 0 && !this.lastUrgentTick) {
      this.playSfx('tick');
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
