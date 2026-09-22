import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ThemeService } from '../../core/theme/theme.service';
import { UiIconComponent } from './ui-icon.component';

@Component({
  selector: 'ui-theme-toggle',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [UiIconComponent],
  template: `
    <button
      type="button"
      class="theme-toggle"
      [attr.aria-label]="theme.mode() === 'dark' ? dayAria : nightAria"
      [title]="theme.mode() === 'dark' ? dayTitle : nightTitle"
      (click)="theme.toggle()">
      @if (theme.mode() === 'dark') {
        <ui-icon name="sun" />
        <span class="label">{{ dayLabel }}</span>
      } @else {
        <ui-icon name="moon" />
        <span class="label">Noche</span>
      }
    </button>
  `,
  styles: [`
    :host { display: inline-flex; }

    .theme-toggle {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 0.4rem;
      min-height: var(--control-height);
      min-width: var(--control-height);
      padding: 0 0.75rem;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-sm);
      background: var(--color-surface);
      color: var(--color-text);
      font: inherit;
      font-size: var(--text-sm);
      font-weight: 600;
      cursor: pointer;
      transition:
        background var(--transition),
        border-color var(--transition),
        color var(--transition);
    }

    .theme-toggle:hover {
      background: var(--color-chip);
      border-color: var(--color-border-strong);
    }

    .label { line-height: 1; }

    @media (max-width: 700px) {
      .label { display: none; }
      .theme-toggle { padding: 0; width: var(--control-height); }
    }
  `]
})
export class UiThemeToggleComponent {
  readonly theme = inject(ThemeService);

  /** Avoid mojibake if the source file encoding is wrong. */
  readonly dayLabel = 'D\u00eda';
  readonly dayTitle = 'Modo d\u00eda';
  readonly dayAria = 'Activar modo d\u00eda';
  readonly nightTitle = 'Modo noche';
  readonly nightAria = 'Activar modo noche';
}
