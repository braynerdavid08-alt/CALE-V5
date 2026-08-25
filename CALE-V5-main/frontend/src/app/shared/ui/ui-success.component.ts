import { Component, Input } from '@angular/core';

@Component({
  selector: 'ui-success',
  standalone: true,
  template: `
    @if (message) {
      <p class="ok" role="status">{{ message }}</p>
    }
  `,
  styles: [`
    .ok {
      margin: 0 0 var(--spacing-md);
      padding: 0.7rem 0.85rem;
      border-radius: var(--radius-md);
      background: var(--color-success-soft);
      color: var(--color-success);
      font-weight: 600;
    }
  `]
})
export class UiSuccessComponent {
  @Input() message: string | null = null;
}
