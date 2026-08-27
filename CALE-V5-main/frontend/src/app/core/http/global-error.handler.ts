import { HttpErrorResponse } from '@angular/common/http';
import { ErrorHandler, Injectable } from '@angular/core';
import { env } from '../config/env';
import { extractTraceId, getLastRequestId } from './observability.interceptor';

/**
 * Must NOT inject HttpClient: ErrorHandler is created during bootstrap and
 * that creates a circular DI graph (blank white/dark screen).
 */
@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  private lastSent = '';
  private lastSentAt = 0;

  handleError(error: unknown): void {
    const message = this.describe(error);
    const stack = error instanceof Error ? error.stack ?? '' : '';
    const traceId = extractTraceId(error) ?? getLastRequestId() ?? undefined;

    console.error('[CALE]', message, error);

    if (error instanceof HttpErrorResponse && error.url?.includes('/api/client-errors')) {
      return;
    }

    this.report(message, stack, traceId, 'global');
  }

  private describe(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      return `HTTP ${error.status}: ${error.message}`;
    }
    if (error instanceof Error) {
      return error.message || 'Unhandled error';
    }
    return String(error ?? 'Unknown error');
  }

  private report(
    message: string,
    stack: string,
    traceId: string | undefined,
    source: string
  ): void {
    const key = `${source}|${message}|${traceId ?? ''}`;
    const now = Date.now();
    if (key === this.lastSent && now - this.lastSentAt < 15_000) {
      return;
    }
    this.lastSent = key;
    this.lastSentAt = now;

    const body = JSON.stringify({
      message: message.slice(0, 200),
      stack: stack.slice(0, 2000),
      url: typeof location !== 'undefined' ? location.href.slice(0, 500) : '',
      traceId,
      source
    });

    try {
      void fetch(`${env.apiUrl}/api/client-errors`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body,
        keepalive: true
      }).catch(() => undefined);
    } catch {
      /* ignore reporting failures */
    }
  }
}
