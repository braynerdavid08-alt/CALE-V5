import { newClientId, SlideElement } from './presentation.models';

/** Catalog entry from `/api/courses/signs` (senales-catalog.json). */
export interface CatalogSignRef {
  code: string;
  family: string;
  name: string;
  imageUrl: string;
}

export type SignFamilyFilter = 'all' | 'reglamentarias' | 'preventivas' | 'informativas';

export const SIGN_FAMILY_FILTERS: { key: SignFamilyFilter; label: string; match: string }[] = [
  { key: 'all', label: 'Todos', match: '' },
  { key: 'reglamentarias', label: 'Reglamentación', match: 'reglamentarias' },
  { key: 'preventivas', label: 'Prevención', match: 'preventivas' },
  { key: 'informativas', label: 'Información', match: 'informativas' }
];

export function signFamilyShortLabel(family: string): string {
  const f = family.toLowerCase();
  if (f.includes('reglament')) return 'Reglamentación';
  if (f.includes('prevent')) return 'Prevención';
  if (f.includes('informat')) return 'Información';
  return family;
}

export function filterCatalogSigns(
  signs: CatalogSignRef[],
  family: SignFamilyFilter,
  search: string
): CatalogSignRef[] {
  const q = search.trim().toLowerCase();
  const fam = SIGN_FAMILY_FILTERS.find((f) => f.key === family)?.match ?? '';
  return signs.filter((s) => {
    if (fam && !s.family.toLowerCase().includes(fam)) {
      return false;
    }
    if (!q) {
      return true;
    }
    return (
      s.name.toLowerCase().includes(q) ||
      s.code.toLowerCase().includes(q) ||
      s.family.toLowerCase().includes(q)
    );
  });
}

/** Inserts a photo from the official exam-based sign bank (never a schematic SVG). */
export function buildCatalogSignImageElement(
  sign: CatalogSignRef,
  x = 780,
  y = 300,
  size = 360
): SlideElement {
  const src = (sign.imageUrl || '').trim();
  if (!src || src.includes('/signals/')) {
    throw new Error('Señal sin foto real del examen.');
  }
  return {
    id: newClientId('el'),
    type: 'image',
    x,
    y,
    w: size,
    h: size,
    rotation: 0,
    z: 1,
    props: {
      src,
      opacity: 1,
      objectFit: 'contain'
    }
  };
}
