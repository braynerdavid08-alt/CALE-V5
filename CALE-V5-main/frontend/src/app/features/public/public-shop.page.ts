import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { brandPageTitle } from '../../core/brand';
import { mapApiError } from '../../core/http/map-api-error';
import { resolveMediaUrl } from '../../core/media/resolve-media-url';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import { ShopApi } from './shop.api';
import { PublicShopDto, PublicShopProductDto } from './shop.models';

@Component({
  selector: 'app-public-shop-page',
  standalone: true,
  imports: [CurrencyPipe, UiButtonComponent, UiErrorComponent, UiLoadingComponent],
  template: `
    <div class="page">
      @if (loading()) {
        <ui-loading label="Cargando catálogo…" />
      } @else if (error()) {
        <ui-error [message]="error()" />
      } @else {
        @if (shop(); as s) {
          <header class="hero" [class.has-image]="!!s.heroImageUrl">
            @if (s.heroImageUrl) {
              <img class="hero-bg" [src]="media(s.heroImageUrl)" alt="" />
              <div class="hero-scrim" aria-hidden="true"></div>
            }
            <div class="hero-copy">
              <p class="eyebrow">Equipo de protección</p>
              <h1>{{ s.pageTitle }}</h1>
              <p class="lead">{{ s.pageSubtitle }}</p>
            </div>
          </header>

          @if (s.products.length === 0) {
            <p class="empty">Pronto publicaremos cascos y equipo. Vuelve en unos días.</p>
          } @else {
            <div class="grid">
              @for (p of s.products; track p.id) {
                <article class="item" [class.featured]="p.isFeatured">
                  <div class="media">
                    @if (productImages(p).length) {
                      <img
                        [src]="media(activeImage(p))"
                        [alt]="p.name"
                        width="480"
                        height="360"
                        loading="lazy" />
                    } @else {
                      <div class="placeholder" aria-hidden="true">Casco</div>
                    }
                    @if (p.promoLabel || p.discountPercent) {
                      <span class="badge">{{ p.promoLabel || ('−' + p.discountPercent + '%') }}</span>
                    }
                  </div>
                  @if (productImages(p).length > 1) {
                    <div class="thumbs" role="list">
                      @for (url of productImages(p); track url; let i = $index) {
                        <button
                          type="button"
                          class="thumb"
                          [class.on]="activeIndex(p.id) === i"
                          (click)="setActive(p.id, i)"
                          [attr.aria-label]="'Ángulo ' + (i + 1)">
                          <img [src]="media(url)" alt="" width="56" height="42" loading="lazy" />
                        </button>
                      }
                    </div>
                  }
                  <div class="body">
                    <p class="cat">{{ p.category }}</p>
                    <h2>{{ p.name }}</h2>
                    <p class="desc">{{ p.shortDescription }}</p>
                    @if (p.sizes) {
                      <p class="sizes">Tallas: {{ p.sizes }}</p>
                    }
                    <div class="price-row">
                      <strong>{{ p.priceCop | currency:'COP':'symbol-narrow':'1.0-0' }}</strong>
                      @if (p.compareAtPriceCop && p.compareAtPriceCop > p.priceCop) {
                        <span class="was">{{ p.compareAtPriceCop | currency:'COP':'symbol-narrow':'1.0-0' }}</span>
                      }
                    </div>
                    @if (p.stock <= 0) {
                      <p class="stock out">Agotado por ahora</p>
                    } @else if (p.stock <= 3) {
                      <p class="stock">Quedan {{ p.stock }}</p>
                    }
                    <div class="actions">
                      @if (s.canOrder && p.stock > 0) {
                        <ui-button type="button" (click)="order(s, p)">{{ s.ctaLabel }}</ui-button>
                      } @else if (!s.canOrder) {
                        <p class="hint">WhatsApp de pedidos pendiente de configurar.</p>
                      }
                    </div>
                  </div>
                </article>
              }
            </div>
          }
        }
      }
    </div>
  `,
  styles: [`
    .page {
      width: min(1120px, calc(100% - 2rem));
      margin: 0 auto;
      padding: 1.5rem 0 3.5rem;
    }
    .hero {
      position: relative;
      border-radius: var(--radius-xl, 1.25rem);
      overflow: hidden;
      margin-bottom: 1.75rem;
      background:
        radial-gradient(120% 80% at 10% 0%, color-mix(in srgb, var(--color-primary) 28%, transparent), transparent 55%),
        linear-gradient(160deg, #0d1712 0%, #15241c 55%, #1a2e22 100%);
      color: #f4faf6;
      min-height: 12rem;
      display: grid;
      align-items: end;
    }
    .hero.has-image { min-height: 16rem; }
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
      background: linear-gradient(180deg, rgba(8, 16, 12, 0.25), rgba(8, 16, 12, 0.78));
    }
    .hero-copy {
      position: relative;
      z-index: 1;
      padding: 1.75rem 1.5rem 1.6rem;
      max-width: 40rem;
    }
    .eyebrow {
      margin: 0 0 0.35rem;
      font-size: var(--text-xs);
      font-weight: 800;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: color-mix(in srgb, var(--color-primary) 70%, white);
    }
    h1 { margin: 0 0 0.45rem; font-size: clamp(1.6rem, 3vw, 2.2rem); line-height: 1.15; }
    .lead { margin: 0; color: rgba(244, 250, 246, 0.88); line-height: 1.45; }
    .empty {
      margin: 0;
      padding: 2rem 0;
      color: var(--color-text-secondary);
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 1.1rem;
    }
    .item {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      overflow: hidden;
      box-shadow: var(--shadow-sm);
      display: grid;
      grid-template-rows: auto 1fr;
    }
    .item.featured {
      border-color: color-mix(in srgb, var(--color-primary) 45%, var(--color-border));
      box-shadow: 0 0 0 1px color-mix(in srgb, var(--color-primary) 25%, transparent);
    }
    .media {
      position: relative;
      aspect-ratio: 4 / 3;
      background: #101812;
      overflow: hidden;
    }
    .media img {
      width: 100%;
      height: 100%;
      object-fit: cover;
      display: block;
    }
    .thumbs {
      display: flex;
      gap: 0.35rem;
      padding: 0.45rem 0.55rem 0;
      overflow-x: auto;
      scrollbar-width: thin;
    }
    .thumb {
      flex: none;
      width: 3.2rem;
      height: 2.4rem;
      padding: 0;
      border: 2px solid transparent;
      border-radius: 0.4rem;
      overflow: hidden;
      background: #101812;
      cursor: pointer;
    }
    .thumb.on { border-color: var(--color-primary); }
    .thumb img { width: 100%; height: 100%; object-fit: cover; display: block; }
    .placeholder {
      width: 100%;
      height: 100%;
      display: grid;
      place-items: center;
      color: rgba(244, 250, 246, 0.45);
      font-weight: 700;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      font-size: var(--text-xs);
    }
    .badge {
      position: absolute;
      top: 0.7rem;
      left: 0.7rem;
      background: var(--color-primary);
      color: #06210f;
      font-size: 0.72rem;
      font-weight: 800;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      padding: 0.28rem 0.55rem;
      border-radius: 999px;
    }
    .body {
      padding: 0.95rem 1rem 1.1rem;
      display: grid;
      align-content: start;
      gap: 0.35rem;
    }
    .cat {
      margin: 0;
      color: var(--color-primary);
      font-size: var(--text-xs);
      font-weight: 800;
      letter-spacing: 0.04em;
      text-transform: uppercase;
    }
    h2 {
      margin: 0;
      font-size: var(--text-md);
      line-height: 1.25;
    }
    .desc, .sizes, .stock, .hint {
      margin: 0;
      color: var(--color-text-secondary);
      font-size: var(--text-sm);
      line-height: 1.4;
    }
    .price-row {
      display: flex;
      align-items: baseline;
      gap: 0.55rem;
      margin-top: 0.25rem;
    }
    .price-row strong {
      font-size: 1.15rem;
      color: var(--color-text);
    }
    .was {
      text-decoration: line-through;
      color: var(--color-text-secondary);
      font-size: var(--text-sm);
    }
    .stock.out { color: #b42318; font-weight: 700; }
    .actions { margin-top: 0.55rem; }
    @media (max-width: 900px) {
      .grid { grid-template-columns: repeat(2, 1fr); }
    }
    @media (max-width: 560px) {
      .grid { grid-template-columns: 1fr; }
      .hero { min-height: 11rem; }
    }
  `]
})
export class PublicShopPage implements OnInit {
  private readonly api = inject(ShopApi);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly shop = signal<PublicShopDto | null>(null);
  readonly activeByProduct = signal<Record<number, number>>({});

  ngOnInit(): void {
    this.api.getPublic().subscribe({
      next: (data) => {
        this.shop.set(data);
        this.title.setTitle(brandPageTitle(data.pageTitle));
        this.meta.updateTag({ name: 'description', content: data.pageSubtitle });
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

  productImages(p: PublicShopProductDto): string[] {
    if (p.imageUrls?.length) {
      return p.imageUrls;
    }
    return p.imageUrl ? [p.imageUrl] : [];
  }

  activeIndex(productId: number): number {
    return this.activeByProduct()[productId] ?? 0;
  }

  activeImage(p: PublicShopProductDto): string {
    const imgs = this.productImages(p);
    return imgs[this.activeIndex(p.id)] ?? imgs[0] ?? '';
  }

  setActive(productId: number, index: number): void {
    this.activeByProduct.update((m) => ({ ...m, [productId]: index }));
  }

  order(shop: PublicShopDto, product: PublicShopProductDto): void {
    if (!shop.whatsApp) {
      return;
    }
    const price = new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      maximumFractionDigits: 0
    }).format(product.priceCop);
    const text =
      `Hola, me interesa *${product.name}* (${price}). ` +
      `¿Está disponible? Quiero pedirlo desde Luz Verde.`;
    const url = `https://wa.me/${shop.whatsApp}?text=${encodeURIComponent(text)}`;
    window.open(url, '_blank', 'noopener');
  }
}
