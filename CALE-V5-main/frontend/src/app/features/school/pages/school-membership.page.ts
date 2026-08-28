import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { SessionStore } from '../../../core/auth/session.store';
import { env } from '../../../core/config/env';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiStatComponent } from '../../../shared/ui/ui-stat.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';

interface PaymentInstructions {
  bankName: string;
  accountType: string;
  accountNumber: string;
  accountHolder: string;
  holderTaxId: string;
  whatsApp: string;
  supportEmail: string;
  notes: string;
  paymentReferenceHint: string;
}

interface SchoolProfileDto {
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
  monthlyEquivalentCop: number;
  planDurationMonths: number;
  subscriptionStatus: string;
  displayStatus?: string;
  renewalStatus?: string;
  createdAt: string;
  membershipStartsAt?: string | null;
  membershipEndsAt?: string | null;
  daysRemaining: number;
  isMembershipActive: boolean;
  requestedPlanCode?: string | null;
  requestedPlanLabel?: string | null;
  hasPendingRequest: boolean;
  needsPaymentProof: boolean;
  awaitingAdminReview: boolean;
  paymentProofUrl?: string | null;
  paymentReference?: string | null;
  rejectionReason?: string | null;
  suspensionReason?: string | null;
  requestedAt?: string | null;
  proofSubmittedAt?: string | null;
  lastDecisionAt?: string | null;
  paymentInstructions: PaymentInstructions;
  teachersUsed: number;
  teachersMax: number;
  studentsUsed: number;
  studentsMax: number;
}

interface SchoolPlanDto {
  code: string;
  label: string;
  priceCop: number;
  monthlyEquivalentCop: number;
  durationMonths: number;
  maxTeachers: number;
  maxStudents: number;
}

interface MembershipEventDto {
  id: number;
  eventType: string;
  planCode?: string | null;
  planPriceCop?: number | null;
  note?: string | null;
  createdAt: string;
}

@Component({
  selector: 'app-school-membership-page',
  standalone: true,
  imports: [
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    UiBadgeComponent,
    UiButtonComponent,
    UiCardComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent,
    UiStatComponent,
    UiSuccessComponent
  ],
  template: `
    <ui-page-header
      eyebrow="Escuela"
      title="Membresía"
      subtitle="Solicita plan → paga → sube comprobante → espera verificación del administrador." />

    <ui-error [message]="error()" />
    <ui-success [message]="success()" />

    @if (loading()) {
      <ui-loading />
    } @else if (profile()) {
      <div class="grid-stats">
        <ui-stat
          label="Días de membresía"
          [value]="profile()!.daysRemaining"
          [tone]="profile()!.isMembershipActive ? 'success' : 'warning'" />
        <ui-stat label="Instructores" [value]="profile()!.teachersUsed + ' / ' + profile()!.teachersMax" tone="primary" />
        <ui-stat label="Estudiantes" [value]="profile()!.studentsUsed + ' / ' + profile()!.studentsMax" />
        <ui-stat label="Plan" [value]="profile()!.planLabel" />
      </div>

      @if (profile()!.rejectionReason) {
        <ui-card class="alert-card">
          <h2>Solicitud rechazada</h2>
          <p>{{ profile()!.rejectionReason }}</p>
          <p class="muted">Puedes elegir de nuevo un plan, pagar y subir un comprobante corregido.</p>
        </ui-card>
      }

      @if (profile()!.suspensionReason) {
        <ui-card class="alert-card">
          <h2>Membresía suspendida</h2>
          <p>{{ profile()!.suspensionReason }}</p>
        </ui-card>
      }

      <div class="grid-2">
        <ui-card>
          <h2>Estado comercial</h2>
          <p class="plan-name">{{ profile()!.planLabel }}</p>
          <p>
            <ui-badge [tone]="statusTone(profile()!.displayStatus || profile()!.subscriptionStatus)">
              {{ statusLabel(profile()!.displayStatus || profile()!.subscriptionStatus) }}
            </ui-badge>
          </p>
          @if (profile()!.requestedPlanLabel && profile()!.hasPendingRequest) {
            <p class="muted">Solicitud en curso: <strong>{{ profile()!.requestedPlanLabel }}</strong></p>
          }
          <dl class="facts">
            @if (profile()!.membershipEndsAt) {
              <div>
                <dt>Vence</dt>
                <dd>{{ profile()!.membershipEndsAt | date:'mediumDate' }}</dd>
              </div>
            }
            <div>
              <dt>Resumen</dt>
              <dd>{{ membershipSummary() }}</dd>
            </div>
          </dl>
          <div class="row actions">
            <a routerLink="/school/users"><ui-button type="button" variant="ghost">Usuarios</ui-button></a>
            @if (canCancelRequest()) {
              <ui-button type="button" variant="danger" [disabled]="busy()" (click)="cancelRequest()">
                Cancelar solicitud
              </ui-button>
            }
          </div>
        </ui-card>

        <ui-card>
          <h2>1. Instrucciones de pago</h2>
          <dl class="facts">
            <div><dt>Banco</dt><dd>{{ profile()!.paymentInstructions.bankName }}</dd></div>
            <div><dt>Tipo</dt><dd>{{ profile()!.paymentInstructions.accountType }}</dd></div>
            <div>
              <dt>Cuenta</dt>
              <dd class="copy-row">
                <span>{{ profile()!.paymentInstructions.accountNumber }}</span>
                <button type="button" class="copy-btn" (click)="copyText(profile()!.paymentInstructions.accountNumber, 'Número de cuenta')">
                  Copiar
                </button>
              </dd>
            </div>
            <div><dt>Titular</dt><dd>{{ profile()!.paymentInstructions.accountHolder }}</dd></div>
            <div><dt>NIT titular</dt><dd>{{ profile()!.paymentInstructions.holderTaxId }}</dd></div>
            <div><dt>Referencia</dt><dd>{{ profile()!.paymentInstructions.paymentReferenceHint }}</dd></div>
            <div>
              <dt>Soporte</dt>
              <dd>
                {{ profile()!.paymentInstructions.supportEmail }}
                ·
                <a [href]="whatsAppLink()" target="_blank" rel="noopener">
                  WhatsApp {{ profile()!.paymentInstructions.whatsApp }}
                </a>
                <button type="button" class="copy-btn" (click)="copyText(profile()!.paymentInstructions.whatsApp, 'WhatsApp')">
                  Copiar
                </button>
              </dd>
            </div>
          </dl>
          <p class="muted">{{ profile()!.paymentInstructions.notes }}</p>
          <p class="price">
            Valor plan:
            {{ (selectedPlanPrice() ?? profile()!.planPriceCop) | currency:'COP':'symbol-narrow':'1.0-0' }}
          </p>
        </ui-card>
      </div>

      <ui-card style="margin-top: 1rem;">
        <h2>2. Solicitar plan</h2>
        <div class="plans">
          @for (plan of plans(); track plan.code) {
            <button
              type="button"
              class="plan"
              [class.selected]="selectedPlan() === plan.code"
              (click)="pickPlan(plan.code)">
              <span class="plan-title">{{ plan.label }}</span>
              <span class="plan-price">{{ plan.priceCop | currency:'COP':'symbol-narrow':'1.0-0' }}</span>
              <span class="muted">{{ plan.durationMonths }} mes(es) · {{ plan.maxTeachers }} instructores · {{ plan.maxStudents }} estudiantes</span>
            </button>
          }
        </div>
        <ui-button type="button" [disabled]="busy() || !selectedPlan()" (click)="requestSelected()">
          {{ requestLabel() }}
        </ui-button>
      </ui-card>

      @if (profile()!.needsPaymentProof || profile()!.awaitingAdminReview || profile()!.hasPendingRequest) {
        <ui-card style="margin-top: 1rem;">
          <h2>3. Comprobante de pago</h2>
          @if (profile()!.awaitingAdminReview) {
            <p class="notice">Comprobante enviado. Un administrador está revisando tu pago.</p>
            @if (profile()!.paymentProofUrl) {
              <p><a [href]="absoluteUrl(profile()!.paymentProofUrl!)" target="_blank" rel="noopener">Ver comprobante</a></p>
            }
          } @else if (profile()!.needsPaymentProof) {
            <p class="muted">Sube la imagen o PDF del comprobante (máx. 5 MB) para pasar a revisión.</p>
            <label class="field">Archivo
              <input type="file" accept="image/*,.pdf,application/pdf" (change)="onFile($event)" />
            </label>
            @if (proofFileName()) {
              <p class="proof-file">
                Archivo: <strong>{{ proofFileName() }}</strong>
                @if (proofUrl()) {
                  · <a [href]="absoluteUrl(proofUrl()!)" target="_blank" rel="noopener">Vista previa</a>
                }
              </p>
            }
            <label class="field">Referencia / número de transacción (opcional)
              <input [formControl]="proofRef" />
            </label>
            <div class="row actions">
              <ui-button type="button" [disabled]="busy() || !proofUrl()" (click)="submitProof()">
                Enviar comprobante
              </ui-button>
              @if (canCancelRequest()) {
                <ui-button type="button" variant="ghost" [disabled]="busy()" (click)="cancelRequest()">
                  Cancelar solicitud
                </ui-button>
              }
            </div>
          }
        </ui-card>
      }

      <div class="grid-2" style="margin-top: 1rem;">
        <ui-card>
          <h2>Facturación</h2>
          <form class="stack" [formGroup]="billingForm" (ngSubmit)="saveBilling()">
            <label class="field">Razón social <input formControlName="legalName" /></label>
            <label class="field">NIT <input formControlName="taxId" /></label>
            <label class="field">Correo factura <input type="email" formControlName="billingEmail" /></label>
            <label class="field">Teléfono <input formControlName="phone" /></label>
            <label class="field">Dirección <input formControlName="address" /></label>
            <div class="row-2">
              <label class="field">Ciudad <input formControlName="city" /></label>
              <label class="field">Departamento <input formControlName="department" /></label>
            </div>
            <ui-button type="submit" [disabled]="busy() || billingForm.invalid">Guardar facturación</ui-button>
          </form>
        </ui-card>

        <ui-card>
          <h2>Historial</h2>
          @if (!history().length) {
            <p class="muted">Aún no hay eventos comerciales.</p>
          } @else {
            <ul class="history">
              @for (item of history(); track item.id) {
                <li>
                  <strong>{{ eventLabel(item.eventType) }}</strong>
                  <span class="muted"> · {{ item.createdAt | date:'short' }}</span>
                  @if (item.note) { <div class="muted">{{ item.note }}</div> }
                </li>
              }
            </ul>
          }
        </ui-card>
      </div>
    }
  `,
  styles: [`
    .plan-name { margin: 0; font-weight: 700; }
    .price { margin: 0.75rem 0 0; font-size: var(--text-lg); font-weight: 800; }
    .muted { color: var(--color-text-secondary); margin: 0.35rem 0 0; }
    .notice {
      margin: 0 0 0.75rem;
      padding: 0.75rem 0.9rem;
      border-radius: var(--radius-md);
      border: 1px solid color-mix(in srgb, var(--color-warning, #c47b00) 35%, var(--color-border));
      background: color-mix(in srgb, var(--color-warning, #c47b00) 12%, transparent);
      font-size: var(--text-sm);
    }
    .alert-card { margin-bottom: 1rem; border-color: color-mix(in srgb, #b00020 40%, var(--color-border)); }
    .facts { margin: 0.75rem 0 0; display: grid; gap: 0.55rem; }
    dt { font-size: var(--text-xs); text-transform: uppercase; letter-spacing: 0.04em; color: var(--color-text-secondary); }
    dd { margin: 0; font-weight: 600; }
    .actions { margin-top: 1rem; }
    .stack { display: grid; gap: 0.75rem; }
    .row-2 { display: grid; gap: 0.75rem; grid-template-columns: 1fr 1fr; }
    .field { display: grid; gap: 0.35rem; font-weight: 600; font-size: var(--text-sm); }
    .field input {
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      padding: 0.55rem 0.7rem;
      background: var(--color-surface);
      color: var(--color-text);
    }
    .plans {
      display: grid;
      gap: 0.75rem;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      margin: 1rem 0;
    }
    .plan {
      text-align: left;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 0.9rem;
      background: var(--color-surface);
      color: inherit;
      cursor: pointer;
      display: grid;
      gap: 0.35rem;
    }
    .plan.selected { border-color: var(--color-primary); box-shadow: 0 0 0 1px var(--color-primary); }
    .plan-title { font-weight: 800; }
    .plan-price { font-size: var(--text-lg); font-weight: 700; }
    .history { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.65rem; }
    .copy-row { display: flex; flex-wrap: wrap; align-items: center; gap: 0.5rem; }
    .copy-btn {
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      background: var(--color-background);
      color: var(--color-primary);
      font-weight: 700;
      font-size: var(--text-xs);
      padding: 0.25rem 0.5rem;
      cursor: pointer;
    }
    .proof-file { margin: 0.5rem 0 0; font-size: var(--text-sm); }
    .proof-file a { color: var(--color-primary); font-weight: 700; }
    @media (max-width: 720px) { .row-2 { grid-template-columns: 1fr; } }
  `]
})
export class SchoolMembershipPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly session = inject(SessionStore);

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly profile = signal<SchoolProfileDto | null>(null);
  readonly plans = signal<SchoolPlanDto[]>([]);
  readonly selectedPlan = signal<string | null>(null);
  readonly history = signal<MembershipEventDto[]>([]);
  readonly proofUrl = signal<string | null>(null);
  readonly proofFileName = signal<string | null>(null);
  readonly proofRef = this.fb.nonNullable.control('');

  private static readonly MAX_PROOF_BYTES = 5 * 1024 * 1024;

  readonly billingForm = this.fb.nonNullable.group({
    legalName: ['', [Validators.required, Validators.maxLength(250)]],
    taxId: ['', [Validators.required, Validators.maxLength(32)]],
    billingEmail: ['', [Validators.required, Validators.email]],
    phone: ['', [Validators.required, Validators.maxLength(40)]],
    address: ['', [Validators.required, Validators.maxLength(300)]],
    city: ['', [Validators.required, Validators.maxLength(120)]],
    department: ['', [Validators.required, Validators.maxLength(120)]]
  });

  readonly membershipSummary = computed(() => {
    const p = this.profile();
    if (!p) return '';
    if (p.awaitingAdminReview) return 'Comprobante en revisión por administrador';
    if (p.needsPaymentProof) return 'Paga y sube el comprobante para continuar';
    if (p.isMembershipActive) {
      return p.daysRemaining === 1 ? 'Queda 1 día' : `Quedan ${p.daysRemaining} días`;
    }
    if (p.subscriptionStatus === 'Expired') return 'Membresía vencida — solicita renovación';
    if (p.subscriptionStatus === 'Rejected') return 'Solicitud rechazada — corrige y vuelve a intentar';
    return 'Solicitud pendiente';
  });

  ngOnInit(): void {
    this.reload();
    this.http.get<SchoolPlanDto[]>(`${env.apiUrl}/api/school/plans`).subscribe({
      next: (plans) => this.plans.set(plans),
      error: () => this.plans.set([])
    });
  }

  selectedPlanPrice(): number | null {
    const code = this.selectedPlan();
    return this.plans().find((p) => p.code === code)?.priceCop ?? null;
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.http.get<SchoolProfileDto>(`${env.apiUrl}/api/school/profile`).subscribe({
      next: (dto) => {
        this.applyProfile(dto);
        this.loading.set(false);
        this.loadHistory();
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  loadHistory(): void {
    this.http.get<MembershipEventDto[]>(`${env.apiUrl}/api/school/plan/history`).subscribe({
      next: (rows) => this.history.set(rows),
      error: () => this.history.set([])
    });
  }

  pickPlan(code: string): void {
    this.selectedPlan.set(code);
  }

  requestSelected(): void {
    const code = this.selectedPlan() ?? this.profile()?.planCode;
    if (!code) {
      this.error.set('Selecciona un plan primero.');
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.success.set(null);
    this.http.post<SchoolProfileDto>(`${env.apiUrl}/api/school/plan/request`, { planCode: code })
      .subscribe({
        next: (dto) => {
          this.applyProfile(dto);
          this.busy.set(false);
          this.success.set('Solicitud creada. Realiza el pago y sube el comprobante.');
          this.loadHistory();
        },
        error: (err) => {
          this.busy.set(false);
          this.error.set(mapApiError(err));
        }
      });
  }

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    const isPdf = file.type === 'application/pdf' || /\.pdf$/i.test(file.name);
    const isImage = file.type.startsWith('image/');
    if (!isPdf && !isImage) {
      this.error.set('Solo se permiten imágenes o PDF.');
      input.value = '';
      return;
    }
    if (file.size > SchoolMembershipPage.MAX_PROOF_BYTES) {
      this.error.set('El archivo no puede superar 5 MB.');
      input.value = '';
      return;
    }

    const body = new FormData();
    body.append('file', file);
    this.busy.set(true);
    this.error.set(null);
    this.http.post<{ url: string }>(`${env.apiUrl}/api/school/plan/proof/upload`, body).subscribe({
      next: (res) => {
        this.proofUrl.set(res.url);
        this.proofFileName.set(file.name);
        this.busy.set(false);
        this.success.set('Archivo cargado. Ahora envía el comprobante.');
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  submitProof(): void {
    const url = this.proofUrl();
    if (!url) {
      this.error.set('Sube el archivo del comprobante primero.');
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.http.post<SchoolProfileDto>(`${env.apiUrl}/api/school/plan/proof`, {
      paymentProofUrl: url,
      paymentReference: this.proofRef.value || null
    }).subscribe({
      next: (dto) => {
        this.applyProfile(dto);
        this.busy.set(false);
        this.proofUrl.set(null);
        this.proofFileName.set(null);
        this.success.set('Comprobante enviado. Espera la verificación del administrador.');
        this.loadHistory();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  canCancelRequest(): boolean {
    const p = this.profile();
    if (!p) return false;
    if (p.needsPaymentProof) return true;
    return p.hasPendingRequest && !p.awaitingAdminReview;
  }

  cancelRequest(): void {
    this.busy.set(true);
    this.error.set(null);
    this.success.set(null);
    this.http.post<SchoolProfileDto>(`${env.apiUrl}/api/school/plan/cancel`, { note: null }).subscribe({
      next: (dto) => {
        this.applyProfile(dto);
        this.busy.set(false);
        this.proofUrl.set(null);
        this.proofFileName.set(null);
        this.success.set('Solicitud cancelada.');
        this.loadHistory();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  whatsAppLink(): string {
    const raw = this.profile()?.paymentInstructions.whatsApp ?? '';
    const digits = raw.replace(/\D/g, '');
    return digits ? `https://wa.me/${digits}` : 'https://wa.me/';
  }

  async copyText(value: string, label: string): Promise<void> {
    try {
      if (navigator.clipboard?.writeText && window.isSecureContext) {
        await navigator.clipboard.writeText(value);
      } else {
        const ta = document.createElement('textarea');
        ta.value = value;
        ta.setAttribute('readonly', '');
        ta.style.position = 'fixed';
        ta.style.left = '-9999px';
        document.body.appendChild(ta);
        ta.select();
        const ok = document.execCommand('copy');
        document.body.removeChild(ta);
        if (!ok) {
          throw new Error('copy_failed');
        }
      }
      this.success.set(`${label} copiado.`);
    } catch {
      this.error.set(`No se pudo copiar ${label.toLowerCase()}. Selecciona y copia manualmente.`);
    }
  }

  saveBilling(): void {
    if (this.billingForm.invalid) {
      this.billingForm.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.http.put<SchoolProfileDto>(`${env.apiUrl}/api/school/billing`, this.billingForm.getRawValue())
      .subscribe({
        next: (dto) => {
          this.applyProfile(dto);
          this.busy.set(false);
          this.success.set('Datos de facturación guardados.');
        },
        error: (err) => {
          this.busy.set(false);
          this.error.set(mapApiError(err));
        }
      });
  }

  absoluteUrl(path: string): string {
    if (path.startsWith('http')) return path;
    return `${env.apiUrl}${path}`;
  }

  requestLabel(): string {
    const p = this.profile();
    if (!p) return 'Solicitar membresía';
    if (p.isMembershipActive) return 'Solicitar renovación / cambio';
    if (p.subscriptionStatus === 'Expired') return 'Solicitar reactivación';
    if (p.subscriptionStatus === 'Rejected') return 'Volver a solicitar';
    return 'Solicitar membresía';
  }

  eventLabel(type: string): string {
    if (type === 'Requested') return 'Solicitud';
    if (type === 'ProofSubmitted') return 'Comprobante enviado';
    if (type === 'Activated') return 'Activación';
    if (type === 'Renewed') return 'Renovación';
    if (type === 'Rejected') return 'Rechazo';
    if (type === 'Expired') return 'Vencimiento';
    if (type === 'Cancelled') return 'Cancelación';
    if (type === 'Suspended') return 'Suspensión';
    if (type === 'Unsuspended') return 'Reactivación';
    return type;
  }

  statusLabel(status: string): string {
    if (status === 'Active') return 'Activo';
    if (status === 'Expiring') return 'Por vencer';
    if (status === 'None') return 'Sin membresía';
    if (status === 'PendingPayment') return 'Pendiente de pago';
    if (status === 'UnderReview' || status === 'PaymentSubmitted') return 'En revisión';
    if (status === 'Rejected') return 'Rechazado';
    if (status === 'Cancelled') return 'Cancelada';
    if (status === 'Suspended') return 'Suspendida';
    if (status === 'Expired') return 'Vencido';
    return status;
  }

  statusTone(status: string): 'success' | 'warning' | 'danger' | 'neutral' | 'primary' {
    if (status === 'Active') return 'success';
    if (status === 'Expiring' || status === 'PendingPayment') return 'warning';
    if (status === 'UnderReview' || status === 'PaymentSubmitted') return 'primary';
    if (status === 'Rejected' || status === 'Expired' || status === 'Suspended' || status === 'Cancelled') {
      return 'danger';
    }
    return 'neutral';
  }

  private applyProfile(dto: SchoolProfileDto): void {
    this.profile.set(dto);
    this.selectedPlan.set(dto.requestedPlanCode || dto.planCode);
    this.billingForm.patchValue({
      legalName: dto.legalName,
      taxId: dto.taxId,
      billingEmail: dto.billingEmail,
      phone: dto.phone,
      address: dto.address,
      city: dto.city,
      department: dto.department
    });
  }
}
