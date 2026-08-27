import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { AuthFacade } from '../application/auth.facade';
import { AuthApi, SchoolPlanDto } from '../api/auth.api';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiThemeToggleComponent } from '../../../shared/ui/ui-theme-toggle.component';
import { BRAND } from '../../../core/brand';

const FALLBACK_PLANS: SchoolPlanDto[] = [
  {
    code: 'Deferred',
    label: 'Solo cuenta (sin pagar)',
    priceCop: 0,
    monthlyEquivalentCop: 0,
    durationMonths: 0,
    maxTeachers: 0,
    maxStudents: 0
  },
  {
    code: 'Trial',
    label: 'Prueba gratis 1 mes',
    priceCop: 0,
    monthlyEquivalentCop: 0,
    durationMonths: 1,
    maxTeachers: 5,
    maxStudents: 50
  },
  {
    code: 'Monthly',
    label: 'Mensual',
    priceCop: 150_000,
    monthlyEquivalentCop: 150_000,
    durationMonths: 1,
    maxTeachers: 5,
    maxStudents: 50
  },
  {
    code: 'Semestral',
    label: 'Semestral',
    priceCop: 800_000,
    monthlyEquivalentCop: 133_333.33,
    durationMonths: 6,
    maxTeachers: 12,
    maxStudents: 150
  },
  {
    code: 'Annual',
    label: 'Anual',
    priceCop: 1_500_000,
    monthlyEquivalentCop: 125_000,
    durationMonths: 12,
    maxTeachers: 25,
    maxStudents: 400
  }
];

@Component({
  selector: 'app-register-school-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    CurrencyPipe,
    UiButtonComponent,
    UiErrorComponent,
    UiThemeToggleComponent
  ],
  templateUrl: './register-school.page.html',
  styleUrl: './register-school.page.css'
})
export class RegisterSchoolPage implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(AuthApi);
  readonly auth = inject(AuthFacade);
  readonly brand = BRAND;

  readonly plans = signal<SchoolPlanDto[]>(FALLBACK_PLANS);
  readonly plansError = signal<string | null>(null);
  readonly formHint = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    contactName: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    legalName: ['', [Validators.required, Validators.maxLength(250)]],
    taxId: ['', [Validators.required, Validators.maxLength(32)]],
    billingEmail: ['', [Validators.required, Validators.email]],
    phone: ['', [Validators.required, Validators.maxLength(40)]],
    address: ['', [Validators.required, Validators.maxLength(300)]],
    city: ['', [Validators.required, Validators.maxLength(120)]],
    department: ['', [Validators.required, Validators.maxLength(120)]],
    planCode: ['Deferred', Validators.required],
    claimFreeTrial: [false]
  });

  readonly isTrialSelected = () =>
    this.form.controls.claimFreeTrial.value
    || this.form.controls.planCode.value === 'Trial';

  readonly isDeferredSelected = () =>
    this.form.controls.planCode.value === 'Deferred';

  ngOnInit(): void {
    this.api.schoolPlans().subscribe({
      next: (plans) => {
        if (plans?.length) {
          this.plans.set(plans);
          const codes = new Set(plans.map((p) => p.code));
          if (!codes.has(this.form.controls.planCode.value)) {
            this.selectPlan(codes.has('Deferred') ? 'Deferred' : plans[0].code);
          }
        }
      },
      error: () => {
        this.plans.set(FALLBACK_PLANS);
        this.plansError.set(null);
      }
    });
  }

  selectPlan(code: string): void {
    this.formHint.set(null);
    if (code === 'Trial') {
      this.form.controls.planCode.setValue('Trial');
      this.form.controls.claimFreeTrial.setValue(true);
      return;
    }
    this.form.controls.claimFreeTrial.setValue(false);
    this.form.controls.planCode.setValue(code);
  }

  submit(): void {
    this.formHint.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.formHint.set(
        'Completa todos los campos (incluido dirección, ciudad y departamento). El correo de facturación debe ser un email válido.'
      );
      return;
    }
    const raw = this.form.getRawValue();
    this.auth.registerSchool({
      ...raw,
      claimFreeTrial: raw.claimFreeTrial === true || raw.planCode === 'Trial'
    });
  }
}
