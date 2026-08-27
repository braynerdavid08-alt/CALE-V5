import {
  HttpErrorResponse,
  HttpInterceptorFn,
  HttpResponse
} from '@angular/common/http';
import { tap } from 'rxjs/operators';

const HEADER = 'X-Request-Id';
const STORAGE_KEY = 'cale.lastRequestId';

export function getLastRequestId(): string | null {
  try {
    return sessionStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function rememberRequestId(id: string | null | undefined): void {
  if (!id) {
    return;
  }
  try {
    sessionStorage.setItem(STORAGE_KEY, id);
  } catch {
    /* ignore */
  }
}

export function extractTraceId(error: unknown): string | null {
  if (!(error instanceof HttpErrorResponse)) {
    return getLastRequestId();
  }

  const fromBody = error.error?.traceId;
  if (typeof fromBody === 'string' && fromBody.trim()) {
    return fromBody.trim();
  }

  const fromHeader = error.headers?.get(HEADER);
  if (fromHeader) {
    return fromHeader;
  }

  return getLastRequestId();
}

/** Propagates correlation id and remembers it for support messages. */
export const observabilityInterceptor: HttpInterceptorFn = (req, next) => {
  const existing = req.headers.get(HEADER) ?? getLastRequestId();
  const outbound = existing
    ? req.clone({ setHeaders: { [HEADER]: existing } })
    : req;

  return next(outbound).pipe(
    tap({
      next: (event) => {
        if (event instanceof HttpResponse) {
          rememberRequestId(event.headers.get(HEADER));
        }
      },
      error: (err) => {
        if (err instanceof HttpErrorResponse) {
          rememberRequestId(
            err.headers?.get(HEADER) ?? err.error?.traceId ?? existing
          );
        }
      }
    })
  );
};
