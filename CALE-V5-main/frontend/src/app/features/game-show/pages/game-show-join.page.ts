import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { mapApiError } from '../../../core/http/map-api-error';
import { SessionStore } from '../../../core/auth/session.store';
import { GameShowApi } from '../api/game-show.api';

@Component({
  selector: 'app-game-show-join-page',
  standalone: true,
  imports: [FormsModule, RouterLink, UiButtonComponent, UiErrorComponent],
  template: `
    <section class="join">
      <p class="eyebrow">100 Estudiantes Dijeron</p>
      <h1>Unirse a la partida</h1>
      <ui-error [message]="error()" />
      <label class="field">Código
        <input class="input" [(ngModel)]="code" name="code" maxlength="12" />
      </label>
      <label class="field">Tu nombre
        <input class="input" [(ngModel)]="name" name="name" />
      </label>
      <label class="field">Equipo
        <select class="input" [(ngModel)]="team" name="team">
          <option value="A">Equipo A</option>
          <option value="B">Equipo B</option>
        </select>
      </label>
      <ui-button type="button" [loading]="loading()" (click)="submit()">Entrar</ui-button>
      <p class="hint"><a routerLink="/live/join">¿Buscabas Aula en vivo?</a></p>
    </section>
  `,
  styles: [`
    .join { max-width: 26rem; margin: 2rem auto; display: grid; gap: 0.85rem; padding: 1rem; }
    .eyebrow { margin: 0; letter-spacing: 0.08em; text-transform: uppercase; color: var(--color-primary); font-weight: 800; font-size: 0.75rem; }
    .field { display: grid; gap: 0.35rem; font-weight: 600; }
    .hint { color: var(--color-text-secondary); }
  `]
})
export class GameShowJoinPage implements OnInit {
  private readonly api = inject(GameShowApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly session = inject(SessionStore);

  readonly error = signal<string | null>(null);
  readonly loading = signal(false);
  code = '';
  name = '';
  team = 'A';

  ngOnInit(): void {
    const pre = this.route.snapshot.paramMap.get('code');
    if (pre) this.code = pre.toUpperCase();
    this.name = this.session.user()?.name?.split(' ')[0] || '';
  }

  submit(): void {
    this.error.set(null);
    this.loading.set(true);
    this.api.join(this.code.trim(), this.name.trim(), this.team).subscribe({
      next: (res) => {
        this.loading.set(false);
        this.api.savePlayerToken(res.sessionId, res.playerToken);
        localStorage.setItem(`cale.game-show.team.${res.sessionId}`, this.team);
        void this.router.navigate(['/game-show/play', res.sessionId]);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}
