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
import { PublicHomeDto, PublicTestimonialsDto } from './public.models';
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
                    <a class="ghost-btn" routerLink="/contacto">
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
        ctaPrimaryPath: '/cascos',
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
