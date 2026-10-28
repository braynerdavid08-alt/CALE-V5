import { Component, Input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

export type SchoolDayBoard = 'attendance' | 'exam' | 'fleet';

/**
 * Shared switcher for the three related “today” boards.
 * Keeps each page’s own logic; only changes which board is open.
 */
@Component({
  selector: 'app-school-day-boards-nav',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="school-tabs day-boards-nav" aria-label="Operación de hoy">
      <a
        class="tab-link"
        routerLink="/school/attendance"
        routerLinkActive="on"
        [routerLinkActiveOptions]="{ exact: true }"
        [attr.aria-current]="active === 'attendance' ? 'page' : null">
        Teoría
      </a>
      <a
        class="tab-link"
        routerLink="/school/exam-control"
        routerLinkActive="on"
        [routerLinkActiveOptions]="{ exact: true }"
        [attr.aria-current]="active === 'exam' ? 'page' : null">
        Examen
      </a>
      <a
        class="tab-link"
        routerLink="/school/practical-fleet"
        routerLinkActive="on"
        [routerLinkActiveOptions]="{ exact: true }"
        [attr.aria-current]="active === 'fleet' ? 'page' : null">
        Manejo
      </a>
    </nav>
  `,
  styles: `
    :host {
      display: block;
      margin: 0 0 0.85rem;
    }
    .day-boards-nav .tab-link {
      border-color: transparent;
    }
    .day-boards-nav .tab-link.on,
    .day-boards-nav .tab-link.router-link-active {
      border-color: color-mix(in srgb, var(--color-primary) 35%, var(--color-border));
      background: var(--color-primary-soft);
      color: var(--color-primary);
    }
  `
})
export class SchoolDayBoardsNavComponent {
  @Input({ required: true }) active!: SchoolDayBoard;
}
