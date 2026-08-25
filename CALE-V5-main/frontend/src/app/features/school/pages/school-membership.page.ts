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
  createdAt: string;
  membershipStartsAt?: string | null;
  membershipEndsAt?: string | null;
  daysRemaining: number;
  isMembershipActive: boolean;
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
      subtitle="Plan, cupos, facturación y renovación." />

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
        <ui-stat label="Docentes" [value]="profile()!.teachersUsed + ' / ' + profile()!.teachersMax" tone="primary" />
        <ui-stat label="Estudiantes" [value]="profile()!.studentsUsed + ' / ' + profile()!.studentsMax" />
        <ui-stat label="Plan" [value]="profile()!.planLabel" />
      </div>

      <div class="grid-2">
        <ui-card>
          <h2>Membresía actual</h2>
          <p class="plan-name">{{ profile()!.planLabel }}</p>
          <p class="price">
            {{ profile()!.planPriceCop | currency:'COP':'symbol-narrow':'1.0-0' }}
          </p>
          <p class="muted">
            {{ profile()!.planDurationMonths }} mes(es) · ≈
            {{ profile()!.monthlyEquivalentCop | currency:'COP':'symbol-narrow':'1.0-0' }}/mes
          </p>
          <p>
            <ui-badge [tone]="statusTone(profile()!.subscriptionStatus)">
              {{ statusLabel(profile()!.subscriptionStatus) }}
            </ui-badge>
          </p>

          <dl class="facts" style="margin-top: 1rem;">
            @if (profile()!.membershipStartsAt) {
              <div>
                <dt>Inicio</dt>
                <dd>{{ profile()!.membershipStartsAt | date:'mediumDate' }}</dd>
              </div>
            }
            @if (profile()!.membershipEndsAt) {
              <div>
                <dt>Vence</dt>
                <dd>{{ profile()!.membershipEndsAt | date:'mediumDate' }}</dd>
              </div>
            }
            <div>
              <dt>Tiempo restante</dt>
              <dd>{{ membershipSummary() }}</dd>
            </div>
          </dl>

          <div class="row actions">
            <ui-button type="button" [disabled]="busy()" (click)="activateCurrent()">
              {{ activateLabel() }}
            </ui-button>
            <a routerLink="/school/users"><ui-button type="button" variant="ghost">Usuarios</ui-button></a>
          </div>
        </ui-card>

        <ui-card>
          <h2>Facturación</h2>
          <form class="stack" [formGroup]="billingForm" (ngSubmit)="saveBilling()">
            <label class="field">Razón social
              <input formControlName="legalName" />
            </label>
            <label class="field">NIT
              <input formControlName="taxId" />
            </label>
            <label class="field">Correo factura
              <input type="email" formControlName="billingEmail" />
            </label>
            <label class="field">Teléfono
              <input formControlName="phone" />
            </label>
            <label class="field">Dirección
              <input formControlName="address" />
            </label>
            <div class="row-2">
              <label class="field">Ciudad
                <input formControlName="city" />
              </label>
              <label class="field">Departamento
                <input formControlName="department" />
              </label>
            </div>
            <ui-button type="submit" [disabled]="busy() || billingForm.invalid">
              Guardar facturación
            </ui-button>
          </form>
        </ui-card>
      </div>

      <ui-card style="margin-top: 1rem;">
        <h2>Adquirir o cambiar plan</h2>
        <p class="muted">
          Elige un plan y actívalo. Si ya tienes membresía vigente, al activar se suma el nuevo período
          a la fecha de vencimiento actual.
        </p>
        <div class="plans">
          @for (plan of plans(); track plan.code) {
            <button
              type="button"
              class="plan"
              [class.selected]="selectedPlan() === plan.code"
              [class.current]="profile()!.planCode === plan.code"
              (click)="pickPlan(plan.code)">
              <span class="plan-title">{{ plan.label }}</span>
              <span class="plan-price">
                {{ plan.priceCop | currency:'COP':'symbol-narrow':'1.0-0' }}
              </span>
              <span class="muted">
                {{ plan.durationMonths }} mes(es) · {{ plan.maxTeachers }} docentes ·
                {{ plan.maxStudents }} estudiantes
              </span>
              @if (profile()!.planCode === plan.code) {
                <ui-badge tone="primary">Plan actual</ui-badge>
              }
            </button>
          }
        </div>
        <div class="row actions">
          <ui-button type="button" variant="ghost" [disabled]="busy()" (click)="selectOnly()">
            Solo cambiar plan (sin pagar)
          </ui-button>
          <ui-button type="button" [disabled]="busy() || !selectedPlan()" (click)="activateSelected()">
            Activar / renovar plan elegido
          </ui-button>
        </div>
      </ui-card>
    }
  `,
  styles: [`
    .plan-name { margin: 0; font-weight: 700; }
    .price { margin: 0.25rem 0; font-size: var(--text-2xl); font-weight: 800; }
    .muted { color: var(--color-text-secondary); margin: 0.35rem 0 0; }
    .facts { margin: 0; display: grid; gap: 0.65rem; }
    .facts div { display: grid; gap: 0.1rem; }
    dt {
      font-size: var(--text-xs);
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: var(--color-text-secondary);
    }
    dd { margin: 0; font-weight: 600; }
    .actions { margin-top: 1rem; gap: 0.75rem; flex-wrap: wrap; }
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
    .plan.current { background: color-mix(in srgb, var(--color-primary) 8%, transparent); }
    .plan-title { font-weight: 800; }
    .plan-price { font-size: var(--text-lg); font-weight: 700; }
    @media (max-width: 720px) {
      .row-2 { grid-template-columns: 1fr; }
    }
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
    if (p.isMembershipActive) {
      return p.daysRemaining === 1
        ? 'Queda 1 día'
        : `Quedan ${p.daysRemaining} días`;
    }
    if (p.subscriptionStatus === 'Expired') {
      return 'Membresía vencida';
    }
    return 'Sin membresía activa (pago pendiente)';
  });

  ngOnInit(): void {
    this.reload();
    this.http.get<SchoolPlanDto[]>(`${env.apiUrl}/api/school/plans`).subscribe({
      next: (plans) => this.plans.set(plans),
      error: () => this.plans.set([])
    });
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.http.get<SchoolProfileDto>(`${env.apiUrl}/api/school/profile`).subscribe({
      next: (dto) => {
        this.applyProfile(dto);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  pickPlan(code: string): void {
    this.selectedPlan.set(code);
  }

  selectOnly(): void {
    const code = this.selectedPlan();
    if (!code) {
      this.error.set('Selecciona un plan primero.');
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.success.set(null);
    this.http.put<SchoolProfileDto>(`${env.apiUrl}/api/school/plan`, { planCode: code })
      .subscribe({
        next: (dto) => {
          this.applyProfile(dto);
          this.busy.set(false);
          this.success.set('Plan actualizado. Actívalo para iniciar o extender la membresía.');
        },
        error: (err) => {
          this.busy.set(false);
          this.error.set(mapApiError(err));
        }
      });
  }

  activateSelected(): void {
    const code = this.selectedPlan() ?? this.profile()?.planCode;
    if (!code) return;
    this.activate(code);
  }

  activateCurrent(): void {
    this.activate(this.profile()?.planCode ?? undefined);
  }

  activate(planCode?: string): void {
    this.busy.set(true);
    this.error.set(null);
    this.success.set(null);
    this.http.post<SchoolProfileDto>(`${env.apiUrl}/api/school/plan/activate`, {
      planCode: planCode ?? null
    }).subscribe({
      next: (dto) => {
        this.applyProfile(dto);
        this.busy.set(false);
        this.success.set('Membresía activada / renovada correctamente.');
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  saveBilling(): void {
    if (this.billingForm.invalid) {
      this.billingForm.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.success.set(null);
    this.http.put<SchoolProfileDto>(
      `${env.apiUrl}/api/school/billing`,
      this.billingForm.getRawValue()
    ).subscribe({
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

  activateLabel(): string {
    const p = this.profile();
    if (!p) return 'Activar plan';
    if (p.isMembershipActive) return 'Renovar membresía';
    if (p.subscriptionStatus === 'Expired') return 'Reactivar membresía';
    return 'Adquirir / activar plan';
  }

  statusLabel(status: string): string {
    if (status === 'Active') return 'Activo';
    if (status === 'PendingPayment') return 'Pago pendiente';
    if (status === 'Expired') return 'Vencido';
    return status;
  }

  statusTone(status: string): 'success' | 'warning' | 'danger' | 'neutral' {
    if (status === 'Active') return 'success';
    if (status === 'PendingPayment') return 'warning';
    if (status === 'Expired') return 'danger';
    return 'neutral';
  }

  private applyProfile(dto: SchoolProfileDto): void {
    this.profile.set(dto);
    this.selectedPlan.set(dto.planCode);
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
