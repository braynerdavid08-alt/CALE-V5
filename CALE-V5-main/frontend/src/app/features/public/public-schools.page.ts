import { Component, OnInit, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../core/http/map-api-error';
import { brandPageTitle } from '../../core/brand';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import { PublicHomeApi } from './public-home.api';
import { PublicSchoolCardDto } from './public.models';

@Component({
  selector: 'app-public-schools-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiErrorComponent, UiLoadingComponent],
  template: `
    <div class="page">
      <header class="head">
        <p class="eyebrow">Escuelas</p>
        <h1>Escuelas de manejo aliadas</h1>
        <p class="lead">Escuelas activas del ecosistema CALE disponibles públicamente.</p>
      </header>

      @if (loading()) {
        <ui-loading />
      } @else if (error()) {
        <ui-error [message]="error()" />
      } @else if (!schools().length) {
        <p class="empty">Aún no hay escuelas publicadas.</p>
        <a routerLink="/register-school">
          <ui-button type="button">Registrar mi escuela</ui-button>
        </a>
      } @else {
        <div class="grid">
          @for (s of schools(); track s.id) {
            <article class="card">
              <h2>{{ s.name }}</h2>
              <p class="muted">
                {{ s.city }}{{ s.department ? ', ' + s.department : '' }}
              </p>
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
    h2 { margin: 0 0 0.35rem; font-size: var(--text-md); }
    .muted { margin: 0; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .meta {
      margin: 0.6rem 0 0;
      font-size: var(--text-xs);
      font-weight: 700;
      color: var(--color-primary);
    }
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
export class PublicSchoolsPage implements OnInit {
  private readonly api = inject(PublicHomeApi);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly schools = signal<PublicSchoolCardDto[]>([]);

  ngOnInit(): void {
    this.title.setTitle(brandPageTitle('Escuelas'));
    this.meta.updateTag({
      name: 'description',
      content: 'Escuelas de manejo aliadas en la plataforma CALE.'
    });

    this.api.listSchools().subscribe({
      next: (rows) => {
        this.schools.set(rows);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }
}
