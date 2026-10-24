import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { BRAND } from '../../core/brand';
import { SessionStore } from '../../core/auth/session.store';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { UiThemeToggleComponent } from '../../shared/ui/ui-theme-toggle.component';
import { LEGAL_LINKS } from '../legal/legal-links';

@Component({
  selector: 'app-public-shell',
  standalone: true,
  imports: [
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
            <a routerLink="/cascos" routerLinkActive="active" class="nav-shop">
              Tienda
              <span class="nav-promo">Ofertas</span>
            </a>
            <a routerLink="/contacto" routerLinkActive="active">Para CEA</a>
            <a routerLink="/nosotros" routerLinkActive="active">Nosotros</a>
          </nav>

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
            <nav aria-label="Móvil">
              <a routerLink="/" (click)="closeMenu()">Inicio</a>
              <a routerLink="/cascos" (click)="closeMenu()">
                Tienda <span class="nav-promo">Ofertas</span>
              </a>
              <a routerLink="/contacto" (click)="closeMenu()">Para CEA</a>
              <a routerLink="/nosotros" (click)="closeMenu()">Nosotros</a>
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

      <footer class="foot">
        <div class="foot-inner">
          <a routerLink="/" class="foot-brand">
            <img
              [src]="brand.logoWide"
              [alt]="brand.name + ' — ' + brand.slogan"
              width="790"
              height="316"
              loading="lazy" />
          </a>
          <nav class="foot-nav" aria-label="Pie">
            <a routerLink="/">Inicio</a>
            <a routerLink="/cascos">Tienda</a>
            <a routerLink="/contacto">Para CEA</a>
            <a routerLink="/nosotros">Nosotros</a>
            <a routerLink="/cursos">Cursos</a>
            <a routerLink="/escuelas">Escuelas</a>
            <a routerLink="/instructores">Instructores</a>
            <a routerLink="/contacto">Contacto</a>
            <a routerLink="/apoyar">Apoyar Luz Verde</a>
          </nav>
          <nav class="foot-nav foot-legal" aria-label="Documentos legales">
            @for (link of legalLinks; track link.key) {
              <a [routerLink]="['/legal', link.key]">{{ link.label }}</a>
            }
            <a routerLink="/pqr">PQR y datos personales</a>
          </nav>
        </div>
      </footer>
    </div>
  `,
  styleUrl: './public-shell.component.css'
})
export class PublicShellComponent {
  private readonly session = inject(SessionStore);
  readonly brand = BRAND;
  readonly legalLinks = LEGAL_LINKS;
  readonly menuOpen = signal(false);
  readonly loggedIn = computed(() => this.session.isAuthenticated());
  readonly accountPath = computed(() => this.session.homeRoute());

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
