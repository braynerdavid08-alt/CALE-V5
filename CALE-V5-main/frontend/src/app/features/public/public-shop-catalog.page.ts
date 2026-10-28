import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { brandPageTitle } from '../../core/brand';
import { mapApiError } from '../../core/http/map-api-error';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import { discountPercent, sortProducts, type ShopSort } from './shop-commerce';
import { ShopAnalyticsService } from './shop-analytics.service';
import { ShopFavoritesService } from './shop-favorites.service';
import { ShopProductCardComponent } from './shop-product-card.component';
import { ShopApi } from './shop.api';
import {
  PublicShopDto,
  PublicShopProductDto,
  SHOP_CATEGORIES,
  SHOP_HELMET_TYPES
} from './shop.models';

const PAGE_SIZE = 12;

@Component({
  selector: 'app-public-shop-catalog-page',
  standalone: true,
  imports: [
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
        <ui-loading label="Cargando catálogo…" />
      } @else if (error()) {
        <ui-error [message]="error()" />
        <ui-button type="button" (click)="reload()">Reintentar</ui-button>
      } @else if (shop()) {
        <header class="head">
          <div>
            <p class="eyebrow"><a routerLink="/tienda">Tienda</a> / Catálogo</p>
            <h1>{{ shop()!.pageTitle }}</h1>
            <p class="sub">{{ filtered().length }} resultado{{ filtered().length === 1 ? '' : 's' }}</p>
          </div>
          <div class="head-actions">
            <button
              type="button"
              class="fav-toggle"
              [class.on]="favoritesOnly"
              (click)="toggleFavoritesOnly()"
              [attr.aria-pressed]="favoritesOnly"
              [attr.aria-label]="favoritesOnly ? 'Ver todo el catálogo' : 'Ver solo favoritos'">
              ♥
              <span>Favoritos{{ fav.count() ? ' (' + fav.count() + ')' : '' }}</span>
            </button>
            <label class="sort">
              Ordenar
              <select [(ngModel)]="sort" (ngModelChange)="onSort($event)" name="sort">
                <option value="discount">Mayor descuento</option>
                <option value="priceAsc">Menor precio</option>
                <option value="priceDesc">Mayor precio</option>
                <option value="newest">Más recientes</option>
                <option value="featured">Destacados</option>
              </select>
            </label>
            <button type="button" class="filters-btn" (click)="filtersOpen.set(!filtersOpen())">
              Filtros
            </button>
          </div>
        </header>

        <div class="layout">
          <aside class="filters" [class.open]="filtersOpen()">
            <div class="filters-inner">
              <label>
                Buscar
                <input
                  type="search"
                  [(ngModel)]="query"
                  (ngModelChange)="onQuery($event)"
                  name="q"
                  placeholder="Nombre, marca o referencia"
                  maxlength="80" />
              </label>

              <fieldset>
                <legend>Categoría</legend>
                <label class="check">
                  <input type="radio" name="cat" [checked]="!category()" (change)="setCategory(null)" />
                  Todas
                </label>
                @for (c of categoryOptions(); track c) {
                  <label class="check">
                    <input
                      type="radio"
                      name="cat"
                      [checked]="category() === c"
                      (change)="setCategory(c)"
                      [disabled]="!categoryHas(c)" />
                    {{ c }}
                  </label>
                }
              </fieldset>

              <fieldset>
                <legend>Tipo de casco</legend>
                <label class="check">
                  <input type="radio" name="type" [checked]="!productType()" (change)="setType(null)" />
                  Todos
                </label>
                @for (t of typeOptions(); track t) {
                  <label class="check">
                    <input
                      type="radio"
                      name="type"
                      [checked]="productType() === t"
                      (change)="setType(t)"
                      [disabled]="!typeHas(t)" />
                    {{ t }}
                  </label>
                }
              </fieldset>

              @if (brands().length) {
                <fieldset>
                  <legend>Marca</legend>
                  <label class="check">
                    <input type="radio" name="brand" [checked]="!brand()" (change)="setBrand(null)" />
                    Todas
                  </label>
                  @for (b of brands(); track b) {
                    <label class="check">
                      <input
                        type="radio"
                        name="brand"
                        [checked]="brand() === b"
                        (change)="setBrand(b)" />
                      {{ b }}
                    </label>
                  }
                </fieldset>
              }

              <fieldset class="row2">
                <legend>Precio (COP)</legend>
                <label>
                  Mín.
                  <input type="number" [(ngModel)]="minPrice" (ngModelChange)="page.set(1)" name="min" min="0" />
                </label>
                <label>
                  Máx.
                  <input type="number" [(ngModel)]="maxPrice" (ngModelChange)="page.set(1)" name="max" min="0" />
                </label>
              </fieldset>

              <fieldset>
                <legend>Descuento mínimo</legend>
                <select [(ngModel)]="minDiscount" (ngModelChange)="page.set(1)" name="disc">
                  <option [ngValue]="0">Cualquiera</option>
                  <option [ngValue]="10">Desde 10%</option>
                  <option [ngValue]="20">Desde 20%</option>
                  <option [ngValue]="30">Desde 30%</option>
                  <option [ngValue]="40">Desde 40%</option>
                </select>
              </fieldset>

              <fieldset>
                <legend>Disponibilidad</legend>
                <label class="check">
                  <input type="checkbox" [(ngModel)]="inStockOnly" (ngModelChange)="page.set(1)" name="stock" />
                  Solo disponibles
                </label>
                <label class="check">
                  <input type="checkbox" [(ngModel)]="favoritesOnly" (ngModelChange)="page.set(1)" name="fav" />
                  Solo favoritos ({{ fav.count() }})
                </label>
              </fieldset>

              <ui-button type="button" variant="secondary" (click)="resetFilters()">Limpiar filtros</ui-button>
              <button type="button" class="close-filters" (click)="filtersOpen.set(false)">Ver resultados</button>
            </div>
          </aside>

          <div class="results">
            @if (waHint()) {
              <p class="hint">Configura el WhatsApp comercial en administración para habilitar consultas.</p>
            }
            @if (paged().length === 0) {
              <div class="empty">
                <p>No hay productos con esos filtros.</p>
                <ui-button type="button" (click)="resetFilters()">Limpiar filtros</ui-button>
              </div>
            } @else {
              <div class="grid">
                @for (p of paged(); track p.id) {
                  <app-shop-product-card [product]="p" [shop]="shop()!" (whatsappMissing)="waHint.set(true)" />
                }
              </div>
              @if (totalPages() > 1) {
                <nav class="pager" aria-label="Paginación">
                  <ui-button
                    type="button"
                    variant="secondary"
                    [disabled]="page() <= 1"
                    (click)="prevPage()"
                    >Anterior</ui-button
                  >
                  <span>Página {{ page() }} de {{ totalPages() }}</span>
                  <ui-button
                    type="button"
                    variant="secondary"
                    [disabled]="page() >= totalPages()"
                    (click)="nextPage()"
                    >Siguiente</ui-button
                  >
                </nav>
              }
            }
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    .page {
      width: min(1180px, calc(100% - 2rem));
      margin: 0 auto;
      padding: 1.25rem 0 3rem;
      overflow-x: clip;
      box-sizing: border-box;
    }
    .results {
      min-width: 0;
    }
    .eyebrow {
      margin: 0;
      font-size: 0.75rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.06em;
      color: var(--color-text-secondary);
    }
    .eyebrow a {
      color: inherit;
    }
    h1 {
      margin: 0.25rem 0 0;
      font-size: clamp(1.4rem, 2.5vw, 1.85rem);
    }
    .sub {
      margin: 0.25rem 0 0;
      color: var(--color-text-secondary);
    }
    .head {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      gap: 1rem;
      margin-bottom: 1.1rem;
    }
    .head-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.55rem;
      align-items: end;
    }
    .fav-toggle {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      min-height: 2.5rem;
      padding: 0.35rem 0.75rem;
      border-radius: 999px;
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: var(--color-text-secondary);
      font: inherit;
      font-size: 0.85rem;
      font-weight: 700;
      cursor: pointer;
    }
    .fav-toggle.on {
      border-color: color-mix(in srgb, var(--color-primary) 45%, var(--color-border));
      background: color-mix(in srgb, var(--color-primary) 12%, var(--color-surface));
      color: var(--color-primary);
    }
    .sort {
      display: grid;
      gap: 0.25rem;
      font-size: 0.8rem;
      font-weight: 700;
    }
    .sort select,
    .filters input,
    .filters select {
      box-sizing: border-box;
      width: 100%;
      max-width: 100%;
      min-width: 0;
      min-height: 2.4rem;
      padding: 0.35rem 0.55rem;
      border-radius: 0.55rem;
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: inherit;
      font: inherit;
    }
    .filters .check input {
      width: auto;
      min-height: 0;
      padding: 0;
    }
    .filters-btn,
    .close-filters {
      min-height: 2.5rem;
      padding: 0 0.9rem;
      border-radius: 0.55rem;
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: inherit;
      font: inherit;
      font-weight: 700;
      cursor: pointer;
    }
    .close-filters {
      display: none;
    }
    .layout {
      display: grid;
      grid-template-columns: minmax(0, 16.5rem) minmax(0, 1fr);
      gap: 1.25rem;
      align-items: start;
    }
    .filters {
      position: sticky;
      top: 1rem;
      min-width: 0;
      max-width: 100%;
    }
    .filters-inner {
      display: grid;
      gap: 0.85rem;
      padding: 1rem;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      background: var(--color-surface);
      min-width: 0;
      max-width: 100%;
      overflow-x: clip;
      box-sizing: border-box;
    }
    .filters-inner > ui-button,
    .filters-inner > .close-filters {
      width: 100%;
      max-width: 100%;
      box-sizing: border-box;
    }
    fieldset {
      margin: 0;
      padding: 0;
      border: 0;
      display: grid;
      gap: 0.35rem;
      min-width: 0;
      max-width: 100%;
    }
    legend {
      font-size: 0.78rem;
      font-weight: 800;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      margin-bottom: 0.2rem;
      max-width: 100%;
    }
    label {
      display: grid;
      gap: 0.25rem;
      font-size: 0.85rem;
      min-width: 0;
      max-width: 100%;
    }
    .check {
      display: flex;
      align-items: flex-start;
      gap: 0.4rem;
      grid-template-columns: none;
      min-width: 0;
    }
    .check span,
    .check {
      overflow-wrap: anywhere;
    }
    .row2 {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
      gap: 0.45rem;
    }
    .row2 legend {
      grid-column: 1 / -1;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 1rem;
    }
    .empty {
      padding: 2rem 0;
      display: grid;
      gap: 0.75rem;
      justify-items: start;
      color: var(--color-text-secondary);
    }
    .hint {
      margin: 0 0 0.75rem;
      color: #b42318;
      font-size: 0.9rem;
    }
    .pager {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      align-items: center;
      margin-top: 1.25rem;
    }
    @media (max-width: 960px) {
      .layout {
        grid-template-columns: 1fr;
      }
      .filters {
        position: fixed;
        inset: 0;
        z-index: 40;
        background: rgb(0 0 0 / 0.45);
        display: none;
        padding: 1rem;
        overflow: auto;
      }
      .filters.open {
        display: block;
      }
      .filters-inner {
        max-width: 24rem;
        margin: 0 auto;
      }
      .close-filters {
        display: block;
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
export class PublicShopCatalogPage implements OnInit {
  private readonly api = inject(ShopApi);
  private readonly analytics = inject(ShopAnalyticsService);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly fav = inject(ShopFavoritesService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly shop = signal<PublicShopDto | null>(null);
  readonly category = signal<string | null>(null);
  readonly productType = signal<string | null>(null);
  readonly brand = signal<string | null>(null);
  readonly querySig = signal('');
  readonly sortSig = signal<ShopSort>('discount');
  readonly page = signal(1);
  readonly filtersOpen = signal(false);
  readonly waHint = signal(false);

  query = '';
  sort: ShopSort = 'discount';
  minPrice: number | null = null;
  maxPrice: number | null = null;
  minDiscount = 0;
  inStockOnly = false;
  favoritesOnly = false;

  readonly categoryOptions = computed(() => [...SHOP_CATEGORIES]);
  readonly typeOptions = computed(() => [...SHOP_HELMET_TYPES]);

  readonly brands = computed(() => {
    const set = new Set<string>();
    for (const p of this.shop()?.products ?? []) {
      if (p.brand?.trim()) set.add(p.brand.trim());
    }
    return [...set].sort((a, b) => a.localeCompare(b));
  });

  readonly filtered = computed(() => {
    let list = this.shop()?.products ?? [];
    const cat = this.category();
    const type = this.productType();
    const brand = this.brand();
    const q = this.querySig().trim().toLowerCase();
    const min = this.minPrice;
    const max = this.maxPrice;
    const minDisc = this.minDiscount;
    const stockOnly = this.inStockOnly;
    const favOnly = this.favoritesOnly;
    const favIds = new Set(this.fav.list());

    list = list.filter((p) => {
      if (cat && (p.category || '').trim().toLowerCase() !== cat.toLowerCase()) return false;
      if (type && (p.productType || '').trim().toLowerCase() !== type.toLowerCase()) return false;
      if (brand && (p.brand || '').trim().toLowerCase() !== brand.toLowerCase()) return false;
      if (stockOnly && p.stock <= 0) return false;
      if (favOnly && !favIds.has(p.id)) return false;
      if (min != null && min > 0 && p.priceCop < min) return false;
      if (max != null && max > 0 && p.priceCop > max) return false;
      if (minDisc > 0 && discountPercent(p) < minDisc) return false;
      if (q) {
        const hay = `${p.name} ${p.brand ?? ''} ${p.reference ?? ''} ${p.category} ${p.productType ?? ''}`.toLowerCase();
        if (!hay.includes(q)) return false;
      }
      return true;
    });

    return sortProducts(list, this.sortSig());
  });

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.filtered().length / PAGE_SIZE)));

  readonly paged = computed(() => {
    const all = this.filtered();
    const p = Math.min(this.page(), this.totalPages());
    const start = (p - 1) * PAGE_SIZE;
    return all.slice(start, start + PAGE_SIZE);
  });

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    const cat = qp.get('categoria') || qp.get('category');
    const q = qp.get('q') || '';
    const type = qp.get('tipo');
    if (cat) this.category.set(cat);
    if (type) this.productType.set(type);
    if (q) {
      this.query = q;
      this.querySig.set(q);
    }
    if (qp.get('fav') === '1') this.favoritesOnly = true;
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getPublic().subscribe({
      next: (data) => {
        this.shop.set(data);
        this.analytics.track('view_shop');
        this.title.setTitle(brandPageTitle('Catálogo — ' + data.pageTitle));
        this.meta.updateTag({ name: 'description', content: data.pageSubtitle });
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }

  categoryHas(name: string): boolean {
    return (this.shop()?.products ?? []).some(
      (p) => (p.category || '').trim().toLowerCase() === name.toLowerCase()
    );
  }

  typeHas(name: string): boolean {
    return (this.shop()?.products ?? []).some(
      (p) => (p.productType || '').trim().toLowerCase() === name.toLowerCase()
    );
  }

  toggleFavoritesOnly(): void {
    this.favoritesOnly = !this.favoritesOnly;
    this.page.set(1);
  }

  onQuery(v: string): void {
    this.querySig.set(v ?? '');
    this.page.set(1);
  }

  onSort(v: ShopSort): void {
    this.sortSig.set(v);
    this.page.set(1);
  }

  setCategory(v: string | null): void {
    this.category.set(v);
    this.page.set(1);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { categoria: v || null },
      queryParamsHandling: 'merge'
    });
  }

  setType(v: string | null): void {
    this.productType.set(v);
    this.page.set(1);
  }

  setBrand(v: string | null): void {
    this.brand.set(v);
    this.page.set(1);
  }

  prevPage(): void {
    this.page.update((n) => Math.max(1, n - 1));
  }

  nextPage(): void {
    this.page.update((n) => Math.min(this.totalPages(), n + 1));
  }

  resetFilters(): void {
    this.category.set(null);
    this.productType.set(null);
    this.brand.set(null);
    this.query = '';
    this.querySig.set('');
    this.minPrice = null;
    this.maxPrice = null;
    this.minDiscount = 0;
    this.inStockOnly = false;
    this.favoritesOnly = false;
    this.sort = 'discount';
    this.sortSig.set('discount');
    this.page.set(1);
    void this.router.navigate([], { relativeTo: this.route, queryParams: {} });
  }
}
