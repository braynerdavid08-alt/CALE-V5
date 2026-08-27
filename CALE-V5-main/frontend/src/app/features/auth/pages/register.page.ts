import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiThemeToggleComponent } from '../../../shared/ui/ui-theme-toggle.component';
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
    UiThemeToggleComponent
  ],
  templateUrl: './register.page.html',
  styleUrl: './login.page.css'
})
export class RegisterPage {
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthFacade);
  readonly brand = BRAND;

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { name, email, password } = this.form.getRawValue();
    this.auth.register(name, email, password);
  }
}
