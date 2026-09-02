import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SessionStore } from './core/auth/session.store';
import { ThemeService } from './core/theme/theme.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: '<router-outlet />'
})
export class AppComponent {
  /** Ensures theme is applied as soon as the app boots. */
  private readonly theme = inject(ThemeService);
  private readonly session = inject(SessionStore);

  constructor() {
    void this.session.bootstrap();
  }
}
