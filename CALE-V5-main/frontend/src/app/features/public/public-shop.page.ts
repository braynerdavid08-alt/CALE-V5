import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { brandPageTitle } from '../../core/brand';
import { mapApiError } from '../../core/http/map-api-error';
import { resolveMediaUrl } from '../../core/media/resolve-media-url';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import { discountPercent, sortProducts } from './shop-commerce';
import { ShopAnalyticsService } from './shop-analytics.service';
import { ShopProductCardComponent } from './shop-product-card.component';
import { ShopApi } from './shop.api';
import { PublicShopDto, PublicShopProductDto, SHOP_CATEGORIES } from './shop.models';

@Component({
  selector: 'app-public-shop-page',
  standalone: true,
  imports: [
    RouterLink,
    UiButtonComponent,
    UiErrorComponent,
    UiLoadingComponent,
    ShopProductCardComponent
  ],
  template: `
    <div class="page">
      @if (loading()) {
        <ui-loading label="Cargando catálogo…" />
      } @else if (error()) {
        <ui-error [message]="error()" />
        <ui-button type="button" (click)="load()">Reintentar</ui-button>
      } @else if (shop()) {
        <header class="hero" [class.has-image]="!!shop()?.heroImageUrl">
          @if (shop()!.heroImageUrl) {
            <img class="hero-bg" [src]="media(shop()!.heroImageUrl)" alt="" />
            <div class="hero-scrim" aria-hidden="true"></div>
          }
          <div class="hero-copy">
            <p class="eyebrow">Tienda Luz Verde</p>
            <h1>Equípate para un camino más seguro</h1>
            <p class="lead">
              Encuentra cascos, guantes y accesorios para motociclistas con excelentes precios. Explora
              las ofertas y consulta por WhatsApp, sin compromiso.
            </p>
            <div class="hero-actions">
              <ui-button type="button" (click)="scrollTo('ofertas')">Ver ofertas</ui-button>
              <a routerLink="/tienda/catalogo" class="ghost">Explorar catálogo</a>
            </div>
            <p class="hero-note">Consulta disponibilidad por WhatsApp</p>
          </div>
        </header>

        <section class="cats" aria-label="Categorías">
          @for (c of categoryChips(); track c.name) {
            <a
              class="chip"
              [class.disabled]="c.count === 0"
              [routerLink]="c.count ? '/tienda/catalogo' : null"
              [queryParams]="c.count ? { categoria: c.name } : null">
              {{ c.name }}
              <span class="n">{{ c.count }}</span>
            </a>
          }
        </section>

        @if (topDiscounts().length) {
          <section id="ofertas" class="block">
            <header class="block-head">
              <div>
                <p class="eyebrow">Mayores descuentos</p>
                <h2>Ofertas destacadas</h2>
              </div>
              <a routerLink="/tienda/catalogo" [queryParams]="{}" class="more">Ver catálogo</a>
            </header>
            <div class="grid">
              @for (p of topDiscounts(); track p.id) {
                <app-shop-product-card [product]="p" [shop]="shop()!" />
              }
            </div>
          </section>
        }

        @if (featured().length) {
          <section class="block">
            <header class="block-head">
              <div>
                <p class="eyebrow">Selección Luz Verde</p>
                <h2>Productos destacados</h2>
              </div>
            </header>
            <div class="grid">
              @for (p of featured(); track p.id) {
                <app-shop-product-card [product]="p" [shop]="shop()!" />
              }
            </div>
          </section>
        }

        @if (newest().length) {
          <section class="block">
            <header class="block-head">
              <div>
                <p class="eyebrow">Novedades</p>
                <h2>Nuevos productos</h2>
              </div>
            </header>
            <div class="grid">
              @for (p of newest(); track p.id) {
                <app-shop-product-card [product]="p" [shop]="shop()!" />
              }
            </div>
          </section>
        }

        @for (sec of typeSections(); track sec.title) {
          @if (sec.items.length) {
            <section class="block">
              <header class="block-head">
                <div>
                  <p class="eyebrow">{{ sec.eyebrow }}</p>
                  <h2>{{ sec.title }}</h2>
                </div>
                <a
                  routerLink="/tienda/catalogo"
                  [queryParams]="sec.query"
                  class="more"
                  >Ver más</a
                >
              </header>
              <div class="grid">
                @for (p of sec.items; track p.id) {
                  <app-shop-product-card [product]="p" [shop]="shop()!" />
                }
              </div>
            </section>
          }
        }

        <section class="cta-banner">
          <div>
            <h2>Explora el catálogo completo</h2>
            <p>Filtra por categoría, marca, precio y descuento. Consulta por WhatsApp cuando quieras.</p>
          </div>
          <ui-button routerLink="/tienda/catalogo" type="button">Ir al catálogo</ui-button>
        </section>
      }
    </div>
  `,
  styles: `
    .page {
      width: min(1120px, calc(100% - 2rem));
      margin: 0 auto;
      padding: 1.25rem 0 3.5rem;
    }
    .hero {
      position: relative;
      border-radius: var(--radius-xl, 1.25rem);
      overflow: hidden;
      margin-bottom: 1.25rem;
      background:
        radial-gradient(120% 80% at 10% 0%, color-mix(in srgb, var(--color-primary) 28%, transparent), transparent 55%),
        linear-gradient(160deg, #0d1712 0%, #15241c 55%, #1a2e22 100%);
      color: #f4faf6;
      min-height: 14rem;
      display: grid;
      align-items: end;
    }
    .hero.has-image {
      min-height: 18rem;
    }
    .hero-bg {
      position: absolute;
      inset: 0;
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
    .hero-scrim {
      position: absolute;
      inset: 0;
      background: linear-gradient(180deg, rgba(8, 16, 12, 0.2), rgba(8, 16, 12, 0.82));
    }
    .hero-copy {
      position: relative;
      z-index: 1;
      padding: 1.75rem 1.5rem 1.5rem;
      max-width: 40rem;
      display: grid;
      gap: 0.65rem;
    }
    .eyebrow {
      margin: 0;
      font-size: var(--text-xs);
      font-weight: 800;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: color-mix(in srgb, var(--color-primary) 70%, white);
    }
    h1 {
      margin: 0;
      font-size: clamp(1.65rem, 3.2vw, 2.35rem);
      line-height: 1.15;
    }
    .lead {
      margin: 0;
      color: rgba(244, 250, 246, 0.88);
      line-height: 1.45;
    }
    .hero-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.55rem;
    }
    .ghost {
      min-height: 2.6rem;
      padding: 0 1rem;
      border-radius: var(--radius-md);
      border: 1px solid rgba(255, 255, 255, 0.28);
      background: rgba(255, 255, 255, 0.06);
      color: #fff;
      font: inherit;
      font-weight: 700;
      display: inline-grid;
      place-items: center;
      text-decoration: none;
    }
    .hero-note {
      margin: 0;
      font-size: 0.8rem;
      color: rgba(244, 250, 246, 0.7);
    }
    .cats {
      display: flex;
      flex-wrap: wrap;
      gap: 0.45rem;
      margin-bottom: 1.5rem;
    }
    .chip {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      min-height: 2.35rem;
      padding: 0.3rem 0.75rem;
      border-radius: 999px;
      border: 1px solid var(--color-border);
      color: inherit;
      text-decoration: none;
      font-size: 0.85rem;
      font-weight: 700;
    }
    .chip.disabled {
      opacity: 0.4;
      pointer-events: none;
    }
    .chip .n {
      font-size: 0.72rem;
      color: var(--color-text-secondary);
    }
    .block {
      margin-bottom: 2rem;
    }
    .block-head {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: end;
      margin-bottom: 0.9rem;
    }
    .block-head h2 {
      margin: 0.15rem 0 0;
      font-size: clamp(1.25rem, 2.5vw, 1.55rem);
    }
    .block-head .eyebrow {
      color: var(--color-primary);
    }
    .more {
      font-weight: 700;
      color: var(--color-primary);
      text-decoration: none;
      white-space: nowrap;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 1.1rem;
    }
    .cta-banner {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      gap: 1rem;
      align-items: center;
      padding: 1.4rem 1.35rem;
      border-radius: var(--radius-xl);
      border: 1px solid color-mix(in srgb, var(--color-primary) 35%, var(--color-border));
      background:
        radial-gradient(80% 120% at 100% 0%, color-mix(in srgb, var(--color-primary) 18%, transparent), transparent),
        var(--color-surface);
    }
    .cta-banner h2 {
      margin: 0 0 0.3rem;
    }
    .cta-banner p {
      margin: 0;
      color: var(--color-text-secondary);
      max-width: 36rem;
    }
    @media (max-width: 900px) {
      .grid {
        grid-template-columns: repeat(2, 1fr);
      }
    }
    @media (max-width: 560px) {
      .grid {
        grid-template-columns: 1fr;
      }
    }
  `
})
export class PublicShopPage implements OnInit {
  private readonly api = inject(ShopApi);
  private readonly analytics = inject(ShopAnalyticsService);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly shop = signal<PublicShopDto | null>(null);

  readonly categoryChips = computed(() => {
    const products = this.shop()?.products ?? [];
    const counts = new Map<string, number>();
    for (const p of products) {
      const key = (p.category || 'Cascos').trim();
      counts.set(key, (counts.get(key) ?? 0) + 1);
    }
    return SHOP_CATEGORIES.map((name) => ({ name, count: counts.get(name) ?? 0 }));
  });

  readonly topDiscounts = computed(() =>
    sortProducts(this.shop()?.products ?? [], 'discount')
      .filter((p) => discountPercent(p) > 0)
      .slice(0, 8)
  );

  readonly featured = computed(() => {
    const featured = (this.shop()?.products ?? []).filter((p) => p.isFeatured);
    return featured.slice(0, 4);
  });

  readonly newest = computed(() => {
    const list = sortProducts(this.shop()?.products ?? [], 'newest');
    // Only show if we have real createdAt diversity / any products
    return list.slice(0, 4);
  });

  readonly typeSections = computed(() => {
    const products = this.shop()?.products ?? [];
    const byType = (label: string, match: (p: PublicShopProductDto) => boolean, query: Record<string, string>) => ({
      eyebrow: 'Colección',
      title: label,
      query,
      items: products.filter(match).slice(0, 3)
    });

    return [
      byType(
        'Cascos integrales',
        (p) => /integral/i.test(p.productType ?? '') || /integral/i.test(p.name),
        { tipo: 'Integral', categoria: 'Cascos' }
      ),
      byType(
        'Cascos abatibles',
        (p) => /abatible|modular/i.test(p.productType ?? '') || /abatible/i.test(p.name),
        { tipo: 'Abatible', categoria: 'Cascos' }
      ),
      byType(
        'Cascos abiertos',
        (p) => /abierto/i.test(p.productType ?? '') || /abierto/i.test(p.name),
        { tipo: 'Abierto', categoria: 'Cascos' }
      ),
      byType(
        'Accesorios y equipamiento',
        (p) => !/^cascos$/i.test(p.category || ''),
        { categoria: 'Accesorios' }
      )
    ];
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getPublic().subscribe({
      next: (data) => {
        this.shop.set(data);
        this.analytics.track('view_shop');
        this.title.setTitle(brandPageTitle(data.pageTitle || 'Tienda'));
        this.meta.updateTag({
          name: 'description',
          content:
            data.pageSubtitle ||
            'Cascos y equipo de protección. Consulta por WhatsApp desde Luz Verde.'
        });
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }

  media(path: string | null | undefined): string {
    return resolveMediaUrl(path);
  }

  scrollTo(id: string): void {
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
}
