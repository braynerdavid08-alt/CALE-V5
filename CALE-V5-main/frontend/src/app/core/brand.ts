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
  seoTitle: 'Luz Verde — Formación que impulsa tu camino.',
  /** Document / SEO fallback description. */
  seoDescription:
    'Luz Verde: practica gratis el simulacro del examen CALE todas las veces que quieras y sigue tu formación vial con tu escuela.',
  /** Full horizontal logo (traffic light + wordmark + slogan) on a black background. */
  logoWide: '/brand/luz-verde-logo.jpg',
  /** PWA / favicon paths (public/). */
  icon192: '/icons/icon-192.png',
  icon512: '/icons/icon-512.png',
  appleTouchIcon: '/icons/apple-touch-icon.png',
  favicon: '/icons/favicon.png',
  themeColor: '#070b09',
  pwaBackground: '#000000'
} as const;

export function brandPageTitle(page: string): string {
  return `${page} — ${BRAND.name}`;
}
