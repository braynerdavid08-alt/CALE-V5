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
      color: var(--color-text, #132a30);
      font-weight: 650;
      font-size: var(--text-sm);
      text-decoration: none;
      padding: 0.45rem 0.85rem;
      min-height: 2.5rem;
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
      border-radius: 999px;
      background: color-mix(in srgb, var(--color-surface, #fff) 90%, transparent);
      border: 1px solid color-mix(in srgb, var(--color-border, #dce8df) 90%, transparent);
      box-shadow: 0 4px 14px rgb(9 35 52 / 0.06);
      backdrop-filter: blur(8px);
      transition: background 160ms ease, border-color 160ms ease;
    }
    .back-home:hover {
      background: color-mix(in srgb, var(--color-primary, #168a4a) 10%, #fff);
      border-color: color-mix(in srgb, var(--color-primary, #168a4a) 35%, var(--color-border, #dce8df));
    }
    .back-home:focus-visible {
      outline: 2px solid var(--color-primary, #168a4a);
      outline-offset: 2px;
    }
  `
})
export class AuthBackHomeComponent {}
