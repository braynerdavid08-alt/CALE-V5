import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { mapApiError } from '../../../core/http/map-api-error';
import { SessionStore } from '../../../core/auth/session.store';
import { LiveApi } from '../api/live.api';
import { LiveQrScannerComponent } from '../components/live-qr-scanner.component';

const TOKEN_KEY = 'cale.live.participant';

@Component({
  selector: 'app-live-join-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    UiButtonComponent,
    UiErrorComponent,
    LiveQrScannerComponent
  ],
  template: `
    <section class="join">
      <p class="eyebrow">CALE LIVE</p>
      <h1>Unirse a la sala</h1>
      <p class="lead">
        Debes tener sesión iniciada para entrar y registrar tu progreso.
      </p>
      <ui-error [message]="error()" />
      <div class="mode-tabs">
        <button type="button" [class.on]="mode() === 'code'" (click)="setMode('code')">Código</button>
        <button type="button" [class.on]="mode() === 'scan'" (click)="setMode('scan')">Escanear QR</button>
      </div>
      @if (mode() === 'scan') {
        <app-live-qr-scanner (codeScanned)="joinWithCode($event)" />
      } @else {
        <form [formGroup]="form" (ngSubmit)="submit()">
          <label class="field">
            Código de sala
            <input formControlName="code" autocomplete="off" maxlength="12" />
          </label>
          <p class="account">Entrarás como <strong>{{ accountName() }}</strong></p>
          <ui-button type="submit" [loading]="loading()">Entrar</ui-button>
        </form>
      }
      <p class="hint"><a [routerLink]="homeLink()">Volver al panel</a></p>
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
    .mode-tabs {
      display: flex;
      gap: 0.5rem;
      margin: 1rem 0;
    }
    .mode-tabs button {
      flex: 1;
      padding: 0.55rem 0.75rem;
      border-radius: 10px;
      border: 1px solid #2a3441;
      background: #161d27;
      color: #9aa4b2;
      font-weight: 700;
      cursor: pointer;
    }
    .mode-tabs button.on {
      border-color: #2bb0ed;
      color: #fff;
      background: color-mix(in srgb, #2bb0ed 18%, #161d27);
    }
    .field { display: grid; gap: 0.35rem; margin: 0.85rem 0; }
    input {
      padding: 0.85rem 1rem; border-radius: 12px; border: 1px solid #2a3441;
      background: #161d27; color: #fff; font-size: 1.1rem; text-transform: uppercase;
    }
    .account { color: #9aa4b2; font-size: 0.95rem; }
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
  readonly mode = signal<'code' | 'scan'>('code');

  homeLink(): string {
    return this.session.homeRoute();
  }

  readonly form = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.minLength(4)]]
  });

  ngOnInit(): void {
    const code = this.route.snapshot.paramMap.get('code');
    if (code) {
      this.form.controls.code.setValue(code.toUpperCase());
    }
    if (this.route.snapshot.queryParamMap.get('scan') === '1') {
      this.mode.set('scan');
    }
  }

  accountName(): string {
    return this.session.user()?.name || 'tu cuenta';
  }

  setMode(next: 'code' | 'scan'): void {
    this.mode.set(next);
    this.error.set(null);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { code } = this.form.getRawValue();
    this.joinWithCode(code.trim().toUpperCase());
  }

  joinWithCode(code: string): void {
    if (!code || code.length < 4) {
      this.error.set('Código inválido.');
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    this.form.controls.code.setValue(code);
    const displayName = this.session.user()?.name || 'Estudiante';
    this.api.join(code, displayName).subscribe({
      next: (res) => {
        this.loading.set(false);
        saveLiveParticipant(res);
        void this.router.navigate(['/live/play', res.sessionId]);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}

export function saveLiveParticipant(res: {
  sessionId: number;
  participantToken: string;
  displayName: string;
  participantId?: number;
}): void {
  const payload = JSON.stringify({
    sessionId: res.sessionId,
    participantToken: res.participantToken,
    displayName: res.displayName,
    participantId: res.participantId ?? null
  });
  sessionStorage.setItem(`${TOKEN_KEY}:${res.sessionId}`, payload);
  sessionStorage.setItem(TOKEN_KEY, payload);
}

export function readLiveParticipant(sessionId: number): {
  participantToken: string;
  displayName: string;
  participantId?: number | null;
} | null {
  try {
    const raw =
      sessionStorage.getItem(`${TOKEN_KEY}:${sessionId}`)
      ?? sessionStorage.getItem(TOKEN_KEY);
    if (!raw) {
      return null;
    }
    const parsed = JSON.parse(raw) as {
      sessionId: number;
      participantToken: string;
      displayName: string;
      participantId?: number | null;
    };
    if (parsed.sessionId !== sessionId) {
      return null;
    }
    return parsed;
  } catch {
    return null;
  }
}
