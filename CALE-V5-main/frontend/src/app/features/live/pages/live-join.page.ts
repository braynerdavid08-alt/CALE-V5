import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { mapApiError } from '../../../core/http/map-api-error';
import { SessionStore } from '../../../core/auth/session.store';
import { LiveApi } from '../api/live.api';

const TOKEN_KEY = 'cale.live.participant';

@Component({
  selector: 'app-live-join-page',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, UiButtonComponent, UiErrorComponent],
  template: `
    <section class="join">
      <p class="eyebrow">CALE LIVE</p>
      <h1>Unirse a la sala</h1>
      <p class="lead">Ingresa el código que proyecta el instructor (o abre el enlace del QR).</p>
      <ui-error [message]="error()" />
      <form [formGroup]="form" (ngSubmit)="submit()">
        <label class="field">
          Código
          <input formControlName="code" autocomplete="off" maxlength="12" />
        </label>
        <label class="field">
          Tu nombre
          <input formControlName="displayName" autocomplete="nickname" />
        </label>
        <ui-button type="submit" [loading]="loading()">Entrar</ui-button>
      </form>
      <p class="hint"><a routerLink="/login">Iniciar sesión</a> (opcional) para vincular tu cuenta.</p>
    </section>
  `,
  styles: `
    :host {
      display: grid;
      min-height: 100vh;
      place-items: center;
      padding: max(1rem, env(safe-area-inset-top)) max(1rem, env(safe-area-inset-right))
        max(1rem, env(safe-area-inset-bottom)) max(1rem, env(safe-area-inset-left));
      background: var(--color-background, #0f1419);
      color: var(--color-text, #fff);
    }
    .join { width: min(420px, 100%); }
    .eyebrow { color: #2bb0ed; font-weight: 800; letter-spacing: 0.08em; text-transform: uppercase; font-size: 0.75rem; }
    .lead { color: #9aa4b2; }
    .field { display: grid; gap: 0.35rem; margin: 0.85rem 0; }
    input {
      padding: 0.85rem 1rem; border-radius: 12px; border: 1px solid #2a3441;
      background: #161d27; color: #fff; font-size: 1.1rem; text-transform: uppercase;
    }
    .field:last-of-type input { text-transform: none; }
    .hint { margin-top: 1rem; color: #9aa4b2; font-size: 0.9rem; }
    a { color: #2bb0ed; }
  `
})
export class LiveJoinPage implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(LiveApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly session = inject(SessionStore);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.minLength(4)]],
    displayName: ['', [Validators.required, Validators.maxLength(80)]]
  });

  ngOnInit(): void {
    const code = this.route.snapshot.paramMap.get('code');
    if (code) {
      this.form.controls.code.setValue(code.toUpperCase());
    }
    const user = this.session.user();
    if (user?.name) {
      this.form.controls.displayName.setValue(user.name);
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    const { code, displayName } = this.form.getRawValue();
    this.api.join(code.trim().toUpperCase(), displayName.trim()).subscribe({
      next: (res) => {
        this.loading.set(false);
        sessionStorage.setItem(
          TOKEN_KEY,
          JSON.stringify({
            sessionId: res.sessionId,
            participantToken: res.participantToken,
            displayName: res.displayName
          })
        );
        void this.router.navigate(['/live/play', res.sessionId]);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}

export function readLiveParticipant(sessionId: number): {
  participantToken: string;
  displayName: string;
} | null {
  try {
    const raw = sessionStorage.getItem(TOKEN_KEY);
    if (!raw) {
      return null;
    }
    const parsed = JSON.parse(raw) as {
      sessionId: number;
      participantToken: string;
      displayName: string;
    };
    if (parsed.sessionId !== sessionId) {
      return null;
    }
    return parsed;
  } catch {
    return null;
  }
}
