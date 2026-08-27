import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SessionStore } from './core/auth/session.store';
import { ThemeService } from './core/theme/theme.service';
import { AuthApi } from './features/auth/api/auth.api';

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
  private readonly authApi = inject(AuthApi);

  constructor() {
    if (this.session.isAuthenticated()) {
      this.authApi.me().subscribe({
        next: (me) => this.session.applySchoolContext(me.school ?? null),
        error: () => undefined
      });
    }
  }
}
