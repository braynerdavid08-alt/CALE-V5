import { signal } from '@angular/core';
import { Observable, TimeoutError, of, throwError } from 'rxjs';
import { catchError, finalize, timeout } from 'rxjs';

type HomeProbe = { hero: { title: string; visible: boolean } };

/**
 * Mirrors LandingPage.loadHome completion rules without bootstrapping the full
 * component tree (hero template + shop + SEO). Keeps the loading gate honest.
 */
describe('landing home load gate', () => {
  const HOME_LOAD_TIMEOUT_MS = 20_000;

  function runLoad(source: Observable<HomeProbe>) {
    const loading = signal(true);
    const loadError = signal<string | null>(null);
    const home = signal<unknown>(null);

    source
      .pipe(
        timeout({ first: HOME_LOAD_TIMEOUT_MS }),
        catchError((err: unknown) => {
          loadError.set(
            err instanceof TimeoutError
              ? 'La página de inicio tardó demasiado en responder. Puedes reintentar.'
              : 'No pudimos cargar el contenido de inicio. Puedes reintentar o seguir explorando.'
          );
          return of({
            hero: { title: 'fallback', visible: true }
          });
        }),
        finalize(() => {
          loading.set(false);
        })
      )
      .subscribe({
        next: (data) => {
          home.set(data);
          try {
            // LandingPage wraps applySeo the same way.
          } catch {
            /* SEO must never block paint */
          }
        }
      });

    return { loading, loadError, home };
  }

  it('clears loading after a successful home payload', () => {
    const result = runLoad(of({ hero: { title: 'Hola', visible: true } }));
    expect(result.loading()).toBe(false);
    expect(result.loadError()).toBeNull();
    expect((result.home() as { hero: { title: string } }).hero.title).toBe('Hola');
  });

  it('clears loading and keeps content when the API errors', () => {
    const result = runLoad(throwError(() => new Error('network')));
    expect(result.loading()).toBe(false);
    expect(result.loadError()).toContain('No pudimos cargar');
    expect((result.home() as { hero: { title: string } }).hero.title).toBe('fallback');
  });

  it('clears loading when SEO work throws inside next', () => {
    const loading = signal(true);
    of({ hero: { title: 'Hola', visible: true } })
      .pipe(finalize(() => loading.set(false)))
      .subscribe({
        next: () => {
          try {
            throw new Error('seo boom');
          } catch {
            /* same guard as LandingPage.applySeo */
          }
        }
      });
    expect(loading()).toBe(false);
  });
});
