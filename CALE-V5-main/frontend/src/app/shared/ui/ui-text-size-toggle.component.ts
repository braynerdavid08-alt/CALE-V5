import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TextSizeService } from '../../core/theme/text-size.service';

@Component({
  selector: 'ui-text-size-toggle',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      class="size-toggle"
      [class.on]="size.large()"
      [attr.aria-pressed]="size.large()"
      [title]="size.large() ? 'Volver a la letra normal' : 'Ver la letra más grande'"
      (click)="size.toggle()">
      <span class="aa" aria-hidden="true">A<span>A</span></span>
      <span class="label">{{ size.large() ? 'Letra normal' : 'Letra grande' }}</span>
    </button>
  `,
  styles: [`
    :host { display: inline-flex; }
    .size-toggle {
      display: inline-flex;
      align-items: center;
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
      font-weight: 700;
      cursor: pointer;
    }
    .size-toggle:hover { background: var(--color-chip); border-color: var(--color-border-strong); }
    .size-toggle.on { border-color: var(--color-primary); }
    .aa { font-weight: 800; font-size: 0.85em; line-height: 1; }
    .aa span { font-size: 1.35em; }
    .label { line-height: 1; white-space: nowrap; }
    @media (max-width: 700px) {
      .label { display: none; }
      .size-toggle { padding: 0; justify-content: center; width: var(--control-height); }
    }
  `]
})
export class UiTextSizeToggleComponent {
  readonly size = inject(TextSizeService);
}
