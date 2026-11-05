/** Product brand — keep UI copy in sync with this source. */
export const BRAND = {
  /** Primary product name (hero / logo wordmark). */
  name: 'Luz Verde',
  /** Short tagline for tight UI (sidebar, nav). */
  sloganShort: 'Plataforma para CEA',
  /** Full tagline for auth, footer, meta. */
  slogan: 'Formación que impulsa tu camino.',
  /** Compact mark inside the round logo. */
  mark: 'L',
  /** Document / SEO fallback title (B2B — software for driving schools). */
  seoTitle: 'Luz Verde | Plataforma para CEA y Escuelas de Conducción en Colombia',
  /** Alternate name for structured data / brand recognition. */
  alternateName: 'Luz Verde CEA',
  /** Document / SEO fallback description. */
  seoDescription:
    'Luz Verde es una plataforma tecnológica para escuelas de manejo, escuelas de conducción y Centros de Enseñanza Automovilística. Descubre herramientas de formación vial, gestión académica y preparación para el examen CALE.',
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
