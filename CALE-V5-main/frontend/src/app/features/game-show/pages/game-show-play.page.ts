import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HubConnection } from '@microsoft/signalr';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { mapApiError } from '../../../core/http/map-api-error';
import { GameShowApi, GameShowLobbyDto } from '../api/game-show.api';

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
            <span>{{ L.teamAName }} {{ L.teamAScore }}</span>
            <span>{{ L.teamBName }} {{ L.teamBScore }}</span>
          </div>
        </header>
        <ui-error [message]="error()" />
        @if (flash()) { <p class="flash">{{ flash() }}</p> }

        @if (L.currentRound; as R) {
          <p class="q">{{ R.questionText }}</p>
          <ol class="board">
            @for (a of R.answers; track a.id) {
              <li>{{ a.isRevealed ? (a.text + ' · ' + a.points) : '████████' }}</li>
            }
          </ol>
          <p>Errores del equipo: {{ R.strikes }} / 3 · Fase: {{ R.phase }}</p>

          @if (R.phase === 'WaitingBuzz') {
            <ui-button type="button" class="buzz" (click)="buzz()">¡PRESIONAR!</ui-button>
          }

          @if ((R.phase === 'Playing' || R.phase === 'Steal') && myTurn(L)) {
            <label class="field">Tu respuesta
              <input class="input" [(ngModel)]="answer" name="answer" (keyup.enter)="send()" />
            </label>
            <ui-button type="button" (click)="send()">Enviar respuesta</ui-button>
            <ui-button type="button" variant="secondary" (click)="listenVoice()">Hablar (opcional)</ui-button>
          } @else if (R.phase === 'Playing' || R.phase === 'Steal') {
            <p class="muted">Espera el turno de tu equipo.</p>
          }
        } @else {
          <p>Esperando en el lobby… Código {{ L.joinCode }}</p>
        }
        <a routerLink="/game-show/join">Salir</a>
      </section>
    } @else {
      <ui-error [message]="error() || 'Cargando…'" />
    }
  `,
  styles: [`
    .play { max-width: 40rem; margin: 0 auto; padding: 1rem; display: grid; gap: 0.85rem; }
    .score { display: flex; gap: 1rem; font-weight: 800; font-size: 1.2rem; }
    .q { font-size: 1.35rem; font-weight: 800; }
    .board { list-style: none; padding: 0; display: grid; gap: 0.4rem; }
    .board li { padding: 0.75rem 1rem; border-radius: 12px; background: var(--color-surface); border: 1px solid var(--color-border); font-weight: 700; letter-spacing: 0.04em; }
    .buzz { min-height: 4.5rem; font-size: 1.4rem; font-weight: 900; }
    .flash { color: var(--color-primary); font-weight: 900; font-size: 1.2rem; }
    .muted { color: var(--color-text-secondary); }
    .field { display: grid; gap: 0.35rem; font-weight: 600; }
  `]
})
export class GameShowPlayPage implements OnInit, OnDestroy {
  private readonly api = inject(GameShowApi);
  private readonly route = inject(ActivatedRoute);

  readonly lobby = signal<GameShowLobbyDto | null>(null);
  readonly error = signal<string | null>(null);
  readonly flash = signal<string | null>(null);
  answer = '';
  private hub: HubConnection | null = null;
  private sessionId = 0;
  private token = '';
  private myTeam = 'A';

  ngOnInit(): void {
    this.sessionId = Number(this.route.snapshot.paramMap.get('sessionId'));
    this.token = this.api.loadPlayerToken(this.sessionId) || '';
    if (!this.token) {
      this.error.set('No hay sesión de jugador. Vuelve a unirte con el código.');
      return;
    }
    this.reload();
    this.connectHub();
  }

  ngOnDestroy(): void {
    void this.hub?.stop();
  }

  myTurn(L: GameShowLobbyDto): boolean {
    const r = L.currentRound;
    if (!r) return false;
    if (r.phase === 'Playing') return r.controllingTeam === this.myTeam;
    if (r.phase === 'Steal') {
      const other = r.controllingTeam === 'A' ? 'B' : 'A';
      return other === this.myTeam;
    }
    return false;
  }

  buzz(): void {
    this.api.buzz(this.sessionId, this.token).subscribe({
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  send(): void {
    const text = this.answer.trim();
    if (!text) return;
    this.api.answer(this.sessionId, this.token, text).subscribe({
      next: () => { this.answer = ''; },
      error: (err) => this.error.set(mapApiError(err))
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
      next: (lobby) => {
        this.lobby.set(lobby);
        // Infer team from connected players matching token is not available; keep last known from join.
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private connectHub(): void {
    this.hub = this.api.buildHub();
    this.hub.on('LobbyUpdated', (lobby: GameShowLobbyDto) => this.lobby.set(lobby));
    this.hub.on('BuzzWon', () => this.flash.set('¡Buzzer ganado!'));
    this.hub.on('CorrectAnswer', () => this.flash.set('¡Correcto!'));
    this.hub.on('Strike', () => this.flash.set('❌'));
    this.hub.on('StealOpportunity', () => this.flash.set('¡Oportunidad de robo!'));
    this.hub.on('StealSucceeded', () => this.flash.set('🎉 Robo exitoso'));
    this.hub.on('StealFailed', () => this.flash.set('Robo fallido'));
    void this.hub.start().then(() => this.hub!.invoke('JoinAsPlayer', this.sessionId, this.token));

    const savedTeam = localStorage.getItem(`cale.game-show.team.${this.sessionId}`);
    if (savedTeam) this.myTeam = savedTeam;
  }
}
