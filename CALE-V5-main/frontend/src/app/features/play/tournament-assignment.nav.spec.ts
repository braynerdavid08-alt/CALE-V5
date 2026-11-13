import {
  applyParticipantPresence,
  decideMatchAssignmentNav,
  decideMatchReadyNav,
  isTransientHttpError
} from './tournament-assignment.nav';

describe('tournament assignment navigation', () => {
  const base = {
    myUserId: 10,
    currentTournamentId: 5,
    currentMatchId: 100,
    lastNavigatedMatchId: null as number | null
  };

  it('navigates when MatchAssigned is for me in the same tournament', () => {
    expect(
      decideMatchAssignmentNav({
        ...base,
        payload: {
          matchId: 200,
          tournamentId: 5,
          player1UserId: 10,
          player2UserId: 11,
          status: 'active'
        }
      })
    ).toBe(200);
  });

  it('ignores assignment for another player', () => {
    expect(
      decideMatchAssignmentNav({
        ...base,
        payload: { matchId: 200, tournamentId: 5, player1UserId: 20, player2UserId: 21 }
      })
    ).toBeNull();
  });

  it('ignores assignment from another tournament', () => {
    expect(
      decideMatchAssignmentNav({
        ...base,
        payload: { matchId: 200, tournamentId: 99, player1UserId: 10, player2UserId: 11 }
      })
    ).toBeNull();
  });

  it('is idempotent for the same matchId and current match', () => {
    expect(
      decideMatchAssignmentNav({
        ...base,
        currentMatchId: 200,
        payload: { matchId: 200, tournamentId: 5, player1UserId: 10, player2UserId: 11 }
      })
    ).toBeNull();

    expect(
      decideMatchAssignmentNav({
        ...base,
        lastNavigatedMatchId: 200,
        payload: { matchId: 200, tournamentId: 5, player1UserId: 10, player2UserId: 11 }
      })
    ).toBeNull();
  });

  it('handles MatchReady only for the authenticated user', () => {
    expect(decideMatchReadyNav(10, 100, null, { userId: 10, matchId: 300 })).toBe(300);
    expect(decideMatchReadyNav(10, 100, null, { userId: 99, matchId: 300 })).toBeNull();
    expect(decideMatchReadyNav(10, 300, null, { userId: 10, matchId: 300 })).toBeNull();
  });

  it('patches presence without cloning when unchanged', () => {
    const participants = [
      { userId: 1, isConnected: true, name: 'A' },
      { userId: 2, isConnected: false, name: 'B' }
    ];
    expect(applyParticipantPresence(participants, 1, true)).toBe(participants);
    const next = applyParticipantPresence(participants, 2, true);
    expect(next).not.toBe(participants);
    expect(next[1].isConnected).toBe(true);
  });

  it('detects transient HTTP failures', () => {
    expect(isTransientHttpError({ status: 502 })).toBe(true);
    expect(isTransientHttpError({ status: 503 })).toBe(true);
    expect(isTransientHttpError({ status: 504 })).toBe(true);
    expect(isTransientHttpError({ status: 0 })).toBe(true);
    expect(isTransientHttpError({ status: 404 })).toBe(false);
  });
});
