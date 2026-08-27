import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { env } from '../../../core/config/env';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiStatComponent } from '../../../shared/ui/ui-stat.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';

interface MembershipRequestDto {
  userId: number;
  contactName: string;
  email: string;
  legalName: string;
  taxId: string;
  billingEmail: string;
  phone: string;
  city: string;
  department: string;
  planCode: string;
  planLabel: string;
  planPriceCop: number;
  planDurationMonths: number;
  subscriptionStatus: string;
  displayStatus?: string;
  renewalStatus?: string;
  requestedPlanCode?: string | null;
  isRenewalRequest: boolean;
  hasPaymentProof: boolean;
  paymentProofUrl?: string | null;
  paymentReference?: string | null;
  requestedAt?: string | null;
  proofSubmittedAt?: string | null;
  createdAt: string;
  membershipStartsAt?: string | null;
  membershipEndsAt?: string | null;
  teachersUsed?: number;
  teachersMax?: number;
  studentsUsed?: number;
  studentsMax?: number;
  teachersMaxOverride?: number | null;
  studentsMaxOverride?: number | null;
  rejectionReason?: string | null;
  suspensionReason?: string | null;
}

interface AdminSchoolSummary {
  userId: number;
  contactName: string;
  email: string;
  legalName: string;
  taxId: string;
  planCode: string;
  planLabel: string;
  subscriptionStatus: string;
  displayStatus: string;
  renewalStatus: string;
  isMembershipActive: boolean;
  daysRemaining: number;
  membershipEndsAt?: string | null;
  teachersUsed: number;
  teachersMax: number;
  studentsUsed: number;
  studentsMax: number;
  hasSeatOverrides: boolean;
  hasOpenRequest: boolean;
  createdAt: string;
}

interface HistoryEvent {
  id: number;
  eventType: string;
  planCode?: string | null;
  planPriceCop?: number | null;
  note?: string | null;
  createdAt: string;
}

interface SchoolMemberRow {
  id: number;
  name: string;
  email: string;
  role: string;
  isActive: boolean;
  createdAt: string;
  lastLoginAt?: string | null;
}

interface SchoolDetailDto {
  userId: number;
  contactName: string;
  email: string;
  legalName: string;
  taxId: string;
  billingEmail: string;
  phone: string;
  address: string;
  city: string;
  department: string;
  planCode: string;
  planLabel: string;
  planPriceCop: number;
  subscriptionStatus: string;
  displayStatus: string;
  renewalStatus: string;
  isMembershipActive: boolean;
  daysRemaining: number;
  membershipStartsAt?: string | null;
  membershipEndsAt?: string | null;
  teachersUsed: number;
  teachersMax: number;
  studentsUsed: number;
  studentsMax: number;
  members: SchoolMemberRow[];
  history: HistoryEvent[];
}

type Tab = 'queue' | 'schools';

@Component({
  selector: 'app-admin-memberships-page',
  standalone: true,
  imports: [
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule,
    UiBadgeComponent,
    UiButtonComponent,
    UiCardComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent,
    UiStatComponent,
    UiSuccessComponent
  ],
  template: `
    @if (!embedded()) {
      <ui-page-header
        eyebrow="Escuelas de Manejo"
        title="Solicitudes y escuelas"
        subtitle="Aquí apruebas, rechazas, ajustas cupos y revisas cada escuela. Sin ir a otra pantalla." />
    }

    <ui-error [message]="error()" />
    <ui-success [message]="ok()" />

    <div class="tabs">
      <button type="button" [class.active]="tab() === 'queue'" (click)="setTab('queue')">
        Solicitudes pendientes
      </button>
      <button type="button" [class.active]="tab() === 'schools'" (click)="setTab('schools')">
        Directorio de escuelas
      </button>
    </div>

    @if (loading()) {
      <ui-loading />
    } @else if (tab() === 'queue') {
      <div class="grid-stats">
        <ui-stat label="Por revisar" [value]="items().length" tone="warning" />
        <ui-stat label="Ya enviaron comprobante" [value]="withProof()" tone="primary" />
      </div>

      @if (items().length === 0) {
        <ui-empty
          title="No hay solicitudes por revisar"
          message="Cuando una escuela pida membresía aparecerá aquí para que la actives o rechaces. El historial está en «Directorio de escuelas»." />
      } @else {
        <div class="list">
          @for (item of items(); track item.userId) {
            <ui-card>
              <div class="head">
                <div>
                  <h2>{{ item.legalName }}</h2>
                  <p class="muted">{{ item.contactName }} · {{ item.email }}</p>
                </div>
                <div class="badges">
                  @if (item.isRenewalRequest) {
                    <ui-badge tone="primary">Renovación</ui-badge>
                  }
                  @if (item.hasPaymentProof) {
                    <ui-badge tone="success">Comprobante</ui-badge>
                  } @else {
                    <ui-badge tone="warning">Sin comprobante</ui-badge>
                  }
                  <ui-badge tone="neutral">{{ item.displayStatus || item.subscriptionStatus }}</ui-badge>
                </div>
              </div>

              <dl class="facts">
                <div>
                  <dt>Plan solicitado</dt>
                  <dd>{{ item.planLabel }} · {{ item.planPriceCop | currency:'COP':'symbol-narrow':'1.0-0' }}</dd>
                </div>
                <div>
                  <dt>Cupos</dt>
                  <dd>Inst {{ item.teachersUsed ?? 0 }}/{{ item.teachersMax ?? 0 }} · Est {{ item.studentsUsed ?? 0 }}/{{ item.studentsMax ?? 0 }}</dd>
                </div>
                <div>
                  <dt>Comprobante</dt>
                  <dd>
                    @if (item.paymentProofUrl) {
                      <a [href]="apiUrl(item.paymentProofUrl)" target="_blank" rel="noopener">Abrir</a>
                    } @else { — }
                  </dd>
                </div>
              </dl>

              @if (rejectingId() === item.userId) {
                <form class="reject-form" [formGroup]="rejectForm" (ngSubmit)="confirmReject(item)">
                  <label class="field">Motivo del rechazo
                    <input formControlName="note" />
                  </label>
                  <div class="row actions">
                    <ui-button type="submit" [disabled]="rejectForm.invalid">Confirmar rechazo</ui-button>
                    <ui-button type="button" variant="ghost" (click)="rejectingId.set(null)">Cancelar</ui-button>
                  </div>
                </form>
              } @else {
                <div class="row actions">
                  <ui-button
                    type="button"
                    [disabled]="busyId() === item.userId || !item.hasPaymentProof"
                    (click)="activate(item, false)">
                    {{ item.hasPaymentProof ? 'Activar' : 'Esperando comprobante' }}
                  </ui-button>
                  <ui-button type="button" variant="secondary" (click)="activate(item, true)">
                    Activar sin comprobante
                  </ui-button>
                  <ui-button type="button" variant="ghost" (click)="startReject(item)">Rechazar</ui-button>
                  <ui-button type="button" variant="ghost" (click)="openSchool(item.userId)">Gestionar</ui-button>
                </div>
              }
            </ui-card>
          }
        </div>
      }
    } @else {
      <div class="grid-stats">
        <ui-stat label="Escuelas registradas" [value]="schools().length" />
        <ui-stat label="Con membresía vigente" [value]="activeSchools()" tone="success" />
        <ui-stat label="Con solicitud abierta" [value]="openRequestSchools()" tone="warning" />
      </div>

      <label class="field search">
        Buscar escuela
        <input [value]="schoolQuery()" (input)="schoolQuery.set($any($event.target).value)" placeholder="Nombre, NIT o correo" />
      </label>

      @if (filteredSchools().length === 0) {
        <ui-empty title="No hay escuelas en el directorio" message="Cuando se registren escuelas aparecerán aquí para editar cupos, suspender o reactivar." />
      } @else {
        <div class="list">
          @for (s of filteredSchools(); track s.userId) {
            <ui-card>
              <div class="head">
                <div>
                  <h2>{{ s.legalName }}</h2>
                  <p class="muted">{{ s.contactName }} · {{ s.email }} · NIT {{ s.taxId }}</p>
                </div>
                <div class="badges">
                  <ui-badge [tone]="statusTone(s.displayStatus)">{{ statusLabel(s.displayStatus) }}</ui-badge>
                  @if (s.hasOpenRequest) {
                    <ui-badge tone="warning">Solicitud abierta</ui-badge>
                  }
                  @if (s.hasSeatOverrides) {
                    <ui-badge tone="primary">Cupos custom</ui-badge>
                  }
                </div>
              </div>
              <dl class="facts">
                <div>
                  <dt>Plan</dt>
                  <dd>{{ s.planLabel }}</dd>
                </div>
                <div>
                  <dt>Vigencia</dt>
                  <dd>
                    @if (s.membershipEndsAt) {
                      {{ s.membershipEndsAt | date:'mediumDate' }} ({{ s.daysRemaining }} d)
                    } @else { — }
                  </dd>
                </div>
                <div>
                  <dt>Instructores</dt>
                  <dd>{{ s.teachersUsed }} / {{ s.teachersMax }}</dd>
                </div>
                <div>
                  <dt>Estudiantes</dt>
                  <dd>{{ s.studentsUsed }} / {{ s.studentsMax }}</dd>
                </div>
              </dl>
              <div class="row actions">
                <ui-button type="button" (click)="openSchool(s.userId)">Editar control</ui-button>
                <ui-button type="button" variant="ghost" (click)="loadDetail(s.userId)">
                  Ver ficha completa
                </ui-button>
              </div>

              @if (editingId() === s.userId) {
                <div class="editor">
                  <h3>Cupos</h3>
                  <form class="grid-2" [formGroup]="seatsForm" (ngSubmit)="saveSeats(s)">
                    <label class="field">Máx. instructores
                      <input type="number" formControlName="teachersMax" min="0" placeholder="Vacío = plan" />
                    </label>
                    <label class="field">Máx. estudiantes
                      <input type="number" formControlName="studentsMax" min="0" placeholder="Vacío = plan" />
                    </label>
                    <label class="field full">Nota
                      <input formControlName="note" />
                    </label>
                    <div class="row actions full">
                      <ui-button type="submit">Guardar cupos</ui-button>
                      <ui-button type="button" variant="ghost" (click)="clearSeats(s)">Usar defaults del plan</ui-button>
                    </div>
                  </form>

                  <h3>Membresía</h3>
                  <form class="grid-2" [formGroup]="overrideForm" (ngSubmit)="saveOverride(s)">
                    <label class="field">Plan
                      <select formControlName="planCode">
                        <option value="">(sin cambio)</option>
                        <option value="Monthly">Mensual</option>
                        <option value="Semestral">Semestral</option>
                        <option value="Annual">Anual</option>
                      </select>
                    </label>
                    <label class="field">Estado
                      <select formControlName="subscriptionStatus">
                        <option value="">(sin cambio)</option>
                        <option value="Active">Active</option>
                        <option value="PendingPayment">PendingPayment</option>
                        <option value="UnderReview">UnderReview</option>
                        <option value="Rejected">Rejected</option>
                        <option value="Expired">Expired</option>
                        <option value="Suspended">Suspended</option>
                        <option value="Cancelled">Cancelled</option>
                        <option value="None">None</option>
                      </select>
                    </label>
                    <label class="field">Fin membresía (UTC)
                      <input type="datetime-local" formControlName="membershipEndsAt" />
                    </label>
                    <label class="field">Nota
                      <input formControlName="note" />
                    </label>
                    <div class="row actions full">
                      <ui-button type="submit">Aplicar override</ui-button>
                      <ui-button type="button" variant="secondary" (click)="forceActivate(s)">Forzar Active</ui-button>
                      <ui-button type="button" variant="ghost" (click)="reopen(s)">Reabrir solicitud</ui-button>
                      <ui-button type="button" variant="ghost" (click)="suspend(s)">Suspender</ui-button>
                      <ui-button type="button" variant="ghost" (click)="unsuspend(s)">Quitar suspensión</ui-button>
                    </div>
                  </form>
                </div>
              }

              @if (detailId() === s.userId) {
                <div class="detail">
                  @if (detailLoading()) {
                    <ui-loading />
                  } @else if (detail()) {
                    <h3>Datos de la escuela</h3>
                    <dl class="facts">
                      <div><dt>Razón social</dt><dd>{{ detail()!.legalName }}</dd></div>
                      <div><dt>NIT</dt><dd>{{ detail()!.taxId }}</dd></div>
                      <div><dt>Contacto</dt><dd>{{ detail()!.contactName }}</dd></div>
                      <div><dt>Correo acceso</dt><dd>{{ detail()!.email }}</dd></div>
                      <div><dt>Facturación</dt><dd>{{ detail()!.billingEmail }}</dd></div>
                      <div><dt>Teléfono</dt><dd>{{ detail()!.phone || '—' }}</dd></div>
                      <div><dt>Dirección</dt><dd>{{ detail()!.address || '—' }}</dd></div>
                      <div><dt>Ciudad</dt><dd>{{ detail()!.city }}{{ detail()!.department ? ', ' + detail()!.department : '' }}</dd></div>
                      <div><dt>Plan</dt><dd>{{ detail()!.planLabel }} ({{ detail()!.planPriceCop | currency:'COP':'symbol-narrow':'1.0-0' }})</dd></div>
                      <div>
                        <dt>Vigencia</dt>
                        <dd>
                          @if (detail()!.membershipStartsAt) {
                            {{ detail()!.membershipStartsAt | date:'mediumDate' }} →
                          }
                          @if (detail()!.membershipEndsAt) {
                            {{ detail()!.membershipEndsAt | date:'mediumDate' }} ({{ detail()!.daysRemaining }} d)
                          } @else {
                            —
                          }
                        </dd>
                      </div>
                    </dl>

                    <h3>Instructores y estudiantes ({{ detail()!.members.length }})</h3>
                    @if (!detail()!.members.length) {
                      <p class="muted">Aún no hay miembros vinculados a esta escuela.</p>
                    } @else {
                      <div class="table-wrap">
                        <table class="data">
                          <thead>
                            <tr>
                              <th>Nombre</th>
                              <th>Correo</th>
                              <th>Rol</th>
                              <th>Estado</th>
                              <th>Alta</th>
                            </tr>
                          </thead>
                          <tbody>
                            @for (m of detail()!.members; track m.id) {
                              <tr>
                                <td>{{ m.name }}</td>
                                <td>{{ m.email }}</td>
                                <td>{{ m.role === 'Teacher' ? 'Instructor' : 'Estudiante' }}</td>
                                <td>{{ m.isActive ? 'Activo' : 'Inactivo' }}</td>
                                <td>{{ m.createdAt | date:'shortDate' }}</td>
                              </tr>
                            }
                          </tbody>
                        </table>
                      </div>
                    }

                    <h3>Historial de altas y membresía</h3>
                    @if (!detail()!.history.length) {
                      <p class="muted">Sin eventos registrados.</p>
                    } @else {
                      <ul class="history-list">
                        @for (e of detail()!.history; track e.id) {
                          <li>
                            <strong>{{ eventLabel(e.eventType) }}</strong>
                            · {{ e.createdAt | date:'short' }}
                            @if (e.note) { — {{ e.note }} }
                          </li>
                        }
                      </ul>
                    }
                  }
                </div>
              }
            </ui-card>
          }
        </div>
      }
    }
  `,
  styles: [`
    .tabs { display: flex; gap: 0.35rem; margin-bottom: 1rem; flex-wrap: wrap; }
    .tabs button {
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: var(--color-text);
      border-radius: var(--radius-md);
      padding: 0.45rem 0.85rem;
      font-weight: 650;
      cursor: pointer;
    }
    .tabs button.active { background: var(--color-primary-soft); color: var(--color-primary); border-color: transparent; }
    .list { display: grid; gap: 1rem; }
    .head { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; flex-wrap: wrap; }
    .badges { display: flex; gap: 0.4rem; flex-wrap: wrap; }
    h2 { margin: 0 0 0.25rem; font-size: var(--text-lg); }
    h3 { margin: 1rem 0 0.5rem; font-size: var(--text-md); }
    .muted { margin: 0; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .facts { margin: 1rem 0 0; display: grid; gap: 0.65rem; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); }
    dt { font-size: var(--text-xs); text-transform: uppercase; letter-spacing: 0.04em; color: var(--color-text-secondary); }
    dd { margin: 0; font-weight: 600; }
    .actions { margin-top: 1rem; gap: 0.5rem; flex-wrap: wrap; display: flex; }
    .reject-form, .editor, .detail { margin-top: 1rem; display: grid; gap: 0.75rem; }
    .field { display: grid; gap: 0.35rem; font-weight: 600; font-size: var(--text-sm); }
    .field input, .field select {
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      padding: 0.55rem 0.7rem;
      background: var(--color-surface);
      color: var(--color-text);
    }
    .grid-2 { display: grid; gap: 0.75rem; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); }
    .full { grid-column: 1 / -1; }
    .search { max-width: 28rem; margin-bottom: 1rem; }
    .history-list { margin: 0; padding-left: 1.1rem; }
    .history-list li { margin: 0.35rem 0; font-size: var(--text-sm); }
    .table-wrap { overflow: auto; }
    .data { width: 100%; border-collapse: collapse; font-size: var(--text-sm); }
    .data th, .data td { text-align: left; padding: 0.45rem 0.35rem; border-bottom: 1px solid var(--color-border); }
  `]
})
export class AdminMembershipsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);

  /** When true, hides the standalone page header (used inside Métricas). */
  readonly embedded = input(false);

  readonly tab = signal<Tab>('queue');
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly items = signal<MembershipRequestDto[]>([]);
  readonly schools = signal<AdminSchoolSummary[]>([]);
  readonly schoolQuery = signal('');
  readonly busyId = signal<number | null>(null);
  readonly rejectingId = signal<number | null>(null);
  readonly editingId = signal<number | null>(null);
  readonly detailId = signal<number | null>(null);
  readonly detailLoading = signal(false);
  readonly detail = signal<SchoolDetailDto | null>(null);

  readonly rejectForm = this.fb.nonNullable.group({
    note: ['', [Validators.required, Validators.minLength(3)]]
  });

  readonly seatsForm = this.fb.group({
    teachersMax: this.fb.control<number | null>(null),
    studentsMax: this.fb.control<number | null>(null),
    note: this.fb.control('')
  });

  readonly overrideForm = this.fb.group({
    planCode: this.fb.control(''),
    subscriptionStatus: this.fb.control(''),
    membershipEndsAt: this.fb.control(''),
    note: this.fb.control('')
  });

  readonly withProof = computed(() => this.items().filter((x) => x.hasPaymentProof).length);
  readonly activeSchools = computed(() => this.schools().filter((s) => s.isMembershipActive).length);
  readonly openRequestSchools = computed(() => this.schools().filter((s) => s.hasOpenRequest).length);
  readonly filteredSchools = computed(() => {
    const q = this.schoolQuery().trim().toLowerCase();
    if (!q) return this.schools();
    return this.schools().filter((s) =>
      [s.legalName, s.email, s.taxId, s.contactName].some((v) => v.toLowerCase().includes(q))
    );
  });

  ngOnInit(): void {
    this.reload();
  }

  setTab(tab: Tab): void {
    this.tab.set(tab);
    this.error.set(null);
    this.ok.set(null);
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    if (this.tab() === 'queue') {
      this.http.get<MembershipRequestDto[]>(`${env.apiUrl}/api/admin/memberships/pending`).subscribe({
        next: (rows) => {
          this.items.set(rows);
          this.loading.set(false);
        },
        error: (err) => {
          this.loading.set(false);
          this.error.set(mapApiError(err));
        }
      });
      return;
    }

    this.http.get<AdminSchoolSummary[]>(`${env.apiUrl}/api/admin/schools`).subscribe({
      next: (rows) => {
        this.schools.set(rows);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  apiUrl(path: string): string {
    if (path.startsWith('http')) return path;
    return `${env.apiUrl}${path.startsWith('/') ? '' : '/'}${path}`;
  }

  startReject(item: MembershipRequestDto): void {
    this.rejectingId.set(item.userId);
    this.rejectForm.reset({ note: '' });
  }

  confirmReject(item: MembershipRequestDto): void {
    if (this.rejectForm.invalid) return;
    this.busyId.set(item.userId);
    this.http.post(`${env.apiUrl}/api/admin/memberships/${item.userId}/reject`, {
      note: this.rejectForm.getRawValue().note
    }).subscribe({
      next: () => {
        this.busyId.set(null);
        this.rejectingId.set(null);
        this.ok.set('Solicitud rechazada.');
        this.reload();
      },
      error: (err) => {
        this.busyId.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }

  activate(item: MembershipRequestDto, force: boolean): void {
    this.busyId.set(item.userId);
    this.http.post(`${env.apiUrl}/api/admin/memberships/${item.userId}/activate`, {
      planCode: item.planCode,
      forceWithoutProof: force
    }).subscribe({
      next: () => {
        this.busyId.set(null);
        this.ok.set(force ? 'Membresía activada (forzada).' : 'Membresía activada.');
        this.reload();
      },
      error: (err) => {
        this.busyId.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }

  openSchool(userId: number): void {
    this.tab.set('schools');
    this.editingId.set(userId);
    this.detailId.set(null);
    this.detail.set(null);
    const s = this.schools().find((x) => x.userId === userId);
    if (!s) {
      this.reload();
      setTimeout(() => this.prefillEditor(userId), 400);
      return;
    }
    this.prefillEditor(userId);
  }

  private prefillEditor(userId: number): void {
    const s = this.schools().find((x) => x.userId === userId);
    if (!s) return;
    this.seatsForm.patchValue({
      teachersMax: s.hasSeatOverrides ? s.teachersMax : null,
      studentsMax: s.hasSeatOverrides ? s.studentsMax : null,
      note: ''
    });
    this.overrideForm.patchValue({
      planCode: s.planCode,
      subscriptionStatus: s.subscriptionStatus,
      membershipEndsAt: s.membershipEndsAt ? toLocalInput(s.membershipEndsAt) : '',
      note: ''
    });
  }

  saveSeats(s: AdminSchoolSummary): void {
    const v = this.seatsForm.getRawValue();
    this.http.put(`${env.apiUrl}/api/admin/schools/${s.userId}/seats`, {
      teachersMax: v.teachersMax === null || v.teachersMax === ('' as never) ? null : Number(v.teachersMax),
      studentsMax: v.studentsMax === null || v.studentsMax === ('' as never) ? null : Number(v.studentsMax),
      note: v.note || null
    }).subscribe({
      next: () => {
        this.ok.set('Cupos actualizados.');
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  clearSeats(s: AdminSchoolSummary): void {
    this.http.put(`${env.apiUrl}/api/admin/schools/${s.userId}/seats`, {
      teachersMax: null,
      studentsMax: null,
      note: 'Cupos restaurados al plan'
    }).subscribe({
      next: () => {
        this.ok.set('Cupos del plan restaurados.');
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  saveOverride(s: AdminSchoolSummary): void {
    const v = this.overrideForm.getRawValue();
    const ends = v.membershipEndsAt ? new Date(v.membershipEndsAt).toISOString() : null;
    this.http.put(`${env.apiUrl}/api/admin/schools/${s.userId}/membership`, {
      planCode: v.planCode || null,
      subscriptionStatus: v.subscriptionStatus || null,
      membershipEndsAt: ends,
      clearRejection: true,
      note: v.note || null
    }).subscribe({
      next: () => {
        this.ok.set('Membresía actualizada.');
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  forceActivate(s: AdminSchoolSummary): void {
    this.http.post(`${env.apiUrl}/api/admin/memberships/${s.userId}/activate`, {
      planCode: s.planCode,
      forceWithoutProof: true
    }).subscribe({
      next: () => {
        this.ok.set('Escuela forzada a Active.');
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  reopen(s: AdminSchoolSummary): void {
    this.http.post(`${env.apiUrl}/api/admin/schools/${s.userId}/reopen`, {
      planCode: s.planCode,
      note: 'Reabierta por admin'
    }).subscribe({
      next: () => {
        this.ok.set('Solicitud reabierta en cola.');
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  suspend(s: AdminSchoolSummary): void {
    this.http.post(`${env.apiUrl}/api/admin/memberships/${s.userId}/suspend`, {
      note: 'Suspensión administrativa'
    }).subscribe({
      next: () => {
        this.ok.set('Escuela suspendida.');
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  unsuspend(s: AdminSchoolSummary): void {
    this.http.post(`${env.apiUrl}/api/admin/memberships/${s.userId}/unsuspend`, {}).subscribe({
      next: () => {
        this.ok.set('Suspensión levantada.');
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  loadDetail(userId: number): void {
    this.detailId.set(userId);
    this.detailLoading.set(true);
    this.detail.set(null);
    this.http.get<SchoolDetailDto>(`${env.apiUrl}/api/admin/schools/${userId}`).subscribe({
      next: (dto) => {
        this.detail.set(dto);
        this.detailLoading.set(false);
      },
      error: (err) => {
        this.detailLoading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  eventLabel(type: string): string {
    const map: Record<string, string> = {
      Requested: 'Solicitud de membresía',
      ProofSubmitted: 'Comprobante enviado',
      Activated: 'Membresía activada',
      Renewed: 'Renovación',
      Rejected: 'Rechazada',
      Expired: 'Vencida',
      Cancelled: 'Cancelada',
      Suspended: 'Suspendida',
      Unsuspended: 'Suspensión levantada',
      SeatsAdjusted: 'Cupos ajustados',
      MembershipOverridden: 'Membresía editada por admin',
      RequestReopened: 'Solicitud reabierta',
      MemberCreated: 'Alta de miembro',
      MemberAttached: 'Vinculación de miembro',
      MemberUpdated: 'Edición de miembro',
      MemberImported: 'Importación CSV'
    };
    return map[type] ?? type;
  }

  statusLabel(status: string): string {
    if (status === 'Active') return 'Activo';
    if (status === 'Expiring') return 'Por vencer';
    if (status === 'PendingPayment') return 'Pendiente pago';
    if (status === 'UnderReview' || status === 'PaymentSubmitted') return 'En revisión';
    if (status === 'Rejected') return 'Rechazada';
    if (status === 'Suspended') return 'Suspendida';
    if (status === 'Expired') return 'Vencida';
    if (status === 'Cancelled') return 'Cancelada';
    if (status === 'None') return 'Sin membresía';
    return status;
  }

  statusTone(status: string): 'success' | 'warning' | 'danger' | 'neutral' | 'primary' {
    if (status === 'Active') return 'success';
    if (status === 'Expiring' || status === 'PendingPayment' || status === 'UnderReview') return 'warning';
    if (status === 'Rejected' || status === 'Expired' || status === 'Suspended' || status === 'Cancelled') {
      return 'danger';
    }
    return 'neutral';
  }
}

function toLocalInput(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
