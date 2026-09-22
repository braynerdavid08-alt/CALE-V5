import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HubConnection } from '@microsoft/signalr';
import { buildQrDataUrl } from '../../../core/qr/build-qr-data-url';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { mapApiError } from '../../../core/http/map-api-error';
import { GameShowApi, GameShowLobbyDto, GameShowStatsDto } from '../api/game-show.api';
import { GameShowSfxService } from '../api/game-show-sfx.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-game-show-host-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiErrorComponent],
  template: `
    @if (lobby(); as L) {
      <section class="host">
        <header class="bar">
          <div>
            <p class="eyebrow">Control del profesor</p>
            <h1>{{ L.title }}</h1>
            <p class="status">Estado: <strong>{{ L.status }}</strong></p>
          </div>
          <div class="links">
            <ui-button type="button" variant="ghost" (click)="exportQuestions('csv')">Exportar CSV</ui-button>
            <ui-button type="button" variant="ghost" (click)="exportQuestions('json')">Exportar JSON</ui-button>
            <a [routerLink]="['/game-show/screen', L.id]" target="_blank">Abrir proyector</a>
            <a routerLink="/teacher/game-show">Volver</a>
          </div>
        </header>
        <ui-error [message]="error()" />
        @if (connection()) {
          <p class="conn">{{ connection() }}</p>
        }
        @if (flash()) {
          <p class="flash">{{ flash() }}</p>
        }

        <article class="join-panel">
          <div class="join-copy">
            <p class="join-label">Código para unirse</p>
            <p class="join-code">{{ L.joinCode }}</p>
            <p class="join-hint">Los estudiantes entran en la app → <strong>Aula en vivo</strong> con este código:</p>
            <p class="join-url">{{ joinUrl(L.joinCode) }}</p>
            <div class="join-actions">
              <ui-button type="button" variant="secondary" (click)="copyCode(L.joinCode)">Copiar código</ui-button>
              <ui-button type="button" variant="ghost" (click)="copyLink(L.joinCode)">Copiar enlace</ui-button>
            </div>
            @if (copied()) {
              <p class="copied">{{ copied() }}</p>
            }
          </div>
          @if (qrUrl()) {
            <img class="qr" [src]="qrUrl()" [alt]="'QR para unirse con código ' + L.joinCode" width="220" height="220" />
          }
        </article>

        <div class="score">
          <div><span>{{ L.teamAName }}</span><strong>{{ L.teamAScore }}</strong></div>
          <div><span>{{ L.teamBName }}</span><strong>{{ L.teamBScore }}</strong></div>
        </div>

        <div class="controls">
          @if (L.status === 'Lobby') {
            <ui-button type="button" (click)="start()">Iniciar partida</ui-button>
          }
          @if (L.status === 'Running') {
            <ui-button type="button" variant="secondary" (click)="pause()">Pausar</ui-button>
          }
          @if (L.status === 'Paused') {
            <ui-button type="button" (click)="resume()">Reanudar</ui-button>
          }
          @if (L.currentRound?.phase === 'WaitingBuzz') {
            <ui-button type="button" variant="secondary" (click)="forceBuzz('A')">Turno {{ L.teamAName }}</ui-button>
            <ui-button type="button" variant="secondary" (click)="forceBuzz('B')">Turno {{ L.teamBName }}</ui-button>
          }
          @if (L.currentRound?.phase === 'Playing') {
            <ui-button type="button" variant="secondary" (click)="strike()">Registrar error (X)</ui-button>
            <ui-button type="button" variant="ghost" (click)="endRound()">Terminar ronda</ui-button>
          }
          @if (L.currentRound?.phase === 'Steal') {
            <ui-button type="button" variant="secondary" (click)="failSteal()">Cerrar robo (fallido)</ui-button>
            <ui-button type="button" variant="ghost" (click)="endRound()">Terminar ronda</ui-button>
          }
          @if (L.currentRound?.phase === 'Finished') {
            <ui-button type="button" (click)="next()">Siguiente ronda</ui-button>
          }
          @if (L.status === 'Running' || L.status === 'Paused') {
            <ui-button type="button" variant="ghost" (click)="finish()">Finalizar</ui-button>
          }
        </div>

        @if (L.status === 'Ended') {
          <article class="board finale">
            <h2>Partida finalizada</h2>
            <p class="q">{{ L.teamAScore > L.teamBScore ? 'Gana ' + L.teamAName : L.teamBScore > L.teamAScore ? 'Gana ' + L.teamBName : 'Empate' }}</p>
            @if (stats(); as S) {
              <p class="meta">Aciertos {{ S.correctAnswers }} · Errores {{ S.wrongAnswers }} · Robos {{ S.stealsSucceeded }} · Rondas {{ S.roundCount }}</p>
            }
            <div class="controls">
              <ui-button type="button" (click)="replay()">Rejugar con el mismo pack</ui-button>
              <a routerLink="/teacher/game-show">Volver al hub</a>
            </div>
          </article>
        }

        @if (L.currentRound; as R) {
          <article class="board">
            <h2>Ronda {{ R.sortOrder + 1 }} / {{ L.roundCount }}</h2>
            <p class="q">{{ R.questionText }}</p>
            <p class="meta">Fase: {{ phaseLabel(R.phase) }} · Errores: {{ '❌'.repeat(R.strikes) }}{{ '⬜'.repeat(Math.max(0, 3 - R.strikes)) }}</p>
            <p class="meta">
              Control: <strong>{{ teamName(L, R.controllingTeam) }}</strong>
              @if (R.buzzWinnerTeam) { · Buzzer: <strong>{{ teamName(L, R.buzzWinnerTeam) }}</strong> }
              · Puntos acumulados: <strong>{{ R.roundPointsForController }}</strong>
            </p>
            @if (R.phase === 'Steal') {
              <p class="steal">Roba <strong>{{ teamName(L, stealingTeam(R.controllingTeam)) }}</strong> con una sola respuesta.</p>
            }
            <ol>
              @for (a of R.answers; track a.id) {
                <li [class.on]="a.isRevealed">
                  <span class="rank">{{ a.rank }}</span>
                  <span class="txt">{{ a.isRevealed ? a.text : '████████████' }}</span>
                  <span class="pts">{{ a.isRevealed ? a.points : '??' }}</span>
                  @if (!a.isRevealed && R.phase !== 'Finished') {
                    <ui-button type="button" variant="ghost" (click)="reveal(a.id)">Revelar</ui-button>
                  }
                </li>
              }
            </ol>
          </article>
        }

        <article class="players">
          <h3>Jugadores ({{ L.players.length }})</h3>
          <div class="cols">
            <div>
              <h4>{{ L.teamAName }} ({{ teamCount(L, 'A') }})</h4>
              @for (p of teamPlayers(L, 'A'); track p.id) {
                <p>{{ p.displayName }} {{ p.isConnected ? '●' : '○' }}
                  <button type="button" class="link" (click)="assign(p.id, 'B')">→ B</button>
                </p>
              }
            </div>
            <div>
              <h4>{{ L.teamBName }} ({{ teamCount(L, 'B') }})</h4>
              @for (p of teamPlayers(L, 'B'); track p.id) {
                <p>{{ p.displayName }} {{ p.isConnected ? '●' : '○' }}
                  <button type="button" class="link" (click)="assign(p.id, 'A')">→ A</button>
                </p>
              }
            </div>
          </div>
        </article>
      </section>
    } @else {
      <ui-error [message]="error() || 'Cargando…'" />
    }
  `,
  styles: [`
    .host { display: grid; gap: 1rem; }
    .bar { display: flex; justify-content: space-between; gap: 1rem; flex-wrap: wrap; }
    .eyebrow { margin: 0; color: var(--color-text-secondary); font-weight: 700; letter-spacing: 0.06em; text-transform: uppercase; font-size: 0.75rem; }
    .status { margin: 0.35rem 0 0; color: var(--color-text-secondary); }
    .links { display: flex; gap: 0.85rem; align-items: center; flex-wrap: wrap; }
    .join-panel {
      display: grid;
      grid-template-columns: 1fr auto;
      gap: 1.25rem;
      align-items: center;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: 16px;
      padding: 1.25rem 1.4rem;
    }
    .join-label {
      margin: 0;
      text-transform: uppercase;
      letter-spacing: 0.08em;
      font-size: 0.75rem;
      font-weight: 800;
      color: var(--color-primary);
    }
    .join-code {
      margin: 0.35rem 0 0.75rem;
      font-size: clamp(2.4rem, 6vw, 3.6rem);
      font-weight: 900;
      letter-spacing: 0.18em;
      line-height: 1;
    }
    .join-hint { margin: 0; color: var(--color-text-secondary); font-size: 0.95rem; }
    .join-url { margin: 0.35rem 0 0.85rem; word-break: break-all; font-weight: 700; }
    .join-actions { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    .copied { margin: 0.55rem 0 0; color: var(--color-success, #16a34a); font-weight: 700; }
    .qr {
      width: 220px;
      height: 220px;
      border-radius: 12px;
      background: #fff;
      padding: 0.65rem;
      box-sizing: border-box;
    }
    .score { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    .score > div { background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 14px; padding: 1rem; display: flex; justify-content: space-between; align-items: baseline; }
    .score strong { font-size: 2rem; }
    .controls { display: flex; flex-wrap: wrap; gap: 0.55rem; }
    .board, .players { background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 16px; padding: 1rem; }
    .q { font-size: 1.25rem; font-weight: 700; }
    ol { list-style: none; padding: 0; margin: 0; display: grid; gap: 0.45rem; }
    li { display: grid; grid-template-columns: 2rem 1fr 3rem auto; gap: 0.5rem; align-items: center; padding: 0.55rem 0.65rem; border-radius: 10px; background: color-mix(in srgb, var(--color-primary) 8%, transparent); }
    li.on { background: color-mix(in srgb, var(--color-success) 18%, transparent); }
    .cols { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .link { background: none; border: 0; color: var(--color-primary); cursor: pointer; }
    .flash { color: var(--color-success); font-weight: 800; }
    .conn { color: var(--color-text-secondary); font-weight: 700; }
    .steal { font-weight: 800; color: var(--color-primary); }
    @media (max-width: 700px) {
      .join-panel, .cols, .score { grid-template-columns: 1fr; }
      .qr { justify-self: start; }
      li { grid-template-columns: 2rem 1fr 3rem; }
    }
  `]
})
export class GameShowHostPage implements OnInit, OnDestroy {
  private readonly api = inject(GameShowApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sfx = inject(GameShowSfxService);
  readonly Math = Math;

  readonly lobby = signal<GameShowLobbyDto | null>(null);
  readonly stats = signal<GameShowStatsDto | null>(null);
  readonly error = signal<string | null>(null);
  readonly flash = signal<string | null>(null);
  readonly connection = signal<string | null>(null);
  readonly qrUrl = signal('');
  readonly copied = signal<string | null>(null);
  private hub: HubConnection | null = null;
  private sessionId = 0;
  private flashTimer: ReturnType<typeof setTimeout> | null = null;
  private copiedTimer: ReturnType<typeof setTimeout> | null = null;
  private lastQrCode = '';
  private lastStatus: string | null = null;

  ngOnInit(): void {
    this.sessionId = Number(this.route.snapshot.paramMap.get('sessionId'));
    this.reload();
    this.connectHub();
  }

  ngOnDestroy(): void {
    if (this.flashTimer) clearTimeout(this.flashTimer);
    if (this.copiedTimer) clearTimeout(this.copiedTimer);
    void this.hub?.stop();
  }

  phaseLabel(phase: string): string {
    switch (phase) {
      case 'WaitingBuzz': return 'Buzzer';
      case 'Playing': return 'Respondiendo';
      case 'Steal': return 'Oportunidad de robo';
      case 'Finished': return 'Ronda terminada';
      default: return phase;
    }
  }

  teamName(L: GameShowLobbyDto, team: string | null): string {
    if (team === 'A') return L.teamAName;
    if (team === 'B') return L.teamBName;
    return 'Sin definir';
  }

  stealingTeam(controllingTeam: string | null): string | null {
    if (!controllingTeam) return null;
    return controllingTeam === 'A' ? 'B' : 'A';
  }

  teamPlayers(L: GameShowLobbyDto, team: string) {
    return L.players.filter((p) => p.team === team);
  }

  teamCount(L: GameShowLobbyDto, team: string): number {
    return this.teamPlayers(L, team).length;
  }

  joinUrl(code: string): string {
    const origin = typeof window !== 'undefined' ? window.location.origin : '';
    return `${origin}/live/join/${code}`;
  }

  copyCode(code: string): void {
    void this.copyText(code, 'Código copiado');
  }

  copyLink(code: string): void {
    void this.copyText(this.joinUrl(code), 'Enlace copiado');
  }

  start(): void { this.act(() => this.api.start(this.sessionId)); }
  pause(): void { this.api.pause(this.sessionId).subscribe({ next: () => this.reload(), error: (e) => this.error.set(mapApiError(e)) }); }
  resume(): void { this.api.resume(this.sessionId).subscribe({ next: () => this.reload(), error: (e) => this.error.set(mapApiError(e)) }); }
  forceBuzz(team: string): void { this.act(() => this.api.forceBuzz(this.sessionId, team)); }
  strike(): void { this.act(() => this.api.strike(this.sessionId)); }
  failSteal(): void { this.act(() => this.api.failSteal(this.sessionId)); }
  endRound(): void { this.act(() => this.api.endRound(this.sessionId)); }
  reveal(answerId: number): void { this.act(() => this.api.reveal(this.sessionId, answerId)); }
  next(): void { this.act(() => this.api.nextRound(this.sessionId)); }
  finish(): void {
    if (!confirm('¿Finalizar la partida?')) return;
    this.act(() => this.api.finish(this.sessionId));
  }
  assign(playerId: number, team: string): void {
    this.act(() => this.api.assign(this.sessionId, playerId, team));
  }

  replay(): void {
    this.api.replay(this.sessionId, {}).subscribe({
      next: (lobby) => void this.router.navigate(['/teacher/game-show', lobby.id, 'host']),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  exportQuestions(format: 'csv' | 'json'): void {
    this.api.exportQuestions(this.sessionId, format).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `cale-100-dijeron-${this.sessionId}.${format}`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private act(call: () => import('rxjs').Observable<GameShowLobbyDto>): void {
    call().subscribe({
      next: (lobby) => this.applyLobby(lobby),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private reload(): void {
    this.api.get(this.sessionId, null, true).subscribe({
      next: (lobby) => this.applyLobby(lobby),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private applyLobby(lobby: GameShowLobbyDto): void {
    if (lobby.status === 'Ended' && this.lastStatus !== 'Ended') {
      this.api.stats(this.sessionId).subscribe({
        next: (s) => this.stats.set(s),
        error: () => this.stats.set(null)
      });
    }
    this.lastStatus = lobby.status;
    this.lobby.set(lobby);
    this.refreshQr(lobby.joinCode);
  }

  private refreshQr(code: string): void {
    if (!code || code === this.lastQrCode) return;
    this.lastQrCode = code;
    try {
      this.qrUrl.set(buildQrDataUrl(this.joinUrl(code), 220));
    } catch {
      this.qrUrl.set('');
    }
  }

  private async copyText(text: string, okMessage: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.copied.set(okMessage);
      if (this.copiedTimer) clearTimeout(this.copiedTimer);
      this.copiedTimer = setTimeout(() => this.copied.set(null), 2000);
    } catch {
      this.error.set('No se pudo copiar. Copia el código manualmente.');
    }
  }

  private showFlash(message: string): void {
    this.flash.set(message);
    if (this.flashTimer) clearTimeout(this.flashTimer);
    this.flashTimer = setTimeout(() => this.flash.set(null), 2500);
  }

  private connectHub(): void {
    this.hub = this.api.buildHub();
    this.hub.on('LobbyUpdated', (lobby: GameShowLobbyDto) => {
      // El payload del hub oculta las respuestas sin revelar: recargamos la vista de host.
      this.reload();
      void lobby;
    });
    this.hub.on('BuzzWon', () => {
      this.sfx.play('buzz');
      this.showFlash('¡Buzzer!');
    });
    this.hub.on('CorrectAnswer', () => {
      this.sfx.play('correct');
      this.showFlash('¡Correcto!');
    });
    this.hub.on('Strike', () => {
      this.sfx.play('strike');
      this.showFlash('Error');
    });
    this.hub.on('StealOpportunity', () => {
      this.sfx.play('steal');
      this.showFlash('¡Oportunidad de robo!');
    });
    this.hub.on('StealSucceeded', () => {
      this.sfx.play('correct');
      this.showFlash('¡Robo exitoso!');
    });
    this.hub.on('StealFailed', () => {
      this.sfx.play('stealFail');
      this.showFlash('Robo fallido');
    });
    this.hub.on('GameEnded', () => {
      this.sfx.play('end');
      this.showFlash('Partida finalizada');
    });
    this.hub.onreconnecting(() => this.connection.set('Reconectando…'));
    this.hub.onreconnected(() => {
      this.connection.set(null);
      void this.hub?.invoke('JoinAsHost', this.sessionId);
      this.reload();
    });
    this.hub.onclose(() => this.connection.set('Conexión perdida. Recarga la página.'));
    void this.hub.start().then(() => this.hub!.invoke('JoinAsHost', this.sessionId));
  }
}
