/** Product brand — keep UI copy in sync with this source. */
export const BRAND = {
  /** Primary product name (hero / logo wordmark). */
  name: 'Luz Verde',
  /** Short tagline for tight UI (sidebar, nav). */
  sloganShort: 'Formación vial',
  /** Full tagline for auth, footer, meta. */
  slogan: 'Formación que impulsa tu camino.',
  /** Compact mark inside the round logo. */
  mark: 'L',
  /** Document / SEO fallback title. */
  seoTitle: 'Luz Verde | Formación vial en Barranquilla — A2, B1 y C1',
  /** Document / SEO fallback description. */
  seoDescription:
    'Luz Verde: formación que impulsa tu camino. Plataforma de formación de conductores en Barranquilla con simulacro CALE gratis, cursos de conducción y categorías A2, B1 y C1 para escuelas CEA.',
  /** Full horizontal logo (traffic light + wordmark + slogan) on a transparent background; its dark text needs a light surface behind it. */
  logoWide: '/brand/luz-verde-logo.png',
  /** Same logo with light lettering, for dark surfaces. */
  logoWideDark: '/brand/luz-verde-logo-dark.png',
  /** PWA / favicon paths (public/). */
  icon192: '/icons/icon-192.png',
  icon512: '/icons/icon-512.png',
  appleTouchIcon: '/icons/apple-touch-icon.png',
  favicon: '/icons/favicon.png',
  themeColor: '#070b09',
  pwaBackground: '#ffffff'
} as const;

export function brandPageTitle(page: string): string {
  return `${page} — ${BRAND.name}`;
}
