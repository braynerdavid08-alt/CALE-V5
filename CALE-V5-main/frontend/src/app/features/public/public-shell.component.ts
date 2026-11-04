import { CurrencyPipe } from '@angular/common';
import {
  Component,
  HostListener,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { resolveMediaUrl } from '../../core/media/resolve-media-url';
import { BRAND } from '../../core/brand';
import { SessionStore } from '../../core/auth/session.store';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { UiThemeToggleComponent } from '../../shared/ui/ui-theme-toggle.component';
import { ShopApi } from './shop.api';
import { PublicShopProductDto } from './shop.models';

@Component({
  selector: 'app-public-shell',
  standalone: true,
  imports: [
    CurrencyPipe,
    FormsModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    UiButtonComponent,
    UiIconComponent,
    UiThemeToggleComponent
  ],
  template: `
    <div class="public-shell">
      <header class="top">
        <div class="top-inner">
          <a routerLink="/" class="brand" aria-label="Inicio — Luz Verde" (click)="closeMenu()">
            <img
              class="brand-logo"
              [src]="brand.logoWideDark"
              [alt]="brand.name + ' — ' + brand.slogan"
              width="790"
              height="316" />
          </a>

          <nav class="nav desktop" aria-label="Principal">
            <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Inicio</a>
            <a routerLink="/escuelas" routerLinkActive="active">Escuelas vinculadas</a>
            <a routerLink="/register-school" routerLinkActive="active">Registra Tu CEA</a>
            <a routerLink="/tienda" routerLinkActive="active">Tienda</a>
          </nav>

          <div class="search desktop" [class.open]="searchOpen()">
            <label class="search-field">
              <ui-icon name="search" />
              <input
                type="search"
                name="shopSearch"
                autocomplete="off"
                placeholder="Buscar en la tienda..."
                maxlength="80"
                [ngModel]="searchQuery()"
                (ngModelChange)="onSearchInput($event)"
                (focus)="onSearchFocus()"
                (keydown.enter)="submitSearch($event)"
                (keydown.escape)="closeSearch()"
                aria-label="Buscar productos en la tienda"
                [attr.aria-expanded]="searchOpen()"
                aria-controls="shop-search-results"
                aria-autocomplete="list" />
            </label>
            @if (searchOpen() && searchQuery().trim()) {
              <div id="shop-search-results" class="search-dropdown" role="listbox">
                @if (searchResults().length) {
                  @for (p of searchResults(); track p.id) {
                    <button
                      type="button"
                      class="search-item"
                      role="option"
                      (mousedown)="goToProduct(p, $event)">
                      <span class="search-thumb">
                        @if (productImage(p)) {
                          <img [src]="productImage(p)" alt="" width="40" height="40" />
                        }
                      </span>
                      <span class="search-meta">
                        <strong>{{ p.name }}</strong>
                        <small>{{ p.category || 'Tienda' }} · {{ p.priceCop | currency: 'COP' : 'symbol-narrow' : '1.0-0' }}</small>
                      </span>
                    </button>
                  }
                  <a
                    class="search-all"
                    [routerLink]="['/tienda/catalogo']"
                    [queryParams]="{ q: searchQuery().trim() }"
                    (click)="closeSearch()">
                    Ver todos los resultados
                  </a>
                } @else {
                  <p class="search-empty">Sin coincidencias en el catálogo.</p>
                }
              </div>
            }
          </div>

          <div class="actions desktop">
            <span class="theme-slot" title="Tema claro u oscuro">
              <ui-theme-toggle />
            </span>
            @if (loggedIn()) {
              <ui-button [routerLink]="accountPath()" type="button">Mi cuenta</ui-button>
            } @else {
              <a routerLink="/login" class="link-login">Iniciar sesión</a>
              <ui-button routerLink="/register" type="button">Registrarme</ui-button>
            }
          </div>

          <button
            type="button"
            class="burger"
            [attr.aria-expanded]="menuOpen()"
            aria-controls="public-mobile-nav"
            aria-label="Menú"
            (click)="toggleMenu()">
            <ui-icon [name]="menuOpen() ? 'close' : 'menu'" />
          </button>
        </div>

        @if (menuOpen()) {
          <div id="public-mobile-nav" class="mobile-panel">
            <div class="search mobile-search" [class.open]="searchOpen()">
              <label class="search-field">
                <ui-icon name="search" />
                <input
                  type="search"
                  name="shopSearchMobile"
                  autocomplete="off"
                  placeholder="Buscar en la tienda..."
                  maxlength="80"
                  [ngModel]="searchQuery()"
                  (ngModelChange)="onSearchInput($event)"
                  (focus)="onSearchFocus()"
                  (keydown.enter)="submitSearch($event)"
                  aria-label="Buscar productos en la tienda" />
              </label>
              @if (searchOpen() && searchQuery().trim()) {
                <div class="search-dropdown" role="listbox">
                  @if (searchResults().length) {
                    @for (p of searchResults(); track p.id) {
                      <button
                        type="button"
                        class="search-item"
                        role="option"
                        (mousedown)="goToProduct(p, $event)">
                        <span class="search-thumb">
                          @if (productImage(p)) {
                            <img [src]="productImage(p)" alt="" width="40" height="40" />
                          }
                        </span>
                        <span class="search-meta">
                          <strong>{{ p.name }}</strong>
                          <small>{{ p.category || 'Tienda' }}</small>
                        </span>
                      </button>
                    }
                    <a
                      class="search-all"
                      [routerLink]="['/tienda/catalogo']"
                      [queryParams]="{ q: searchQuery().trim() }"
                      (click)="closeMenu(); closeSearch()">
                      Ver todos los resultados
                    </a>
                  } @else {
                    <p class="search-empty">Sin coincidencias en el catálogo.</p>
                  }
                </div>
              }
            </div>

            <nav aria-label="Móvil">
              <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }" (click)="closeMenu()">Inicio</a>
              <a routerLink="/escuelas" routerLinkActive="active" (click)="closeMenu()">Escuelas vinculadas</a>
              <a routerLink="/register-school" routerLinkActive="active" (click)="closeMenu()">Registra Tu CEA</a>
              <a routerLink="/tienda" routerLinkActive="active" (click)="closeMenu()">Tienda</a>
            </nav>
            <div class="mobile-actions">
              <span class="theme-slot">
                <ui-theme-toggle />
              </span>
              @if (loggedIn()) {
                <a [routerLink]="accountPath()" (click)="closeMenu()">
                  <ui-button type="button">Mi cuenta</ui-button>
                </a>
              } @else {
                <a routerLink="/login" class="link-login" (click)="closeMenu()">Iniciar sesión</a>
                <a routerLink="/register" (click)="closeMenu()">
                  <ui-button type="button">Registrarme</ui-button>
                </a>
              }
            </div>
          </div>
        }
      </header>

      <main class="main">
        <router-outlet />
      </main>

      <footer class="site-footer">
        <div class="footer-inner">
          <div class="footer-brand">
            <p class="footer-name">{{ brand.name }}</p>
            <p class="footer-tag">{{ brand.slogan }}</p>
            <p class="footer-pitch">
              Plataforma digital para CEA y escuelas de conducción en Colombia.
            </p>
          </div>
          <nav class="footer-nav" aria-label="Pie de página">
            <a routerLink="/cursos">Cursos</a>
            <a routerLink="/escuelas">Escuelas</a>
            <a routerLink="/instructores">Instructores</a>
            <a routerLink="/contacto">Contacto / Demo CEA</a>
            <a routerLink="/register-school">Registra Tu CEA</a>
            <a routerLink="/blog">Blog</a>
            <a routerLink="/tienda">Tienda</a>
            <a routerLink="/nosotros">Nosotros</a>
            <a routerLink="/apoyar">Apoyar</a>
            <a routerLink="/pqr">PQR</a>
          </nav>
        </div>
      </footer>
    </div>
  `,
  styleUrl: './public-shell.component.css'
})
export class PublicShellComponent implements OnInit {
  private readonly session = inject(SessionStore);
  private readonly shopApi = inject(ShopApi);
  private readonly router = inject(Router);

  readonly brand = BRAND;
  readonly menuOpen = signal(false);
  readonly loggedIn = computed(() => this.session.isAuthenticated());
  readonly accountPath = computed(() => this.session.homeRoute());

  readonly searchQuery = signal('');
  readonly searchOpen = signal(false);
  readonly catalog = signal<PublicShopProductDto[]>([]);

  readonly searchResults = computed(() => {
    const q = this.searchQuery().trim().toLowerCase();
    if (!q) return [] as PublicShopProductDto[];
    return this.catalog()
      .filter((p) => {
        const hay = `${p.name} ${p.category ?? ''} ${p.brand ?? ''} ${p.reference ?? ''} ${p.productType ?? ''}`.toLowerCase();
        return hay.includes(q);
      })
      .slice(0, 6);
  });

  ngOnInit(): void {
    this.shopApi.getPublic().subscribe({
      next: (s) => this.catalog.set(s.products ?? []),
      error: () => this.catalog.set([])
    });
  }

  onSearchInput(value: string): void {
    this.searchQuery.set(value ?? '');
    this.searchOpen.set(true);
  }

  onSearchFocus(): void {
    this.searchOpen.set(true);
  }

  closeSearch(): void {
    this.searchOpen.set(false);
  }

  submitSearch(event: Event): void {
    event.preventDefault();
    const q = this.searchQuery().trim();
    const first = this.searchResults()[0];
    this.closeSearch();
    this.closeMenu();
    if (first && this.searchResults().length === 1) {
      void this.router.navigate(['/tienda', first.slug]);
      return;
    }
    void this.router.navigate(['/tienda/catalogo'], { queryParams: q ? { q } : {} });
  }

  goToProduct(p: PublicShopProductDto, event: Event): void {
    event.preventDefault();
    this.closeSearch();
    this.closeMenu();
    void this.router.navigate(['/tienda', p.slug]);
  }

  productImage(p: PublicShopProductDto): string {
    const url = p.imageUrls?.[0] || p.imageUrl;
    return url ? resolveMediaUrl(url) : '';
  }

  toggleMenu(): void {
    this.menuOpen.update((v) => !v);
  }

  closeMenu(): void {
    this.menuOpen.set(false);
  }

  @HostListener('window:resize')
  onResize(): void {
    if (typeof window !== 'undefined' && window.innerWidth > 960) {
      this.menuOpen.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeSearch();
    this.closeMenu();
  }

  @HostListener('document:click', ['$event'])
  onDocClick(event: MouseEvent): void {
    const target = event.target as HTMLElement | null;
    if (!target?.closest('.search')) {
      this.closeSearch();
    }
  }
}
