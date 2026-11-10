/** Safe tenure labels for ranking rows. Never invent durations from sentinel dates. */

const MIN_YEAR = 2000;
const MAX_YEARS = 40;

export function rankingHeldLabel(
  positionHeldSinceUtc: string | null | undefined,
  nowMs: number = Date.now()
): string | null {
  if (!positionHeldSinceUtc) return null;
  const since = new Date(positionHeldSinceUtc).getTime();
  if (Number.isNaN(since)) return null;
  const sinceDate = new Date(positionHeldSinceUtc);
  if (sinceDate.getUTCFullYear() < MIN_YEAR) return null;

  const days = Math.floor((nowMs - since) / 86_400_000);
  if (days < 0) return null;
  if (days === 0) return 'En este puesto desde hoy';
  if (days === 1) return '1 día en este puesto';
  if (days < 30) return `${days} días en este puesto`;
  const months = Math.floor(days / 30);
  if (months === 1) return '1 mes en este puesto';
  if (months < 12) return `${months} meses en este puesto`;
  const years = Math.floor(months / 12);
  if (years > MAX_YEARS) return null;
  return years === 1 ? '1 año en este puesto' : `${years} años en este puesto`;
}
