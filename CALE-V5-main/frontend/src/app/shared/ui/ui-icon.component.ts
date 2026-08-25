import { Component, Input } from '@angular/core';

@Component({
  selector: 'ui-icon',
  standalone: true,
  template: `
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="1.8"
      stroke-linecap="round"
      stroke-linejoin="round"
      aria-hidden="true">
      @switch (name) {
        @case ('home') {
          <path d="M3 10.5 12 3l9 7.5V21a1 1 0 0 1-1 1h-5v-7H9v7H4a1 1 0 0 1-1-1z"/>
        }
        @case ('users') {
          <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/>
          <circle cx="9" cy="7" r="4"/>
          <path d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/>
        }
        @case ('book') {
          <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/>
          <path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/>
        }
        @case ('exam') {
          <rect x="4" y="3" width="16" height="18" rx="2"/>
          <path d="M8 8h8M8 12h8M8 16h5"/>
        }
        @case ('chart') {
          <path d="M4 19V5M4 19h16"/>
          <path d="M8 15v-4M12 15V8M16 15v-7"/>
        }
        @case ('bell') {
          <path d="M6 8a6 6 0 1 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/>
          <path d="M10 21a2 2 0 0 0 4 0"/>
        }
        @case ('logout') {
          <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/>
          <path d="M16 17l5-5-5-5M21 12H9"/>
        }
        @case ('menu') {
          <path d="M4 6h16M4 12h16M4 18h16"/>
        }
        @case ('close') {
          <path d="M18 6 6 18M6 6l12 12"/>
        }
        @case ('plus') {
          <path d="M12 5v14M5 12h14"/>
        }
        @case ('search') {
          <circle cx="11" cy="11" r="7"/>
          <path d="m20 20-3-3"/>
        }
        @case ('group') {
          <path d="M3 7h18M5 7v12a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V7"/>
          <path d="M9 3h6v4H9z"/>
        }
        @case ('settings') {
          <circle cx="12" cy="12" r="3"/>
          <path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9c.3.6.9 1 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"/>
        }
        @case ('play') {
          <circle cx="12" cy="12" r="10"/>
          <path d="m10 8 6 4-6 4z"/>
        }
        @case ('star') {
          <path d="m12 3 2.6 5.4L20 9.3l-4 3.9.9 5.5L12 16.9 7.1 18.7 8 13.2 4 9.3l5.4-.9z"/>
        }
        @case ('clock') {
          <circle cx="12" cy="12" r="9"/>
          <path d="M12 7v5l3 2"/>
        }
        @case ('bank') {
          <path d="M3 10h18M5 10v8M19 10v8M9 10v8M15 10v8M2 18h20M12 3l9 7H3z"/>
        }
        @case ('sun') {
          <circle cx="12" cy="12" r="4"/>
          <path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/>
        }
        @case ('moon') {
          <path d="M21 14.5A8.5 8.5 0 1 1 9.5 3 7 7 0 0 0 21 14.5z"/>
        }
      }
    </svg>
  `,
  styles: [`
    :host { display: inline-flex; line-height: 0; }
    svg { width: 1.2rem; height: 1.2rem; }
  `]
})
export class UiIconComponent {
  @Input() name = 'home';
}
