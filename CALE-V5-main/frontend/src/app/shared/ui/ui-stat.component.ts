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
      min-height: 6.5rem;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      padding: var(--spacing-md) 1.1rem;
      box-shadow: var(--shadow-sm);
      display: grid;
      align-content: start;
      gap: 0.2rem;
    }
    .label {
      display: block;
      color: var(--color-text-secondary);
      font-size: var(--text-sm);
      font-weight: 650;
      line-height: var(--leading-tight);
    }
    strong {
      display: block;
      font-size: var(--text-xl);
      letter-spacing: -0.03em;
      line-height: var(--leading-tight);
      color: var(--color-text);
    }
    .hint {
      display: block;
      color: var(--color-text-secondary);
      font-size: var(--text-xs);
      line-height: var(--leading-body);
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
