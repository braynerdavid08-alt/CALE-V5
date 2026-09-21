import { Component, Input } from '@angular/core';

@Component({
  selector: 'ui-loading',
  standalone: true,
  template: `
    <div class="loading" role="status" aria-live="polite" aria-busy="true">
      <span class="dot" aria-hidden="true"></span>
      <span>{{ label }}</span>
    </div>
  `,
  styles: [`
    .loading {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      color: var(--color-text-secondary);
      padding: var(--spacing-md) 0;
    }
    .dot {
      width: 0.85rem;
      height: 0.85rem;
      border-radius: 50%;
      background: var(--color-primary);
      animation: pulse 0.9s ease-in-out infinite;
    }
    @keyframes pulse {
      50% { opacity: 0.35; transform: scale(0.85); }
    }
    @media (prefers-reduced-motion: reduce) {
      .dot { animation: none; opacity: 0.85; }
    }
  `]
})
export class UiLoadingComponent {
  @Input() label = 'Cargando información...';
}
