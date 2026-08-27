import { ResolvedStatDto } from './public.models';

/** Display resolved API value only — never invent numbers. */
export function formatStatDisplay(stat: ResolvedStatDto): string {
  if (stat.displayValue != null && String(stat.displayValue).trim() !== '') {
    return String(stat.displayValue);
  }
  if (stat.key === 'rating') {
    return 'Sin valoraciones';
  }
  return '—';
}
