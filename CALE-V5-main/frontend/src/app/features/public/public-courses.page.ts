import { Component, OnInit, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../core/http/map-api-error';
import { brandPageTitle } from '../../core/brand';
import { resolveMediaUrl } from '../../core/media/resolve-media-url';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import { PublicHomeApi } from './public-home.api';
import { PublicCourseDto } from './public.models';

@Component({
  selector: 'app-public-courses-page',
  standalone: true,
  imports: [
    RouterLink,
    UiButtonComponent,
    UiErrorComponent,
    UiLoadingComponent
  ],
  template: `
    <div class="page">
      <header class="head">
        <p class="eyebrow">Cursos</p>
        <h1>Formación vial con Luz Verde</h1>
        <p class="lead">
          Estos son los cursos que tienes al crear tu cuenta gratis: de las normas y señales
          hasta lo específico de moto, carro y servicio público.
        </p>
        <div class="actions">
          <ui-button routerLink="/register" type="button">Crear cuenta gratis</ui-button>
          <ui-button routerLink="/login" type="button" variant="secondary">Iniciar sesión</ui-button>
        </div>
      </header>

      @if (loading()) {
        <ui-loading />
      } @else if (error()) {
        <ui-error [message]="error()" />
      } @else if (!courses().length) {
        <p class="muted">Pronto publicaremos el catálogo de cursos.</p>
      } @else {
        <div class="grid">
          @for (c of courses(); track c.id) {
            <article class="card">
              <div class="cover">
                @if (c.coverUrl) {
                  <img [src]="url(c.coverUrl)" alt="" loading="lazy" />
                } @else {
                  <span aria-hidden="true">📘</span>
                }
              </div>
              <div class="body">
                <span class="tag">{{ c.category }}</span>
                <h2>{{ c.title }}</h2>
                @if (c.description) { <p class="muted desc">{{ c.description }}</p> }
                <p class="meta">
                  {{ c.lessons.length }} {{ c.lessons.length === 1 ? 'tema' : 'temas' }}
                  @if (c.estimatedMinutes) { · {{ hours(c.estimatedMinutes) }} }
                </p>
                @if (c.lessons.length) {
                  <details>
                    <summary>Ver temas</summary>
                    <ol>
                      @for (l of c.lessons; track $index) { <li>{{ l }}</li> }
                    </ol>
                  </details>
                }
              </div>
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
      margin: 0 0 1rem;
      max-width: 44rem;
      color: var(--color-text-secondary);
      line-height: var(--leading-body);
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.65rem;
      margin-bottom: 1.75rem;
    }
    .actions a { text-decoration: none; }
    .grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 1rem;
      align-items: start;
    }
    .card {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-sm);
      overflow: hidden;
    }
    .cover {
      height: 9rem;
      display: grid;
      place-items: center;
      background: var(--color-primary-soft);
      font-size: 2.5rem;
    }
    .cover img {
      width: 100%;
      height: 100%;
      object-fit: contain;
      padding: 0.75rem;
    }
    .body { padding: 1rem 1.1rem 1.15rem; display: grid; gap: 0.45rem; }
    .tag {
      justify-self: start;
      padding: 0.15rem 0.55rem;
      border-radius: 999px;
      background: var(--color-primary-soft);
      color: var(--color-primary);
      font-size: var(--text-xs);
      font-weight: 700;
    }
    h2 { margin: 0; font-size: var(--text-md); }
    .muted { margin: 0; color: var(--color-text-secondary); line-height: var(--leading-body); }
    .desc { font-size: var(--text-sm); }
    .meta { margin: 0; font-size: var(--text-sm); font-weight: 700; }
    details summary {
      cursor: pointer;
      color: var(--color-primary);
      font-weight: 700;
      font-size: var(--text-sm);
    }
    ol {
      margin: 0.5rem 0 0;
      padding-left: 1.2rem;
      display: grid;
      gap: 0.2rem;
      font-size: var(--text-sm);
      color: var(--color-text-secondary);
    }
    @media (max-width: 900px) {
      .grid { grid-template-columns: 1fr 1fr; }
    }
    @media (max-width: 600px) {
      .grid { grid-template-columns: 1fr; }
    }
  `]
})
export class PublicCoursesPage implements OnInit {
  private readonly api = inject(PublicHomeApi);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly courses = signal<PublicCourseDto[]>([]);

  ngOnInit(): void {
    this.title.setTitle(brandPageTitle('Cursos'));
    this.meta.updateTag({
      name: 'description',
      content: 'Cursos de formación vial de Luz Verde: señales, normas, vehículo seguro, primeros auxilios y categorías A2, B1 y C1.'
    });

    this.api.listCourses().subscribe({
      next: (rows) => {
        this.courses.set(rows);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }

  url(path: string | null): string {
    return resolveMediaUrl(path);
  }

  hours(minutes: number): string {
    if (minutes < 60) return `${minutes} min`;
    const h = Math.round(minutes / 6) / 10;
    return `${String(h).replace('.', ',')} h aprox.`;
  }
}
