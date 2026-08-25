import { Component, Input } from '@angular/core';

@Component({
  selector: 'ui-error',
  standalone: true,
  template: `
    @if (message) {
      <p class="error" role="alert">{{ message }}</p>
    }
  `,
  styles: [`
    .error {
      margin: 0 0 0.9rem;
      padding: 0.7rem 0.85rem;
      border-radius: var(--radius-md);
      background: var(--color-danger-soft);
      color: var(--color-danger);
      font-size: 0.92rem;
    }
  `]
})
export class UiErrorComponent {
  @Input() message: string | null = null;
}
