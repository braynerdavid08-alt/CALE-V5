import { Component, OnInit, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../core/http/map-api-error';
import { BRAND, brandPageTitle } from '../../core/brand';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import { PublicHomeApi } from './public-home.api';
import { PublicInstructorCardDto } from './public.models';

@Component({
  selector: 'app-public-instructors-page',
  standalone: true,
  imports: [
    RouterLink,
    UiButtonComponent,
    UiErrorComponent,
    UiIconComponent,
    UiLoadingComponent
  ],
  template: `
    <div class="page">
      <header class="head">
        <p class="eyebrow">Instructores</p>
        <h1>Instructores del ecosistema</h1>
        <p class="lead">Formadores activos disponibles en el directorio público de CALE.</p>
      </header>

      @if (loading()) {
        <ui-loading />
      } @else if (error()) {
        <ui-error [message]="error()" />
      } @else if (!instructors().length) {
        <p class="empty">Aún no hay instructores publicados.</p>
        <a routerLink="/register-teacher">
          <ui-button type="button">Registrarme como instructor</ui-button>
        </a>
      } @else {
        <div class="grid">
          @for (ins of instructors(); track ins.id) {
            <article class="card">
              <div class="avatar" aria-hidden="true">
                <ui-icon name="instructor" />
              </div>
              <h2>{{ ins.displayName }}</h2>
              <p class="muted">{{ ins.schoolName || ('Instructor ' + brand.name) }}</p>
            </article>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .page {
      width: min(1120px, calc(100% - 2rem));
      margin: 0 auto;
      padding: 2.5rem 0 3.5rem;
    }
    .eyebrow {
      margin: 0 0 0.35rem;
      color: var(--color-primary);
      font-size: var(--text-xs);
      font-weight: 800;
      letter-spacing: 0.08em;
      text-transform: uppercase;
    }
    h1 { margin: 0 0 0.5rem; }
    .lead {
      margin: 0 0 1.75rem;
      color: var(--color-text-secondary);
      max-width: 40rem;
      line-height: var(--leading-body);
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 1rem;
    }
    .card {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 1.2rem;
      box-shadow: var(--shadow-sm);
    }
    .avatar {
      width: 2.5rem;
      height: 2.5rem;
      border-radius: 50%;
      background: var(--color-primary-soft);
      color: var(--color-primary);
      display: grid;
      place-items: center;
      margin-bottom: 0.65rem;
    }
    h2 { margin: 0 0 0.35rem; font-size: var(--text-md); }
    .muted { margin: 0; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .empty { color: var(--color-text-secondary); margin: 0 0 1rem; }
    a { text-decoration: none; }
    @media (max-width: 900px) {
      .grid { grid-template-columns: 1fr 1fr; }
    }
    @media (max-width: 600px) {
      .grid { grid-template-columns: 1fr; }
    }
  `]
})
export class PublicInstructorsPage implements OnInit {
  readonly brand = BRAND;
  private readonly api = inject(PublicHomeApi);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly instructors = signal<PublicInstructorCardDto[]>([]);

  ngOnInit(): void {
    this.title.setTitle(brandPageTitle('Instructores'));
    this.meta.updateTag({
      name: 'description',
      content: 'Instructores de formación vial en la plataforma CALE.'
    });

    this.api.listInstructors().subscribe({
      next: (rows) => {
        this.instructors.set(rows);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }
}
