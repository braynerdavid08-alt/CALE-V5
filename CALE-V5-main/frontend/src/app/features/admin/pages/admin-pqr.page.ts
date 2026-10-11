import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiSheetComponent } from '../../../shared/ui/ui-sheet.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { AdminPqr, PQR_EVENT_LABEL, PQR_STATUS_LABEL, PqrApi, PqrKind, PqrStatus } from '../../legal/pqr.api';

const STATUSES: PqrStatus[] = ['PENDIENTE', 'EN_REVISION', 'ESCALADA', 'RESPONDIDA', 'RECHAZADA'];

@Component({
  selector: 'app-admin-pqr-page',
  standalone: true,
  imports: [
    DatePipe,
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
      title="PQR y datos personales"
      subtitle="Responde dentro del plazo. Las solicitudes nunca se borran; rechazar exige justificación." />

    <ui-error [message]="error()" />
    <ui-success [message]="ok()" />

    <ui-card>
      <div class="filters">
        <select [value]="statusFilter()" (change)="statusFilter.set($any($event.target).value); reload()">
          <option value="open">Abiertas</option>
          <option value="">Todas</option>
          @for (s of statuses; track s) {
            <option [value]="s">{{ statusLabel(s) }}</option>
          }
        </select>
        <select [value]="kindFilter()" (change)="kindFilter.set($any($event.target).value); reload()">
          <option value="">Todos los tipos</option>
          @for (k of kinds(); track k.key) {
            <option [value]="k.key">{{ k.label }}</option>
          }
        </select>
        <span class="muted">{{ items().length }} solicitudes · {{ overdue() }} vencidas</span>
      </div>

      <ul class="list">
        @for (p of items(); track p.id) {
          <li [class.late]="p.overdue">
            <div class="head">
              <strong>{{ p.radicado }}</strong>
              <ui-badge [tone]="tone(p)">{{ statusLabel(p.status) }}</ui-badge>
              @if (p.overdue) { <ui-badge tone="danger">Vencida</ui-badge> }
              @if (p.isDataRight) { <ui-badge tone="neutral">Datos personales</ui-badge> }
              @if (p.userActive === false) { <ui-badge tone="neutral">Cuenta suspendida</ui-badge> }
            </div>
            <p class="subject">{{ p.subject }} <small class="muted">· {{ p.kindLabel }}</small></p>
            <small class="muted">
              {{ p.contactName }} · {{ p.contactEmail }}
              @if (p.userId) { · usuario #{{ p.userId }} ({{ p.userRole }}) } @else { · sin cuenta }
              · radicada {{ p.createdAt | date: 'dd/MM/yyyy' }}
              · vence {{ p.dueAt | date: 'dd/MM/yyyy' }}@if (p.extended) { (prorrogada) }
            </small>
            <div class="actions">
              <ui-button type="button" variant="ghost" (click)="open(p)">Ver y responder</ui-button>
            </div>
          </li>
        } @empty {
          <li class="muted">No hay solicitudes con este filtro.</li>
        }
      </ul>
    </ui-card>

    <ui-sheet [open]="!!selected()" [title]="selected()?.radicado ?? ''" (close)="selected.set(null)">
      @if (selected(); as p) {
        <p><strong>{{ p.subject }}</strong> · {{ p.kindLabel }}</p>
        <p class="muted">{{ p.contactName }} · {{ p.contactEmail }}</p>
        <p class="message">{{ p.message }}</p>
        @if (p.response) {
          <div class="response"><strong>Respuesta enviada</strong><p>{{ p.response }}</p></div>
        }

        <h3>Historial</h3>
        <ul class="events">
          @for (e of p.events; track $index) {
            <li>
              {{ e.createdAt | date: 'dd/MM/yyyy HH:mm' }} · {{ eventLabel(e.action) }}
              @if (e.toStatus && e.action === 'StatusChanged') { → {{ statusLabel(e.toStatus) }} }
              @if (!e.visibleToRequester) { <small class="muted">(solo administración)</small> }
              @if (e.note && e.action !== 'StatusChanged') { <br /><small>{{ e.note }}</small> }
            </li>
          }
        </ul>

        @if (p.status !== 'RESPONDIDA' && p.status !== 'RECHAZADA') {
          <h3>Actualizar</h3>
          <label class="field">
            <span>Nuevo estado</span>
            <select [value]="newStatus()" (change)="newStatus.set($any($event.target).value)">
              @for (s of statuses; track s) {
                <option [value]="s">{{ statusLabel(s) }}</option>
              }
            </select>
          </label>
          @if (needsResponse()) {
            <label class="field">
              <span>{{ newStatus() === 'RECHAZADA' ? 'Justificación del rechazo' : 'Respuesta' }} (la recibe el solicitante)</span>
              <textarea rows="6" maxlength="4000" [value]="response()" (input)="response.set($any($event.target).value)"></textarea>
            </label>
          }
          <label class="field">
            <span>Nota interna (no la ve el solicitante)</span>
            <textarea rows="2" maxlength="4000" [value]="note()" (input)="note.set($any($event.target).value)"></textarea>
          </label>
          <ui-button type="button" [disabled]="busy()" (click)="save(p)">Guardar</ui-button>

          @if (!p.extended) {
            <h3>Prorrogar el plazo</h3>
            <p class="muted">Solo una vez y con motivo; el solicitante recibe un aviso.</p>
            <label class="field">
              <span>Motivo</span>
              <input [value]="extendReason()" (input)="extendReason.set($any($event.target).value)" maxlength="500" />
            </label>
            <ui-button type="button" variant="secondary" [disabled]="busy() || extendReason().trim().length < 10" (click)="extend(p)">
              Prorrogar
            </ui-button>
          }
        }
      }
    </ui-sheet>
  `,
  styles: [`
    .filters { display: flex; flex-wrap: wrap; gap: 0.5rem; align-items: center; margin-bottom: 0.75rem; }
    .muted { color: var(--color-text-secondary); font-size: 0.85rem; }
    .list { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.7rem; }
    .list li { padding: 0.75rem 0.85rem; border: 1px solid var(--color-border); border-radius: var(--radius-md); }
    .list li.late { border-color: var(--color-danger); }
    .head { display: flex; flex-wrap: wrap; gap: 0.4rem; align-items: center; }
    .subject { margin: 0.3rem 0 0.15rem; }
    .actions { margin-top: 0.45rem; }
    .message { white-space: pre-line; padding: 0.6rem 0.75rem; border-left: 3px solid var(--color-border); }
    .response { padding: 0.6rem 0.75rem; border-radius: var(--radius-md); background: var(--color-success-soft); }
    .response p { white-space: pre-line; margin: 0.25rem 0 0; }
    h3 { margin: 1rem 0 0.4rem; font-size: 0.98rem; }
    .events { margin: 0; padding-left: 1.1rem; display: grid; gap: 0.3rem; font-size: 0.88rem; }
    .field { display: grid; gap: 0.3rem; margin-bottom: 0.7rem; }
    .field span { font-weight: 600; font-size: 0.88rem; }
    select, input, textarea {
      padding: 0.45rem 0.6rem;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      background: var(--color-surface);
      color: var(--color-text);
      font: inherit;
    }
    ui-card { display: block; margin-bottom: 1rem; }
  `]
})
export class AdminPqrPage implements OnInit {
  private readonly api = inject(PqrApi);

  readonly statuses = STATUSES;
  readonly kinds = signal<PqrKind[]>([]);
  readonly items = signal<AdminPqr[]>([]);
  readonly statusFilter = signal('open');
  readonly kindFilter = signal('');
  readonly selected = signal<AdminPqr | null>(null);
  readonly newStatus = signal<PqrStatus>('EN_REVISION');
  readonly response = signal('');
  readonly note = signal('');
  readonly extendReason = signal('');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);

  readonly overdue = computed(() => this.items().filter((p) => p.overdue).length);
  readonly needsResponse = computed(() => this.newStatus() === 'RESPONDIDA' || this.newStatus() === 'RECHAZADA');

  ngOnInit(): void {
    this.api.kinds().subscribe({ next: (k) => this.kinds.set(k) });
    this.reload();
  }

  statusLabel(s: PqrStatus): string {
    return PQR_STATUS_LABEL[s] ?? s;
  }

  eventLabel(action: string): string {
    return PQR_EVENT_LABEL[action] ?? action;
  }

  tone(p: AdminPqr): 'success' | 'warning' | 'neutral' | 'danger' {
    if (p.status === 'RESPONDIDA') return 'success';
    if (p.status === 'RECHAZADA') return 'neutral';
    return p.status === 'ESCALADA' ? 'danger' : 'warning';
  }

  open(p: AdminPqr): void {
    this.ok.set(null);
    this.newStatus.set(p.status === 'PENDIENTE' ? 'EN_REVISION' : p.status);
    this.response.set('');
    this.note.set('');
    this.extendReason.set('');
    this.selected.set(p);
  }

  save(p: AdminPqr): void {
    this.run(
      this.api.update(p.id, this.newStatus(), this.needsResponse() ? this.response() : null, this.note() || null),
      `${p.radicado} actualizada.`
    );
  }

  extend(p: AdminPqr): void {
    this.run(this.api.extend(p.id, this.extendReason()), `Plazo de ${p.radicado} prorrogado.`);
  }

  reload(): void {
    this.api.adminList(this.statusFilter() || undefined, this.kindFilter() || undefined).subscribe({
      next: (rows) => {
        this.items.set(rows);
        const current = this.selected();
        if (current) this.selected.set(rows.find((r) => r.id === current.id) ?? null);
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private run(request: Observable<unknown>, message: string): void {
    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: () => {
        this.busy.set(false);
        this.selected.set(null);
        this.ok.set(message);
        this.reload();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}
