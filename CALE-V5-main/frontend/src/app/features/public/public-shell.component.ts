import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { BRAND } from '../../core/brand';
import { SessionStore } from '../../core/auth/session.store';
import { env } from '../../core/config/env';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { UiThemeToggleComponent } from '../../shared/ui/ui-theme-toggle.component';
import { LEGAL_LINKS } from '../legal/legal-links';
import { PublicHomeApi } from './public-home.api';
import { PublicCourseCardDto } from './public.models';

type OpenMenu = 'cursos' | 'instructores' | null;

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
          <a routerLink="/" class="brand" (click)="closeAll()">
            <img class="brand-logo" [src]="brand.logoWideDark" [alt]="brand.name + ' — ' + brand.slogan" width="790" height="316" />
          </a>

          <nav class="nav desktop" aria-label="Principal">
            @for (link of simpleLinks; track link.path) {
              <a
                [routerLink]="link.path"
                routerLinkActive="active"
                [routerLinkActiveOptions]="link.exact ? { exact: true } : { exact: false }">
                {{ link.label }}
              </a>
            }

            <div class="nav-item" (mouseenter)="openDesktop('cursos')" (mouseleave)="scheduleClose()">
              <button
                type="button"
                class="nav-trigger"
                [class.active]="openMenu() === 'cursos'"
                [attr.aria-expanded]="openMenu() === 'cursos'"
                (click)="toggleDesktop('cursos')">
                Cursos
                <span class="chev" aria-hidden="true">▾</span>
              </button>
              @if (openMenu() === 'cursos') {
                <div class="mega" role="menu" (mouseenter)="cancelClose()">
                  <div class="mega-head">
                    <div>
                      <p class="mega-kicker">Malla Luz Verde</p>
                      <h2>Cursos de formación vial</h2>
                    </div>
                    <a routerLink="/cursos" class="mega-all" (click)="closeAll()">Ver todos</a>
                  </div>
                  <div class="mega-grid">
                    @for (c of courses(); track c.slug) {
                      <a class="mega-card" routerLink="/cursos" [fragment]="c.slug" (click)="closeAll()">
                        <img [src]="coverSrc(c.coverUrl)" [alt]="''" width="72" height="72" loading="lazy" />
                        <span>
                          <strong>{{ c.title }}</strong>
                          <small>{{ c.category }} · {{ c.lessonCount }} lecciones</small>
                        </span>
                      </a>
                    }
                  </div>
                  <p class="mega-note">El contenido completo se abre al iniciar sesión.</p>
                </div>
              }
            </div>

            <a routerLink="/escuelas" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Escuelas</a>

            <div class="nav-item" (mouseenter)="openDesktop('instructores')" (mouseleave)="scheduleClose()">
              <button
                type="button"
                class="nav-trigger"
                [class.active]="openMenu() === 'instructores'"
                [attr.aria-expanded]="openMenu() === 'instructores'"
                (click)="toggleDesktop('instructores')">
                Instructores
                <span class="chev" aria-hidden="true">▾</span>
              </button>
              @if (openMenu() === 'instructores') {
                <div class="flyout" role="menu" (mouseenter)="cancelClose()">
                  <a routerLink="/instructores" (click)="closeAll()">Ver directorio</a>
                  <a routerLink="/instructores" [queryParams]="{ accion: 'contratar' }" (click)="closeAll()">
                    Contratar clase particular
                  </a>
                  <a routerLink="/instructores" [queryParams]="{ accion: 'calificar' }" (click)="closeAll()">
                    Calificar instructor
                  </a>
                </div>
              }
            </div>

            <a routerLink="/contacto" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Contacto</a>
            <a routerLink="/apoyar" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }" class="nav-support">
              Apoyar Luz Verde
            </a>
          </nav>

          <div class="actions desktop">
            <ui-theme-toggle />
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
              @for (link of simpleLinks; track link.path) {
                <a [routerLink]="link.path" (click)="closeAll()">{{ link.label }}</a>
              }
              <details class="mobile-group" [open]="mobileGroup() === 'cursos'">
                <summary (click)="toggleMobileGroup('cursos', $event)">Cursos</summary>
                <div class="mobile-courses">
                  @for (c of courses(); track c.slug) {
                    <a routerLink="/cursos" [fragment]="c.slug" (click)="closeAll()">
                      <img [src]="coverSrc(c.coverUrl)" alt="" width="40" height="40" loading="lazy" />
                      <span>{{ c.title }}</span>
                    </a>
                  }
                  <a routerLink="/cursos" class="mobile-all" (click)="closeAll()">Ver todos los cursos</a>
                </div>
              </details>
              <a routerLink="/escuelas" (click)="closeAll()">Escuelas</a>
              <details class="mobile-group" [open]="mobileGroup() === 'instructores'">
                <summary (click)="toggleMobileGroup('instructores', $event)">Instructores</summary>
                <div class="mobile-sub">
                  <a routerLink="/instructores" (click)="closeAll()">Ver directorio</a>
                  <a routerLink="/instructores" [queryParams]="{ accion: 'contratar' }" (click)="closeAll()">Contratar clase particular</a>
                  <a routerLink="/instructores" [queryParams]="{ accion: 'calificar' }" (click)="closeAll()">Calificar instructor</a>
                </div>
              </details>
              <a routerLink="/contacto" (click)="closeAll()">Contacto</a>
              <a routerLink="/apoyar" (click)="closeAll()">Apoyar Luz Verde</a>
            </nav>
            <div class="mobile-actions">
              <ui-theme-toggle />
              @if (loggedIn()) {
                <a [routerLink]="accountPath()" (click)="closeAll()">
                  <ui-button type="button">Mi cuenta</ui-button>
                </a>
              } @else {
                <a routerLink="/login" (click)="closeAll()">Iniciar sesión</a>
                <a routerLink="/register" (click)="closeAll()">
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
            <img [src]="brand.logoWide" [alt]="brand.name + ' — ' + brand.slogan" width="790" height="316" loading="lazy" />
          </a>
          <nav class="foot-nav" aria-label="Pie">
            @for (link of footerLinks; track link.path) {
              <a [routerLink]="link.path">{{ link.label }}</a>
            }
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
export class PublicShellComponent implements OnInit {
  private readonly session = inject(SessionStore);
  private readonly api = inject(PublicHomeApi);
  readonly brand = BRAND;
  readonly legalLinks = LEGAL_LINKS;
  readonly menuOpen = signal(false);
  readonly openMenu = signal<OpenMenu>(null);
  readonly mobileGroup = signal<OpenMenu>(null);
  readonly courses = signal<PublicCourseCardDto[]>([]);
  readonly loggedIn = computed(() => this.session.isAuthenticated());
  readonly accountPath = computed(() => this.session.homeRoute());

  private closeTimer: ReturnType<typeof setTimeout> | null = null;

  readonly simpleLinks = [
    { label: 'Inicio', path: '/', exact: true },
    { label: 'Nosotros', path: '/nosotros', exact: true }
  ];

  readonly footerLinks = [
    { label: 'Inicio', path: '/' },
    { label: 'Nosotros', path: '/nosotros' },
    { label: 'Cursos', path: '/cursos' },
    { label: 'Escuelas', path: '/escuelas' },
    { label: 'Instructores', path: '/instructores' },
    { label: 'Contacto', path: '/contacto' },
    { label: 'Apoyar Luz Verde', path: '/apoyar' }
  ];

  ngOnInit(): void {
    this.api.listCourses().subscribe({
      next: (list) => this.courses.set(list),
      error: () => this.courses.set([])
    });
  }

  coverSrc(url: string): string {
    if (!url) return '';
    if (url.startsWith('http')) return url;
    return `${env.apiUrl}${url.startsWith('/') ? url : '/' + url}`;
  }

  openDesktop(menu: OpenMenu): void {
    this.cancelClose();
    this.openMenu.set(menu);
  }

  toggleDesktop(menu: OpenMenu): void {
    this.openMenu.update((v) => (v === menu ? null : menu));
  }

  scheduleClose(): void {
    this.cancelClose();
    this.closeTimer = setTimeout(() => this.openMenu.set(null), 180);
  }

  cancelClose(): void {
    if (this.closeTimer) {
      clearTimeout(this.closeTimer);
      this.closeTimer = null;
    }
  }

  toggleMenu(): void {
    this.menuOpen.update((v) => !v);
    if (!this.menuOpen()) {
      this.mobileGroup.set(null);
    }
  }

  toggleMobileGroup(group: OpenMenu, event: Event): void {
    event.preventDefault();
    this.mobileGroup.update((v) => (v === group ? null : group));
  }

  closeAll(): void {
    this.cancelClose();
    this.menuOpen.set(false);
    this.openMenu.set(null);
    this.mobileGroup.set(null);
  }

  @HostListener('window:resize')
  onResize(): void {
    if (window.innerWidth > 960) {
      this.menuOpen.set(false);
      this.mobileGroup.set(null);
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeAll();
  }
}
