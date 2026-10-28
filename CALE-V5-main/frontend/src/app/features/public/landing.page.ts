import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { resolveMediaUrl } from '../../core/media/resolve-media-url';
import { BRAND } from '../../core/brand';
import { SessionStore } from '../../core/auth/session.store';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import { PwaInstallButtonComponent } from '../../shared/pwa/pwa-install-button.component';
import { PublicHomeApi } from './public-home.api';
import { PublicHomeDto, PublicTestimonialsDto } from './public.models';
import { ShopApi } from './shop.api';
import { PublicShopDto, PublicShopProductDto } from './shop.models';

@Component({
  selector: 'app-landing-page',
  standalone: true,
  imports: [
    CurrencyPipe,
    RouterLink,
    UiButtonComponent,
    UiIconComponent,
    UiLoadingComponent,
    PwaInstallButtonComponent
  ],
  template: `
    @if (loading()) {
      <div class="pad">
        <ui-loading label="Cargando página de inicio..." />
      </div>
    } @else {
      @if (home(); as h) {
        <div class="home-lead">
          @if (h.hero.visible) {
            <section
              class="hero"
              [class.has-vitrine-left]="vitrineOffers().length > 0"
              [class.has-vitrine-right]="vitrineFeatured().length > 0"
              aria-labelledby="home-heading">
              <div class="hero-bg" aria-hidden="true">
                <span class="glow g1"></span>
                <span class="glow g2"></span>
                <span class="grid-lines"></span>
              </div>

              @if (vitrineOffers().length) {
                <aside class="vitrine vitrine-left" aria-label="En oferta">
                  <p class="vitrine-title">En oferta</p>
                  <div class="vitrine-list">
                    @for (p of vitrineOffers(); track p.id) {
                      <a class="vitrine-item" routerLink="/tienda">
                        <span class="vitrine-thumb">
                          @if (productImage(p)) {
                            <img [src]="productImage(p)" [alt]="''" width="56" height="56" loading="lazy" />
                          }
                        </span>
                        <span class="vitrine-meta">
                          @if (productBadge(p); as badge) {
                            <span class="vitrine-badge">{{ badge }}</span>
                          }
                          <strong>{{ p.name }}</strong>
                          <span class="vitrine-price">
                            {{ p.priceCop | currency: 'COP' : 'symbol-narrow' : '1.0-0' }}
                            @if (p.compareAtPriceCop && p.compareAtPriceCop > p.priceCop) {
                              <s>{{ p.compareAtPriceCop | currency: 'COP' : 'symbol-narrow' : '1.0-0' }}</s>
                            }
                          </span>
                        </span>
                      </a>
                    }
                  </div>
                  <a class="vitrine-more" routerLink="/tienda">Ver tienda →</a>
                </aside>
              }

              <div class="hero-inner">
                <div class="hero-copy">
                  <p class="eyebrow home-kicker">Inicio Luz Verde</p>
                  @if (h.hero.badge && !isUnverifiableBadge(h.hero.badge)) {
                    <p class="badge"><span class="dot" aria-hidden="true"></span>{{ h.hero.badge }}</p>
                  }
                  <h1 id="home-heading">
                    {{ h.hero.title }}
                    @if (h.hero.titleHighlight) {
                      <span class="hl">{{ h.hero.titleHighlight }}</span>
                    }
                  </h1>
                  <p class="lead">{{ h.hero.description }}</p>
                  <div class="cta-row">
                    @if (loggedIn()) {
                      <ui-button [routerLink]="accountPath()">Mi cuenta</ui-button>
                    } @else {
                      <ui-button routerLink="/login">Iniciar sesión</ui-button>
                      <ui-button routerLink="/register">Crear cuenta</ui-button>
                    }
                    <ui-button routerLink="/tienda">Explorar tienda</ui-button>
                    <ui-button routerLink="/contacto">
                      <ui-icon name="building" size="sm" />
                      Plataforma para CEA
                    </ui-button>
                  </div>

                  <div class="trust">
                    @if (reviews(); as r) {
                      @if (r.count > 0) {
                        <span class="avatars" aria-hidden="true">
                          @for (t of r.items.slice(0, 4); track t.id) {
                            <span class="av">{{ initials(t.displayName) }}</span>
                          }
                        </span>
                        <span class="trust-text">
                          <span class="stars" [attr.aria-label]="r.average + ' de 5 estrellas'">{{
                            starString(r.average)
                          }}</span>
                          <strong>{{ r.average }}/5</strong> · {{ r.count }}
                          {{ r.count === 1 ? 'valoración' : 'valoraciones' }} de estudiantes
                        </span>
                      } @else {
                        <span class="trust-text"
                          >Formación vial, gestión para CEA y equipo de protección en un solo lugar.</span
                        >
                      }
                    } @else {
                      <span class="trust-text"
                        >Formación vial, gestión para CEA y equipo de protección en un solo lugar.</span
                      >
                    }
                  </div>
                </div>

                @if (h.hero.imageEnabled && heroImageUrl(h)) {
                  <div class="hero-media">
                    <img [src]="heroImageUrl(h)" [alt]="h.hero.imageAlt || brand.name" loading="eager" />
                  </div>
                } @else {
                  <div class="device-wrap" aria-hidden="true">
                    <div class="device">
                      <div class="device-notch"></div>
                      <div class="device-screen">
                        <div class="mock-top">
                          <span>Simulacro CALE</span>
                          <span class="mock-timer">⏱ 24:10</span>
                        </div>
                        <div class="mock-progress"><span></span></div>
                        <p class="mock-q-num">Pregunta 12 de 25</p>
                        <div class="mock-sign">
                          <svg viewBox="0 0 100 100" width="64" height="64">
                            <polygon
                              points="30,4 70,4 96,30 96,70 70,96 30,96 4,70 4,30"
                              fill="#c0352b"
                              stroke="#fff"
                              stroke-width="4" />
                            <text
                              x="50"
                              y="59"
                              text-anchor="middle"
                              font-size="24"
                              font-weight="800"
                              fill="#fff"
                              font-family="system-ui">
                              PARE
                            </text>
                          </svg>
                        </div>
                        <p class="mock-q">¿Qué debe hacer el conductor ante esta señal?</p>
                        <div class="mock-opt">A. Reducir la velocidad</div>
                        <div class="mock-opt ok">B. Detenerse por completo ✓</div>
                        <div class="mock-opt">C. Tocar el pito y seguir</div>
                        <div class="mock-ready">
                          <span>¿Listo para el examen?</span>
                          <strong>86%</strong>
                        </div>
                      </div>
                    </div>
                  </div>
                }
              </div>

              @if (vitrineFeatured().length) {
                <aside class="vitrine vitrine-right" aria-label="Destacados">
                  <p class="vitrine-title">Destacados</p>
                  <div class="vitrine-list">
                    @for (p of vitrineFeatured(); track p.id) {
                      <a class="vitrine-item" routerLink="/tienda">
                        <span class="vitrine-thumb">
                          @if (productImage(p)) {
                            <img [src]="productImage(p)" [alt]="''" width="56" height="56" loading="lazy" />
                          }
                        </span>
                        <span class="vitrine-meta">
                          @if (productBadge(p); as badge) {
                            <span class="vitrine-badge">{{ badge }}</span>
                          }
                          <strong>{{ p.name }}</strong>
                          <span class="vitrine-price">
                            {{ p.priceCop | currency: 'COP' : 'symbol-narrow' : '1.0-0' }}
                            @if (p.compareAtPriceCop && p.compareAtPriceCop > p.priceCop) {
                              <s>{{ p.compareAtPriceCop | currency: 'COP' : 'symbol-narrow' : '1.0-0' }}</s>
                            }
                          </span>
                        </span>
                      </a>
                    }
                  </div>
                  <a class="vitrine-more" routerLink="/tienda">Ver tienda →</a>
                </aside>
              }

              @if (mobileOffers().length) {
                <div class="mobile-offers" aria-label="Ofertas de la tienda">
                  <div class="mobile-offers-head">
                    <p class="eyebrow">Ofertas</p>
                    <a routerLink="/tienda">Ver tienda</a>
                  </div>
                  <div class="mobile-offers-track">
                    @for (p of mobileOffers(); track p.id) {
                      <a class="mobile-offer-card" routerLink="/tienda">
                        <span class="mobile-offer-media">
                          @if (productImage(p)) {
                            <img [src]="productImage(p)" [alt]="p.name" width="120" height="90" loading="lazy" />
                          }
                          @if (productBadge(p); as badge) {
                            <span class="shop-badge">{{ badge }}</span>
                          }
                        </span>
                        <strong>{{ p.name }}</strong>
                        <span class="shop-price">
                          {{ p.priceCop | currency: 'COP' : 'symbol-narrow' : '1.0-0' }}
                        </span>
                      </a>
                    }
                  </div>
                </div>
              }
            </section>
          }

          <section class="section pwa-band" aria-label="Instalar Luz Verde">
            <div class="wrap pwa-wrap">
              <app-pwa-install-button variant="card" />
            </div>
          </section>

          <section class="section shop-band" aria-labelledby="shop-heading">
            <div class="wrap">
              <header class="sec-head row">
                <div>
                  <p class="eyebrow">Tienda Luz Verde</p>
                  <h2 id="shop-heading">{{ shopHeadline() }}</h2>
                  <p>
                    @if (shopPromoCount() > 0) {
                      Equipo de protección con precio vigente del catálogo. Elige y pide por WhatsApp.
                    } @else {
                      Cascos y equipo para motociclistas. Catálogo real, precios claros y pedido por WhatsApp.
                    }
                  </p>
                </div>
                <ui-button routerLink="/tienda" variant="secondary">Ver toda la tienda</ui-button>
              </header>

              @if (shopProducts().length) {
                <div
                  class="shop-grid"
                  [class.cols-3]="shopProducts().length === 3"
                  [class.cols-5]="shopProducts().length >= 5">
                  @for (p of shopProducts(); track p.id) {
                    <a class="shop-card" routerLink="/tienda">
                      <div class="shop-media">
                        @if (productImage(p)) {
                          <img [src]="productImage(p)" [alt]="p.name" width="280" height="200" loading="lazy" />
                        } @else {
                          <span class="shop-ph" aria-hidden="true">Producto</span>
                        }
                        @if (productBadge(p); as badge) {
                          <span class="shop-badge">{{ badge }}</span>
                        }
                      </div>
                      <strong>{{ p.name }}</strong>
                      <span class="shop-price">
                        {{ p.priceCop | currency: 'COP' : 'symbol-narrow' : '1.0-0' }}
                        @if (p.compareAtPriceCop && p.compareAtPriceCop > p.priceCop) {
                          <s>{{ p.compareAtPriceCop | currency: 'COP' : 'symbol-narrow' : '1.0-0' }}</s>
                        }
                      </span>
                    </a>
                  }
                </div>
              } @else {
                <div class="shop-empty">
                  <p>El catálogo se está actualizando. Mientras tanto puedes abrir la tienda o escribirnos.</p>
                  <div class="cta-row">
                    <ui-button routerLink="/tienda">Ir a la tienda</ui-button>
                    <ui-button routerLink="/contacto">Contacto</ui-button>
                  </div>
                </div>
              }
            </div>
          </section>

          <section class="section" aria-labelledby="audience-heading">
            <div class="wrap">
              <header class="sec-head center">
                <p class="eyebrow">Estudiantes y escuelas</p>
                <h2 id="audience-heading">Todo lo que necesitas para avanzar</h2>
                <p>Aprende, practica y gestiona tu escuela de conducción en un solo lugar.</p>
              </header>

              <div class="audience">
                <article class="aud-card student">
                  <span class="aud-tag">Para estudiantes</span>
                  <h3>Prepárate para tu licencia</h3>
                  <p class="aud-lead">
                    Herramientas de estudio y práctica para reforzar tus conocimientos viales.
                  </p>
                  <ul>
                    <li>
                      <span class="check" aria-hidden="true">✓</span>
                      Simulacros de preparación tipo CALE
                    </li>
                    <li>
                      <span class="check" aria-hidden="true">✓</span>
                      Seguimiento de tu progreso
                    </li>
                    <li>
                      <span class="check" aria-hidden="true">✓</span>
                      Cursos y recursos de formación
                    </li>
                    <li>
                      <span class="check" aria-hidden="true">✓</span>
                      Retos y juegos de práctica
                    </li>
                  </ul>
                  <div class="aud-actions">
                    <ui-button [routerLink]="studentExplorePath()">Explorar para estudiantes</ui-button>
                  </div>
                </article>

                <article class="aud-card school">
                  <span class="aud-tag">Para CEA</span>
                  <h3>Gestiona tu escuela</h3>
                  <p class="aud-lead">
                    Panel para organizar estudiantes, instructores, clases y evaluaciones.
                  </p>
                  <ul>
                    <li>
                      <span class="check" aria-hidden="true">✓</span>
                      Gestión de estudiantes
                    </li>
                    <li>
                      <span class="check" aria-hidden="true">✓</span>
                      Instructores y grupos
                    </li>
                    <li>
                      <span class="check" aria-hidden="true">✓</span>
                      Clases, horarios y teoría
                    </li>
                    <li>
                      <span class="check" aria-hidden="true">✓</span>
                      Evaluaciones e informes
                    </li>
                  </ul>
                  <div class="aud-actions">
                    <ui-button routerLink="/contacto" variant="secondary">Conocer plataforma CEA</ui-button>
                    <a class="ghost-btn soft" routerLink="/register-school">Registrar escuela</a>
                  </div>
                </article>
              </div>
            </div>
          </section>

          <section class="section alt" aria-labelledby="benefits-heading">
            <div class="wrap">
              <header class="sec-head center">
                <p class="eyebrow">Beneficios</p>
                <h2 id="benefits-heading">Aprende con herramientas que te ayudan a avanzar</h2>
                <p>Todo lo que necesitas para prepararte, practicar y mejorar tus conocimientos viales.</p>
              </header>

              <div class="bento even-4">
                <article class="bento-card" data-tone="blue">
                  <span class="tone-icon" aria-hidden="true"><ui-icon name="book" size="lg" /></span>
                  <h3>Aprende a tu ritmo</h3>
                  <p>Accede a los contenidos disponibles y estudia según tu progreso.</p>
                </article>
                <article class="bento-card" data-tone="green">
                  <span class="tone-icon" aria-hidden="true"><ui-icon name="play" size="lg" /></span>
                  <h3>Formación práctica</h3>
                  <p>Refuerza tus conocimientos con herramientas y actividades de aprendizaje.</p>
                </article>
                <article class="bento-card" data-tone="yellow">
                  <span class="tone-icon" aria-hidden="true"><ui-icon name="exam" size="lg" /></span>
                  <h3>Evaluaciones</h3>
                  <p>Practica y comprueba tus conocimientos con las evaluaciones disponibles.</p>
                </article>
                <article class="bento-card" data-tone="green">
                  <span class="tone-icon" aria-hidden="true"><ui-icon name="chart" size="lg" /></span>
                  <h3>Seguimiento de progreso</h3>
                  <p>Consulta tus resultados y reconoce oportunidades de mejora en tu perfil.</p>
                </article>
              </div>
            </div>
          </section>

          <section class="section games" aria-labelledby="practice-heading">
            <div class="wrap games-inner">
              <div class="games-copy">
                <p class="eyebrow">Práctica</p>
                <h2 id="practice-heading">Aprende jugando, mejora cada día</h2>
                <p>
                  Pon a prueba tus conocimientos y refuerza tu preparación vial con las herramientas
                  de práctica disponibles.
                </p>
                <ui-button [routerLink]="practicePath()">Empezar a practicar</ui-button>
              </div>
              <div class="games-grid">
                <article class="game">
                  <span class="game-icon" aria-hidden="true"><ui-icon name="star" size="lg" /></span>
                  <h3>Reto diario</h3>
                  <p>Preguntas del día para mantener el hábito de estudio.</p>
                </article>
                <article class="game">
                  <span class="game-icon" aria-hidden="true"><ui-icon name="users" size="lg" /></span>
                  <h3>Duelo 1 vs. 1</h3>
                  <p>Compite con otro estudiante y refuerza lo aprendido.</p>
                </article>
                <article class="game">
                  <span class="game-icon" aria-hidden="true"><ui-icon name="graduate" size="lg" /></span>
                  <h3>Insignias</h3>
                  <p>Reconocimientos por rachas, retos y logros reales de tu cuenta.</p>
                </article>
                <article class="game">
                  <span class="game-icon" aria-hidden="true"><ui-icon name="chart" size="lg" /></span>
                  <h3>Clasificación</h3>
                  <p>Consulta el ranking según los resultados registrados en la plataforma.</p>
                </article>
              </div>
            </div>
          </section>

          <section class="section" aria-labelledby="steps-heading">
            <div class="wrap">
              <header class="sec-head center">
                <p class="eyebrow">Cómo funciona</p>
                <h2 id="steps-heading">Así funciona Luz Verde</h2>
                <p>Cuatro pasos para comenzar tu preparación.</p>
              </header>

              <ol class="timeline">
                <li class="t-step" data-tone="blue">
                  <span class="t-num" aria-hidden="true">1</span>
                  <h3>Regístrate</h3>
                  <p>Crea una cuenta y accede a las funciones de tu perfil.</p>
                </li>
                <li class="t-step" data-tone="green">
                  <span class="t-num" aria-hidden="true">2</span>
                  <h3>Estudia</h3>
                  <p>Consulta contenidos y recursos de formación disponibles.</p>
                </li>
                <li class="t-step" data-tone="yellow">
                  <span class="t-num" aria-hidden="true">3</span>
                  <h3>Practica</h3>
                  <p>Realiza simulacros y actividades según tu perfil.</p>
                </li>
                <li class="t-step" data-tone="green">
                  <span class="t-num" aria-hidden="true">4</span>
                  <h3>Prepárate</h3>
                  <p>Refuerza tus conocimientos con las herramientas de la plataforma.</p>
                </li>
              </ol>
            </div>
          </section>
        </div>
      }
    }
  `,
  styleUrl: './landing.page.css'
})
export class LandingPage implements OnInit {
  readonly brand = BRAND;
  private readonly api = inject(PublicHomeApi);
  private readonly shopApi = inject(ShopApi);
  private readonly session = inject(SessionStore);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  readonly loading = signal(true);
  readonly home = signal<PublicHomeDto | null>(null);
  readonly reviews = signal<PublicTestimonialsDto | null>(null);
  readonly shop = signal<PublicShopDto | null>(null);
  readonly loggedIn = computed(() => this.session.isAuthenticated());
  readonly accountPath = computed(() => this.session.homeRoute());
  readonly studentExplorePath = computed(() =>
    this.loggedIn() ? this.accountPath() || '/student' : '/register'
  );
  readonly practicePath = computed(() =>
    this.loggedIn() ? '/student/play/daily' : '/login'
  );

  readonly shopProducts = computed(() => {
    const list = this.shop()?.products ?? [];
    const featured = list.filter((p) => p.isFeatured || p.hasPromo);
    const pool = featured.length >= 4 ? featured : list;
    return pool.slice(0, 6);
  });

  readonly shopPromoCount = computed(
    () => (this.shop()?.products ?? []).filter((p) => p.hasPromo).length
  );

  readonly shopHeadline = computed(() =>
    this.shopPromoCount() > 0 ? 'Equipo con precios vigentes' : 'Tienda de protección vial'
  );

  /** Desktop left rail: real promos only. */
  readonly vitrineOffers = computed(() => {
    const list = this.shop()?.products ?? [];
    const promos = list.filter((p) => p.hasPromo || this.hasCompare(p));
    return (promos.length ? promos : list).slice(0, 3);
  });

  /** Desktop right rail: featured (or remaining items), no invented “best sellers”. */
  readonly vitrineFeatured = computed(() => {
    const list = this.shop()?.products ?? [];
    const offerIds = new Set(this.vitrineOffers().map((p) => p.id));
    const featured = list.filter((p) => p.isFeatured && !offerIds.has(p.id));
    const pool = featured.length ? featured : list.filter((p) => !offerIds.has(p.id));
    return pool.slice(0, 3);
  });

  /** Mobile: compact horizontal strip under the hero. */
  readonly mobileOffers = computed(() => {
    const list = this.shop()?.products ?? [];
    if (!list.length) return [] as PublicShopProductDto[];
    const promos = list.filter((p) => p.hasPromo || p.isFeatured || this.hasCompare(p));
    return (promos.length ? promos : list).slice(0, 4);
  });

  private hasCompare(p: PublicShopProductDto): boolean {
    return p.compareAtPriceCop != null && p.compareAtPriceCop > p.priceCop;
  }

  ngOnInit(): void {
    this.api.getHome().subscribe({
      next: (data) => {
        this.home.set(data);
        this.applySeo(data);
        this.loading.set(false);
      },
      error: () => {
        const fallback = this.buildLocalFallback();
        this.home.set(fallback);
        this.applySeo(fallback);
        this.loading.set(false);
      }
    });
    this.api.listTestimonials(6).subscribe({
      next: (r) => this.reviews.set(r),
      error: () => this.reviews.set(null)
    });
    this.shopApi.getPublic().subscribe({
      next: (s) => this.shop.set(s),
      error: () => this.shop.set(null)
    });
  }

  heroImageUrl(h: PublicHomeDto): string {
    return resolveMediaUrl(h.hero.imageUrl);
  }

  productImage(p: { imageUrl: string | null; imageUrls?: string[] }): string {
    const url = p.imageUrls?.[0] || p.imageUrl;
    return url ? resolveMediaUrl(url) : '';
  }

  productBadge(p: {
    promoLabel: string | null;
    discountPercent: number | null;
    compareAtPriceCop: number | null;
    priceCop: number;
  }): string | null {
    if (p.promoLabel?.trim()) return p.promoLabel.trim();
    if (p.discountPercent != null && p.discountPercent > 0) return `−${p.discountPercent}%`;
    if (p.compareAtPriceCop != null && p.compareAtPriceCop > p.priceCop) {
      const pct = Math.round((1 - p.priceCop / p.compareAtPriceCop) * 100);
      return pct > 0 ? `−${pct}%` : null;
    }
    return null;
  }

  isUnverifiableBadge(raw: string): boolean {
    return /#\s*1\b|n[uú]mero\s*uno|n[uú]mero\s*1/i.test(raw.trim());
  }

  initials(name: string): string {
    const parts = name.replace(/[^\p{L}\s]/gu, ' ').trim().split(/\s+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || 'MC';
  }

  starString(value: number): string {
    const full = Math.round(value);
    return '★'.repeat(full) + '☆'.repeat(Math.max(0, 5 - full));
  }

  private applySeo(data: PublicHomeDto): void {
    const t = data.seoTitle?.trim() || BRAND.seoTitle;
    const d = data.seoDescription?.trim() || BRAND.seoDescription;
    this.title.setTitle(t);
    this.meta.updateTag({ name: 'description', content: d });
  }

  private buildLocalFallback(): PublicHomeDto {
    return {
      hero: {
        visible: true,
        badge: 'Formación vial y protección',
        title: 'Luz Verde',
        titleHighlight: 'seguridad en la vía',
        description:
          'Plataforma de formación para conductores y CEA, con tienda de equipo de protección.',
        ctaPrimaryLabel: 'Explorar tienda',
        ctaPrimaryPath: '/tienda',
        ctaSecondaryLabel: 'Para CEA',
        videoUrl: null,
        imageUrl: null,
        imageUrlMobile: null,
        imageAlt: BRAND.name,
        imageEnabled: false
      },
      benefits: [],
      stepsVisible: false,
      stepsTitle: '',
      stepsSubtitle: '',
      steps: [],
      stats: [],
      schoolsVisible: false,
      schools: [],
      instructorsVisible: false,
      instructors: [],
      seoTitle: BRAND.seoTitle,
      seoDescription: BRAND.seoDescription,
      aboutHtml: '',
      blogIntro: '',
      contactEmail: '',
      contactPhone: '',
      updatedAt: new Date().toISOString()
    };
  }
}
