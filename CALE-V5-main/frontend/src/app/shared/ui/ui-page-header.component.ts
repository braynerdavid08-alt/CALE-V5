import { Component, Input } from '@angular/core';

@Component({
  selector: 'ui-page-header',
  standalone: true,
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
    .ph {
      display: flex;
      justify-content: space-between;
      gap: var(--spacing-md);
      align-items: flex-start;
      flex-wrap: wrap;
      margin-bottom: var(--spacing-lg);
    }
    h1 { margin: 0; }
    .eyebrow {
      margin: 0 0 0.2rem;
      color: var(--color-primary);
      font-size: var(--text-xs);
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: uppercase;
    }
    .sub {
      margin: 0.3rem 0 0;
      color: var(--color-text-secondary);
    }
    .actions { display: flex; gap: var(--spacing-sm); flex-wrap: wrap; }
  `]
})
export class UiPageHeaderComponent {
  @Input({ required: true }) title = '';
  @Input() subtitle = '';
  @Input() eyebrow = '';
}
