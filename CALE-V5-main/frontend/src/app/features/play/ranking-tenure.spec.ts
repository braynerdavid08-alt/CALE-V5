import { rankingHeldLabel } from './ranking-tenure';

describe('rankingHeldLabel', () => {
  const now = Date.parse('2026-11-08T12:00:00.000Z');

  it('returns null for missing or sentinel dates (prevents 2055 años)', () => {
    expect(rankingHeldLabel(null, now)).toBeNull();
    expect(rankingHeldLabel(undefined, now)).toBeNull();
    expect(rankingHeldLabel('0001-01-01T00:00:00', now)).toBeNull();
    expect(rankingHeldLabel('0001-01-01T00:00:00Z', now)).toBeNull();
    expect(rankingHeldLabel('not-a-date', now)).toBeNull();
  });

  it('formats verified recent holds', () => {
    expect(rankingHeldLabel('2026-11-08T10:00:00.000Z', now)).toBe('En este puesto desde hoy');
    expect(rankingHeldLabel('2026-11-07T12:00:00.000Z', now)).toBe('1 día en este puesto');
    expect(rankingHeldLabel('2026-10-09T12:00:00.000Z', now)).toBe('1 mes en este puesto');
  });

  it('rejects absurd multi-decade spans', () => {
    expect(rankingHeldLabel('1970-01-01T00:00:00.000Z', now)).toBeNull();
  });
});
