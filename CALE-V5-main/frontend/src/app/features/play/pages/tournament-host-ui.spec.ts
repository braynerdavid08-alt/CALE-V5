import { FINISH_REASON_LABELS } from '../api/tournament.api';
import {
  filterLiveRealMatches,
  teacherShowsLiveBoard,
  tournamentFootText,
  tournamentMatchLink,
  tournamentNeedsRealtime,
  tournamentSlotName
} from '../tournament-host.logic';

describe('Tournament host / spectator UI logic', () => {
  const live = {
    id: 42,
    status: 'active',
    player1IsBye: false,
    player2IsBye: false,
    player1Name: 'Ana',
    player2Name: 'Luis',
    finishReason: null as string | null
  };

  it('builds teacher and student match links for live real matches', () => {
    expect(tournamentMatchLink('/teacher/tournaments', live)).toBe('/teacher/tournaments/match/42');
    expect(tournamentMatchLink('/student/play/tournaments', live)).toBe(
      '/student/play/tournaments/match/42'
    );
  });

  it('does not link bye or pending slots', () => {
    expect(
      tournamentMatchLink('/teacher/tournaments', { ...live, player2IsBye: true, player2Name: null })
    ).toBeNull();
    expect(tournamentMatchLink('/teacher/tournaments', { ...live, status: 'pending' })).toBeNull();
  });

  it('labels bye side as waiting for rival', () => {
    const bye = { ...live, player2IsBye: true, status: 'finished', finishReason: 'bye' };
    expect(tournamentSlotName(bye, 2)).toBe('Espera rival');
    expect(tournamentFootText(bye, FINISH_REASON_LABELS)).toContain('Esperando rival');
  });

  it('shows the teacher live board only while in progress', () => {
    expect(teacherShowsLiveBoard(true, 'in_progress')).toBe(true);
    expect(teacherShowsLiveBoard(true, 'registration_open')).toBe(false);
    expect(teacherShowsLiveBoard(false, 'in_progress')).toBe(false);
  });

  it('skips realtime for finished or cancelled tournaments', () => {
    expect(tournamentNeedsRealtime('registration_open')).toBe(true);
    expect(tournamentNeedsRealtime('in_progress')).toBe(true);
    expect(tournamentNeedsRealtime('finished')).toBe(false);
    expect(tournamentNeedsRealtime('cancelled')).toBe(false);
  });

  it('filters only active real matches for the live board', () => {
    const matches = [
      live,
      { ...live, id: 43, player2IsBye: true },
      { ...live, id: 44, status: 'finished' }
    ];
    expect(filterLiveRealMatches(matches).map((m) => m.id)).toEqual([42]);
  });
});
