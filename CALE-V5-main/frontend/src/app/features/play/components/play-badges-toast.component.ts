import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UiIconComponent } from '../../../shared/ui/ui-icon.component';
import { Badge } from '../api/play.api';

@Component({
  selector: 'play-badges-toast',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, UiIconComponent],
  template: `
    @if (badges.length) {
      <aside class="toast" role="status" aria-live="polite">
        <p class="toast-kicker">¡Nueva insignia!</p>
        @for (badge of badges; track badge.code) {
          <div class="toast-badge">
            <span class="toast-icon" aria-hidden="true"><ui-icon [name]="badge.icon" /></span>
            <span>
              <strong>{{ badge.title }}</strong>
              <small>{{ badge.description }}</small>
            </span>
          </div>
        }
        <div class="toast-actions">
          <a routerLink="/student/play/achievements">Ver mis logros</a>
          <button type="button" (click)="closed.emit()">Cerrar</button>
        </div>
      </aside>
    }
  `,
  styles: [`
    .toast {
      position: fixed;
      right: 1rem;
      bottom: 1rem;
      z-index: 1000;
      width: min(22rem, calc(100vw - 2rem));
      padding: 1rem 1.1rem;
      border-radius: var(--radius-lg);
      background: var(--color-surface);
      border: 2px solid #f59e0b;
      box-shadow: 0 18px 40px rgba(15, 23, 42, 0.25);
      animation: toast-in 0.35s ease;
    }
    .toast-kicker {
      margin: 0 0 0.5rem;
      font-size: var(--text-xs);
      font-weight: 800;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: #b45309;
    }
    .toast-badge { display: flex; gap: 0.75rem; align-items: center; margin-bottom: 0.5rem; }
    .toast-badge small { display: block; color: var(--color-text-secondary); }
    .toast-icon {
      flex: none;
      display: grid;
      place-items: center;
      width: 2.8rem;
      height: 2.8rem;
      border-radius: 50%;
      color: #b45309;
      background: #fef3c7;
    }
    .toast-actions { display: flex; justify-content: space-between; align-items: center; margin-top: 0.4rem; }
    .toast-actions a { color: var(--color-primary); font-weight: 700; }
    .toast-actions button {
      border: 0;
      background: transparent;
      color: var(--color-text-secondary);
      font: inherit;
      cursor: pointer;
    }
    @keyframes toast-in { from { transform: translateY(20px); opacity: 0; } }
  `]
})
export class PlayBadgesToastComponent {
  @Input() badges: Badge[] = [];
  @Output() readonly closed = new EventEmitter<void>();
}
