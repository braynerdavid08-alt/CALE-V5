import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiThemeToggleComponent } from '../../../shared/ui/ui-theme-toggle.component';
import { AuthFacade } from '../application/auth.facade';
import { BRAND } from '../../../core/brand';

@Component({
  selector: 'app-verify-email-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    UiButtonComponent,
    UiErrorComponent,
    UiThemeToggleComponent
  ],
  templateUrl: './verify-email.page.html',
  styleUrl: './login.page.css'
})
export class VerifyEmailPage implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  readonly auth = inject(AuthFacade);
  readonly brand = BRAND;

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    code: ['', [Validators.required, Validators.minLength(4), Validators.maxLength(8)]]
  });

  ngOnInit(): void {
    const email = this.route.snapshot.queryParamMap.get('email');
    const code = this.route.snapshot.queryParamMap.get('code');
    if (email) {
      this.form.patchValue({ email });
    }
    if (code) {
      this.form.patchValue({ code });
      this.auth.devConfirmationCode.set(code);
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { email, code } = this.form.getRawValue();
    this.auth.confirmEmail(email, code.trim());
  }

  resend(): void {
    const email = this.form.controls.email.value;
    if (!email || this.form.controls.email.invalid) {
      this.form.controls.email.markAsTouched();
      return;
    }
    this.auth.resendConfirmation(email);
  }
}
