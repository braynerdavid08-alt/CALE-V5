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
    }
  `]
})
export class UiCardComponent {}
