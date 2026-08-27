import { Component, HostListener, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { BRAND } from '../../core/brand';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { UiThemeToggleComponent } from '../../shared/ui/ui-theme-toggle.component';

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
          <a routerLink="/" class="brand" (click)="closeMenu()">
            <img class="brand-mark" [src]="brand.icon192" [alt]="brand.name" width="36" height="36" />
            <span class="brand-stack">
              <span class="brand-name">{{ brand.name }}</span>
              <span class="brand-tag">{{ brand.sloganShort }}</span>
            </span>
          </a>

          <nav class="nav desktop" aria-label="Principal">
            @for (link of links; track link.path) {
              <a
                [routerLink]="link.path"
                routerLinkActive="active"
                [routerLinkActiveOptions]="link.exact ? { exact: true } : { exact: false }">
                {{ link.label }}
              </a>
            }
          </nav>

          <div class="actions desktop">
            <ui-theme-toggle />
            <a routerLink="/login" class="link-login">Iniciar sesión</a>
            <a routerLink="/register">
              <ui-button type="button">Registrarme</ui-button>
            </a>
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
              @for (link of links; track link.path) {
                <a
                  [routerLink]="link.path"
                  routerLinkActive="active"
                  [routerLinkActiveOptions]="link.exact ? { exact: true } : { exact: false }"
                  (click)="closeMenu()">
                  {{ link.label }}
                </a>
              }
            </nav>
            <div class="mobile-actions">
              <ui-theme-toggle />
              <a routerLink="/login" (click)="closeMenu()">Iniciar sesión</a>
              <a routerLink="/register" (click)="closeMenu()">
                <ui-button type="button">Registrarme</ui-button>
              </a>
            </div>
          </div>
        }
      </header>

      <main class="main">
        <router-outlet />
      </main>

      <footer class="foot">
        <div class="foot-inner">
          <p class="foot-brand">{{ brand.name }}</p>
          <p class="foot-copy">{{ brand.slogan }}</p>
          <nav class="foot-nav" aria-label="Pie">
            @for (link of links; track link.path) {
              <a [routerLink]="link.path">{{ link.label }}</a>
            }
          </nav>
        </div>
      </footer>
    </div>
  `,
  styleUrl: './public-shell.component.css'
})
export class PublicShellComponent {
  readonly brand = BRAND;
  readonly menuOpen = signal(false);

  readonly links = [
    { label: 'Inicio', path: '/', exact: true },
    { label: 'Nosotros', path: '/nosotros', exact: true },
    { label: 'Cursos', path: '/cursos', exact: true },
    { label: 'Escuelas', path: '/escuelas', exact: true },
    { label: 'Instructores', path: '/instructores', exact: true },
    { label: 'Blog', path: '/blog', exact: true },
    { label: 'Contacto', path: '/contacto', exact: true }
  ];

  toggleMenu(): void {
    this.menuOpen.update((v) => !v);
  }

  closeMenu(): void {
    this.menuOpen.set(false);
  }

  @HostListener('window:resize')
  onResize(): void {
    if (window.innerWidth > 960) {
      this.closeMenu();
    }
  }
}
