import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { resolveMediaUrl } from '../../core/media/resolve-media-url';
import { ShopAnalyticsService } from './shop-analytics.service';
import { discountLabel, openWhatsAppOrder, productImages, sortProducts } from './shop-commerce';
import { ShopApi } from './shop.api';
import { PublicShopDto, PublicShopProductDto } from './shop.models';

@Component({
  selector: 'app-shop-offers-widget',
  standalone: true,
  imports: [CurrencyPipe, RouterLink],
  template: `
    @if (offers().length && shop(); as s) {
      <section class="offers" [attr.aria-label]="title()">
        <header class="head">
          <div>
            <p class="eyebrow">Tienda Luz Verde</p>
            <h2>{{ title() }}</h2>
          </div>
          <a routerLink="/tienda" class="all">Ver tienda →</a>
        </header>
        <ul class="list">
          @for (p of offers(); track p.id) {
            <li>
              <a class="row" [routerLink]="['/tienda', p.slug]">
                <span class="thumb">
                  @if (cover(p); as img) {
                    <img [src]="media(img)" [alt]="" loading="lazy" width="56" height="56" />
                  } @else {
                    <span class="ph" aria-hidden="true">·</span>
                  }
                  @if (badge(p); as b) {
                    <span class="badge">{{ b }}</span>
                  }
                </span>
                <span class="meta">
                  <strong>{{ p.name }}</strong>
                  <span class="price">
                    {{ p.priceCop | currency: 'COP' : 'symbol-narrow' : '1.0-0' }}
                    @if (p.compareAtPriceCop && p.compareAtPriceCop > p.priceCop) {
                      <s>{{ p.compareAtPriceCop | currency: 'COP' : 'symbol-narrow' : '1.0-0' }}</s>
                    }
                  </span>
                </span>
              </a>
              @if (s.canOrder && p.stock > 0) {
                <button type="button" class="wa" (click)="consult(s, p)">WhatsApp</button>
              }
            </li>
          }
        </ul>
      </section>
    }
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
      position: relative;
      z-index: 0;
    }
    .offers {
      margin: 0;
      padding: 0.85rem 0.9rem;
      border-radius: var(--radius-md);
      border: 1px solid var(--color-border);
      background: var(--color-surface-raised, var(--color-surface));
      position: relative;
      z-index: 0;
    }
    .head {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      gap: 0.4rem 0.75rem;
      align-items: baseline;
      margin-bottom: 0.65rem;
    }
    .eyebrow {
      margin: 0;
      font-size: 0.68rem;
      font-weight: 800;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      color: var(--color-text-secondary);
    }
    h2 {
      margin: 0.1rem 0 0;
      font-size: 0.95rem;
      font-weight: 750;
      color: var(--color-text);
    }
    .all {
      font-size: 0.85rem;
      font-weight: 700;
      color: var(--color-primary);
      text-decoration: none;
      white-space: nowrap;
    }
    .list {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.45rem;
    }
    .list > li {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      gap: 0.45rem;
      align-items: center;
      min-width: 0;
    }
    .row {
      display: grid;
      grid-template-columns: 3.1rem minmax(0, 1fr);
      gap: 0.65rem;
      align-items: center;
      min-width: 0;
      text-decoration: none;
      color: inherit;
      padding: 0.35rem;
      border-radius: 0.65rem;
    }
    .row:hover,
    .row:focus-visible {
      background: color-mix(in srgb, var(--color-primary) 8%, transparent);
      outline: none;
    }
    .thumb {
      position: relative;
      width: 3.1rem;
      height: 3.1rem;
      border-radius: 0.55rem;
      overflow: hidden;
      background: var(--color-chip, #e5eee8);
      flex-shrink: 0;
    }
    .thumb img {
      width: 100%;
      height: 100%;
      object-fit: cover;
      display: block;
    }
    .ph {
      display: grid;
      place-items: center;
      width: 100%;
      height: 100%;
      color: var(--color-text-secondary);
    }
    .badge {
      position: absolute;
      top: 0.15rem;
      left: 0.15rem;
      background: var(--color-primary);
      color: #06210f;
      font-size: 0.58rem;
      font-weight: 800;
      padding: 0.08rem 0.28rem;
      border-radius: 999px;
      line-height: 1.2;
    }
    .meta {
      display: grid;
      gap: 0.12rem;
      min-width: 0;
    }
    .meta strong {
      font-size: 0.88rem;
      font-weight: 700;
      line-height: 1.25;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .price {
      display: flex;
      flex-wrap: wrap;
      gap: 0.35rem;
      align-items: baseline;
      font-size: 0.82rem;
      font-weight: 750;
      color: var(--color-primary);
    }
    .price s {
      font-weight: 600;
      color: var(--color-text-secondary);
      font-size: 0.75rem;
    }
    .wa {
      flex-shrink: 0;
      min-height: 2.1rem;
      padding: 0 0.65rem;
      border-radius: 0.5rem;
      border: 1px solid color-mix(in srgb, var(--color-primary) 40%, var(--color-border));
      background: color-mix(in srgb, var(--color-primary) 12%, transparent);
      color: var(--color-primary);
      font: inherit;
      font-size: 0.78rem;
      font-weight: 750;
      cursor: pointer;
    }
    @media (max-width: 520px) {
      .list > li {
        grid-template-columns: minmax(0, 1fr);
      }
      .wa {
        justify-self: start;
        margin-left: 3.75rem;
      }
    }
  `
})
export class ShopOffersWidgetComponent implements OnInit {
  private readonly api = inject(ShopApi);
  private readonly analytics = inject(ShopAnalyticsService);

  readonly title = input('Ofertas para ti');
  readonly limit = input(3);

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
    if (openWhatsAppOrder(shop, product)) {
      this.analytics.track('whatsapp', product.id);
    }
  }
}
