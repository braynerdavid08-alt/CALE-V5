import { Component, OnInit, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { BRAND, brandPageTitle } from '../../core/brand';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
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
        <h1>Quiénes somos</h1>
        <p class="lead">Conoce {{ brand.name }} y su misión en la formación vial.</p>
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
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly sanitizer = inject(DomSanitizer);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly aboutHtml = signal<SafeHtml>('');

  ngOnInit(): void {
    this.title.setTitle(brandPageTitle('Nosotros'));
    this.meta.updateTag({
      name: 'description',
      content: `${BRAND.name}: ${BRAND.slogan}. Formación vial con tu CEA.`
    });

    this.api.getHome().subscribe({
      next: (data) => {
        const html = data.aboutHtml?.trim()
          || `<p><strong>${BRAND.name}</strong> — ${BRAND.slogan}.</p>`;
        this.aboutHtml.set(this.sanitizer.bypassSecurityTrustHtml(html));
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }
}
