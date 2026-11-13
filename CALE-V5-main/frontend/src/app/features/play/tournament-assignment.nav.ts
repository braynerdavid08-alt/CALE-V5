/** Pure helpers for MatchAssigned / MatchReady auto-navigation (unit-testable). */

export interface AssignedMatchPayload {
  matchId?: number;
  tournamentId?: number;
  player1UserId?: number | null;
  player2UserId?: number | null;
  status?: string;
}

export interface MatchReadyPayload {
  userId?: number;
  matchId?: number;
}

export interface MatchAssignmentDecisionInput {
  myUserId: number;
  currentTournamentId: number | null;
  currentMatchId: number;
  /** Last matchId we already navigated to (idempotency). */
  lastNavigatedMatchId: number | null;
  payload: AssignedMatchPayload | null | undefined;
}

/**
 * Returns the matchId to navigate to, or null if the assignment must be ignored.
 */
export function decideMatchAssignmentNav(input: MatchAssignmentDecisionInput): number | null {
  const { myUserId, currentTournamentId, currentMatchId, lastNavigatedMatchId, payload } = input;
  if (!payload || myUserId <= 0) return null;

  const matchId = Number(payload.matchId);
  if (!Number.isFinite(matchId) || matchId <= 0) return null;
  if (matchId === currentMatchId) return null;
  if (lastNavigatedMatchId === matchId) return null;

  if (payload.tournamentId != null && currentTournamentId != null) {
    if (Number(payload.tournamentId) !== currentTournamentId) return null;
  }

  const p1 = payload.player1UserId ?? null;
  const p2 = payload.player2UserId ?? null;
  // If seats are known, require the viewer to be one of them.
  if (p1 != null || p2 != null) {
    if (p1 !== myUserId && p2 !== myUserId) return null;
  }

  return matchId;
}

/**
 * MatchReady is a group cue: only navigate when the userId matches the viewer.
 */
export function decideMatchReadyNav(
  myUserId: number,
  currentMatchId: number,
  lastNavigatedMatchId: number | null,
  payload: MatchReadyPayload | null | undefined
): number | null {
  if (!payload || myUserId <= 0) return null;
  if (payload.userId !== myUserId) return null;
  const matchId = Number(payload.matchId);
  if (!Number.isFinite(matchId) || matchId <= 0) return null;
  if (matchId === currentMatchId) return null;
  if (lastNavigatedMatchId === matchId) return null;
  return matchId;
}

export function isTransientHttpError(err: unknown): boolean {
  const status =
    err && typeof err === 'object' && 'status' in err ? Number((err as { status: unknown }).status) : NaN;
  return status === 0 || status === 502 || status === 503 || status === 504;
}

/** Apply presence-only patch without refetching the full lobby. */
export function applyParticipantPresence<T extends { userId: number; isConnected: boolean }>(
  participants: T[],
  userId: number,
  isConnected: boolean
): T[] {
  let changed = false;
  const next = participants.map((p) => {
    if (p.userId !== userId) return p;
    if (p.isConnected === isConnected) return p;
    changed = true;
    return { ...p, isConnected };
  });
  return changed ? next : participants;
}
