import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { resolveMediaUrl } from '../../core/media/resolve-media-url';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { discountLabel, openWhatsAppOrder, productImages, sortProducts } from './shop-commerce';
import { ShopApi } from './shop.api';
import { PublicShopDto, PublicShopProductDto } from './shop.models';

@Component({
  selector: 'app-shop-offers-widget',
  standalone: true,
  imports: [CurrencyPipe, RouterLink, UiButtonComponent],
  template: `
    @if (offers().length && shop(); as s) {
      <section class="offers" [attr.aria-label]="title()">
        <header class="head">
          <div>
            <p class="eyebrow">Tienda Luz Verde</p>
            <h2>{{ title() }}</h2>
          </div>
          <a routerLink="/tienda" class="all">Ver toda la tienda</a>
        </header>
        <div class="row">
          @for (p of offers(); track p.id) {
            <article class="card">
              <a class="media" [routerLink]="['/tienda', p.slug]">
                @if (cover(p); as img) {
                  <img [src]="media(img)" [alt]="p.name" loading="lazy" />
                } @else {
                  <div class="ph">Producto</div>
                }
                @if (badge(p); as b) {
                  <span class="badge">{{ b }}</span>
                }
              </a>
              <div class="body">
                <h3>
                  <a [routerLink]="['/tienda', p.slug]">{{ p.name }}</a>
                </h3>
                <div class="price">
                  <strong>{{ p.priceCop | currency: 'COP' : 'symbol-narrow' : '1.0-0' }}</strong>
                  @if (p.compareAtPriceCop && p.compareAtPriceCop > p.priceCop) {
                    <span class="was">{{
                      p.compareAtPriceCop | currency: 'COP' : 'symbol-narrow' : '1.0-0'
                    }}</span>
                  }
                </div>
                <div class="actions">
                  <ui-button type="button" variant="secondary" [routerLink]="['/tienda', p.slug]"
                    >Ver producto</ui-button
                  >
                  @if (s.canOrder && p.stock > 0) {
                    <ui-button type="button" (click)="consult(s, p)">{{ s.ctaLabel }}</ui-button>
                  }
                </div>
              </div>
            </article>
          }
        </div>
      </section>
    }
  `,
  styles: `
    .offers {
      margin: 1.25rem 0 0;
      padding: 1.1rem 1rem 1.2rem;
      border-radius: var(--radius-lg);
      border: 1px solid var(--color-border);
      background: var(--color-surface);
    }
    .head {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      gap: 0.75rem;
      align-items: end;
      margin-bottom: 0.85rem;
    }
    .eyebrow {
      margin: 0;
      font-size: 0.72rem;
      font-weight: 800;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      color: var(--color-primary);
    }
    h2 {
      margin: 0.15rem 0 0;
      font-size: 1.15rem;
    }
    .all {
      font-weight: 700;
      color: var(--color-primary);
      text-decoration: none;
    }
    :host {
      display: block;
      min-width: 0;
    }
    .row {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(11rem, 1fr));
      gap: 0.75rem;
    }
    .card {
      border: 1px solid var(--color-border);
      border-radius: 0.85rem;
      overflow: hidden;
      background: color-mix(in srgb, var(--color-surface) 92%, #000 8%);
      display: grid;
      grid-template-rows: auto 1fr;
    }
    .media {
      position: relative;
      aspect-ratio: 4 / 3;
      background: #f4f7f5;
      display: block;
    }
    .media img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
    .ph {
      width: 100%;
      height: 100%;
      display: grid;
      place-items: center;
      color: #6b7c72;
      font-size: 0.7rem;
      font-weight: 700;
      text-transform: uppercase;
    }
    .badge {
      position: absolute;
      top: 0.45rem;
      left: 0.45rem;
      background: var(--color-primary);
      color: #06210f;
      font-size: 0.68rem;
      font-weight: 800;
      padding: 0.2rem 0.45rem;
      border-radius: 999px;
    }
    .body {
      padding: 0.65rem 0.7rem 0.8rem;
      display: grid;
      gap: 0.35rem;
      align-content: start;
    }
    h3 {
      margin: 0;
      font-size: 0.92rem;
      line-height: 1.25;
    }
    h3 a {
      color: inherit;
      text-decoration: none;
    }
    .price {
      display: flex;
      flex-wrap: wrap;
      gap: 0.35rem;
      align-items: baseline;
    }
    .price strong {
      color: var(--color-primary);
    }
    .was {
      text-decoration: line-through;
      color: var(--color-text-secondary);
      font-size: 0.8rem;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.35rem;
    }
    @media (max-width: 520px) {
      .row {
        grid-template-columns: 1fr;
      }
      .actions {
        flex-direction: column;
        align-items: stretch;
      }
      .head {
        align-items: start;
      }
    }
  `
})
export class ShopOffersWidgetComponent implements OnInit {
  private readonly api = inject(ShopApi);

  /** Widget heading */
  readonly title = input('Ofertas para ti');
  readonly limit = input(4);

  readonly shop = signal<PublicShopDto | null>(null);
  readonly offers = signal<PublicShopProductDto[]>([]);

  ngOnInit(): void {
    this.api.getPublic().subscribe({
      next: (data) => {
        this.shop.set(data);
        const ranked = sortProducts(data.products, 'discount').filter(
          (p) => (p.discountPercent ?? 0) > 0 || (p.compareAtPriceCop ?? 0) > p.priceCop || p.isFeatured
        );
        const pick = ranked.length ? ranked : sortProducts(data.products, 'featured');
        this.offers.set(pick.slice(0, this.limit()));
      },
      error: () => {
        this.shop.set(null);
        this.offers.set([]);
      }
    });
  }

  cover(p: PublicShopProductDto): string | null {
    return productImages(p)[0] ?? null;
  }

  badge(p: PublicShopProductDto): string | null {
    return discountLabel(p);
  }

  media(path: string): string {
    return resolveMediaUrl(path);
  }

  consult(shop: PublicShopDto, product: PublicShopProductDto): void {
    openWhatsAppOrder(shop, product);
  }
}
