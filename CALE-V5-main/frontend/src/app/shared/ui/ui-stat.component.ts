import { Component, Input } from '@angular/core';

@Component({
  selector: 'ui-stat',
  standalone: true,
  template: `
    <article class="stat" [attr.data-tone]="tone">
      <span class="label">{{ label }}</span>
      <strong>{{ value }}</strong>
      @if (hint) {
        <span class="hint">{{ hint }}</span>
      }
    </article>
  `,
  styles: [`
    .stat {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      padding: 1rem 1.1rem;
      box-shadow: var(--shadow-sm);
    }
    .label {
      display: block;
      color: var(--color-text-secondary);
      font-size: var(--text-sm);
      font-weight: 650;
    }
    strong {
      display: block;
      margin-top: 0.2rem;
      font-size: 1.55rem;
      letter-spacing: -0.03em;
    }
    .hint {
      display: block;
      margin-top: 0.25rem;
      color: var(--color-text-secondary);
      font-size: var(--text-xs);
    }
    .stat[data-tone='primary'] strong { color: var(--color-primary); }
    .stat[data-tone='success'] strong { color: var(--color-success); }
    .stat[data-tone='warning'] strong { color: var(--color-warning); }
  `]
})
export class UiStatComponent {
  @Input({ required: true }) label = '';
  @Input() value: string | number = '';
  @Input() hint = '';
  @Input() tone: 'neutral' | 'primary' | 'success' | 'warning' = 'neutral';
}
