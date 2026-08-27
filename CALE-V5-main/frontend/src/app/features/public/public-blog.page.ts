import { Component, OnInit, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../core/http/map-api-error';
import { brandPageTitle } from '../../core/brand';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import { PublicHomeApi } from './public-home.api';

@Component({
  selector: 'app-public-blog-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiErrorComponent, UiLoadingComponent],
  template: `
    <div class="page">
      <header class="head">
        <p class="eyebrow">Blog</p>
        <h1>Novedades y formación vial</h1>
      </header>

      @if (loading()) {
        <ui-loading />
      } @else if (error()) {
        <ui-error [message]="error()" />
      } @else {
        <article class="panel">
          <p class="intro">{{ intro() }}</p>
          <p class="muted">
            Los artículos públicos se publicarán aquí. Mientras tanto puedes explorar
            escuelas e instructores, o crear tu cuenta para acceder a cursos.
          </p>
          <div class="actions">
            <a routerLink="/escuelas">
              <ui-button type="button" variant="secondary">Ver escuelas</ui-button>
            </a>
            <a routerLink="/register">
              <ui-button type="button">Registrarme</ui-button>
            </a>
          </div>
        </article>
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
    h1 { margin: 0 0 1.5rem; }
    .panel {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 1.5rem;
      box-shadow: var(--shadow-sm);
      display: grid;
      gap: 0.85rem;
    }
    .intro {
      margin: 0;
      font-size: var(--text-md);
      line-height: var(--leading-body);
    }
    .muted {
      margin: 0;
      color: var(--color-text-secondary);
      line-height: var(--leading-body);
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.65rem;
      margin-top: 0.35rem;
    }
    .actions a { text-decoration: none; }
  `]
})
export class PublicBlogPage implements OnInit {
  private readonly api = inject(PublicHomeApi);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly intro = signal('');

  ngOnInit(): void {
    this.title.setTitle(brandPageTitle('Blog'));
    this.meta.updateTag({
      name: 'description',
      content: 'Artículos y novedades de formación vial en CALE.'
    });

    this.api.getHome().subscribe({
      next: (data) => {
        this.intro.set(
          data.blogIntro?.trim()
          || 'Pronto publicaremos artículos sobre formación vial.'
        );
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }
}
