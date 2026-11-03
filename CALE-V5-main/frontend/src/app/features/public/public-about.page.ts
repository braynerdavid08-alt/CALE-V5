import { Component, OnInit, inject, signal } from '@angular/core';
import { BRAND } from '../../core/brand';
import { PAGE_SEO } from '../../core/seo/page-seo';
import { SeoService } from '../../core/seo/seo.service';
import { mapApiError } from '../../core/http/map-api-error';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import { PublicHomeApi } from './public-home.api';

@Component({
  selector: 'app-public-about-page',
  standalone: true,
  imports: [UiErrorComponent, UiLoadingComponent],
  template: `
    <div class="page">
      <header class="head">
        <p class="eyebrow">Nosotros</p>
        <h1>Plataforma tecnológica para CEA</h1>
        <p class="lead">
          {{ brand.name }} construye software para Centros de Enseñanza Automovilística:
          {{ brand.slogan }} No somos una escuela física; impulsamos la operación digital de las escuelas.
        </p>
      </header>

      @if (loading()) {
        <ui-loading />
      } @else if (error()) {
        <ui-error [message]="error()" />
      } @else {
        <article class="body" [innerHTML]="aboutHtml()"></article>
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
    .body {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 1.5rem 1.6rem;
      line-height: var(--leading-body);
      box-shadow: var(--shadow-sm);
    }
  `]
})
export class PublicAboutPage implements OnInit {
  readonly brand = BRAND;
  private readonly api = inject(PublicHomeApi);
  private readonly seo = inject(SeoService);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly aboutHtml = signal('');

  ngOnInit(): void {
    this.seo.apply({
      title: PAGE_SEO.about.title,
      description: PAGE_SEO.about.description,
      path: '/nosotros',
      index: true
    });

    this.api.getHome().subscribe({
      next: (data) => {
        const html = data.aboutHtml?.trim()
          || `<p><strong>${BRAND.name}</strong> es una plataforma digital para CEA y escuelas de conducción en Colombia. ${BRAND.slogan}</p><p>Ayudamos a administrar estudiantes e instructores, programar clases y exámenes, y formar conductores con herramientas digitales — sin reemplazar la habilitación presencial de cada escuela.</p>`;
        this.aboutHtml.set(html);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }
}
