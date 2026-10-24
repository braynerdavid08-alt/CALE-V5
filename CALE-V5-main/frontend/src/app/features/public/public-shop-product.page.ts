import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { brandPageTitle } from '../../core/brand';
import { mapApiError } from '../../core/http/map-api-error';
import { resolveMediaUrl } from '../../core/media/resolve-media-url';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import {
  discountLabel,
  openWhatsAppOrder,
  productImages,
  savingsCop,
  splitVariants
} from './shop-commerce';
import { ShopFavoritesService } from './shop-favorites.service';
import { ShopProductCardComponent } from './shop-product-card.component';
import { ShopApi } from './shop.api';
import { PublicShopDto, PublicShopProductDto } from './shop.models';

@Component({
  selector: 'app-public-shop-product-page',
  standalone: true,
  imports: [
    CurrencyPipe,
    FormsModule,
    RouterLink,
    UiButtonComponent,
    UiErrorComponent,
    UiLoadingComponent,
    ShopProductCardComponent
  ],
  template: `
    <div class="page">
      @if (loading()) {
        <ui-loading label="Cargando producto…" />
      } @else if (error()) {
        <ui-error [message]="error()" />
        <p><a routerLink="/tienda/catalogo">Volver al catálogo</a></p>
      } @else if (product()) {
        <p class="crumb">
          <a routerLink="/tienda">Tienda</a> /
          <a routerLink="/tienda/catalogo">Catálogo</a> /
          <span>{{ product()!.name }}</span>
        </p>

        <div class="layout">
          <div class="gallery">
            <div class="main">
              @if (images().length) {
                <img [src]="media(images()[active()])" [alt]="product()!.name" />
              } @else {
                <div class="ph">Sin imagen</div>
              }
              @if (disc()) {
                <span class="badge">{{ disc() }}</span>
              }
            </div>
            @if (images().length > 1) {
              <div class="thumbs">
                @for (url of images(); track url; let i = $index) {
                  <button type="button" class="thumb" [class.on]="active() === i" (click)="active.set(i)">
                    <img [src]="media(url)" alt="" />
                  </button>
                }
              </div>
            }
          </div>

          <div class="info">
            <p class="cat">
              {{ product()!.category }}
              @if (product()!.productType) {
                · {{ product()!.productType }}
              }
            </p>
            @if (product()!.brand) {
              <p class="brand">{{ product()!.brand }}</p>
            }
            <h1>{{ product()!.name }}</h1>
            @if (product()!.reference) {
              <p class="ref">Ref. {{ product()!.reference }}</p>
            }

            <div class="price-row">
              <strong>{{ product()!.priceCop | currency: 'COP' : 'symbol-narrow' : '1.0-0' }}</strong>
              @if (product()!.compareAtPriceCop != null && product()!.compareAtPriceCop! > product()!.priceCop) {
                <span class="was">{{
                  product()!.compareAtPriceCop! | currency: 'COP' : 'symbol-narrow' : '1.0-0'
                }}</span>
              }
              @if (disc()) {
                <span class="disc">{{ disc() }}</span>
              }
            </div>
            @if (save(); as saveAmt) {
              <p class="save">Ahorras {{ saveAmt | currency: 'COP' : 'symbol-narrow' : '1.0-0' }}</p>
            }

            @if (product()!.stock <= 0) {
              <p class="stock out">Producto agotado por ahora</p>
            } @else {
              <p class="stock">
                Disponible{{ product()!.stock <= 3 ? ' · Quedan ' + product()!.stock : '' }}
              </p>
            }

            <p class="desc">{{ product()!.description || product()!.shortDescription }}</p>

            @if (colorOpts().length) {
              <p class="label">Color</p>
              <div class="opts">
                @for (c of colorOpts(); track c) {
                  <button type="button" class="opt" [class.on]="color() === c" (click)="color.set(c)">
                    {{ c }}
                  </button>
                }
              </div>
            }
            @if (sizeOpts().length) {
              <p class="label">Talla</p>
              <div class="opts">
                @for (sz of sizeOpts(); track sz) {
                  <button type="button" class="opt" [class.on]="size() === sz" (click)="size.set(sz)">
                    {{ sz }}
                  </button>
                }
              </div>
            }

            <label class="qty">
              Cantidad
              <input type="number" min="1" max="20" [(ngModel)]="qty" name="qty" />
            </label>

            <div class="actions">
              <button
                type="button"
                class="fav"
                [class.on]="fav.isFavorite(product()!.id)"
                (click)="fav.toggle(product()!.id)">
                {{ fav.isFavorite(product()!.id) ? 'En favoritos' : 'Guardar en favoritos' }}
              </button>
              @if (shop()?.canOrder && product()!.stock > 0) {
                <ui-button type="button" (click)="consult()">{{ shop()!.ctaLabel }}</ui-button>
              } @else if (!shop()?.canOrder) {
                <p class="hint">WhatsApp comercial pendiente de configurar.</p>
              }
            </div>
          </div>
        </div>

        @if (related().length && shop()) {
          <section class="related">
            <h2>Productos relacionados</h2>
            <div class="grid">
              @for (r of related(); track r.id) {
                <app-shop-product-card [product]="r" [shop]="shop()!" />
              }
            </div>
          </section>
        }
      }
    </div>
  `,
  styles: `
    .page {
      width: min(1100px, calc(100% - 2rem));
      margin: 0 auto;
      padding: 1.25rem 0 3rem;
    }
    .crumb {
      margin: 0 0 1rem;
      font-size: 0.85rem;
      color: var(--color-text-secondary);
    }
    .crumb a {
      color: inherit;
    }
    .layout {
      display: grid;
      grid-template-columns: 1.05fr 1fr;
      gap: 1.5rem;
      align-items: start;
    }
    .main {
      position: relative;
      aspect-ratio: 4 / 3;
      border-radius: var(--radius-lg);
      overflow: hidden;
      background: #f4f7f5;
    }
    .main img {
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
    }
    .badge {
      position: absolute;
      top: 0.75rem;
      left: 0.75rem;
      background: var(--color-primary);
      color: #06210f;
      font-weight: 800;
      font-size: 0.75rem;
      padding: 0.3rem 0.55rem;
      border-radius: 999px;
    }
    .thumbs {
      display: flex;
      gap: 0.4rem;
      margin-top: 0.55rem;
      overflow-x: auto;
    }
    .thumb {
      flex: none;
      width: 4rem;
      height: 3rem;
      padding: 0;
      border: 2px solid transparent;
      border-radius: 0.45rem;
      overflow: hidden;
      background: #101812;
      cursor: pointer;
    }
    .thumb.on {
      border-color: var(--color-primary);
    }
    .thumb img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
    .cat {
      margin: 0;
      color: var(--color-primary);
      font-size: 0.75rem;
      font-weight: 800;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }
    .brand {
      margin: 0.25rem 0 0;
      font-weight: 700;
    }
    h1 {
      margin: 0.2rem 0 0;
      font-size: clamp(1.4rem, 2.8vw, 1.9rem);
      line-height: 1.2;
    }
    .ref,
    .desc,
    .stock,
    .save,
    .hint,
    .label {
      margin: 0.35rem 0 0;
      color: var(--color-text-secondary);
      line-height: 1.45;
    }
    .price-row {
      display: flex;
      flex-wrap: wrap;
      gap: 0.45rem;
      align-items: baseline;
      margin-top: 0.65rem;
    }
    .price-row strong {
      font-size: 1.45rem;
      color: var(--color-primary);
    }
    .was {
      text-decoration: line-through;
      color: var(--color-text-secondary);
    }
    .disc {
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
    .label {
      font-weight: 700;
      color: var(--color-text);
      margin-top: 0.85rem;
    }
    .opts {
      display: flex;
      flex-wrap: wrap;
      gap: 0.35rem;
      margin-top: 0.35rem;
    }
    .opt {
      min-height: 2.35rem;
      padding: 0 0.7rem;
      border-radius: 0.5rem;
      border: 1px solid var(--color-border);
      background: transparent;
      color: inherit;
      font: inherit;
      font-weight: 700;
      cursor: pointer;
    }
    .opt.on {
      border-color: var(--color-primary);
      background: color-mix(in srgb, var(--color-primary) 16%, transparent);
    }
    .qty {
      display: grid;
      gap: 0.25rem;
      margin-top: 0.85rem;
      max-width: 8rem;
      font-weight: 700;
      font-size: 0.85rem;
    }
    .qty input {
      min-height: 2.4rem;
      padding: 0.35rem 0.5rem;
      border-radius: 0.5rem;
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: inherit;
      font: inherit;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.55rem;
      margin-top: 1rem;
      align-items: center;
    }
    .fav {
      min-height: 2.6rem;
      padding: 0 0.9rem;
      border-radius: var(--radius-md);
      border: 1px solid var(--color-border);
      background: transparent;
      color: inherit;
      font: inherit;
      font-weight: 700;
      cursor: pointer;
    }
    .fav.on {
      border-color: var(--color-primary);
      color: var(--color-primary);
    }
    .related {
      margin-top: 2.5rem;
    }
    .related h2 {
      margin: 0 0 0.85rem;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 1rem;
    }
    @media (max-width: 800px) {
      .layout {
        grid-template-columns: 1fr;
      }
      .grid {
        grid-template-columns: repeat(2, 1fr);
      }
    }
    @media (max-width: 520px) {
      .grid {
        grid-template-columns: 1fr;
      }
    }
  `
})
export class PublicShopProductPage implements OnInit {
  private readonly api = inject(ShopApi);
  private readonly route = inject(ActivatedRoute);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  readonly fav = inject(ShopFavoritesService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly shop = signal<PublicShopDto | null>(null);
  readonly product = signal<PublicShopProductDto | null>(null);
  readonly related = signal<PublicShopProductDto[]>([]);
  readonly active = signal(0);
  readonly size = signal<string | null>(null);
  readonly color = signal<string | null>(null);
  qty = 1;

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const slug = params.get('slug') ?? '';
      this.load(slug);
    });
  }

  images(): string[] {
    const p = this.product();
    return p ? productImages(p) : [];
  }

  disc(): string | null {
    const p = this.product();
    return p ? discountLabel(p) : null;
  }

  save(): number | null {
    const p = this.product();
    return p ? savingsCop(p) : null;
  }

  sizeOpts(): string[] {
    return splitVariants(this.product()?.sizes);
  }

  colorOpts(): string[] {
    return splitVariants(this.product()?.colors);
  }

  media(path: string): string {
    return resolveMediaUrl(path);
  }

  consult(): void {
    const s = this.shop();
    const p = this.product();
    if (!s || !p) return;
    openWhatsAppOrder(s, p, {
      size: this.size(),
      color: this.color(),
      quantity: this.qty
    });
  }

  private load(slug: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.active.set(0);

    forkJoin({
      shop: this.api.getPublic().pipe(catchError(() => of(null))),
      product: this.api.getPublicBySlug(slug).pipe(
        catchError((err) => {
          this.error.set(
            err?.status === 404 ? 'Producto no encontrado o no está publicado.' : mapApiError(err)
          );
          return of(null);
        })
      )
    }).subscribe(({ shop, product }) => {
      this.shop.set(shop);
      this.product.set(product);
      if (product) {
        this.title.setTitle(brandPageTitle(product.name));
        this.meta.updateTag({
          name: 'description',
          content: product.shortDescription || product.name
        });
        const sizes = splitVariants(product.sizes);
        const colors = splitVariants(product.colors);
        this.size.set(sizes[0] ?? null);
        this.color.set(colors[0] ?? null);
        const related = (shop?.products ?? [])
          .filter(
            (x) =>
              x.id !== product.id &&
              (x.category === product.category ||
                (product.productType && x.productType === product.productType))
          )
          .slice(0, 3);
        this.related.set(related);
      }
      this.loading.set(false);
    });
  }
}
