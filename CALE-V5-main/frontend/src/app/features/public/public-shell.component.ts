import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { BRAND } from '../../core/brand';
import { SessionStore } from '../../core/auth/session.store';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { UiThemeToggleComponent } from '../../shared/ui/ui-theme-toggle.component';
import { PwaInstallButtonComponent } from '../../shared/pwa/pwa-install-button.component';
import { ShopFavoritesService } from './shop-favorites.service';

@Component({
  selector: 'app-public-shell',
  standalone: true,
  imports: [
    FormsModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    UiButtonComponent,
    UiIconComponent,
    UiThemeToggleComponent,
    PwaInstallButtonComponent
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
            <a routerLink="/tienda" routerLinkActive="active" class="nav-shop">
              Tienda
              <span class="nav-promo">Ofertas</span>
            </a>
            <a routerLink="/contacto" routerLinkActive="active">Para CEA</a>
            <a routerLink="/nosotros" routerLinkActive="active">Nosotros</a>
          </nav>

          <form class="shell-search desktop" (ngSubmit)="goSearch()">
            <label class="sr" for="shell-shop-q">Buscar productos</label>
            <input
              id="shell-shop-q"
              type="search"
              name="q"
              [(ngModel)]="searchQ"
              placeholder="Buscar en la tienda…"
              maxlength="80" />
          </form>

          <div class="actions desktop">
            <a
              class="icon-link"
              routerLink="/tienda/catalogo"
              [queryParams]="{ fav: '1' }"
              [attr.aria-label]="'Favoritos (' + fav.count() + ')'"
              title="Favoritos">
              ♥
              @if (fav.count()) {
                <span class="badge">{{ fav.count() }}</span>
              }
            </a>
            <span class="theme-slot" title="Tema claro u oscuro">
              <ui-theme-toggle />
            </span>
            <app-pwa-install-button variant="compact" />
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
            <nav aria-label="Móvil">
              <a routerLink="/" (click)="closeMenu()">Inicio</a>
              <a routerLink="/tienda" (click)="closeMenu()">
                Tienda <span class="nav-promo">Ofertas</span>
              </a>
              <a routerLink="/contacto" (click)="closeMenu()">Para CEA</a>
              <a routerLink="/nosotros" (click)="closeMenu()">Nosotros</a>
              <a routerLink="/tienda/catalogo" [queryParams]="{ fav: '1' }" (click)="closeMenu()">
                Favoritos ({{ fav.count() }})
              </a>
            </nav>
            <form class="shell-search mobile" (ngSubmit)="goSearch(); closeMenu()">
              <label class="sr" for="shell-shop-q-m">Buscar productos</label>
              <input
                id="shell-shop-q-m"
                type="search"
                name="qm"
                [(ngModel)]="searchQ"
                placeholder="Buscar en la tienda…"
                maxlength="80" />
            </form>
            <div class="mobile-actions">
              <span class="theme-slot">
                <ui-theme-toggle />
              </span>
              <app-pwa-install-button variant="default" buttonVariant="secondary" />
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
    </div>
  `,
  styleUrl: './public-shell.component.css'
})
export class PublicShellComponent {
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  readonly fav = inject(ShopFavoritesService);
  readonly brand = BRAND;
  readonly menuOpen = signal(false);
  readonly loggedIn = computed(() => this.session.isAuthenticated());
  readonly accountPath = computed(() => this.session.homeRoute());
  searchQ = '';

  goSearch(): void {
    const q = this.searchQ.trim();
    void this.router.navigate(['/tienda/catalogo'], {
      queryParams: q ? { q } : {}
    });
    this.closeMenu();
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
    this.closeMenu();
  }
}
