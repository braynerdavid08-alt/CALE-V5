import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiThemeToggleComponent } from '../../../shared/ui/ui-theme-toggle.component';
import { AuthBackHomeComponent } from '../components/auth-back-home.component';
import { LegalConsentComponent, consentValue, legalConsentGroup } from '../components/legal-consent.component';
import {
  GuardianFieldsComponent,
  guardianGroup,
  guardianValue,
  syncGuardianGroup
} from '../../legal/guardian-fields.component';
import { AuthFacade } from '../application/auth.facade';
import { BRAND } from '../../../core/brand';

@Component({
  selector: 'app-register-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    UiButtonComponent,
    UiErrorComponent,
    UiThemeToggleComponent,
    AuthBackHomeComponent,
    LegalConsentComponent,
    GuardianFieldsComponent
  ],
  templateUrl: './register.page.html',
  styleUrl: './login.page.css'
})
export class RegisterPage {
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthFacade);
  readonly brand = BRAND;
  readonly showPassword = signal(false);
  readonly isMinor = signal(false);
  readonly today = new Date().toISOString().slice(0, 10);

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    birthDate: ['', Validators.required],
    guardian: guardianGroup(this.fb),
    consent: legalConsentGroup(this.fb)
  });

  constructor() {
    this.form.controls.birthDate.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((value) => this.isMinor.set(syncGuardianGroup(this.form.controls.guardian, value)));
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { name, email, password, birthDate } = this.form.getRawValue();
    this.auth.register(
      name,
      email,
      password,
      consentValue(this.form.controls.consent),
      birthDate,
      guardianValue(this.form.controls.guardian)
    );
  }
}
