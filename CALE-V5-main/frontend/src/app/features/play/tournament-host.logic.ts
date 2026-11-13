/** Pure helpers for tournament host/spectator UI (unit-testable without Karma). */

export interface TournamentHostMatchLike {
  id: number;
  status: string;
  player1IsBye: boolean;
  player2IsBye: boolean;
  player1Name?: string | null;
  player2Name?: string | null;
  finishReason?: string | null;
}

export function tournamentMatchLink(base: string, m: TournamentHostMatchLike): string | null {
  if (m.status !== 'active' && m.status !== 'finished') return null;
  if (m.player1IsBye || m.player2IsBye) return null;
  return `${base}/match/${m.id}`;
}

export function tournamentSlotName(m: TournamentHostMatchLike, player: 1 | 2): string {
  const isBye = player === 1 ? m.player1IsBye : m.player2IsBye;
  const name = player === 1 ? m.player1Name : m.player2Name;
  if (isBye) return 'Espera rival';
  return name || 'Por definir';
}

export function tournamentFootText(
  m: TournamentHostMatchLike,
  finishReasonLabels: Record<string, string>
): string {
  if (m.status === 'active') return '● En juego · tocar para ver';
  if (m.status === 'finished') {
    if (m.finishReason === 'bye') return 'Esperando rival de la siguiente ronda';
    const reason = m.finishReason ? finishReasonLabels[m.finishReason] : '';
    return reason ? `Finalizado · ${reason}` : 'Finalizado';
  }
  return '';
}

/** Teacher lobby should offer a live board while the tournament is in progress. */
export function teacherShowsLiveBoard(canManage: boolean, status: string): boolean {
  return canManage && status === 'in_progress';
}

/** Hub + polling only while the tournament can still change. */
export function tournamentNeedsRealtime(status: string): boolean {
  return status !== 'finished' && status !== 'cancelled';
}

export function filterLiveRealMatches<T extends TournamentHostMatchLike>(matches: T[]): T[] {
  return matches.filter((m) => m.status === 'active' && !m.player1IsBye && !m.player2IsBye);
}
