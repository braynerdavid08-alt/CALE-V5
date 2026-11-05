import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { env } from '../../core/config/env';
import { mapApiError } from '../../core/http/map-api-error';
import { SeoService } from '../../core/seo/seo.service';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import { PublicHomeApi } from './public-home.api';
import { CommercialSchoolDetailDto } from './public.models';

@Component({
  selector: 'app-public-school-detail-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiErrorComponent, UiLoadingComponent],
  template: `
    <div class="page">
      <p class="back">
        <a routerLink="/escuelas">← Directorio CEA</a>
      </p>

      @if (loading()) {
        <ui-loading />
      } @else if (error()) {
        <section class="missing">
          <ui-error [message]="error()" />
          <ui-button routerLink="/escuelas" type="button" variant="secondary">Volver al directorio</ui-button>
        </section>
      } @else if (school()) {
        <header class="hero">
          @if (coverSrc(); as cover) {
            <div class="cover-wrap">
              <img class="cover contain" [src]="cover" [alt]="'Portada de ' + school()!.name" />
            </div>
          } @else {
            @if (logoSrc(); as logo) {
              <div class="cover-wrap logo-banner">
                <img class="cover contain" [src]="logo" [alt]="'Logo de ' + school()!.name" />
              </div>
            }
          }
          <div class="hero-inner">
            @if (logoSrc(); as logo) {
              <img class="logo" [src]="logo" [alt]="'Logo de ' + school()!.name" width="96" height="96" />
            }
            <div class="hero-copy">
              <p class="eyebrow">CEA en Luz Verde</p>
              <h1>{{ school()!.name }}</h1>
              <p class="place">
                {{ school()!.city }}{{ school()!.department ? ', ' + school()!.department : '' }}
              </p>
              @if (school()!.licenseCategories) {
                <p class="cats">{{ school()!.licenseCategories }}</p>
              }
            </div>
          </div>
        </header>

        <div class="layout">
          <section class="main">
            <h2>Quiénes somos</h2>
            <p class="about">{{ school()!.about }}</p>

            @if (school()!.promotions.length) {
              <h2 class="spaced">Promociones</h2>
              <ul class="feed">
                @for (promo of school()!.promotions; track promo.id) {
                  <li>
                    <strong>{{ promo.title }}</strong>
                    <p>{{ promo.body }}</p>
                  </li>
                }
              </ul>
            }

            @if (school()!.posts.length) {
              <h2 class="spaced">Novedades</h2>
              <ul class="feed">
                @for (post of school()!.posts; track post.id) {
                  <li><p>{{ post.body }}</p></li>
                }
              </ul>
            }
          </section>

          <aside class="side">
            <h2>Contacto</h2>
            @if (school()!.address) {
              <p class="row"><span>Dirección</span>{{ school()!.address }}</p>
            }
            @if (school()!.phone) {
              <p class="row">
                <span>Teléfono</span>
                <a [href]="'tel:' + school()!.phone" (click)="track('PhoneClick')">{{ school()!.phone }}</a>
              </p>
            }
            @if (school()!.publicEmail) {
              <p class="row">
                <span>Correo</span>
                <a [href]="'mailto:' + school()!.publicEmail">{{ school()!.publicEmail }}</a>
              </p>
            }
            <div class="actions">
              @if (school()!.whatsAppUrl) {
                <a class="cta primary" [href]="school()!.whatsAppUrl" target="_blank" rel="noopener noreferrer" (click)="track('WhatsAppClick')">WhatsApp</a>
              }
              @if (school()!.phone) {
                <a class="cta secondary" [href]="'tel:' + school()!.phone" (click)="track('PhoneClick')">Llamar</a>
              }
              @if (school()!.mapsUrl) {
                <a class="cta secondary" [href]="school()!.mapsUrl" target="_blank" rel="noopener noreferrer" (click)="track('MapsClick')">Cómo llegar</a>
              }
            </div>
          </aside>
        </div>
      }
    </div>
  `,
  styles: [`
    .page {
      width: min(1120px, calc(100% - 2rem));
      margin: 0 auto;
      padding: 1.75rem 0 3.5rem;
    }
    .back { margin: 0 0 1.1rem; font-size: var(--text-sm); }
    .back a { color: var(--color-text-secondary); text-decoration: none; font-weight: 600; }
    .back a:hover { color: var(--color-primary); }
    .missing { display: grid; gap: 1rem; max-width: 28rem; }
    .hero {
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      overflow: hidden;
      background: var(--color-surface);
      margin-bottom: 1.5rem;
      box-shadow: var(--shadow-sm);
    }
    .cover-wrap {
      min-height: 11rem;
      max-height: 16rem;
      background: color-mix(in srgb, var(--color-surface-2, #111) 85%, #000);
      display: grid;
      place-items: center;
      overflow: hidden;
    }
    .cover-wrap.logo-banner {
      background: color-mix(in srgb, var(--color-surface-2, var(--color-border)) 55%, var(--color-surface));
      min-height: 9.5rem;
    }
    .cover {
      width: 100%;
      height: 100%;
      min-height: 11rem;
      max-height: 16rem;
      object-fit: contain;
      object-position: center;
      display: block;
      padding: 0.75rem 1rem;
      box-sizing: border-box;
    }
    .hero-inner {
      display: flex;
      gap: 1rem;
      align-items: center;
      padding: 1.15rem 1.25rem 1.35rem;
      background: var(--color-surface);
      position: relative;
      z-index: 1;
    }
    .logo {
      width: 5.5rem;
      height: 5.5rem;
      object-fit: contain;
      border-radius: var(--radius-md, 0.75rem);
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      padding: 0.35rem;
      flex-shrink: 0;
      box-shadow: var(--shadow-sm);
    }
    .eyebrow {
      margin: 0 0 0.3rem;
      color: var(--color-primary);
      font-size: var(--text-xs);
      font-weight: 800;
      letter-spacing: 0.08em;
      text-transform: uppercase;
    }
    h1 {
      margin: 0 0 0.35rem;
      font-family: var(--font-display, inherit);
      font-size: clamp(1.45rem, 2.4vw, 2rem);
      letter-spacing: -0.02em;
      line-height: 1.15;
    }
    .place { margin: 0; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .cats {
      margin: 0.55rem 0 0;
      font-size: var(--text-xs);
      font-weight: 800;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: var(--color-primary);
    }
    .layout {
      display: grid;
      grid-template-columns: minmax(0, 1.6fr) minmax(16rem, 0.9fr);
      gap: 1.25rem;
      align-items: start;
    }
    .main, .side {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 1.2rem 1.25rem 1.35rem;
      box-shadow: var(--shadow-sm);
    }
    h2 { margin: 0 0 0.65rem; font-size: var(--text-md); }
    h2.spaced { margin-top: 1.4rem; }
    .about, .feed p {
      margin: 0;
      color: var(--color-text-secondary);
      line-height: var(--leading-body);
      white-space: pre-wrap;
    }
    .feed { list-style: none; padding: 0; margin: 0; display: grid; gap: 0.75rem; }
    .feed li {
      border-top: 1px solid var(--color-border);
      padding-top: 0.75rem;
    }
    .row {
      margin: 0 0 0.75rem;
      display: grid;
      gap: 0.15rem;
      font-size: var(--text-sm);
    }
    .row span {
      color: var(--color-text-secondary);
      font-size: var(--text-xs);
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .row a { color: var(--color-primary); text-decoration: none; font-weight: 700; }
    .actions { display: flex; flex-wrap: wrap; gap: 0.55rem; margin-top: 0.35rem; }
    .cta {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-height: 2.5rem;
      padding: 0.45rem 0.95rem;
      border-radius: var(--radius-md, 0.7rem);
      font-size: var(--text-sm);
      font-weight: 800;
      text-decoration: none;
    }
    .cta.primary { background: var(--color-primary); color: var(--color-on-primary, #fff); }
    .cta.secondary { border: 1px solid var(--color-border); color: var(--color-text); background: transparent; }
    @media (max-width: 800px) {
      .layout { grid-template-columns: 1fr; }
      .cover-wrap, .cover {
        min-height: 8.5rem;
        max-height: 12rem;
      }
      .hero-inner { align-items: flex-start; }
      .logo { width: 4.5rem; height: 4.5rem; }
    }
  `]
})
export class PublicSchoolDetailPage implements OnInit {
  private readonly api = inject(PublicHomeApi);
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);
  private readonly apiBase = env.apiUrl.replace(/\/$/, '');

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly school = signal<CommercialSchoolDetailDto | null>(null);
  readonly coverSrc = signal<string | null>(null);
  readonly logoSrc = signal<string | null>(null);
  private slug = '';

  ngOnInit(): void {
    this.slug = this.route.snapshot.paramMap.get('slug')?.trim() ?? '';
    if (!this.slug) {
      this.error.set('Escuela no encontrada.');
      this.loading.set(false);
      this.seo.apply({
        title: 'Escuela no encontrada | Luz Verde',
        description: 'La ficha comercial solicitada no está disponible.',
        path: '/escuelas',
        index: false
      });
      return;
    }

    this.api.getCommercialSchool(this.slug).subscribe({
      next: (dto) => {
        this.school.set({
          ...dto,
          posts: dto.posts ?? [],
          promotions: dto.promotions ?? []
        });
        this.coverSrc.set(this.resolveMedia(dto.coverUrl));
        this.logoSrc.set(this.resolveMedia(dto.logoUrl));
        this.loading.set(false);
        this.seo.apply({
          title: `${dto.name} | CEA en Luz Verde`,
          description: dto.about.slice(0, 155) || `Ficha comercial de ${dto.name}.`,
          path: dto.detailPath,
          image: this.resolveMedia(dto.coverUrl || dto.logoUrl) ?? undefined,
          index: true
        });
        this.track('PageView');
      },
      error: (err) => {
        this.error.set(mapApiError(err) || 'Escuela no encontrada.');
        this.loading.set(false);
        this.seo.apply({
          title: 'Escuela no encontrada | Luz Verde',
          description: 'La ficha comercial solicitada no está disponible en el directorio.',
          path: `/escuelas/${this.slug}`,
          index: false
        });
      }
    });
  }

  track(eventType: string): void {
    if (!this.slug) return;
    this.api.recordCommercialEvent(this.slug, eventType).subscribe({ error: () => undefined });
  }

  private resolveMedia(path: string | null | undefined): string | null {
    if (!path) return null;
    if (/^https?:\/\//i.test(path)) return path;
    return `${this.apiBase}${path.startsWith('/') ? path : `/${path}`}`;
  }
}
