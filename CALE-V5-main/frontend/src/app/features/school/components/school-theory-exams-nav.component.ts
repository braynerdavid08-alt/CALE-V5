import { Component, Input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

export type TheoryExamsTab = 'programming' | 'schedule';

/** Shared tabs for the theory-exam module (week programming ↔ recurring hours). */
@Component({
  selector: 'app-school-theory-exams-nav',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="school-tabs theory-exams-nav" aria-label="Exámenes teóricos">
      <a
        class="tab-link"
        routerLink="/school/theory-exams"
        routerLinkActive="on"
        [routerLinkActiveOptions]="{ exact: true }"
        [attr.aria-current]="active === 'programming' ? 'page' : null">
        Programación de exámenes
      </a>
      <a
        class="tab-link"
        routerLink="/school/theory-exams/schedule"
        routerLinkActive="on"
        [routerLinkActiveOptions]="{ exact: true }"
        [attr.aria-current]="active === 'schedule' ? 'page' : null">
        Horarios disponibles
      </a>
    </nav>
  `,
  styles: `
    :host {
      display: block;
      margin: 0 0 0.85rem;
    }
    .theory-exams-nav .tab-link {
      border-color: transparent;
    }
    .theory-exams-nav .tab-link.on,
    .theory-exams-nav .tab-link.router-link-active {
      border-color: color-mix(in srgb, var(--color-primary) 35%, var(--color-border));
      background: var(--color-primary-soft);
      color: var(--color-primary);
    }
  `
})
export class SchoolTheoryExamsNavComponent {
  @Input({ required: true }) active!: TheoryExamsTab;
}
