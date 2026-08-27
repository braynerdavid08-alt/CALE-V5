import { Component, OnInit, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { resolveMediaUrl } from '../../core/media/resolve-media-url';
import { BRAND } from '../../core/brand';
import { mapApiError } from '../../core/http/map-api-error';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import { PublicHomeApi } from './public-home.api';
import { PublicHomeDto, ResolvedStatDto } from './public.models';
import { formatStatDisplay } from './public-stat.util';

@Component({
  selector: 'app-landing-page',
  standalone: true,
  imports: [
    RouterLink,
    UiButtonComponent,
    UiErrorComponent,
    UiIconComponent,
    UiLoadingComponent
  ],
  template: `
    @if (loading()) {
      <div class="pad">
        <ui-loading label="Cargando página de inicio..." />
      </div>
    } @else if (error()) {
      <div class="pad">
        <ui-error [message]="error()" />
      </div>
    } @else {
      @if (home(); as h) {
      @if (h.hero.visible) {
        <section class="hero">
          <div class="hero-bg" aria-hidden="true"></div>
          <div class="hero-inner">
            <div class="hero-copy">
              @if (h.hero.badge) {
                <p class="badge">{{ h.hero.badge }}</p>
              }
              <h1>
                {{ h.hero.title }}
                @if (h.hero.titleHighlight) {
                  <span class="hl"> {{ h.hero.titleHighlight }}</span>
                }
              </h1>
              <p class="lead">{{ h.hero.description }}</p>
              <div class="cta-row">
                <a [routerLink]="h.hero.ctaPrimaryPath || '/register'">
                  <ui-button type="button">
                    {{ h.hero.ctaPrimaryLabel || 'Comenzar ahora' }}
                  </ui-button>
                </a>
                @if (h.hero.videoUrl) {
                  <a
                    class="video-btn"
                    [href]="h.hero.videoUrl"
                    target="_blank"
                    rel="noopener noreferrer">
                    <ui-button type="button" variant="secondary">
                      <ui-icon name="play" />
                      {{ h.hero.ctaSecondaryLabel || 'Ver video' }}
                    </ui-button>
                  </a>
                }
              </div>
            </div>
            @if (h.hero.imageEnabled && heroImageUrl(h)) {
              <div class="hero-media">
                <img
                  [src]="heroImageUrl(h)"
                  [alt]="h.hero.imageAlt || brand.name"
                  loading="eager" />
              </div>
            }
          </div>
        </section>
      }

      @if (h.benefits.length) {
        <section class="section">
          <div class="wrap">
            <header class="sec-head">
              <h2>Beneficios de formarte con {{ brand.name }}</h2>
              <p>Todo lo que necesitas para avanzar en tu licencia, en un solo lugar.</p>
            </header>
            <div class="benefits">
              @for (b of h.benefits; track b.id) {
                <article class="benefit" [attr.data-tone]="b.tone || 'blue'">
                  <div class="tone-icon" aria-hidden="true">
                    <ui-icon [name]="b.icon || 'book'" />
                  </div>
                  <h3>{{ b.title }}</h3>
                  <p>{{ b.description }}</p>
                </article>
              }
            </div>
          </div>
        </section>
      }

      @if (h.stepsVisible && h.steps.length) {
        <section class="section alt">
          <div class="wrap">
            <header class="sec-head">
              <h2>{{ h.stepsTitle }}</h2>
              <p>{{ h.stepsSubtitle }}</p>
            </header>
            <ol class="steps">
              @for (s of h.steps; track s.id) {
                <li class="step" [attr.data-tone]="s.tone || 'blue'">
                  <span class="step-num">{{ s.number || $index + 1 }}</span>
                  <div class="step-icon" aria-hidden="true">
                    <ui-icon [name]="s.icon || 'users'" />
                  </div>
                  <h3>{{ s.title }}</h3>
                  <p>{{ s.description }}</p>
                </li>
              }
            </ol>
          </div>
        </section>
      }

      @if (visibleStats(h).length) {
        <section class="stats-bar">
          <div class="wrap stats-grid">
            @for (st of visibleStats(h); track st.key) {
              <div class="stat">
                <div class="stat-icon" aria-hidden="true">
                  <ui-icon [name]="st.icon || 'users'" />
                </div>
                <p class="stat-value">{{ formatStat(st) }}</p>
                <p class="stat-label">{{ st.label }}</p>
              </div>
            }
          </div>
        </section>
      }

      @if (h.schoolsVisible) {
        <section class="section">
          <div class="wrap">
            <header class="sec-head row">
              <div>
                <h2>Escuelas aliadas</h2>
                <p>Formación presencial y acompañamiento con escuelas del ecosistema CALE.</p>
              </div>
              <a routerLink="/escuelas">
                <ui-button type="button" variant="secondary">Ver todas</ui-button>
              </a>
            </header>
            @if (h.schools.length) {
              <div class="cards">
                @for (school of h.schools; track school.id) {
                  <article class="card">
                    <h3>{{ school.name }}</h3>
                    <p class="muted">
                      {{ school.city }}{{ school.department ? ', ' + school.department : '' }}
                    </p>
                  </article>
                }
              </div>
            } @else {
              <p class="empty">Pronto verás escuelas publicadas aquí.</p>
            }
          </div>
        </section>
      }

      @if (h.instructorsVisible) {
        <section class="section alt">
          <div class="wrap">
            <header class="sec-head row">
              <div>
                <h2>Instructores</h2>
                <p>Instructores y formadores que acompañan tu proceso teórico y práctico.</p>
              </div>
              <a routerLink="/instructores">
                <ui-button type="button" variant="secondary">Ver todos</ui-button>
              </a>
            </header>
            @if (h.instructors.length) {
              <div class="cards">
                @for (ins of h.instructors; track ins.id) {
                  <article class="card">
                    <div class="avatar" aria-hidden="true">
                      <ui-icon name="instructor" />
                    </div>
                    <h3>{{ ins.displayName }}</h3>
                    <p class="muted">{{ ins.schoolName || ('Instructor ' + brand.name) }}</p>
                  </article>
                }
              </div>
            } @else {
              <p class="empty">Pronto verás instructores publicados aquí.</p>
            }
          </div>
        </section>
      }

      <section class="cta-band">
        <div class="wrap cta-band-inner">
          <div>
            <h2>Empieza tu formación hoy</h2>
            <p>Crea tu cuenta y accede a simuladores, contenidos y escuelas aliadas.</p>
          </div>
          <div class="cta-row">
            <a routerLink="/register">
              <ui-button type="button">Registrarme</ui-button>
            </a>
            <a routerLink="/contacto">
              <ui-button type="button" variant="secondary">Contacto</ui-button>
            </a>
          </div>
        </div>
      </section>
      }
    }
  `,
  styleUrl: './landing.page.css'
})
export class LandingPage implements OnInit {
  readonly brand = BRAND;
  private readonly api = inject(PublicHomeApi);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly home = signal<PublicHomeDto | null>(null);

  ngOnInit(): void {
    this.api.getHome().subscribe({
      next: (data) => {
        this.home.set(data);
        this.applySeo(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }

  heroImageUrl(h: PublicHomeDto): string {
    return resolveMediaUrl(h.hero.imageUrl);
  }

  visibleStats(h: PublicHomeDto): ResolvedStatDto[] {
    return [...h.stats]
      .filter((s) => s.visible)
      .sort((a, b) => a.sortOrder - b.sortOrder);
  }

  formatStat(stat: ResolvedStatDto): string {
    return formatStatDisplay(stat);
  }

  private applySeo(data: PublicHomeDto): void {
    const t = data.seoTitle?.trim() || BRAND.seoTitle;
    const d = data.seoDescription?.trim() || BRAND.seoDescription;
    this.title.setTitle(t);
    this.meta.updateTag({ name: 'description', content: d });
  }
}
