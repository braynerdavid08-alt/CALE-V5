import { expect, test } from '@playwright/test';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { rankingHeldLabel } from '../src/app/features/play/ranking-tenure';

const SRC = path.resolve(__dirname, '..', 'src', 'app', 'features', 'play');

test.describe('Simplified competitive ranking UI', () => {
  test('page only exposes XP / Trofeos and three scopes', () => {
    const page = fs.readFileSync(path.join(SRC, 'pages', 'ranking.page.ts'), 'utf8');
    expect(page).toContain("key: 'xp'");
    expect(page).toContain("key: 'trophies'");
    expect(page).not.toContain("key: 'general'");
    expect(page).not.toContain("key: 'achievements'");
    expect(page).not.toContain('selectPeriod');
    expect(page).not.toContain('Últimos 30 días');
    expect(page).toContain('Histórico general');
    expect(page).toContain('Mi escuela');
    expect(page).toContain('Mi grupo');
    expect(page).toContain('Todo Luz Verde');
  });

  test('tenure helper never invents 2055 years', () => {
    const now = Date.parse('2026-11-08T12:00:00.000Z');
    expect(rankingHeldLabel('0001-01-01T00:00:00Z', now)).toBeNull();
    expect(rankingHeldLabel('2026-11-01T12:00:00.000Z', now)).toBe('7 días en este puesto');
  });
});
