import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { AuthFacade } from '../application/auth.facade';
import { AuthApi, SchoolPlanDto } from '../api/auth.api';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiThemeToggleComponent } from '../../../shared/ui/ui-theme-toggle.component';
import { AuthBackHomeComponent } from '../components/auth-back-home.component';
import { BRAND } from '../../../core/brand';

@Component({
  selector: 'app-register-school-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    CurrencyPipe,
    UiButtonComponent,
    UiErrorComponent,
    UiThemeToggleComponent,
    AuthBackHomeComponent
  ],
  templateUrl: './register-school.page.html',
  styleUrl: './register-school.page.css'
})
export class RegisterSchoolPage implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(AuthApi);
  readonly auth = inject(AuthFacade);
  readonly brand = BRAND;

  readonly plans = signal<SchoolPlanDto[]>([]);
  readonly plansError = signal<string | null>(null);

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
    planCode: ['Monthly', Validators.required]
  });

  ngOnInit(): void {
    this.api.schoolPlans().subscribe({
      next: (plans) => this.plans.set(plans),
      error: () => this.plansError.set('No se pudieron cargar los planes.')
    });
  }

  selectPlan(code: string): void {
    this.form.controls.planCode.setValue(code);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.auth.registerSchool(this.form.getRawValue());
  }
}
