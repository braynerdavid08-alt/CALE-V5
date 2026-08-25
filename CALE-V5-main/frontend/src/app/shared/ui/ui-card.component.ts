import { Component } from '@angular/core';

@Component({
  selector: 'ui-card',
  standalone: true,
  template: `<section class="card"><ng-content /></section>`,
  styles: [`
    .card {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-sm);
      padding: var(--spacing-lg);
      color: var(--color-text);
    }

    @media (max-width: 600px) {
      .card { padding: var(--spacing-md); }
    }
  `]
})
export class UiCardComponent {}
