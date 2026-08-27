import { Component, OnInit, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../core/http/map-api-error';
import { brandPageTitle } from '../../core/brand';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import { PublicHomeApi } from './public-home.api';

@Component({
  selector: 'app-public-courses-page',
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
        <p class="eyebrow">Cursos</p>
        <h1>Formación vial con Mi CALE</h1>
        <p class="lead">
          El catálogo completo de cursos, simuladores y evaluaciones está disponible
          después de iniciar sesión.
        </p>
      </header>

      @if (loading()) {
        <ui-loading />
      } @else if (error()) {
        <ui-error [message]="error()" />
      } @else {
        <div class="panel">
          <div class="icon" aria-hidden="true"><ui-icon name="book" /></div>
          <h2>Accede al catálogo con tu cuenta</h2>
          <p>
            Al registrarte podrás unirte a una escuela, practicar con el simulador
            y presentar evaluaciones teóricas. No publicamos un catálogo abierto
            sin autenticación.
          </p>
          <div class="actions">
            <a routerLink="/register">
              <ui-button type="button">Crear cuenta</ui-button>
            </a>
            <a routerLink="/login">
              <ui-button type="button" variant="secondary">Iniciar sesión</ui-button>
            </a>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .page {
      width: min(800px, calc(100% - 2rem));
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
      line-height: var(--leading-body);
    }
    .panel {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 1.75rem;
      box-shadow: var(--shadow-sm);
      display: grid;
      gap: 0.75rem;
    }
    .icon {
      width: 2.75rem;
      height: 2.75rem;
      border-radius: 12px;
      background: var(--color-primary-soft);
      color: var(--color-primary);
      display: grid;
      place-items: center;
    }
    h2 { margin: 0; font-size: var(--text-lg); }
    p { margin: 0; color: var(--color-text-secondary); line-height: var(--leading-body); }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.65rem;
      margin-top: 0.5rem;
    }
    .actions a { text-decoration: none; }
  `]
})
export class PublicCoursesPage implements OnInit {
  private readonly api = inject(PublicHomeApi);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.title.setTitle(brandPageTitle('Cursos'));
    this.meta.updateTag({
      name: 'description',
      content: 'Accede al catálogo de formación vial CALE tras iniciar sesión.'
    });

    this.api.getHome().subscribe({
      next: () => this.loading.set(false),
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }
}
