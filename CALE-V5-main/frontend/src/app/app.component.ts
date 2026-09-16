import { Component, inject } from '@angular/core';
import { NavigationEnd, NavigationError, Router, RouterOutlet } from '@angular/router';
import { ThemeService } from './core/theme/theme.service';

const CHUNK_RELOAD_KEY = 'cale.chunk-reload';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: '<router-outlet />'
})
export class AppComponent {
  /** Ensures theme is applied as soon as the app boots. */
  private readonly theme = inject(ThemeService);
  private readonly router = inject(Router);

  constructor() {
    this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) {
        sessionStorage.removeItem(CHUNK_RELOAD_KEY);
        return;
      }
      if (!(event instanceof NavigationError) || !this.isStaleChunkError(event.error)) {
        return;
      }

      // A tab left open during a deployment may still request an old lazy chunk.
      // Reload once so index.html points at the current build instead of leaving
      // the user on a tile that appears not to open.
      if (sessionStorage.getItem(CHUNK_RELOAD_KEY) !== '1') {
        sessionStorage.setItem(CHUNK_RELOAD_KEY, '1');
        window.location.reload();
      }
    });
  }

  private isStaleChunkError(error: unknown): boolean {
    const message = String(
      error instanceof Error ? `${error.name}: ${error.message}` : error ?? ''
    ).toLowerCase();
    return message.includes('chunkloaderror')
      || message.includes('loading chunk')
      || message.includes('dynamically imported module')
      || message.includes('importing a module script failed');
  }
}
