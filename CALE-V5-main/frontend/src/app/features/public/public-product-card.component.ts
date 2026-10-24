import { CurrencyPipe } from '@angular/common';
import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { resolveMediaUrl } from '../../core/media/resolve-media-url';
import { PublicShopProductDto } from './shop.models';

/**
 * Compact product teaser for landing / catalog consistency.
 * Links to the shop page (no per-product route); purchase stays on /cascos via WhatsApp.
 */
@Component({
  selector: 'app-public-product-card',
  standalone: true,
  imports: [CurrencyPipe, RouterLink],
  template: `
    <a class="card" routerLink="/cascos" [attr.aria-label]="'Ver ' + product.name + ' en la tienda'">
      <div class="media">
        @if (imageSrc) {
          <img [src]="imageSrc" [alt]="product.name" width="320" height="240" loading="lazy" />
        } @else {
          <span class="ph" aria-hidden="true">Producto</span>
        }
        @if (badgeText) {
          <span class="badge">{{ badgeText }}</span>
        }
      </div>
      <div class="body">
        <strong class="name">{{ product.name }}</strong>
        <div class="price-row">
          <span class="price">{{ product.priceCop | currency: 'COP' : 'symbol-narrow' : '1.0-0' }}</span>
          @if (showCompare) {
            <s class="was">{{ product.compareAtPriceCop | currency: 'COP' : 'symbol-narrow' : '1.0-0' }}</s>
          }
        </div>
        <span class="cta">Ver en tienda</span>
      </div>
    </a>
  `,
  styles: `
    :host { display: block; min-width: 0; }
    .card {
      display: grid;
      grid-template-rows: auto 1fr;
      height: 100%;
      text-decoration: none;
      color: inherit;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg, 1rem);
      overflow: hidden;
      box-shadow: var(--shadow-sm);
      transition: transform 0.2s ease, border-color 0.2s ease;
    }
    .card:hover,
    .card:focus-visible {
      transform: translateY(-2px);
      border-color: color-mix(in srgb, var(--color-primary) 45%, var(--color-border));
      outline: none;
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
    .ph {
      display: grid;
      place-items: center;
      height: 100%;
      color: rgba(255, 255, 255, 0.45);
      font-weight: 700;
      font-size: 0.85rem;
    }
    .badge {
      position: absolute;
      top: 0.55rem;
      left: 0.55rem;
      padding: 0.2rem 0.5rem;
      border-radius: 999px;
      background: var(--color-primary);
      color: var(--color-on-primary, #04130a);
      font-size: 0.72rem;
      font-weight: 800;
      line-height: 1.2;
    }
    .body {
      display: grid;
      gap: 0.35rem;
      align-content: start;
      padding: 0.75rem 0.85rem 0.9rem;
      min-width: 0;
    }
    .name {
      font-size: var(--text-sm, 0.9rem);
      line-height: 1.3;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }
    .price-row {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: 0.35rem 0.5rem;
    }
    .price {
      font-weight: 800;
      font-variant-numeric: tabular-nums;
      color: var(--color-primary);
      font-size: 0.95rem;
    }
    .was {
      color: var(--color-text-secondary);
      font-size: 0.8rem;
      font-variant-numeric: tabular-nums;
    }
    .cta {
      margin-top: 0.15rem;
      font-size: 0.78rem;
      font-weight: 700;
      color: var(--color-text-secondary);
    }
    .card:hover .cta,
    .card:focus-visible .cta {
      color: var(--color-primary);
    }
  `
})
export class PublicProductCardComponent {
  @Input({ required: true }) product!: PublicShopProductDto;

  get imageSrc(): string | null {
    const url = this.product.imageUrls?.[0] || this.product.imageUrl;
    return url ? resolveMediaUrl(url) : null;
  }

  get showCompare(): boolean {
    const c = this.product.compareAtPriceCop;
    return c != null && c > this.product.priceCop;
  }

  get badgeText(): string | null {
    if (this.product.promoLabel?.trim()) return this.product.promoLabel.trim();
    if (this.product.discountPercent != null && this.product.discountPercent > 0) {
      return `−${this.product.discountPercent}%`;
    }
    if (this.showCompare) {
      const pct = Math.round(
        (1 - this.product.priceCop / (this.product.compareAtPriceCop as number)) * 100
      );
      return pct > 0 ? `−${pct}%` : null;
    }
    return null;
  }
}
