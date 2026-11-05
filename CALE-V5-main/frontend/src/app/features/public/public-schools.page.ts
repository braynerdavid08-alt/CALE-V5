import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { env } from '../../core/config/env';
import { mapApiError } from '../../core/http/map-api-error';
import { PAGE_SEO } from '../../core/seo/page-seo';
import { SeoService } from '../../core/seo/seo.service';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import { PublicHomeApi } from './public-home.api';
import { CommercialSchoolCardDto } from './public.models';

@Component({
  selector: 'app-public-schools-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiErrorComponent, UiLoadingComponent],
  template: `
    <div class="page">
      <header class="head">
        <p class="eyebrow">Directorio comercial</p>
        <h1>Encuentra escuelas de conducción vinculadas a Luz Verde</h1>
        <p class="lead">
          Centros de enseñanza automovilística con membresía activa que eligieron
          publicarse. Solo aparecen escuelas elegibles y autorizadas.
        </p>
      </header>

      @if (loading()) {
        <ui-loading />
      } @else if (error()) {
        <ui-error [message]="error()" />
      } @else if (!schools().length) {
        <section class="empty-block" aria-live="polite">
          <p class="empty">
            Aún no hay escuelas publicadas en el directorio comercial.
          </p>
          <p class="empty-hint">
            Si representas un CEA con plan mensual o anual activo, podrás completar
            tu ficha y autorizar la publicación cuando el panel comercial esté disponible.
          </p>
          <ui-button routerLink="/register-school" type="button">Registrar mi escuela</ui-button>
        </section>
      } @else {
        <div class="grid">
          @for (s of schools(); track s.id) {
            <a class="card" [routerLink]="s.detailPath || '/escuelas'">
              <div
                class="media"
                [class.has-cover]="!!coverSrc(s)"
                [class.has-logo]="!coverSrc(s) && !!logoSrc(s)">
                @if (coverSrc(s); as cover) {
                  <img class="cover" [src]="cover" [alt]="'Portada de ' + s.name" />
                } @else {
                  @if (logoSrc(s); as logo) {
                    <img class="logo" [src]="logo" [alt]="'Logo de ' + s.name" />
                  }
                }
              </div>
              <div class="body">
                @if (coverSrc(s) && logoSrc(s); as logo) {
                  <img class="logo-badge" [src]="logo" [alt]="'Logo de ' + s.name" />
                }
                <h2>{{ s.name }}</h2>
                <p class="muted">
                  {{ s.city }}{{ s.department ? ', ' + s.department : '' }}
                </p>
                @if (s.licenseCategories) {
                  <p class="cats">{{ s.licenseCategories }}</p>
                }
                @if (s.about) {
                  <p class="about">{{ s.about }}</p>
                }
                <p class="visit">Ver ficha</p>
              </div>
            </a>
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
    h1 {
      margin: 0 0 0.5rem;
      font-family: var(--font-display, inherit);
      letter-spacing: -0.02em;
    }
    .lead {
      margin: 0 0 1.75rem;
      color: var(--color-text-secondary);
      max-width: 42rem;
      line-height: var(--leading-body);
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 1.1rem;
    }
    .card {
      display: flex;
      flex-direction: column;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      overflow: hidden;
      box-shadow: var(--shadow-sm);
      color: inherit;
      text-decoration: none;
      transition: transform 180ms ease, box-shadow 180ms ease;
    }
    .card:hover {
      transform: translateY(-2px);
      box-shadow: var(--shadow-md, var(--shadow-sm));
    }
    .visit {
      margin: 0.85rem 0 0;
      font-size: var(--text-xs);
      font-weight: 800;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: var(--color-primary);
    }
    .media {
      height: 10.5rem;
      background: color-mix(in srgb, var(--color-surface-2, var(--color-border)) 70%, var(--color-surface));
      display: grid;
      place-items: center;
      overflow: hidden;
      flex-shrink: 0;
    }
    .media:not(.has-cover):not(.has-logo) {
      background:
        linear-gradient(160deg, color-mix(in srgb, var(--color-primary) 18%, transparent), transparent 55%),
        var(--color-surface-2, var(--color-border));
    }
    .media .cover,
    .media .logo {
      width: 100%;
      height: 100%;
      object-fit: contain;
      object-position: center;
      display: block;
      padding: 0.65rem;
      box-sizing: border-box;
    }
    .media.has-cover .cover {
      /* Wide photo covers fill; logos/composites still readable via contain above.
         Prefer contain so uploaded logos are never cropped on cards. */
      padding: 0.35rem;
    }
    .body {
      position: relative;
      padding: 1.05rem 1.15rem 1.2rem;
      display: grid;
      gap: 0;
    }
    .logo-badge {
      width: 3.25rem;
      height: 3.25rem;
      object-fit: contain;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md, 0.7rem);
      padding: 0.2rem;
      margin: 0 0 0.65rem;
      display: block;
    }
    h2 {
      margin: 0 0 0.3rem;
      font-size: var(--text-md);
      line-height: 1.25;
    }
    .muted {
      margin: 0;
      color: var(--color-text-secondary);
      font-size: var(--text-sm);
    }
    .cats {
      margin: 0.55rem 0 0;
      font-size: var(--text-xs);
      font-weight: 800;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: var(--color-primary);
    }
    .about {
      margin: 0.55rem 0 0;
      color: var(--color-text-secondary);
      font-size: var(--text-sm);
      line-height: var(--leading-body);
      display: -webkit-box;
      -webkit-line-clamp: 3;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }
    .empty-block { max-width: 36rem; }
    .empty { color: var(--color-text-secondary); margin: 0 0 0.65rem; }
    .empty-hint {
      margin: 0 0 1.15rem;
      color: var(--color-text-secondary);
      font-size: var(--text-sm);
      line-height: var(--leading-body);
    }
    a { text-decoration: none; }
    @media (max-width: 900px) {
      .grid { grid-template-columns: 1fr 1fr; }
    }
    @media (max-width: 600px) {
      .grid { grid-template-columns: 1fr; }
      .page { padding-top: 1.75rem; }
    }
  `]
})
export class PublicSchoolsPage implements OnInit {
  private readonly api = inject(PublicHomeApi);
  private readonly seo = inject(SeoService);
  private readonly apiBase = env.apiUrl.replace(/\/$/, '');

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly schools = signal<CommercialSchoolCardDto[]>([]);

  ngOnInit(): void {
    this.seo.apply({
      title: PAGE_SEO.schools.title,
      description: PAGE_SEO.schools.description,
      path: '/escuelas',
      index: true
    });

    this.api.listCommercialSchools().subscribe({
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

  coverSrc(school: CommercialSchoolCardDto): string | null {
    return this.resolveMedia(school.coverUrl);
  }

  logoSrc(school: CommercialSchoolCardDto): string | null {
    return this.resolveMedia(school.logoUrl);
  }

  private resolveMedia(path: string | null | undefined): string | null {
    if (!path) return null;
    if (/^https?:\/\//i.test(path)) return path;
    return `${this.apiBase}${path.startsWith('/') ? path : `/${path}`}`;
  }
}
