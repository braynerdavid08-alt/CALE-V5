/** Product brand — keep UI copy in sync with this source. */
export const BRAND = {
  /** Primary product name (hero / logo wordmark). */
  name: 'Mi CALE',
  /** Short tagline for tight UI (sidebar, nav). */
  sloganShort: 'en tu CEA',
  /** Full tagline for auth, footer, meta. */
  slogan: 'tu CALE, en tu CEA',
  /** Compact mark inside the round logo. */
  mark: 'C',
  /** Document / SEO fallback title. */
  seoTitle: 'Mi CALE — tu CALE, en tu CEA',
  /** Document / SEO fallback description. */
  seoDescription:
    'Mi CALE: tu CALE, en tu CEA. Formación vial con tu centro de enseñanza automovilística.',
  /** PWA / favicon paths (public/). */
  icon192: '/icons/icon-192.png',
  icon512: '/icons/icon-512.png',
  appleTouchIcon: '/icons/apple-touch-icon.png',
  favicon: '/icons/favicon.png',
  themeColor: '#051128',
  pwaBackground: '#000000'
} as const;

export function brandPageTitle(page: string): string {
  return `${page} — ${BRAND.name}`;
}
