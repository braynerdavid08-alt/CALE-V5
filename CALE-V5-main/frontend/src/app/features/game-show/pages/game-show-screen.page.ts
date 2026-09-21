import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HubConnection } from '@microsoft/signalr';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { mapApiError } from '../../../core/http/map-api-error';
import { GameShowApi, GameShowLobbyDto } from '../api/game-show.api';

@Component({
  selector: 'app-game-show-screen-page',
  standalone: true,
  imports: [UiErrorComponent],
  template: `
    @if (lobby(); as L) {
      <section class="screen">
        <p class="brand">100 ESTUDIANTES DIJERON</p>
        <div class="score">
          <div [class.active]="isControlling(L, 'A')">
            <span>{{ L.teamAName }}</span><strong>{{ L.teamAScore }}</strong>
          </div>
          <div [class.active]="isControlling(L, 'B')">
            <span>{{ L.teamBName }}</span><strong>{{ L.teamBScore }}</strong>
          </div>
        </div>
        @if (flash()) { <p class="flash">{{ flash() }}</p> }
        @if (L.currentRound; as R) {
          <h1>{{ R.questionText }}</h1>
          <p class="phase">
            {{ phase(R.phase) }}
            @if (R.controllingTeam) { · Turno de {{ teamName(L, R.controllingTeam) }} }
          </p>
          @if (R.phase === 'Steal') {
            <p class="steal">🔥 ROBO: {{ teamName(L, otherTeam(R.controllingTeam)) }} tiene una sola respuesta</p>
          }
          <p class="strikes">
            @for (x of strikeSlots(); track $index) {
              <span [class.hit]="$index < R.strikes">✕</span>
            }
          </p>
          <ol>
            @for (a of R.answers; track a.id) {
              <li [class.revealed]="a.isRevealed">
                <em>{{ a.rank }}</em>
                <span>{{ a.isRevealed ? a.text : '████████████████' }}</span>
                <b>{{ a.isRevealed ? a.points : '' }}</b>
              </li>
            }
          </ol>
        } @else {
          <h1>Código {{ L.joinCode }}</h1>
          <p>Esperando jugadores… ({{ L.players.length }} conectados)</p>
        }
        <footer class="foot">
          <span>Únete en <strong>{{ joinUrl() }}</strong></span>
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
    .score strong { font-size: clamp(2rem, 6vw, 4rem); }
    h1 { font-size: clamp(1.6rem, 4.5vw, 3rem); margin: 0; line-height: 1.15; }
    .phase { opacity: 0.8; font-size: 1.2rem; }
    .steal { margin: 0; font-size: clamp(1.2rem, 3vw, 2rem); font-weight: 900; color: #fdba74; }
    .strikes { margin: 0; display: flex; gap: 0.75rem; }
    .strikes span { font-size: clamp(2rem, 6vw, 4rem); font-weight: 900; line-height: 1; color: rgba(255,255,255,0.14); transition: color 0.3s ease, transform 0.3s ease; }
    .strikes span.hit { color: #f87171; transform: scale(1.1); }
    ol { list-style: none; padding: 0; margin: 0; display: grid; gap: 0.75rem; }
    li { display: grid; grid-template-columns: 3rem 1fr 5rem; gap: 1rem; align-items: center; padding: 1rem 1.25rem; border-radius: 16px; background: rgba(0,0,0,0.35); border: 1px solid rgba(94,176,255,0.25); font-size: clamp(1.2rem, 3vw, 2rem); font-weight: 800; letter-spacing: 0.04em; transition: background 0.35s ease, border-color 0.35s ease, transform 0.35s ease; }
    li.revealed { background: rgba(40, 160, 100, 0.25); border-color: rgba(80, 220, 150, 0.45); transform: translateX(0.35rem); }
    .flash { font-size: clamp(1.5rem, 4vw, 2.5rem); font-weight: 900; color: #7dd3fc; text-align: center; }
    .foot { display: flex; justify-content: space-between; gap: 1rem; flex-wrap: wrap; opacity: 0.75; font-size: clamp(1rem, 2vw, 1.4rem); letter-spacing: 0.04em; }
    .conn { color: #fdba74; font-weight: 800; }
  `]
})
export class GameShowScreenPage implements OnInit, OnDestroy {
  private readonly api = inject(GameShowApi);
  private readonly route = inject(ActivatedRoute);
  readonly lobby = signal<GameShowLobbyDto | null>(null);
  readonly error = signal<string | null>(null);
  readonly flash = signal<string | null>(null);
  readonly connection = signal<string | null>(null);
  private hub: HubConnection | null = null;
  private sessionId = 0;
  private flashTimer: ReturnType<typeof setTimeout> | null = null;

  ngOnInit(): void {
    this.sessionId = Number(this.route.snapshot.paramMap.get('sessionId'));
    this.reload();
    this.hub = this.api.buildHub();
    this.hub.on('LobbyUpdated', (lobby: GameShowLobbyDto) => this.lobby.set(lobby));
    this.hub.on('BuzzWon', () => this.showFlash('¡BUZZER!'));
    this.hub.on('CorrectAnswer', () => this.showFlash('🎉 ¡CORRECTO!'));
    this.hub.on('Strike', () => this.showFlash('❌ ERROR'));
    this.hub.on('StealOpportunity', () => this.showFlash('🔥 ¡OPORTUNIDAD DE ROBO!'));
    this.hub.on('StealSucceeded', () => this.showFlash('🎉 ¡ROBO EXITOSO!'));
    this.hub.on('StealFailed', () => this.showFlash('❌ ROBO FALLIDO'));
    this.hub.on('GameEnded', () => this.showFlash('🏆 PARTIDA TERMINADA'));
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
    void this.hub?.stop();
  }

  phase(p: string): string {
    if (p === 'WaitingBuzz') return 'Presionen el buzzer';
    if (p === 'Playing') return 'Respondiendo';
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

  strikeSlots(): readonly number[] {
    return STRIKE_SLOTS;
  }

  joinUrl(): string {
    const origin = typeof window !== 'undefined' ? window.location.origin : '';
    return `${origin}/game-show/join`;
  }

  private showFlash(message: string): void {
    this.flash.set(message);
    if (this.flashTimer) clearTimeout(this.flashTimer);
    this.flashTimer = setTimeout(() => this.flash.set(null), 2500);
  }

  private reload(): void {
    this.api.get(this.sessionId).subscribe({
      next: (lobby) => this.lobby.set(lobby),
      error: (err) => this.error.set(mapApiError(err))
    });
  }
}

const STRIKE_SLOTS = [0, 1, 2] as const;
