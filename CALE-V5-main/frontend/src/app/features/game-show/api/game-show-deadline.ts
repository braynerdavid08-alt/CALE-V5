/** Remaining whole seconds until a UTC deadline from the GameShow lobby. */
export function secondsUntilDeadline(
  deadlineUtc: string | null | undefined,
  nowMs: number = Date.now()
): number | null {
  if (!deadlineUtc) return null;
  const end = Date.parse(deadlineUtc);
  if (Number.isNaN(end)) return null;
  return Math.max(0, Math.ceil((end - nowMs) / 1000));
}

export function phaseHasTurnClock(phase: string | null | undefined): boolean {
  return (
    phase === 'WaitingBuzz'
    || phase === 'FaceOff'
    || phase === 'FaceOffSecond'
    || phase === 'Control'
    || phase === 'Playing'
    || phase === 'Steal'
  );
}
