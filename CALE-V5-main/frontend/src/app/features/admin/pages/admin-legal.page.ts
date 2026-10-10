import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiSheetComponent } from '../../../shared/ui/ui-sheet.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { Observable } from 'rxjs';
import {
  AdminMinor,
  GUARDIAN_STATUS_LABEL,
  GuardianStatus,
  LegalAcceptance,
  LegalApi,
  LegalDocKey,
  LegalSummary,
  LegalVersion,
  SaveLegalVersion
} from '../../legal/legal.api';
import { LEGAL_LINKS } from '../../legal/legal-links';

const GUARDIAN_EVENT_LABEL: Record<string, string> = {
  Declared: 'Fecha de nacimiento registrada',
  GuardianUpdated: 'Datos del representante corregidos',
  EmailSent: 'Correo enviado al representante',
  EmailNotSent: 'Correo no enviado',
  Confirmed: 'El representante autorizó',
  Rejected: 'El representante no autorizó',
  Revoked: 'El representante revocó la autorización'
};

const STATUS_LABEL: Record<LegalVersion['status'], string> = {
  Draft: 'Borrador sin publicar',
  Published: 'Vigente',
  Retired: 'Histórica'
};

@Component({
  selector: 'app-admin-legal-page',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    UiBadgeComponent,
    UiButtonComponent,
    UiCardComponent,
    UiErrorComponent,
    UiPageHeaderComponent,
    UiSheetComponent,
    UiSuccessComponent
  ],
  template: `
    <ui-page-header
      eyebrow="Administración"
      title="Documentos legales"
      subtitle="Versiones de cada documento y evidencia de aceptación. Una versión publicada nunca se edita: se crea otra." />

    <ui-error [message]="error()" />
    <ui-success [message]="ok()" />

    <ui-card>
      <h2>Estado de aceptación</h2>
      <div class="table-wrap">
        <table>
          <thead>
            <tr><th>Documento</th><th>Versión vigente</th><th>Aceptado</th><th>Versión vieja</th><th>Sin verificar</th></tr>
          </thead>
          <tbody>
            @for (s of summary(); track s.key) {
              <tr>
                <td><a [routerLink]="['/legal', s.key]" target="_blank">{{ s.title }}</a></td>
                <td>
                  {{ s.currentVersion ?? '—' }}
                  @if (s.isDraft) { <ui-badge tone="warning">Con pendientes</ui-badge> }
                </td>
                <td>{{ s.accepted }}</td>
                <td>{{ s.outdated }}</td>
                <td>{{ s.notVerified }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <p class="muted">Las cuentas creadas antes de este sistema aparecen "sin verificar": nunca se marcan como aceptadas automáticamente.</p>
    </ui-card>

    <ui-card>
      <div class="row-head">
        <h2>Versiones</h2>
        <select [value]="docFilter()" (change)="docFilter.set($any($event.target).value)">
          @for (l of links; track l.key) {
            <option [value]="l.key">{{ l.label }}</option>
          }
        </select>
      </div>
      <ul class="versions">
        @for (v of filteredVersions(); track v.id) {
          <li>
            <div class="v-head">
              <strong>{{ v.version }}</strong>
              <ui-badge [tone]="v.status === 'Published' ? 'success' : v.status === 'Draft' ? 'warning' : 'neutral'">
                {{ statusLabel(v.status) }}
              </ui-badge>
              @if (v.hasPlaceholders) { <ui-badge tone="warning">Con datos pendientes</ui-badge> }
              @if (v.requiresReacceptance) { <ui-badge tone="neutral">Exige nueva aceptación</ui-badge> }
            </div>
            <small class="muted">
              Creada {{ v.createdAt | date: 'dd/MM/yyyy' }}
              @if (v.publishedAt) { · publicada {{ v.publishedAt | date: 'dd/MM/yyyy HH:mm' }} }
              · {{ v.acceptances }} aceptaciones · hash {{ v.contentHash.slice(0, 12) }}
            </small>
            @if (v.changeSummary) { <p class="summary">{{ v.changeSummary }}</p> }
            <div class="actions">
              @if (v.status === 'Draft') {
                <ui-button type="button" variant="ghost" (click)="openEditor(v)">Editar borrador</ui-button>
                <ui-button type="button" [disabled]="busy()" (click)="publish(v)">Publicar</ui-button>
              } @else {
                <ui-button type="button" variant="ghost" (click)="openEditor(v, true)">Nueva versión a partir de esta</ui-button>
              }
            </div>
          </li>
        } @empty {
          <li class="muted">Sin versiones.</li>
        }
      </ul>
    </ui-card>

    <ui-card>
      <h2>Menores de edad</h2>
      <p class="muted">
        Estudiantes que eran menores al registrar su fecha de nacimiento. Solo su representante legal autoriza, desde el enlace
        personal que recibe por correo. Si no autoriza o revoca, la cuenta queda suspendida.
      </p>
      <ul class="versions">
        @for (m of minors(); track m.userId) {
          <li>
            <div class="v-head">
              <strong>{{ m.userName ?? ('#' + m.userId) }}</strong>
              <small class="muted">{{ m.userEmail }} · {{ m.ageToday }} años</small>
              <ui-badge [tone]="guardianTone(m.guardianStatus)">{{ guardianLabel(m.guardianStatus) }}</ui-badge>
              @if (!m.userActive) { <ui-badge tone="neutral">Cuenta suspendida</ui-badge> }
            </div>
            <small class="muted">
              Representante: {{ m.guardianName ?? '—' }} ({{ m.guardianRelationship ?? '—' }}) · {{ m.guardianEmail ?? '—' }}
              @if (m.guardianStatus === 'PENDING' && m.guardianLinkExpiresAt) {
                · enlace vence {{ m.guardianLinkExpiresAt | date: 'dd/MM/yyyy' }}
              }
            </small>
            <details>
              <summary>Historial ({{ m.events.length }})</summary>
              <ul class="history">
                @for (e of m.events; track $index) {
                  <li>
                    {{ e.createdAt | date: 'dd/MM/yyyy HH:mm' }} · {{ eventLabel(e.action) }}
                    @if (e.documentVersions) { · {{ e.documentVersions }} }
                    @if (e.ipAddress) { · IP {{ e.ipAddress }} }
                    @if (e.note) { <br /><small class="muted">{{ e.note }}</small> }
                  </li>
                }
              </ul>
            </details>
            @if (m.guardianStatus === 'PENDING') {
              <div class="actions">
                <ui-button type="button" variant="ghost" [disabled]="busy()" (click)="resend(m)">Reenviar correo al representante</ui-button>
              </div>
            }
          </li>
        } @empty {
          <li class="muted">No hay estudiantes menores de edad registrados.</li>
        }
      </ul>
    </ui-card>

    <ui-card>
      <div class="row-head">
        <h2>Aceptaciones recientes</h2>
        <ui-button type="button" variant="secondary" [disabled]="busy()" (click)="downloadCsv()">Exportar CSV</ui-button>
      </div>
      <div class="table-wrap">
        <table>
          <thead>
            <tr><th>Fecha</th><th>Usuario</th><th>Documento</th><th>Versión</th><th>Tipo</th><th>Cómo</th><th>IP</th></tr>
          </thead>
          <tbody>
            @for (a of acceptances(); track a.id) {
              <tr>
                <td>{{ a.createdAt | date: 'dd/MM/yyyy HH:mm' }}</td>
                <td>{{ a.userName ?? ('#' + a.userId) }}<br /><small class="muted">{{ a.userEmail }}</small></td>
                <td>{{ titleFor(a.documentKey) }}</td>
                <td>{{ a.version }}</td>
                <td>{{ actionLabel(a.action) }}</td>
                <td>{{ a.method === 'Registration' ? 'Registro' : 'Aviso' }}</td>
                <td>{{ a.ipAddress ?? '—' }}</td>
              </tr>
            } @empty {
              <tr><td colspan="7" class="muted">Aún no hay aceptaciones.</td></tr>
            }
          </tbody>
        </table>
      </div>
    </ui-card>

    <ui-sheet [open]="!!editor()" [title]="editorTitle()" (close)="editor.set(null)">
      @if (editor(); as e) {
        <label class="field">
          <span>Versión</span>
          <input [value]="e.version" (input)="patch({ version: $any($event.target).value })" placeholder="Ej.: 1.1" />
        </label>
        <label class="field">
          <span>Título</span>
          <input [value]="e.title" (input)="patch({ title: $any($event.target).value })" />
        </label>
        <label class="field">
          <span>Qué cambia (resumen)</span>
          <input [value]="e.changeSummary ?? ''" (input)="patch({ changeSummary: $any($event.target).value })" />
        </label>
        <label class="field">
          <span>Contenido (Markdown)</span>
          <textarea rows="14" [value]="e.content" (input)="patch({ content: $any($event.target).value })"></textarea>
        </label>
        <label class="check">
          <input type="checkbox" [checked]="e.requiresReacceptance" (change)="patch({ requiresReacceptance: $any($event.target).checked })" />
          <span>El cambio es relevante: pedir a todos que acepten de nuevo</span>
        </label>
        <p class="muted">
          No completes datos de la empresa sin tenerlos confirmados. Deja los espacios [PENDIENTE] hasta que existan.
        </p>
        <ui-button type="button" [disabled]="busy()" (click)="saveDraft()">Guardar borrador</ui-button>
      }
    </ui-sheet>
  `,
  styles: [`
    h2 { margin: 0 0 0.6rem; font-size: 1.05rem; }
    .muted { color: var(--color-text-secondary); font-size: 0.85rem; }
    .table-wrap { overflow-x: auto; }
    table { width: 100%; border-collapse: collapse; font-size: 0.88rem; }
    th, td { text-align: left; padding: 0.45rem 0.55rem; border-bottom: 1px solid var(--color-border); vertical-align: top; }
    th { color: var(--color-text-secondary); font-weight: 600; }
    .row-head { display: flex; flex-wrap: wrap; gap: 0.5rem; align-items: center; justify-content: space-between; margin-bottom: 0.5rem; }
    .row-head h2 { margin: 0; }
    select, input, textarea {
      padding: 0.45rem 0.6rem;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      background: var(--color-surface);
      color: var(--color-text);
      font: inherit;
    }
    .versions { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.75rem; }
    .versions li { padding: 0.7rem 0.8rem; border: 1px solid var(--color-border); border-radius: var(--radius-md); }
    .v-head { display: flex; flex-wrap: wrap; gap: 0.4rem; align-items: center; }
    .summary { margin: 0.35rem 0 0; font-size: 0.88rem; }
    .actions { display: flex; flex-wrap: wrap; gap: 0.4rem; margin-top: 0.5rem; }
    .field { display: grid; gap: 0.3rem; margin-bottom: 0.7rem; }
    .field span { font-weight: 600; font-size: 0.88rem; }
    .field textarea { font-family: ui-monospace, monospace; font-size: 0.82rem; }
    .check { display: flex; gap: 0.5rem; align-items: flex-start; margin-bottom: 0.6rem; }
    details { margin-top: 0.4rem; font-size: 0.85rem; }
    summary { cursor: pointer; color: var(--color-primary); font-weight: 600; }
    .history { margin: 0.4rem 0 0; padding-left: 1.1rem; display: grid; gap: 0.3rem; }
    ui-card { display: block; margin-bottom: 1rem; }
  `]
})
export class AdminLegalPage implements OnInit {
  private readonly api = inject(LegalApi);

  readonly links = LEGAL_LINKS;
  readonly summary = signal<LegalSummary[]>([]);
  readonly versions = signal<LegalVersion[]>([]);
  readonly acceptances = signal<LegalAcceptance[]>([]);
  readonly minors = signal<AdminMinor[]>([]);
  readonly docFilter = signal<LegalDocKey>('terms');
  readonly editor = signal<(SaveLegalVersion & { id: number | null }) | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);

  readonly filteredVersions = computed(() => this.versions().filter((v) => v.documentKey === this.docFilter()));
  readonly editorTitle = computed(() => (this.editor()?.id ? 'Editar borrador' : 'Nueva versión'));

  ngOnInit(): void {
    this.reload();
  }

  statusLabel(status: LegalVersion['status']): string {
    return STATUS_LABEL[status];
  }

  titleFor(key: LegalDocKey): string {
    return this.links.find((l) => l.key === key)?.label ?? key;
  }

  guardianLabel(status: GuardianStatus): string {
    return GUARDIAN_STATUS_LABEL[status];
  }

  guardianTone(status: GuardianStatus): 'success' | 'warning' | 'neutral' {
    return status === 'CONFIRMED' ? 'success' : status === 'PENDING' ? 'warning' : 'neutral';
  }

  eventLabel(action: string): string {
    return GUARDIAN_EVENT_LABEL[action] ?? action;
  }

  resend(m: AdminMinor): void {
    this.run(this.api.resendGuardian(m.userId), () => this.ok.set(`Enlace reenviado al representante de ${m.userName ?? 'el estudiante'}.`));
  }

  actionLabel(action: string): string {
    return action === 'Authorized' ? 'Autorizó' : action === 'Acknowledged' ? 'Leyó' : 'Aceptó';
  }

  openEditor(v: LegalVersion, asNew = false): void {
    this.ok.set(null);
    this.editor.set({
      id: asNew ? null : v.id,
      documentKey: v.documentKey,
      version: asNew ? '' : v.version,
      title: v.title,
      content: v.content,
      changeSummary: asNew ? '' : v.changeSummary,
      requiresReacceptance: asNew ? true : v.requiresReacceptance
    });
  }

  patch(change: Partial<SaveLegalVersion>): void {
    this.editor.update((e) => (e ? { ...e, ...change } : e));
  }

  saveDraft(): void {
    const e = this.editor();
    if (!e) {
      return;
    }
    const { id, ...body } = e;
    const request = id ? this.api.updateDraft(id, body) : this.api.createDraft(body);
    this.run(request, () => {
      this.editor.set(null);
      this.ok.set('Borrador guardado. Revísalo y publícalo cuando esté listo.');
    });
  }

  publish(v: LegalVersion): void {
    const warning = v.hasPlaceholders ? '\n\nOjo: todavía tiene datos pendientes; se publicará marcada como borrador.' : '';
    if (!confirm(`¿Publicar la versión ${v.version}? La versión vigente pasará al historial y esta ya no se podrá editar.${warning}`)) {
      return;
    }
    this.run(this.api.publish(v.id), () => this.ok.set(`Versión ${v.version} publicada.`));
  }

  downloadCsv(): void {
    this.busy.set(true);
    this.api.acceptancesCsv().subscribe({
      next: (blob) => {
        this.busy.set(false);
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `aceptaciones-legales-${new Date().toISOString().slice(0, 10)}.csv`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  private run(request: Observable<unknown>, done: () => void): void {
    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: () => {
        this.busy.set(false);
        done();
        this.reload();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  private reload(): void {
    this.api.summary().subscribe({ next: (s) => this.summary.set(s), error: (err) => this.error.set(mapApiError(err)) });
    this.api.versions().subscribe({ next: (v) => this.versions.set(v), error: (err) => this.error.set(mapApiError(err)) });
    this.api.acceptances().subscribe({ next: (a) => this.acceptances.set(a), error: (err) => this.error.set(mapApiError(err)) });
    this.api.minors().subscribe({ next: (m) => this.minors.set(m), error: (err) => this.error.set(mapApiError(err)) });
  }
}
