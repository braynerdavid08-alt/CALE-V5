import { HttpErrorResponse, HttpEvent, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { Observable, catchError, from, mergeMap, throwError } from 'rxjs';

/** Must match src/Cale.Api/Middleware/WireEncryptionMiddleware.cs. */
const HEADER = 'X-Cale-Wire';
const A = [
  0x3a, 0x91, 0x5c, 0xe7, 0x12, 0x8f, 0x44, 0xd0, 0x6b, 0x27, 0xf3, 0x09, 0xae, 0x5d, 0x71, 0xc8,
  0x95, 0x1e, 0x60, 0xb4, 0x2f, 0xd9, 0x83, 0x4a, 0x07, 0xec, 0x58, 0x36, 0xa1, 0x7c, 0xf0, 0x1b
];
const B = [
  0xc4, 0x0d, 0xa7, 0x39, 0xe8, 0x52, 0x1f, 0x9b, 0x70, 0xd6, 0x2c, 0x85, 0x4e, 0xb3, 0x6a, 0x17,
  0xfe, 0x41, 0x98, 0x2d, 0x63, 0xca, 0x05, 0xbf, 0x76, 0x1a, 0xe4, 0x8d, 0x30, 0x59, 0xa2, 0xcf
];

const canUse = typeof crypto !== 'undefined' && !!crypto.subtle;
const canGunzip = typeof DecompressionStream !== 'undefined';
let keyPromise: Promise<CryptoKey> | null = null;

function key(): Promise<CryptoKey> {
  keyPromise ??= crypto.subtle.importKey(
    'raw',
    new Uint8Array(A.map((a, i) => a ^ B[i])),
    'AES-GCM',
    false,
    ['decrypt']
  );
  return keyPromise;
}

async function open(body: ArrayBuffer, gzip: boolean): Promise<string> {
  const bytes = new Uint8Array(body);
  let plain: ArrayBuffer = await crypto.subtle.decrypt(
    { name: 'AES-GCM', iv: bytes.subarray(0, 12) },
    await key(),
    bytes.subarray(12)
  );
  if (gzip) {
    const stream = new Blob([plain]).stream().pipeThrough(new DecompressionStream('gzip'));
    plain = await new Response(stream).arrayBuffer();
  }
  return new TextDecoder().decode(plain);
}

function parse(text: string): unknown {
  if (!text) {
    return null;
  }
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

async function decodeBody(body: unknown, wire: string | null): Promise<unknown> {
  if (!(body instanceof ArrayBuffer)) {
    return body ?? null;
  }
  const text = wire ? await open(body, wire === 'gz') : new TextDecoder().decode(body);
  return parse(text);
}

/**
 * JSON from our API travels encrypted so the Network panel shows binary. Requests that ask
 * for blobs or text are left alone.
 */
export const wireInterceptor: HttpInterceptorFn = (req, next) => {
  if (!canUse || req.responseType !== 'json' || !req.url.includes('/api/')) {
    return next(req);
  }

  const wireReq = req.clone({
    responseType: 'arraybuffer',
    setHeaders: { [HEADER]: canGunzip ? 'gz' : '1' }
  });

  return next(wireReq).pipe(
    mergeMap((event): Observable<HttpEvent<unknown>> => {
      if (!(event instanceof HttpResponse)) {
        return from([event]);
      }
      return from(
        decodeBody(event.body, event.headers.get(HEADER)).then((body) => event.clone({ body }))
      );
    }),
    catchError((err: unknown) => {
      if (!(err instanceof HttpErrorResponse) || !(err.error instanceof ArrayBuffer)) {
        return throwError(() => err);
      }
      return from(decodeBody(err.error, null)).pipe(
        mergeMap((error) =>
          throwError(
            () =>
              new HttpErrorResponse({
                error,
                headers: err.headers,
                status: err.status,
                statusText: err.statusText,
                url: err.url ?? undefined
              })
          )
        )
      );
    })
  );
};
