import { expect, test } from '@playwright/test';
import * as fs from 'node:fs';
import * as path from 'node:path';
import {
  filterLiveRealMatches,
  teacherShowsLiveBoard,
  tournamentFootText,
  tournamentMatchLink,
  tournamentSlotName
} from '../src/app/features/play/tournament-host.logic';

/**
 * Angular host/spectator coverage that does not require Karma (broken by rimraf override)
 * or a running ng serve / lab passwords / demo accounts.
 */
const SRC = path.resolve(__dirname, '..', 'src', 'app');
const FINISH_REASON_LABELS: Record<string, string> = {
  score: 'por puntaje',
  time: 'por tiempo',
  sudden_death: 'en muerte súbita',
  walkover: 'por W.O.',
  bye: 'esperando rival'
};

function read(rel: string): string {
  return fs.readFileSync(path.join(SRC, rel), 'utf8');
}

test.describe('Tournament Angular host UI (routes + screens)', () => {
  test('teacher and student tournament routes are registered in order', () => {
    const routes = read('app.routes.ts');
    expect(routes).toContain("path: 'teacher/tournaments/match/:matchId'");
    expect(routes).toContain("path: 'teacher/tournaments/:id'");
    expect(routes).toContain("path: 'tournaments/match/:matchId'");
    expect(routes).toContain("path: 'tournaments/:id'");
    expect(routes).toContain("staffTournamentsBase = '/teacher/tournaments'");
    expect(routes).toContain("studentTournamentsBase = '/student/play/tournaments'");

    const teacherMatch = routes.indexOf("path: 'teacher/tournaments/match/:matchId'");
    const teacherLobby = routes.indexOf("path: 'teacher/tournaments/:id'");
    expect(teacherMatch).toBeGreaterThan(-1);
    expect(teacherLobby).toBeGreaterThan(-1);
    expect(teacherMatch).toBeLessThan(teacherLobby);
  });

  test('lobby screen exposes live board and project CTA for organizers', () => {
    const lobby = read('features/play/pages/tournament-lobby.page.ts');
    expect(lobby).toContain('Partidos en vivo');
    expect(lobby).toContain('Ver / proyectar');
    expect(lobby).toContain('showLiveBoard');
    expect(lobby).toContain('filterLiveRealMatches');
    expect(lobby).toContain("base + '/match/'");
  });

  test('bracket screen links matches for host/spectate and labels bye waiters', () => {
    const bracket = read('features/play/pages/tournament-bracket.page.ts');
    expect(bracket).toContain('tournamentMatchLink');
    expect(bracket).toContain('tournamentSlotName');
    expect(bracket).toContain('tournamentFootText');
  });

  test('match screen has spectator projection mode for non-players', () => {
    const match = read('features/play/pages/tournament-match.page.ts');
    expect(match).toContain('Modo espectador');
    expect(match).toContain('!s.amIPlayer');
    expect(match).toContain('[busy]="true"');
  });

  test('host logic builds links and filters live matches', () => {
    const live = {
      id: 42,
      status: 'active',
      player1IsBye: false,
      player2IsBye: false,
      player1Name: 'Ana',
      player2Name: 'Luis',
      finishReason: null as string | null
    };
    expect(tournamentMatchLink('/teacher/tournaments', live)).toBe('/teacher/tournaments/match/42');
    expect(tournamentMatchLink('/student/play/tournaments', live)).toBe(
      '/student/play/tournaments/match/42'
    );
    expect(
      tournamentMatchLink('/teacher/tournaments', { ...live, player2IsBye: true })
    ).toBeNull();
    expect(tournamentSlotName({ ...live, player2IsBye: true }, 2)).toBe('Espera rival');
    expect(
      tournamentFootText({ ...live, status: 'finished', finishReason: 'bye' }, FINISH_REASON_LABELS)
    ).toContain('Esperando rival');
    expect(teacherShowsLiveBoard(true, 'in_progress')).toBe(true);
    expect(teacherShowsLiveBoard(false, 'in_progress')).toBe(false);
    expect(filterLiveRealMatches([live, { ...live, id: 9, status: 'finished' }]).map((m) => m.id)).toEqual([
      42
    ]);
  });
});
