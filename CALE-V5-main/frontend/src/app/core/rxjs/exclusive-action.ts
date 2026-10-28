import { WritableSignal } from '@angular/core';
import { Observable, Subscription, finalize } from 'rxjs';

/**
 * Runs a single in-flight action keyed by a busy signal.
 * Extra clicks while busy are ignored (exhaustMap-like for imperative handlers).
 */
export function runExclusive(
  busy: WritableSignal<boolean>,
  work: () => Observable<unknown>,
  handlers?: {
    next?: (value: unknown) => void;
    error?: (err: unknown) => void;
  }
): Subscription | null {
  if (busy()) {
    return null;
  }
  busy.set(true);
  return work().pipe(finalize(() => busy.set(false))).subscribe({
    next: (value) => handlers?.next?.(value),
    error: (err) => handlers?.error?.(err)
  });
}

/**
 * Same idea for a numeric “which id is busy” signal (join/accept per row).
 * Returns false if another id (or the same) is already in flight.
 */
export function claimExclusiveId(
  busyId: WritableSignal<number | null>,
  id: number
): boolean {
  if (busyId() != null) {
    return false;
  }
  busyId.set(id);
  return true;
}
