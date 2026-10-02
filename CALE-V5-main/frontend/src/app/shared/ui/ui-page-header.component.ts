import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

@Component({
  selector: 'ui-page-header',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="ph">
      <div>
        @if (eyebrow) {
          <p class="eyebrow">{{ eyebrow }}</p>
        }
        <h1>{{ title }}</h1>
        @if (subtitle) {
          <p class="sub">{{ subtitle }}</p>
        }
      </div>
      <div class="actions"><ng-content /></div>
    </header>
  `,
  styles: [`
    :host { display: block; }
    .ph {
      position: relative;
      overflow: hidden;
      display: flex;
      justify-content: space-between;
      gap: var(--spacing-md);
      align-items: center;
      flex-wrap: wrap;
      margin-bottom: var(--spacing-lg);
      padding: clamp(1.1rem, 3vw, 1.6rem) clamp(1.1rem, 3vw, 1.75rem);
      border-radius: var(--radius-xl);
      border: 1px solid color-mix(in srgb, var(--color-primary) 22%, var(--color-border));
      background:
        radial-gradient(120% 160% at 0% 0%, color-mix(in srgb, var(--color-primary) 16%, transparent), transparent 55%),
        linear-gradient(135deg, var(--color-welcome-start), var(--color-welcome-end));
      box-shadow: var(--shadow-sm);
    }
    .ph > div:first-child { min-width: 0; flex: 1 1 18rem; }
    h1 {
      margin: 0;
      max-width: 44rem;
      font-size: clamp(1.45rem, 3.4vw, var(--text-2xl));
    }
    .eyebrow {
      display: inline-block;
      margin: 0 0 0.5rem;
      padding: 0.2rem 0.65rem;
      border-radius: 999px;
      background: color-mix(in srgb, var(--color-primary) 14%, transparent);
      color: var(--color-primary);
      font-size: var(--text-xs);
      font-weight: 800;
      letter-spacing: 0.08em;
      text-transform: uppercase;
    }
    .sub {
      margin: 0.4rem 0 0;
      color: var(--color-text-secondary);
      max-width: 46rem;
      line-height: var(--leading-body);
    }
    .actions:empty { display: none; }
    .actions {
      display: flex;
      gap: var(--spacing-sm);
      flex-wrap: wrap;
      align-items: center;
    }
  `]
})
export class UiPageHeaderComponent {
  @Input({ required: true }) title = '';
  @Input() subtitle = '';
  @Input() eyebrow = '';
}
