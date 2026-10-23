import { Component, OnInit, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { mapApiError } from '../../core/http/map-api-error';
import { brandPageTitle } from '../../core/brand';
import { env } from '../../core/config/env';
import { SessionStore } from '../../core/auth/session.store';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import { PublicHomeApi } from './public-home.api';
import { PublicCourseCardDto } from './public.models';

@Component({
  selector: 'app-public-courses-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiErrorComponent, UiLoadingComponent],
  template: `
    <div class="page">
      <header class="head">
        <p class="eyebrow">Cursos</p>
        <h1>Formación vial con Luz Verde</h1>
        <p class="lead">
          Esta es la malla actual: señales, normas, vía, vehículo y categorías A2, B1 y C1.
          Para estudiar las lecciones inicia sesión o crea tu cuenta.
        </p>
      </header>

      @if (loading()) {
        <ui-loading />
      } @else if (error()) {
        <ui-error [message]="error()" />
      } @else {
        <div class="grid">
          @for (c of courses(); track c.slug) {
            <article class="card" [attr.id]="c.slug">
              <div class="cover">
                <img [src]="coverSrc(c.coverUrl)" [alt]="c.title" width="320" height="180" loading="lazy" />
              </div>
              <p class="cat">{{ c.category }}</p>
              <h2>{{ c.title }}</h2>
              <p class="desc">{{ c.description }}</p>
              <p class="meta">{{ c.lessonCount }} lecciones</p>
              <div class="actions">
                @if (loggedIn()) {
                  <ui-button [routerLink]="studyPath()" type="button">Estudiar</ui-button>
                } @else {
                  <ui-button routerLink="/register" type="button">Crear cuenta</ui-button>
                  <ui-button routerLink="/login" type="button" variant="secondary">Iniciar sesión</ui-button>
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
      margin: 0 0 1.75rem;
      color: var(--color-text-secondary);
      max-width: 42rem;
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
      overflow: hidden;
      box-shadow: var(--shadow-sm);
      display: grid;
      align-content: start;
      scroll-margin-top: 5.5rem;
    }
    .cover {
      aspect-ratio: 16 / 10;
      background: #0d1410;
      overflow: hidden;
    }
    .cover img {
      width: 100%;
      height: 100%;
      object-fit: cover;
      display: block;
    }
    .cat, .desc, .meta, h2 { margin: 0; padding-left: 1rem; padding-right: 1rem; }
    .cat {
      margin-top: 0.9rem;
      color: var(--color-primary);
      font-size: var(--text-xs);
      font-weight: 800;
      letter-spacing: 0.04em;
      text-transform: uppercase;
    }
    h2 {
      margin-top: 0.35rem;
      font-size: var(--text-md);
      line-height: 1.3;
    }
    .desc {
      margin-top: 0.45rem;
      color: var(--color-text-secondary);
      font-size: var(--text-sm);
      line-height: 1.45;
      display: -webkit-box;
      -webkit-line-clamp: 3;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }
    .meta {
      margin-top: 0.55rem;
      color: var(--color-text-secondary);
      font-size: var(--text-xs);
      font-weight: 600;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      padding: 0.9rem 1rem 1.1rem;
    }
    .actions a { text-decoration: none; }
    @media (max-width: 960px) {
      .grid { grid-template-columns: 1fr 1fr; }
    }
    @media (max-width: 640px) {
      .grid { grid-template-columns: 1fr; }
    }
  `]
})
export class PublicCoursesPage implements OnInit {
  private readonly api = inject(PublicHomeApi);
  private readonly route = inject(ActivatedRoute);
  private readonly session = inject(SessionStore);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly courses = signal<PublicCourseCardDto[]>([]);
  readonly loggedIn = this.session.isAuthenticated;

  ngOnInit(): void {
    this.title.setTitle(brandPageTitle('Cursos'));
    this.meta.updateTag({
      name: 'description',
      content: 'Malla de formación vial de Luz Verde: señales, normas, vía, vehículo y categorías A2, B1 y C1.'
    });

    this.api.listCourses().subscribe({
      next: (list) => {
        this.courses.set(list);
        this.loading.set(false);
        const frag = this.route.snapshot.fragment;
        if (frag) {
          setTimeout(() => document.getElementById(frag)?.scrollIntoView({ behavior: 'smooth', block: 'start' }), 50);
        }
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }

  coverSrc(url: string): string {
    if (!url) return '';
    if (url.startsWith('http')) return url;
    return `${env.apiUrl}${url.startsWith('/') ? url : '/' + url}`;
  }

  studyPath(): string {
    const role = this.session.user()?.role;
    if (role === 'Teacher') return '/teacher/cursos';
    if (role === 'Admin') return '/admin/cursos-virtuales';
    return '/student/cursos';
  }
}
