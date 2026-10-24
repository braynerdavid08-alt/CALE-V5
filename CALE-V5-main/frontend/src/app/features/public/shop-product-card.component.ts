import { CurrencyPipe } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { resolveMediaUrl } from '../../core/media/resolve-media-url';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import {
  discountLabel,
  openWhatsAppOrder,
  productImages,
  savingsCop
} from './shop-commerce';
import { ShopAnalyticsService } from './shop-analytics.service';
import { ShopFavoritesService } from './shop-favorites.service';
import { PublicShopDto, PublicShopProductDto } from './shop.models';

@Component({
  selector: 'app-shop-product-card',
  standalone: true,
  imports: [CurrencyPipe, RouterLink, UiButtonComponent],
  template: `
    <article class="item" [class.featured]="product.isFeatured">
      <a class="media" [routerLink]="['/tienda', product.slug]">
        @if (images.length) {
          <img [src]="media(images[0])" [alt]="product.name" width="480" height="360" loading="lazy" />
        } @else {
          <div class="placeholder" aria-hidden="true">Producto</div>
        }
        @if (disc; as d) {
          <span class="badge">{{ d }}</span>
        }
      </a>
      <button
        type="button"
        class="fav"
        [class.on]="fav.isFavorite(product.id)"
        (click)="fav.toggle(product.id)"
        [attr.aria-label]="fav.isFavorite(product.id) ? 'Quitar de favoritos' : 'Guardar en favoritos'">
        ♥
      </button>
      <div class="body">
        <p class="meta">
          <span class="cat">{{ product.category }}</span>
          @if (product.productType) {
            <span>· {{ product.productType }}</span>
          }
        </p>
        @if (product.brand) {
          <p class="brand">{{ product.brand }}</p>
        }
        <h2>
          <a [routerLink]="['/tienda', product.slug]">{{ product.name }}</a>
        </h2>
        <div class="price-row">
          <strong>{{ product.priceCop | currency: 'COP' : 'symbol-narrow' : '1.0-0' }}</strong>
          @if (product.compareAtPriceCop && product.compareAtPriceCop > product.priceCop) {
            <span class="was">{{
              product.compareAtPriceCop | currency: 'COP' : 'symbol-narrow' : '1.0-0'
            }}</span>
          }
          @if (disc; as d) {
            <span class="disc">{{ d }}</span>
          }
        </div>
        @if (save; as s) {
          <p class="save">Ahorras {{ s | currency: 'COP' : 'symbol-narrow' : '1.0-0' }}</p>
        }
        @if (product.stock <= 0) {
          <p class="stock out">Agotado</p>
        } @else if (product.stock <= 3) {
          <p class="stock">Quedan {{ product.stock }}</p>
        } @else {
          <p class="stock">Disponible</p>
        }
        <div class="actions">
          <ui-button type="button" variant="secondary" [routerLink]="['/tienda', product.slug]"
            >Ver producto</ui-button
          >
          @if (shop.canOrder && product.stock > 0) {
            <ui-button type="button" (click)="consult()">{{ shop.ctaLabel }}</ui-button>
          }
        </div>
      </div>
    </article>
  `,
  styles: `
    .item {
      position: relative;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      overflow: hidden;
      box-shadow: var(--shadow-sm);
      display: grid;
      grid-template-rows: auto 1fr;
      height: 100%;
    }
    .item.featured {
      border-color: color-mix(in srgb, var(--color-primary) 45%, var(--color-border));
    }
    .media {
      position: relative;
      aspect-ratio: 4 / 3;
      background: #f4f7f5;
      display: block;
      overflow: hidden;
    }
    .media img {
      width: 100%;
      height: 100%;
      object-fit: cover;
      display: block;
    }
    .placeholder {
      width: 100%;
      height: 100%;
      display: grid;
      place-items: center;
      color: #6b7c72;
      font-weight: 700;
      font-size: 0.75rem;
      text-transform: uppercase;
      letter-spacing: 0.06em;
      background: #e8efe9;
    }
    .badge {
      position: absolute;
      top: 0.7rem;
      left: 0.7rem;
      background: var(--color-primary);
      color: #06210f;
      font-size: 0.72rem;
      font-weight: 800;
      padding: 0.28rem 0.55rem;
      border-radius: 999px;
    }
    .fav {
      position: absolute;
      top: 0.55rem;
      right: 0.55rem;
      z-index: 2;
      width: 2.2rem;
      height: 2.2rem;
      border: 0;
      border-radius: 999px;
      background: rgba(0, 0, 0, 0.4);
      color: rgba(255, 255, 255, 0.8);
      cursor: pointer;
    }
    .fav.on {
      color: #4ade80;
    }
    .body {
      padding: 0.9rem 1rem 1.05rem;
      display: grid;
      align-content: start;
      gap: 0.3rem;
    }
    .meta,
    .brand,
    .stock,
    .save {
      margin: 0;
      font-size: var(--text-sm);
      color: var(--color-text-secondary);
    }
    .cat {
      color: var(--color-primary);
      font-weight: 800;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      font-size: 0.72rem;
    }
    .brand {
      font-weight: 700;
      color: var(--color-text);
    }
    h2 {
      margin: 0;
      font-size: var(--text-md);
      line-height: 1.25;
    }
    h2 a {
      color: inherit;
      text-decoration: none;
    }
    .price-row {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: 0.4rem;
    }
    .price-row strong {
      color: var(--color-primary);
      font-size: 1.1rem;
    }
    .was {
      text-decoration: line-through;
      color: var(--color-text-secondary);
      font-size: var(--text-sm);
    }
    .disc {
      font-size: 0.75rem;
      font-weight: 800;
      color: var(--color-primary);
    }
    .save {
      color: var(--color-primary);
      font-weight: 700;
    }
    .stock.out {
      color: #b42318;
      font-weight: 700;
    }
    .actions {
      margin-top: 0.4rem;
      display: flex;
      flex-wrap: wrap;
      gap: 0.4rem;
    }
  `
})
export class ShopProductCardComponent {
  readonly fav = inject(ShopFavoritesService);
  private readonly analytics = inject(ShopAnalyticsService);

  @Input({ required: true }) product!: PublicShopProductDto;
  @Input({ required: true }) shop!: PublicShopDto;
  @Output() whatsappMissing = new EventEmitter<void>();

  get images(): string[] {
    return productImages(this.product);
  }

  get disc(): string | null {
    return discountLabel(this.product);
  }

  get save(): number | null {
    return savingsCop(this.product);
  }

  media(path: string): string {
    return resolveMediaUrl(path);
  }

  consult(): void {
    if (!openWhatsAppOrder(this.shop, this.product)) {
      this.whatsappMissing.emit();
      return;
    }
    this.analytics.track('whatsapp', this.product.id);
  }
}
