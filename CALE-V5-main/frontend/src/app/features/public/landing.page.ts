import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { resolveMediaUrl } from '../../core/media/resolve-media-url';
import { BRAND } from '../../core/brand';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import { PublicHomeApi } from './public-home.api';
import { PublicHomeDto, PublicTestimonialsDto, ResolvedStatDto } from './public.models';
import { formatStatDisplay } from './public-stat.util';
import { ShopApi } from './shop.api';
import { PublicShopDto } from './shop.models';

@Component({
  selector: 'app-landing-page',
  standalone: true,
  imports: [CurrencyPipe, RouterLink, UiButtonComponent, UiIconComponent, UiLoadingComponent],
  template: `
    @if (loading()) {
      <div class="pad">
        <ui-loading label="Cargando página de inicio..." />
      </div>
    } @else {
      @if (home(); as h) {
        <!-- Desktop: solo Inicio + Tienda en el bloque superior (sin stats ni otras secciones en medio). -->
        <div class="home-lead">
          @if (h.hero.visible) {
            <section class="hero" aria-labelledby="home-heading">
              <div class="hero-bg" aria-hidden="true">
                <span class="glow g1"></span>
                <span class="glow g2"></span>
                <span class="grid-lines"></span>
              </div>
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
                    <ui-button routerLink="/cascos">Explorar tienda</ui-button>
                    <a class="ghost-btn" routerLink="/" fragment="para-cea">
                      <ui-icon name="building" size="sm" />
                      Plataforma para CEA
                    </a>
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
                          <span class="stars" [attr.aria-label]="r.average + ' de 5 estrellas'">{{ starString(r.average) }}</span>
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
            </section>
          }

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
                <ui-button routerLink="/cascos" variant="secondary">Ver toda la tienda</ui-button>
              </header>

              @if (shopProducts().length) {
                <div
                  class="shop-grid"
                  [class.cols-3]="shopProducts().length === 3"
                  [class.cols-5]="shopProducts().length >= 5">
                  @for (p of shopProducts(); track p.id) {
                    <a class="shop-card" routerLink="/cascos">
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
                    <ui-button routerLink="/cascos">Ir a la tienda</ui-button>
                    <a class="ghost-btn" routerLink="/contacto">Contacto</a>
                  </div>
                </div>
              }
            </div>
          </section>
        </div>

        @if (visibleStats(h).length) {
          <section class="stats-strip">
            <div class="wrap stats-grid">
              @for (st of visibleStats(h); track st.key) {
                <div class="stat">
                  <span class="stat-icon" aria-hidden="true"><ui-icon [name]="st.icon || 'users'" /></span>
                  <span>
                    <strong class="stat-value">{{ formatStat(st) }}</strong>
                    <span class="stat-label">{{ st.label }}</span>
                  </span>
                </div>
              }
            </div>
          </section>
        }

        <section id="para-cea" class="section cea-anchor" aria-labelledby="cea-heading">
          <div class="wrap">
            <header class="sec-head center">
              <p class="eyebrow">Para CEA</p>
              <h2 id="cea-heading">Gestor y formación para tu escuela</h2>
              <p>
                Luz Verde también es la plataforma con la que Centros de Enseñanza Automovilística
                administran aprendices, agenda, exámenes y clases — y donde los estudiantes se preparan
                para el examen teórico.
              </p>
            </header>
            <div class="audience">
              <article class="aud-card student">
                <span class="aud-tag">Estudiantes</span>
                <h3>Prepárate para aprobar</h3>
                <ul>
                  @for (f of studentFeatures; track f) {
                    <li><span class="check" aria-hidden="true">✓</span>{{ f }}</li>
                  }
                </ul>
                <ui-button routerLink="/register">Crear cuenta de estudiante</ui-button>
              </article>
              <article class="aud-card school">
                <span class="aud-tag">Escuelas (CEA)</span>
                <h3>Gestiona sin papeles</h3>
                <ul>
                  @for (f of schoolFeatures; track f) {
                    <li><span class="check" aria-hidden="true">✓</span>{{ f }}</li>
                  }
                </ul>
                <div class="aud-actions">
                  <ui-button routerLink="/register-school">Registrar mi escuela</ui-button>
                  <ui-button routerLink="/contacto" variant="secondary">Hablar con comercial</ui-button>
                </div>
              </article>
            </div>
          </div>
        </section>

        @if (h.benefits.length) {
          <section class="section alt">
            <div class="wrap">
              <header class="sec-head center">
                <p class="eyebrow">Beneficios</p>
                <h2>Por qué formarte con {{ brand.name }}</h2>
                <p>Todo lo que necesitas para avanzar en tu licencia, en un solo lugar.</p>
              </header>
              <div class="bento">
                @for (b of h.benefits; track b.id; let i = $index) {
                  <article class="bento-card" [class.wide]="i === 0" [attr.data-tone]="b.tone || 'blue'">
                    <span class="tone-icon" aria-hidden="true"><ui-icon [name]="b.icon || 'book'" /></span>
                    <h3>{{ b.title }}</h3>
                    <p>{{ b.description }}</p>
                  </article>
                }
              </div>
            </div>
          </section>
        }

        <section class="section games">
          <div class="wrap games-inner">
            <div class="games-copy">
              <p class="eyebrow">Práctica</p>
              <h2>Practica jugando, no memorizando</h2>
              <p>Pequeños retos diarios que se sienten como un juego y te dejan listo para el examen teórico.</p>
              <ui-button routerLink="/register">Empezar a practicar</ui-button>
            </div>
            <div class="games-grid">
              @for (g of games; track g.title) {
                <article class="game">
                  <span class="game-emoji" aria-hidden="true">{{ g.emoji }}</span>
                  <h3>{{ g.title }}</h3>
                  <p>{{ g.text }}</p>
                </article>
              }
            </div>
          </div>
        </section>

        @if (h.stepsVisible && h.steps.length) {
          <section class="section">
            <div class="wrap">
              <header class="sec-head center">
                <p class="eyebrow">Paso a paso</p>
                <h2>{{ h.stepsTitle }}</h2>
                <p>{{ h.stepsSubtitle }}</p>
              </header>
              <ol class="timeline">
                @for (s of h.steps; track s.id) {
                  <li class="t-step" [attr.data-tone]="s.tone || 'blue'">
                    <span class="t-num">{{ s.number || $index + 1 }}</span>
                    <h3>{{ s.title }}</h3>
                    <p>{{ s.description }}</p>
                  </li>
                }
              </ol>
            </div>
          </section>
        }

        @if (reviews(); as r) {
          @if (r.items.length) {
            <section class="section alt">
              <div class="wrap">
                <header class="sec-head row">
                  <div>
                    <p class="eyebrow">Valoraciones</p>
                    <h2>Lo que dicen nuestros estudiantes</h2>
                    <p>Opiniones reales dejadas al terminar un simulacro en {{ brand.name }}.</p>
                  </div>
                  <div class="rating-box">
                    <strong>{{ r.average }}</strong>
                    <span class="stars big">{{ starString(r.average) }}</span>
                    <small>{{ r.count }} {{ r.count === 1 ? 'valoración' : 'valoraciones' }}</small>
                  </div>
                </header>
                <div class="reviews">
                  @for (t of r.items; track t.id) {
                    <figure class="review">
                      <span class="stars">{{ starString(t.stars) }}</span>
                      <blockquote>“{{ t.comment }}”</blockquote>
                      <figcaption>
                        <span class="av solid">{{ initials(t.displayName) }}</span>
                        <span>
                          <strong>{{ t.displayName }}</strong>
                          <small>{{ t.schoolName || 'Estudiante de ' + brand.name }}</small>
                        </span>
                      </figcaption>
                    </figure>
                  }
                </div>
              </div>
            </section>
          }
        }

        @if (h.schoolsVisible && h.schools.length) {
          <section class="section">
            <div class="wrap">
              <header class="sec-head row">
                <div>
                  <p class="eyebrow">Red de escuelas</p>
                  <h2>Escuelas aliadas</h2>
                  <p>Centros de enseñanza automovilística que ya forman a sus estudiantes con {{ brand.name }}.</p>
                </div>
                <ui-button routerLink="/escuelas" variant="secondary">Ver todas</ui-button>
              </header>
              <div class="people">
                @for (school of h.schools; track school.id) {
                  <article class="person">
                    <span class="av square">{{ initials(school.name) }}</span>
                    <span>
                      <strong>{{ school.name }}</strong>
                      <small>{{ school.city }}{{ school.department ? ', ' + school.department : '' }}</small>
                    </span>
                  </article>
                }
              </div>
            </div>
          </section>
        }

        @if (h.instructorsVisible && h.instructors.length) {
          <section class="section alt">
            <div class="wrap">
              <header class="sec-head row">
                <div>
                  <p class="eyebrow">Equipo</p>
                  <h2>Instructores</h2>
                  <p>Formadores que acompañan tu proceso teórico y práctico.</p>
                </div>
                <ui-button routerLink="/instructores" variant="secondary">Ver todos</ui-button>
              </header>
              <div class="people">
                @for (ins of h.instructors; track ins.id) {
                  <article class="person">
                    <span class="av solid">{{ initials(ins.displayName) }}</span>
                    <span>
                      <strong>{{ ins.displayName }}</strong>
                      <small>{{ ins.schoolName || 'Instructor ' + brand.name }}</small>
                    </span>
                  </article>
                }
              </div>
            </div>
          </section>
        }

        <section class="section">
          <div class="wrap faq-wrap">
            <header class="sec-head">
              <p class="eyebrow">Preguntas frecuentes</p>
              <h2>¿Tienes dudas?</h2>
              <p>Si no encuentras tu respuesta, <a routerLink="/contacto">escríbenos</a>.</p>
            </header>
            <div class="faq">
              @for (q of faqs; track q.q) {
                <details>
                  <summary>{{ q.q }}</summary>
                  <p>{{ q.a }}</p>
                </details>
              }
            </div>
          </div>
        </section>

        <section class="cta-band">
          <div class="wrap cta-inner">
            <div>
              <h2>Siguiente paso</h2>
              <p>Explora la tienda o conoce el gestor para tu CEA.</p>
            </div>
            <div class="cta-row">
              <ui-button routerLink="/cascos">Explorar tienda</ui-button>
              <a class="ghost-btn" routerLink="/" fragment="para-cea">Para CEA</a>
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
  private readonly shopApi = inject(ShopApi);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  readonly loading = signal(true);
  readonly home = signal<PublicHomeDto | null>(null);
  readonly reviews = signal<PublicTestimonialsDto | null>(null);
  readonly shop = signal<PublicShopDto | null>(null);

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

  readonly studentFeatures = [
    'Simulacros con preguntas tipo examen oficial',
    'Reto diario y repaso de las preguntas que fallas',
    'Medidor que te dice si ya estás listo',
    'Biblioteca jurídica con las normas de tránsito',
    'Clases en vivo con tu instructor'
  ];

  readonly schoolFeatures = [
    'Aprendices, pagos y saldos en un solo panel',
    'Programación de teoría, práctica y exámenes',
    'Resultados por estudiante y por tema',
    'Alertas de estudiantes que dejaron de practicar',
    'Aula en vivo y presentaciones para tus instructores'
  ];

  readonly games = [
    { emoji: '🔥', title: 'Reto diario', text: '5 preguntas al día para mantener tu racha.' },
    { emoji: '⚡', title: 'Señal relámpago', text: '¿Cuántas señales reconoces en 60 segundos?' },
    { emoji: '⚔️', title: 'Duelo 1 vs 1', text: 'Reta a un compañero con un código.' },
    { emoji: '🏅', title: 'Insignias y ranking', text: 'Sube de nivel y compite con tu escuela.' }
  ];

  readonly faqs = [
    {
      q: '¿Qué es el examen CALE?',
      a: 'Es la evaluación teórica que debes aprobar para obtener tu licencia de conducción en Colombia. En Luz Verde practicas con preguntas del mismo estilo y ves en qué temas debes mejorar.'
    },
    {
      q: '¿Cómo compro en la tienda?',
      a: 'Entra a Tienda, elige el producto y completa el pedido por WhatsApp con el botón del catálogo. No hay carrito en la web: el pedido se confirma por ese canal.'
    },
    {
      q: 'Soy una escuela (CEA), ¿cómo empiezo?',
      a: 'Regístrate como escuela o escríbenos desde Contacto para conocer el gestor de aprendices, agenda, exámenes y formación. También puedes ir a la sección Para CEA en el inicio.'
    },
    {
      q: '¿Cuántas preguntas puedo fallar en el examen?',
      a: 'En los simulacros usamos la regla del examen: puedes tener como máximo 3 respuestas incorrectas para aprobar.'
    },
    {
      q: '¿Funciona en el celular?',
      a: 'Sí. Luz Verde funciona en cualquier navegador y puedes instalarla en tu celular como una aplicación.'
    }
  ];

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

  /** Hide unverifiable marketing claims like “#1” from CMS badges. */
  isUnverifiableBadge(raw: string): boolean {
    return /#\s*1\b|n[uú]mero\s*uno|n[uú]mero\s*1/i.test(raw.trim());
  }

  visibleStats(h: PublicHomeDto): ResolvedStatDto[] {
    return [...h.stats].filter((s) => s.visible).sort((a, b) => a.sortOrder - b.sortOrder);
  }

  formatStat(stat: ResolvedStatDto): string {
    return formatStatDisplay(stat);
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
        ctaPrimaryPath: '/cascos',
        ctaSecondaryLabel: 'Para CEA',
        videoUrl: null,
        imageUrl: null,
        imageUrlMobile: null,
        imageAlt: BRAND.name,
        imageEnabled: false
      },
      benefits: [
        {
          id: 'b1',
          title: 'Aprende a tu ritmo',
          description: 'Estudia desde donde estés con contenidos disponibles 24/7.',
          icon: 'graduate',
          tone: 'blue',
          sortOrder: 1,
          active: true
        },
        {
          id: 'b2',
          title: 'Clases prácticas',
          description: 'Coordina tus clases con instructores certificados de tu escuela.',
          icon: 'play',
          tone: 'green',
          sortOrder: 2,
          active: true
        },
        {
          id: 'b3',
          title: 'Evaluaciones inteligentes',
          description: 'Prepárate con simulacros teóricos como los del examen oficial.',
          icon: 'exam',
          tone: 'purple',
          sortOrder: 3,
          active: true
        },
        {
          id: 'b4',
          title: 'Certificación',
          description: 'Cumple los requisitos y completa tu proceso de formación.',
          icon: 'star',
          tone: 'yellow',
          sortOrder: 4,
          active: true
        }
      ],
      stepsVisible: true,
      stepsTitle: '¿Cómo funciona Luz Verde?',
      stepsSubtitle: 'Cuatro pasos claros para completar tu formación vial.',
      steps: [
        {
          id: 's1',
          number: 1,
          title: 'Regístrate',
          description: 'Crea tu cuenta y vincúlate a tu escuela de conducción.',
          icon: 'users',
          tone: 'blue',
          sortOrder: 1,
          active: true
        },
        {
          id: 's2',
          number: 2,
          title: 'Estudia',
          description: 'Accede a los contenidos teóricos y a la biblioteca jurídica.',
          icon: 'book',
          tone: 'green',
          sortOrder: 2,
          active: true
        },
        {
          id: 's3',
          number: 3,
          title: 'Practica',
          description: 'Presenta simulacros y repasa las preguntas que fallaste.',
          icon: 'exam',
          tone: 'purple',
          sortOrder: 3,
          active: true
        },
        {
          id: 's4',
          number: 4,
          title: 'Aprueba',
          description: 'Llega preparado al examen y obtén tu licencia.',
          icon: 'star',
          tone: 'yellow',
          sortOrder: 4,
          active: true
        }
      ],
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
