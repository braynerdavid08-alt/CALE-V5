import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { mapApiError } from '../../core/http/map-api-error';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import { GuardianView, LegalApi } from './legal.api';

/** Página pública a la que llega el representante legal desde el enlace personal del correo. */
@Component({
  selector: 'app-guardian-consent-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiErrorComponent, UiLoadingComponent],
  template: `
    <section class="guardian">
      <h1>Autorización del representante legal</h1>
      <ui-error [message]="error()" />

      @if (loading()) {
        <ui-loading />
      }
      @if (view(); as v) {
        <p class="lead">
          Hola, <strong>{{ v.guardianName }}</strong>. <strong>{{ v.minorFirstName }}</strong> creó una cuenta de estudiante en
          Luz Verde, una plataforma para prepararse para el examen teórico de conducción, y te indicó como su
          {{ v.guardianRelationship.toLowerCase() }}.
        </p>

        @switch (v.status) {
          @case ('PENDING') {
            @if (v.linkExpired) {
              <p class="notice">Este enlace venció. Pide al estudiante que te lo envíe de nuevo desde su cuenta.</p>
            } @else {
              <div class="card">
                <h2>Antes de decidir, lee estos documentos</h2>
                <ul class="docs">
                  @for (d of v.documents; track d.key) {
                    <li>
                      <a [routerLink]="['/legal', d.key]" target="_blank">{{ d.title }}</a>
                      @if (d.version) { <small>versión {{ d.version }}</small> }
                    </li>
                  }
                </ul>
                <p class="muted">
                  Para prestarle el servicio tratamos su nombre, correo, fecha de nacimiento, foto de perfil (si la sube),
                  progreso, resultados de práctica y conversaciones con el asistente de IA.
                  Puedes revocar esta autorización cuando quieras con este mismo enlace.
                </p>
                <label class="check">
                  <input type="checkbox" [checked]="confirmed()" (change)="confirmed.set($any($event.target).checked)" />
                  <span>
                    Soy el representante legal de {{ v.minorFirstName }}, leí los documentos y autorizo el tratamiento de sus datos
                    personales en Luz Verde.
                  </span>
                </label>
                <div class="row">
                  <ui-button type="button" [disabled]="busy() || !confirmed()" (click)="decide('authorize')">Autorizo</ui-button>
                  <ui-button type="button" variant="secondary" [disabled]="busy()" (click)="decide('reject')">No autorizo</ui-button>
                </div>
                <p class="muted small">Si eliges "No autorizo", la cuenta del estudiante quedará suspendida.</p>
              </div>
            }
          }
          @case ('CONFIRMED') {
            <p class="notice ok">Autorizaste la cuenta. Gracias.</p>
            <div class="card">
              <p>
                Si cambias de opinión puedes revocar la autorización. La cuenta del estudiante quedará suspendida y no podrá
                volver a entrar.
              </p>
              @if (!confirmRevoke()) {
                <ui-button type="button" variant="danger" (click)="confirmRevoke.set(true)">Revocar autorización</ui-button>
              } @else {
                <div class="row">
                  <ui-button type="button" variant="danger" [disabled]="busy()" (click)="decide('revoke')">Sí, revocar</ui-button>
                  <ui-button type="button" variant="ghost" (click)="confirmRevoke.set(false)">Cancelar</ui-button>
                </div>
              }
            </div>
          }
          @case ('REJECTED') {
            <p class="notice">Registramos que no autorizas esta cuenta. La cuenta quedó suspendida.</p>
          }
          @case ('REVOKED') {
            <p class="notice">Revocaste la autorización. La cuenta quedó suspendida.</p>
          }
        }
      }
    </section>
  `,
  styles: [`
    .guardian { max-width: 40rem; margin: 0 auto; padding: 1.5rem 1rem 3rem; display: grid; gap: 1rem; }
    h1 { margin: 0; font-size: 1.5rem; }
    h2 { margin: 0 0 0.6rem; font-size: 1.05rem; }
    p { margin: 0; line-height: 1.5; }
    .lead { font-size: 1rem; }
    .card {
      display: grid;
      gap: 0.85rem;
      padding: 1.1rem;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      background: var(--color-surface);
    }
    .docs { margin: 0; padding-left: 1.1rem; display: grid; gap: 0.35rem; }
    .docs a { color: var(--color-primary); font-weight: 600; }
    .docs small { margin-left: 0.35rem; color: var(--color-text-secondary); }
    .check { display: flex; gap: 0.55rem; align-items: flex-start; line-height: 1.45; cursor: pointer; }
    .check input { margin-top: 0.2rem; width: 1.1rem; height: 1.1rem; flex: none; }
    .row { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    .muted { color: var(--color-text-secondary); font-size: 0.9rem; }
    .small { font-size: 0.8rem; }
    .notice {
      padding: 0.8rem 1rem;
      border-radius: var(--radius-md);
      border: 1px solid #c99400;
      background: color-mix(in srgb, #f5b400 14%, transparent);
    }
    .notice.ok { border-color: var(--color-success); background: var(--color-success-soft); }
  `]
})
export class GuardianConsentPage {
  private readonly api = inject(LegalApi);
  private readonly token = inject(ActivatedRoute).snapshot.paramMap.get('token') ?? '';

  readonly view = signal<GuardianView | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly confirmed = signal(false);
  readonly confirmRevoke = signal(false);

  constructor() {
    this.api.guardianView(this.token).subscribe({
      next: (view) => {
        this.view.set(view);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  decide(decision: 'authorize' | 'reject' | 'revoke'): void {
    this.busy.set(true);
    this.error.set(null);
    this.api.guardianDecision(this.token, decision, this.confirmed()).subscribe({
      next: (view) => {
        this.busy.set(false);
        this.confirmRevoke.set(false);
        this.view.set(view);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}
