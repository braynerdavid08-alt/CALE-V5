import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { BRAND } from '../brand';

/** Canonical public origin (apex redirects to www). */
export const SITE_ORIGIN = 'https://www.cealuzverde.com';

const JSON_LD_ID = 'luz-verde-json-ld';

/** Paths that must stay out of the index. Crawlable so Google can read meta noindex. */
const NOINDEX_PREFIXES = [
  '/login',
  '/register',
  '/register-teacher',
  '/register-school',
  '/verify-email',
  '/school',
  '/teacher',
  '/student',
  '/admin',
  '/live',
  '/game-show',
  '/autorizacion-menor',
  '/mis-pqr',
  '/solicitudes',
  '/donaciones',
  '/legal-pending',
  // Legal drafts: public URLs exist but must not be indexed until final review.
  '/legal'
] as const;

export interface PageSeoOptions {
  title: string;
  description: string;
  path?: string;
  image?: string;
  type?: 'website' | 'article';
  index?: boolean;
}

@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly router = inject(Router);
  private started = false;

  /** Call once from the app root so private routes get noindex automatically. */
  startRouteIndexing(): void {
    if (this.started || typeof document === 'undefined') {
      return;
    }
    this.started = true;
    this.applyIndexPolicy(this.router.url);
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => this.applyIndexPolicy(e.urlAfterRedirects));
  }

  apply(options: PageSeoOptions): void {
    const path = this.normalizePath(options.path ?? this.router.url);
    const url = `${SITE_ORIGIN}${path === '/' ? '/' : path}`;
    const image = options.image?.startsWith('http')
      ? options.image
      : `${SITE_ORIGIN}${options.image || BRAND.logoWide}`;
    const index = options.index ?? !this.isNoIndexPath(path);

    this.title.setTitle(options.title);
    this.meta.updateTag({ name: 'description', content: options.description });
    this.meta.updateTag({
      name: 'robots',
      content: index ? 'index, follow' : 'noindex, nofollow'
    });
    this.setLinkCanonical(url);

    this.meta.updateTag({ property: 'og:type', content: options.type ?? 'website' });
    this.meta.updateTag({ property: 'og:site_name', content: BRAND.name });
    this.meta.updateTag({ property: 'og:locale', content: 'es_CO' });
    this.meta.updateTag({ property: 'og:title', content: options.title });
    this.meta.updateTag({ property: 'og:description', content: options.description });
    this.meta.updateTag({ property: 'og:url', content: url });
    this.meta.updateTag({ property: 'og:image', content: image });

    this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.meta.updateTag({ name: 'twitter:title', content: options.title });
    this.meta.updateTag({ name: 'twitter:description', content: options.description });
    this.meta.updateTag({ name: 'twitter:image', content: image });
  }

  /** Organization + WebSite JSON-LD for the public homepage. */
  injectHomeJsonLd(): void {
    if (typeof document === 'undefined') {
      return;
    }
    const data = {
      '@context': 'https://schema.org',
      '@graph': [
        {
          '@type': 'Organization',
          '@id': `${SITE_ORIGIN}/#organization`,
          name: BRAND.name,
          url: `${SITE_ORIGIN}/`,
          logo: `${SITE_ORIGIN}${BRAND.logoWide}`,
          slogan: BRAND.slogan,
          email: 'notificaciones@cealuzverde.com',
          areaServed: {
            '@type': 'City',
            name: 'Barranquilla',
            containedInPlace: {
              '@type': 'AdministrativeArea',
              name: 'Atlántico'
            }
          },
          sameAs: [`${SITE_ORIGIN}/`]
        },
        {
          '@type': 'WebSite',
          '@id': `${SITE_ORIGIN}/#website`,
          url: `${SITE_ORIGIN}/`,
          name: BRAND.name,
          description: BRAND.seoDescription,
          publisher: { '@id': `${SITE_ORIGIN}/#organization` },
          inLanguage: 'es-CO'
        },
        {
          '@type': 'WebPage',
          '@id': `${SITE_ORIGIN}/#webpage`,
          url: `${SITE_ORIGIN}/`,
          name: BRAND.seoTitle,
          isPartOf: { '@id': `${SITE_ORIGIN}/#website` },
          about: { '@id': `${SITE_ORIGIN}/#organization` },
          description: BRAND.seoDescription,
          inLanguage: 'es-CO'
        },
        {
          '@type': 'SoftwareApplication',
          '@id': `${SITE_ORIGIN}/#software`,
          name: BRAND.name,
          url: `${SITE_ORIGIN}/`,
          applicationCategory: 'BusinessApplication',
          applicationSubCategory: 'EducationalApplication',
          operatingSystem: 'Web',
          description: BRAND.seoDescription,
          publisher: { '@id': `${SITE_ORIGIN}/#organization` },
          inLanguage: 'es-CO'
        }
      ]
    };

    let el = document.getElementById(JSON_LD_ID) as HTMLScriptElement | null;
    if (!el) {
      el = document.createElement('script');
      el.type = 'application/ld+json';
      el.id = JSON_LD_ID;
      document.head.appendChild(el);
    }
    el.textContent = JSON.stringify(data);
  }

  clearJsonLd(): void {
    if (typeof document === 'undefined') {
      return;
    }
    document.getElementById(JSON_LD_ID)?.remove();
  }

  private applyIndexPolicy(url: string): void {
    const path = this.normalizePath(url);
    if (this.isNoIndexPath(path)) {
      this.meta.updateTag({ name: 'robots', content: 'noindex, nofollow' });
      this.clearJsonLd();
      return;
    }
    // Clear noindex inherited from a previous private route before the page sets its own tags.
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
  }

  private isNoIndexPath(path: string): boolean {
    return NOINDEX_PREFIXES.some(
      (p) => path === p || path.startsWith(`${p}/`)
    );
  }

  private normalizePath(url: string): string {
    const path = url.split('?')[0].split('#')[0] || '/';
    if (path.length > 1 && path.endsWith('/')) {
      return path.slice(0, -1);
    }
    return path || '/';
  }

  private setLinkCanonical(href: string): void {
    if (typeof document === 'undefined') {
      return;
    }
    let link = document.querySelector('link[rel="canonical"]') as HTMLLinkElement | null;
    if (!link) {
      link = document.createElement('link');
      link.setAttribute('rel', 'canonical');
      document.head.appendChild(link);
    }
    link.setAttribute('href', href);
  }
}
