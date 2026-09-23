/** Remaining whole seconds until a UTC deadline from the GameShow lobby. */
export function secondsUntilDeadline(
  deadlineUtc: string | null | undefined,
  nowMs: number = Date.now()
): number | null {
  if (!deadlineUtc) return null;
  let raw = String(deadlineUtc).trim();
  if (!raw) return null;
  // Server deadlines are UTC; if a payload omits the offset, force Z so local TZ won't inflate the timer.
  if (!/[zZ]|[+-]\d{2}:?\d{2}$/.test(raw)) {
    raw = `${raw}Z`;
  }
  const end = Date.parse(raw);
  if (Number.isNaN(end)) return null;
  return Math.max(0, Math.ceil((end - nowMs) / 1000));
}

/**
 * Prefer server-computed remaining seconds (immune to client clock skew).
 * Between lobby syncs, count down from the last server snapshot using local elapsed time only.
 */
export function remainingFromServerSnapshot(
  serverSeconds: number | null | undefined,
  capturedAtMs: number,
  nowMs: number = Date.now()
): number | null {
  if (serverSeconds == null || !Number.isFinite(serverSeconds)) return null;
  const elapsed = Math.max(0, Math.floor((nowMs - capturedAtMs) / 1000));
  return Math.max(0, Math.floor(serverSeconds) - elapsed);
}

export function phaseHasTurnClock(phase: string | null | undefined): boolean {
  return (
    phase === 'FaceOff'
    || phase === 'FaceOffSecond'
    || phase === 'Control'
    || phase === 'Playing'
    || phase === 'Steal'
  );
}
