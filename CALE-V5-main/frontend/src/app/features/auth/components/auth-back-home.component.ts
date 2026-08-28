import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'auth-back-home',
  standalone: true,
  imports: [RouterLink],
  template: `<a routerLink="/" class="back-home">← Volver al inicio</a>`,
  styles: `
    .back-home {
      position: absolute;
      top: var(--spacing-md);
      left: var(--spacing-md);
      z-index: 3;
      color: var(--color-primary);
      font-weight: 700;
      font-size: var(--text-sm);
      text-decoration: none;
      padding: 0.35rem 0.5rem;
      border-radius: var(--radius-md);
      background: color-mix(in srgb, var(--color-surface) 88%, transparent);
      border: 1px solid var(--color-border);
    }
    .back-home:hover {
      background: var(--color-primary-soft);
    }
  `
})
export class AuthBackHomeComponent {}
