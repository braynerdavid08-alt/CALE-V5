import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SessionStore } from '../../core/auth/session.store';
import { mapApiError } from '../../core/http/map-api-error';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { Pqr, PqrApi, PqrCreated, PqrKind } from './pqr.api';
import { PqrTimelineComponent } from './pqr-timeline.component';

/** PQR y derechos del titular. Pública: también la usa quien no puede iniciar sesión. */
@Component({
  selector: 'app-pqr-page',
  standalone: true,
  imports: [DatePipe, ReactiveFormsModule, RouterLink, UiButtonComponent, UiErrorComponent, PqrTimelineComponent],
  template: `
    <section class="pqr">
      <header>
        <h1>PQR y datos personales</h1>
        <p class="lead">
          Envía una petición, queja o reclamo, o ejerce tus derechos sobre tus datos personales: conocerlos, actualizarlos,
          corregirlos, pedir que se eliminen o revocar tu autorización. Cada solicitud recibe un número de radicado y una
          fecha máxima de respuesta.
        </p>
      </header>

      @if (created(); as c) {
        <div class="card done" role="status">
          <h2>Recibimos tu solicitud</h2>
          <p>Radicado: <strong>{{ c.radicado }}</strong></p>
          <p>Código de seguimiento: <strong class="code">{{ c.trackingCode }}</strong></p>
          <p>Te responderemos a más tardar el <strong>{{ c.dueAt | date: 'dd/MM/yyyy' }}</strong>.</p>
          <p class="muted">
            Guarda el código: es la única forma de consultar el estado sin iniciar sesión.
            @if (c.emailSent) { También te lo enviamos por correo. }
          </p>
          <div class="row">
            <ui-button type="button" variant="secondary" (click)="track(c.trackingCode)">Ver el estado</ui-button>
            <ui-button type="button" variant="ghost" (click)="created.set(null)">Enviar otra solicitud</ui-button>
          </div>
        </div>
      } @else {
        <form class="card" [formGroup]="form" (ngSubmit)="submit()">
          <h2>Nueva solicitud</h2>
          <ui-error [message]="error()" />
          <label class="field">
            Tipo de solicitud
            <select formControlName="kind">
              <option value="" disabled>Elige una opción</option>
              @for (k of kinds(); track k.key) {
                <option [value]="k.key">{{ k.label }}</option>
              }
            </select>
          </label>
          @if (selectedKind(); as k) {
            <p class="hint">
              Plazo de respuesta: {{ k.deadlineDays }} días hábiles.
              @if (k.isDataRight) { Lo fija la ley de protección de datos personales. }
            </p>
          }
          @if (loggedIn()) {
            <p class="hint">La enviaremos con el nombre y correo de tu cuenta.</p>
          } @else {
            <label class="field">
              Nombre completo
              <input type="text" formControlName="name" autocomplete="name" maxlength="150" />
            </label>
            <label class="field">
              Correo para la respuesta
              <input type="email" formControlName="email" autocomplete="email" maxlength="254" />
            </label>
          }
          <label class="field">
            Asunto
            <input type="text" formControlName="subject" maxlength="200" />
          </label>
          <label class="field">
            Describe tu solicitud
            <textarea formControlName="message" rows="6" maxlength="4000"
              placeholder="Cuéntanos qué necesitas. No incluyas contraseñas."></textarea>
            <small class="muted">{{ form.controls.message.value.length }}/4000 · mínimo 20 caracteres</small>
          </label>
          @if (form.touched && form.invalid) {
            <span class="field-error" role="alert">Completa todos los campos (la descripción necesita al menos 20 caracteres).</span>
          }
          <ui-button type="submit" [loading]="busy()">Enviar solicitud</ui-button>
          <p class="muted small">
            Tus datos se usan solo para tramitar esta solicitud, según la
            <a routerLink="/legal/data-processing">Política de Tratamiento de Datos</a>.
          </p>
        </form>
      }

      <div class="card">
        <h2>Consultar una solicitud</h2>
        @if (loggedIn()) {
          <p class="muted">Tus solicitudes también aparecen en <a routerLink="/mis-pqr">PQR y mis datos</a>.</p>
        }
        <div class="track">
          <input type="text" [value]="code()" (input)="code.set($any($event.target).value)" placeholder="Código de seguimiento" maxlength="64" />
          <ui-button type="button" variant="secondary" [disabled]="code().trim().length < 6" (click)="track(code())">Consultar</ui-button>
        </div>
        <ui-error [message]="trackError()" />
        @if (tracked(); as t) {
          <legal-pqr-timeline [pqr]="t" />
        }
      </div>
    </section>
  `,
  styles: [`
    .pqr { max-width: 44rem; margin: 0 auto; padding: 1.5rem 1rem 3rem; display: grid; gap: 1rem; }
    h1 { margin: 0 0 0.4rem; font-size: 1.6rem; }
    h2 { margin: 0; font-size: 1.1rem; }
    .lead { margin: 0; line-height: 1.55; color: var(--color-text-secondary); }
    .card {
      display: grid;
      gap: 0.8rem;
      padding: 1.1rem;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      background: var(--color-surface);
    }
    .card.done { border-color: var(--color-success); }
    .card p { margin: 0; }
    .code { font-family: ui-monospace, monospace; letter-spacing: 0.05em; }
    .hint { margin: -0.3rem 0 0; font-size: 0.85rem; color: var(--color-text-secondary); }
    .muted { color: var(--color-text-secondary); font-size: 0.85rem; }
    .small { font-size: 0.8rem; }
    .row, .track { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    .track input { flex: 1 1 14rem; min-width: 0; }
  `]
})
export class PqrPage {
  private readonly api = inject(PqrApi);
  private readonly fb = inject(FormBuilder);
  private readonly session = inject(SessionStore);

  readonly loggedIn = computed(() => this.session.isAuthenticated());
  readonly kinds = signal<PqrKind[]>([]);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly created = signal<PqrCreated | null>(null);
  readonly code = signal('');
  readonly tracked = signal<Pqr | null>(null);
  readonly trackError = signal<string | null>(null);
  readonly kindValue = signal('');
  readonly selectedKind = computed(() => this.kinds().find((k) => k.key === this.kindValue()) ?? null);

  readonly form = this.fb.nonNullable.group({
    kind: ['', Validators.required],
    name: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(150)]],
    email: ['', [Validators.required, Validators.email]],
    subject: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(200)]],
    message: ['', [Validators.required, Validators.minLength(20), Validators.maxLength(4000)]]
  });

  constructor() {
    if (this.loggedIn()) {
      this.form.controls.name.disable();
      this.form.controls.email.disable();
    }
    const query = inject(ActivatedRoute).snapshot.queryParamMap;
    const kind = query.get('tipo');
    if (kind) this.form.controls.kind.setValue(kind);
    this.kindValue.set(this.form.controls.kind.value);
    this.form.controls.kind.valueChanges.subscribe((v) => this.kindValue.set(v));
    this.api.kinds().subscribe({ next: (k) => this.kinds.set(k), error: () => this.kinds.set([]) });
    const code = query.get('codigo');
    if (code) this.track(code);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.api
      .create({
        kind: v.kind,
        subject: v.subject,
        message: v.message,
        ...(this.loggedIn() ? {} : { name: v.name, email: v.email })
      })
      .subscribe({
        next: (created) => {
          this.busy.set(false);
          this.created.set(created);
          this.form.reset({ kind: '', name: '', email: '', subject: '', message: '' });
        },
        error: (err) => {
          this.busy.set(false);
          this.error.set(mapApiError(err));
        }
      });
  }

  track(code: string): void {
    this.code.set(code);
    this.trackError.set(null);
    this.api.track(code).subscribe({
      next: (pqr) => this.tracked.set(pqr),
      error: (err) => {
        this.tracked.set(null);
        this.trackError.set(mapApiError(err));
      }
    });
  }
}
