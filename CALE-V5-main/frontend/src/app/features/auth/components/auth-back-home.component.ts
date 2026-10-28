import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'auth-back-home',
  standalone: true,
  imports: [RouterLink],
  template: `<a routerLink="/" class="back-home">← Volver al inicio</a>`,
  styles: `
    :host {
      display: contents;
    }
    .back-home {
      position: absolute;
      top: max(0.65rem, env(safe-area-inset-top, 0px));
      left: max(0.65rem, env(safe-area-inset-left, 0px));
      z-index: 5;
      color: var(--color-primary);
      font-weight: 700;
      font-size: var(--text-sm);
      text-decoration: none;
      padding: 0.45rem 0.7rem;
      min-height: 2.5rem;
      display: inline-flex;
      align-items: center;
      border-radius: var(--radius-md);
      background: color-mix(in srgb, var(--color-surface) 92%, transparent);
      border: 1px solid var(--color-border);
      box-shadow: 0 6px 18px rgb(0 0 0 / 0.18);
    }
    .back-home:hover {
      background: var(--color-primary-soft);
    }
  `
})
export class AuthBackHomeComponent {}
