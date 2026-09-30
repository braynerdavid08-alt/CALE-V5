import { ChangeDetectionStrategy, Component, Input, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PlayFxService } from '../play-fx.service';

@Component({
  selector: 'play-topbar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <header class="top">
      @if (back && back !== '/student') {
        <a class="back" [routerLink]="back">← {{ backLabel }}</a>
      }
      <button
        type="button"
        class="mute"
        [attr.aria-pressed]="fx.muted()"
        (click)="fx.toggleMute()">
        {{ fx.muted() ? '🔇 Sonido apagado' : '🔊 Sonido activado' }}
      </button>
    </header>
    <div class="head">
      <h1>{{ title }}</h1>
      @if (subtitle) {
        <p>{{ subtitle }}</p>
      }
    </div>
  `,
  styles: [`
    :host { display: grid; gap: 0.6rem; }
    .top { display: flex; justify-content: space-between; align-items: center; gap: 0.5rem; }
    .mute { margin-left: auto; }
    .back { color: var(--color-primary); font-weight: 700; text-decoration: none; }
    .back:hover { text-decoration: underline; }
    .mute {
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: var(--color-text-secondary);
      border-radius: 999px;
      padding: 0.3rem 0.8rem;
      font: inherit;
      font-size: var(--text-xs);
      cursor: pointer;
    }
    h1 { margin: 0; font-size: clamp(1.45rem, 3vw, 1.9rem); }
    .head p { margin: 0.3rem 0 0; color: var(--color-text-secondary); }
  `]
})
export class PlayTopbarComponent {
  readonly fx = inject(PlayFxService);
  @Input({ required: true }) title = '';
  @Input() subtitle = '';
  @Input() back = '/student';
  @Input() backLabel = 'Volver al inicio';
}
