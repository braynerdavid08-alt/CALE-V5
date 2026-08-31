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

function newRequestId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID().replace(/-/g, '');
  }
  return `${Date.now().toString(16)}${Math.random().toString(16).slice(2, 10)}`;
}

/** Sends a fresh correlation id per request and remembers the last one for support. */
export const observabilityInterceptor: HttpInterceptorFn = (req, next) => {
  const requestId = req.headers.get(HEADER) || newRequestId();
  const outbound = req.clone({ setHeaders: { [HEADER]: requestId } });

  return next(outbound).pipe(
    tap({
      next: (event) => {
        if (event instanceof HttpResponse) {
          rememberRequestId(event.headers.get(HEADER) ?? requestId);
        }
      },
      error: (err) => {
        if (err instanceof HttpErrorResponse) {
          rememberRequestId(
            err.headers?.get(HEADER) ?? err.error?.traceId ?? requestId
          );
        }
      }
    })
  );
};
