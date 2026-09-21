import { Observable, fromEvent, EMPTY, timer } from 'rxjs';
import { exhaustMap, map, startWith, switchMap } from 'rxjs/operators';

function pageIsVisible(): boolean {
  return typeof document === 'undefined' || document.visibilityState === 'visible';
}

export type PollWhileVisibleOptions = {
  /** When true (default), fire once immediately when visible. */
  leading?: boolean;
};

/**
 * Polls `work` every `intervalMs` while the tab is visible.
 * Pauses when the document is hidden; resumes (with a leading tick if enabled) on focus.
 * Pair with `takeUntilDestroyed()` at the call site.
 */
export function pollWhileVisible<T>(
  intervalMs: number,
  work: () => Observable<T>,
  options?: PollWhileVisibleOptions
): Observable<T> {
  const leading = options?.leading !== false;
  const visibility$ = fromEvent(document, 'visibilitychange').pipe(
    startWith(null),
    map(() => pageIsVisible())
  );

  return visibility$.pipe(
    switchMap((visible) => {
      if (!visible) {
        return EMPTY;
      }
      const ticks$ = leading ? timer(0, intervalMs) : timer(intervalMs, intervalMs);
      return ticks$.pipe(exhaustMap(() => work()));
    })
  );
}
