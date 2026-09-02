/** Server timestamps are UTC but often arrive without a "Z" suffix. */
export function parseUtcIso(value?: string | null): number {
  if (!value) {
    return NaN;
  }
  const s = value.trim();
  if (!s) {
    return NaN;
  }
  if (/[zZ]$/.test(s) || /[+-]\d{2}:\d{2}$/.test(s)) {
    return Date.parse(s);
  }
  return Date.parse(`${s}Z`);
}

export function computeSecondsLeft(
  q: {
    opensAt?: string | null;
    closesAt?: string | null;
    secondsPerQuestion?: number;
  },
  configSeconds?: number
): number | null {
  const maxSecs = Math.max(5, q.secondsPerQuestion ?? configSeconds ?? 30);
  const opensMs = parseUtcIso(q.opensAt);
  const closesMs = parseUtcIso(q.closesAt);

  if (Number.isFinite(closesMs) && Number.isFinite(opensMs)) {
    const durationSec = Math.round((closesMs - opensMs) / 1000);
    if (durationSec > 0 && durationSec <= 600) {
      const left = Math.max(0, Math.ceil((closesMs - Date.now()) / 1000));
      if (left <= durationSec + 2) {
        return left;
      }
    }
  }

  if (Number.isFinite(opensMs)) {
    const elapsed = Math.floor((Date.now() - opensMs) / 1000);
    if (elapsed >= -2 && elapsed <= maxSecs + 2) {
      return Math.max(0, maxSecs - elapsed);
    }
  }

  return null;
}
