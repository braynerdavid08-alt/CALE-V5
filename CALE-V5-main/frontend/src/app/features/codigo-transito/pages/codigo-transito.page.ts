import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  CodigoArticle,
  CodigoArticleSummary,
  CodigoBlock,
  CodigoMeta,
  CodigoTransitoApi
} from '../codigo-transito.api';

@Component({
  selector: 'app-codigo-transito-page',
  standalone: true,
  imports: [FormsModule, UiButtonComponent, UiErrorComponent, UiPageHeaderComponent],
  template: `
    <ui-page-header
      title="Biblioteca Jurídica"
      subtitle="Código Nacional de Tránsito (Ley 769 de 2002) — texto literal según Transiteca." />

    <ui-error [message]="error()" />

    @if (!selected()) {
      <section class="panel">
        <div class="toolbar">
          <label class="field grow">
            Buscar artículo
            <input
              class="input"
              [(ngModel)]="query"
              (ngModelChange)="onSearch($event)"
              placeholder="Número, nombre o texto…" />
          </label>
          <p class="meta">{{ articles().length }} artículos</p>
        </div>
        @if (meta()?.attribution) {
          <p class="attr">{{ meta()?.attribution }}</p>
        }
        <ul class="list">
          @for (a of articles(); track a.number) {
            <li>
              <button type="button" class="row" (click)="open(a.number)">
                <span class="num">Art. {{ a.number }}</span>
                <span class="name">{{ a.name || '(sin nombre)' }}</span>
                @if (a.notes?.length) {
                  <span class="tags">
                    @for (n of a.notes; track n) {
                      <em>{{ n }}</em>
                    }
                  </span>
                }
                <span class="preview">{{ a.preview }}</span>
              </button>
            </li>
          }
        </ul>
      </section>
    } @else {
      @if (article(); as art) {
        <section class="panel reader">
          <div class="nav">
            <ui-button type="button" variant="ghost" (click)="back()">← Lista</ui-button>
            <div class="pager">
              @if (prev()) {
                <ui-button type="button" variant="secondary" (click)="open(prev()!.number)">← Art. {{ prev()!.number }}</ui-button>
              }
              @if (next()) {
                <ui-button type="button" variant="secondary" (click)="open(next()!.number)">Art. {{ next()!.number }} →</ui-button>
              }
            </div>
          </div>
          <header class="head">
            <p class="eyebrow">Artículo {{ art.number }}</p>
            <h2>{{ art.name || ('Artículo ' + art.number) }}</h2>
            @if (art.sourceUrl) {
              <a class="source" [href]="art.sourceUrl" target="_blank" rel="noopener">Ver en Transiteca</a>
            }
          </header>
          <article class="body">
            @for (b of art.blocks || []; track $index) {
              @if (b.type === 'note') {
                <p class="note" [attr.data-variant]="b.variant || ''">{{ blockText(b) }}</p>
              } @else {
                <p class="para">{{ blockText(b) }}</p>
              }
            }
            @if (!art.blocks.length) {
              <pre class="plain">{{ art.plainText }}</pre>
            }
          </article>
        </section>
      }
    }
  `,
  styles: [`
    .panel {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: 16px;
      padding: 1rem 1.1rem;
      display: grid;
      gap: 0.85rem;
    }
    .toolbar { display: flex; flex-wrap: wrap; gap: 0.75rem; align-items: end; }
    .field { display: grid; gap: 0.35rem; font-weight: 700; }
    .field.grow { flex: 1; min-width: 14rem; }
    .input { width: 100%; min-height: 2.6rem; }
    .meta { margin: 0; color: var(--color-text-secondary); font-weight: 700; }
    .attr { margin: 0; font-size: 0.85rem; color: var(--color-text-secondary); }
    .list { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.45rem; }
    .row {
      width: 100%;
      text-align: left;
      border: 1px solid var(--color-border);
      background: color-mix(in srgb, var(--color-surface) 92%, transparent);
      border-radius: 12px;
      padding: 0.75rem 0.9rem;
      display: grid;
      gap: 0.25rem;
      cursor: pointer;
      font: inherit;
      color: inherit;
    }
    .row:hover { border-color: var(--color-primary); }
    .num { font-weight: 900; color: var(--color-primary); }
    .name { font-weight: 800; }
    .preview { color: var(--color-text-secondary); font-size: 0.88rem; }
    .tags { display: flex; flex-wrap: wrap; gap: 0.35rem; }
    .tags em {
      font-style: normal;
      font-size: 0.75rem;
      font-weight: 800;
      padding: 0.1rem 0.45rem;
      border-radius: 999px;
      background: color-mix(in srgb, var(--color-primary) 14%, transparent);
      color: var(--color-primary);
      text-transform: uppercase;
    }
    .nav { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 0.5rem; }
    .pager { display: flex; flex-wrap: wrap; gap: 0.4rem; }
    .eyebrow { margin: 0; letter-spacing: 0.08em; text-transform: uppercase; font-weight: 800; color: var(--color-primary); font-size: 0.8rem; }
    .head h2 { margin: 0.2rem 0 0.35rem; line-height: 1.2; }
    .source { font-weight: 700; color: var(--color-primary); }
    .body { display: grid; gap: 0.85rem; }
    .note {
      margin: 0;
      padding: 0.75rem 0.9rem;
      border-radius: 10px;
      border-left: 4px solid #d97706;
      background: color-mix(in srgb, #d97706 12%, transparent);
      font-weight: 700;
    }
    .note[data-variant='adicionado'] { border-left-color: #059669; background: color-mix(in srgb, #059669 12%, transparent); }
    .note[data-variant='derogado'] { border-left-color: #dc2626; background: color-mix(in srgb, #dc2626 12%, transparent); }
    .para { margin: 0; line-height: 1.55; white-space: pre-wrap; }
    .plain { white-space: pre-wrap; font: inherit; }
  `]
})
export class CodigoTransitoPage implements OnInit {
  private readonly api = inject(CodigoTransitoApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly error = signal<string | null>(null);
  readonly meta = signal<CodigoMeta | null>(null);
  readonly articles = signal<CodigoArticleSummary[]>([]);
  readonly selected = signal<number | null>(null);
  readonly article = signal<CodigoArticle | null>(null);
  readonly prev = signal<{ number: number; name: string | null } | null>(null);
  readonly next = signal<{ number: number; name: string | null } | null>(null);

  query = '';
  private searchTimer: ReturnType<typeof setTimeout> | null = null;
  private basePath = '/student/normas-transito';

  ngOnInit(): void {
    const url = this.router.url;
    this.basePath = url.startsWith('/teacher')
      ? '/teacher/normas-transito'
      : '/student/normas-transito';

    this.reloadList();
    this.route.paramMap.subscribe((pm) => {
      const n = Number(pm.get('number'));
      if (Number.isFinite(n) && n > 0) {
        this.loadArticle(n);
      } else {
        this.selected.set(null);
        this.article.set(null);
      }
    });
  }

  onSearch(value: string): void {
    this.query = value;
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.reloadList(value), 280);
  }

  open(number: number): void {
    void this.router.navigate([this.basePath, number]);
  }

  back(): void {
    void this.router.navigate([this.basePath]);
  }

  blockText(b: CodigoBlock): string {
    const c = b.content;
    if (!c) return '';
    if (typeof c === 'string') return c;
    if (!Array.isArray(c)) return '';
    return c
      .map((x) => {
        if (!x) return '';
        if (typeof x === 'string') return x;
        if (typeof x === 'object' && x && 'text' in x && typeof (x as { text?: string }).text === 'string') {
          return (x as { text: string }).text;
        }
        return '';
      })
      .join('');
  }

  private reloadList(q?: string): void {
    this.error.set(null);
    this.api.index(q).subscribe({
      next: (res) => {
        this.meta.set(res.meta);
        this.articles.set(res.articles || []);
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private loadArticle(number: number): void {
    this.error.set(null);
    this.selected.set(number);
    this.api.article(number).subscribe({
      next: (res) => {
        this.meta.set(res.meta);
        this.article.set(res.article);
        this.prev.set(res.prev);
        this.next.set(res.next);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.article.set(null);
      }
    });
  }
}
