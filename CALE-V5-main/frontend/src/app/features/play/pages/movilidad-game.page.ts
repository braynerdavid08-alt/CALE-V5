import { Component, HostListener, inject, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { Badge, GameSaved, PlayApi } from '../api/play.api';
import { PlayBadgesToastComponent } from '../components/play-badges-toast.component';
import { PlayTopbarComponent } from '../components/play-topbar.component';
import { PlayFxService } from '../play-fx.service';

const RESULT_TYPE = 'luzverde.movilidad.result';
/** Cache-bust so deploys and CSP/static fixes do not leave a blank iframe. */
const GAME_URL = '/games/movilidad/index.html?v=9';

interface MovilidadMessage {
  type?: string;
  mode?: string;
  score?: number;
  correct?: number;
  total?: number;
  won?: boolean;
}

@Component({
  selector: 'app-movilidad-game-page',
  standalone: true,
  imports: [UiButtonComponent, PlayBadgesToastComponent, PlayTopbarComponent],
  styleUrl: './play-page.css',
  template: `
    <div class="play-page wide">
      <play-topbar
        title="Juegos de movilidad"
        subtitle="Practica normas de tránsito. Tu puntaje se guarda en tu cuenta."
        back="/student"
        backLabel="Inicio" />

      @if (notice()) {
        <p class="notice" [class.err]="!!error()">{{ notice() }}</p>
      }

      <div class="frame-wrap">
        <iframe
          #frame
          class="game-frame"
          title="Juegos de movilidad"
          [src]="frameSrc()"
          allow="autoplay"
          referrerpolicy="no-referrer"></iframe>
      </div>

      <p class="hint">
        Si el juego no carga, recarga la página. Al terminar una partida el resultado se envía solo.
      </p>

      <ui-button type="button" variant="secondary" (click)="reload()">Recargar juego</ui-button>
    </div>
    <play-badges-toast [badges]="newBadges()" (dismissed)="newBadges.set([])" />
  `,
  styles: `
    .frame-wrap {
      border-radius: var(--radius-lg);
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      overflow: hidden;
      /* Tall enough on iPhone so control rows are not clipped by the iframe. */
      min-height: min(88dvh, 860px);
    }
    .game-frame {
      display: block;
      width: 100%;
      height: min(88dvh, 860px);
      border: 0;
      background: #eef2f7;
    }
    @media (max-width: 640px) {
      :host {
        display: block;
      }
      .play-page.wide {
        gap: 0.45rem;
        padding-bottom: 0.5rem;
      }
      .hint,
      ui-button {
        display: none;
      }
      .frame-wrap,
      .game-frame {
        min-height: calc(100dvh - 5.5rem);
        height: calc(100dvh - 5.5rem);
        border-radius: 12px;
      }
    }
    .hint {
      margin: 0;
      font-size: var(--text-sm);
      color: var(--color-text-secondary);
    }
    .notice {
      margin: 0;
      padding: 0.65rem 0.9rem;
      border-radius: var(--radius-md);
      background: color-mix(in srgb, var(--color-primary) 12%, var(--color-surface));
      color: var(--color-text);
      font-weight: 600;
    }
    .notice.err {
      background: color-mix(in srgb, #dc2626 12%, var(--color-surface));
    }
  `
})
export class MovilidadGamePage {
  private readonly api = inject(PlayApi);
  private readonly fx = inject(PlayFxService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly frameSrc = signal<SafeResourceUrl>(
    this.sanitizer.bypassSecurityTrustResourceUrl(GAME_URL)
  );
  readonly notice = signal<string | null>(null);
  readonly error = signal(false);
  readonly newBadges = signal<Badge[]>([]);
  private lastFingerprint = '';
  private frameNonce = 0;

  reload(): void {
    this.frameNonce++;
    this.frameSrc.set(
      this.sanitizer.bypassSecurityTrustResourceUrl(`${GAME_URL}&r=${this.frameNonce}`)
    );
    this.notice.set(null);
    this.error.set(false);
  }

  @HostListener('window:message', ['$event'])
  onMessage(event: MessageEvent): void {
    if (event.origin !== window.location.origin) {
      return;
    }
    const data = event.data as MovilidadMessage | null;
    if (!data || data.type !== RESULT_TYPE) {
      return;
    }

    const mode = (data.mode || 'unknown').toString().slice(0, 16);
    const score = Math.max(0, Math.floor(Number(data.score) || 0));
    const correct = Math.max(0, Math.floor(Number(data.correct) || 0));
    const total = Math.max(1, Math.floor(Number(data.total) || 1));
    const won = !!data.won;
    const fingerprint = `${mode}|${score}|${correct}|${total}|${won}|${Date.now() - (Date.now() % 2000)}`;
    if (fingerprint === this.lastFingerprint) {
      return;
    }
    this.lastFingerprint = fingerprint;

    this.api.saveMovilidad({ mode, score, correct, total, won }).subscribe({
      next: (saved: GameSaved) => {
        this.error.set(false);
        this.notice.set(
          saved.isRecord
            ? `¡Nuevo récord en ${mode}! Mejor: ${saved.best}`
            : `Resultado guardado (${mode}): ${saved.score}`
        );
        if (saved.newBadges?.length) {
          this.newBadges.set(saved.newBadges);
          this.fx.play('badge');
        }
      },
      error: (err) => {
        this.error.set(true);
        this.notice.set(mapApiError(err));
      }
    });
  }
}
