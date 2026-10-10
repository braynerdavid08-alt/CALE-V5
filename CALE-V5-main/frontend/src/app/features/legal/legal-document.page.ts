import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { mapApiError } from '../../core/http/map-api-error';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import { LegalApi, LegalDocument } from './legal.api';
import { LEGAL_LINKS } from './legal-links';
import { renderLegalMarkdown } from './simple-markdown';

@Component({
  selector: 'app-legal-document-page',
  standalone: true,
  imports: [DatePipe, RouterLink, UiErrorComponent, UiLoadingComponent],
  template: `
    <section class="legal">
      <nav class="legal-nav" aria-label="Documentos legales">
        @for (link of links; track link.key) {
          <a [routerLink]="['/legal', link.key]" [class.active]="link.key === key()">{{ link.label }}</a>
        }
      </nav>

      <ui-error [message]="error()" />
      @if (loading()) {
        <ui-loading />
      } @else if (doc()) {
        @if (doc()!.isDraft) {
          <p class="draft-banner" role="note">
            <strong>Borrador.</strong> Este documento todavía tiene datos pendientes y no ha tenido revisión jurídica.
            Las partes marcadas en amarillo se completarán antes de la versión final.
          </p>
        }
        <p class="meta">
          Versión {{ doc()!.version }}
          @if (doc()!.publishedAt) { · publicada el {{ doc()!.publishedAt | date: 'dd/MM/yyyy' }} }
        </p>
        <article class="legal-body" [innerHTML]="html()"></article>
      }
    </section>
  `,
  styles: [`
    .legal { max-width: 52rem; margin: 0 auto; padding: 1.5rem 1rem 3rem; }
    .legal-nav { display: flex; flex-wrap: wrap; gap: 0.4rem; margin-bottom: 1.25rem; }
    .legal-nav a {
      padding: 0.35rem 0.75rem;
      border: 1px solid var(--color-border);
      border-radius: 999px;
      font-size: 0.85rem;
      text-decoration: none;
      color: var(--color-text);
    }
    .legal-nav a.active { border-color: var(--color-primary); color: var(--color-primary); font-weight: 600; }
    .draft-banner {
      padding: 0.75rem 1rem;
      border-radius: var(--radius-md);
      background: color-mix(in srgb, #f5b400 18%, transparent);
      border: 1px solid #c99400;
      line-height: 1.45;
    }
    .meta { color: var(--color-text-secondary); font-size: 0.85rem; }
    .legal-body { line-height: 1.6; }
    .legal-body :is(h2, h3, h4) { margin: 1.5rem 0 0.5rem; line-height: 1.3; }
    .legal-body blockquote {
      margin: 1rem 0;
      padding: 0.6rem 0.9rem;
      border-left: 4px solid #c99400;
      background: var(--color-surface-2, transparent);
    }
    .legal-body mark { background: color-mix(in srgb, #f5b400 35%, transparent); color: inherit; padding: 0 0.15rem; }
    .legal-body .table-wrap { overflow-x: auto; }
    .legal-body table { border-collapse: collapse; width: 100%; font-size: 0.9rem; }
    .legal-body :is(th, td) { border: 1px solid var(--color-border); padding: 0.4rem 0.55rem; text-align: left; vertical-align: top; }
  `]
})
export class LegalDocumentPage {
  private readonly api = inject(LegalApi);
  private readonly route = inject(ActivatedRoute);

  readonly links = LEGAL_LINKS;
  readonly key = signal('');
  readonly doc = signal<LegalDocument | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly html = computed(() => (this.doc() ? renderLegalMarkdown(this.doc()!.content) : ''));

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const key = params.get('doc') ?? 'terms';
      this.key.set(key);
      this.loading.set(true);
      this.error.set(null);
      this.api.document(key).subscribe({
        next: (doc) => {
          this.doc.set(doc);
          this.loading.set(false);
        },
        error: (err) => {
          this.doc.set(null);
          this.loading.set(false);
          this.error.set(mapApiError(err));
        }
      });
    });
  }
}
